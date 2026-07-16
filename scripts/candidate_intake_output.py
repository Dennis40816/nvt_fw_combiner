"""Filesystem trust boundary for deterministic candidate-intake output."""

from __future__ import annotations

import os
import stat
from contextlib import contextmanager
from pathlib import Path
from typing import Iterator

OUTPUT_LOCK_FILE = ".candidate-ic-intake.lock"


class IntakeError(ValueError):
    """Raised when declared candidate evidence is unsafe or incomplete."""


def reject_reparse_points(path: Path) -> None:
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    for component in (path, *path.parents):
        if component.exists() and (
            component.is_symlink()
            or getattr(component.stat(), "st_file_attributes", 0) & reparse_flag
        ):
            raise IntakeError(f"reparse point is not allowed: {component}")


def resolve_directory(path: Path, description: str) -> Path:
    raw = path.expanduser().absolute()
    if not raw.is_dir():
        raise IntakeError(f"{description} must be an existing directory: {raw}")
    reject_reparse_points(raw)
    return raw.resolve(strict=True)


class ValidatedOutputDirectory:
    """Keep candidate output bound to one directory while publishing files."""

    def __init__(
        self,
        path: Path,
        status: os.stat_result,
        directory_descriptor: int | None,
    ) -> None:
        self.path = path
        self.status = status
        self.directory_descriptor = directory_descriptor
        self.staged_identities: dict[str, tuple[int, int]] = {}
        self.published_identities: dict[str, tuple[int, int]] = {}

    def validate_identity(self) -> None:
        try:
            reject_reparse_points(self.path)
            current = os.stat(self.path, follow_symlinks=False)
        except OSError as exception:
            raise IntakeError(
                f"output directory changed while intake was running: {self.path}"
            ) from exception
        if not stat.S_ISDIR(current.st_mode) or self.identity(current) != self.identity(
            self.status
        ):
            raise IntakeError(
                f"output directory changed while intake was running: {self.path}"
            )

    def names(self) -> set[str]:
        target: int | Path = (
            self.directory_descriptor
            if self.directory_descriptor is not None
            else self.path
        )
        return set(os.listdir(target))

    def require_names(self, expected: set[str]) -> None:
        actual = self.names()
        if actual != expected:
            raise IntakeError(
                "output directory changed while intake was running: "
                f"expected {sorted(expected)}, found {sorted(actual)}"
            )

    def create_exclusive(self, name: str, *, temporary: bool = False) -> int:
        flags = (
            os.O_WRONLY
            | os.O_CREAT
            | os.O_EXCL
            | getattr(os, "O_BINARY", 0)
            | getattr(os, "O_CLOEXEC", 0)
            | getattr(os, "O_NOFOLLOW", 0)
        )
        if temporary:
            flags |= getattr(os, "O_TEMPORARY", 0)
        if self.directory_descriptor is not None:
            return os.open(name, flags, 0o666, dir_fd=self.directory_descriptor)
        return os.open(self.path / name, flags, 0o666)

    def unlink(self, name: str, *, missing_ok: bool = False) -> None:
        try:
            if self.directory_descriptor is not None:
                os.unlink(name, dir_fd=self.directory_descriptor)
            else:
                os.unlink(self.path / name)
        except FileNotFoundError:
            if not missing_ok:
                raise

    def entry_status(self, name: str) -> os.stat_result:
        if self.directory_descriptor is not None:
            return os.stat(
                name,
                dir_fd=self.directory_descriptor,
                follow_symlinks=False,
            )
        return os.stat(self.path / name, follow_symlinks=False)

    @staticmethod
    def identity(status: os.stat_result) -> tuple[int, int]:
        return status.st_dev, status.st_ino

    def regular_entry_identity(self, name: str, description: str) -> tuple[int, int]:
        status = self.entry_status(name)
        if not stat.S_ISREG(status.st_mode):
            raise IntakeError(f"{description} is not a regular file: {name}")
        return self.identity(status)

    def create_staged(self, name: str) -> int:
        descriptor = self.create_exclusive(name, temporary=os.name == "nt")
        try:
            status = os.fstat(descriptor)
            if not stat.S_ISREG(status.st_mode):
                raise IntakeError(
                    f"staged candidate output is not a regular file: {name}"
                )
            self.staged_identities[name] = self.identity(status)
            return descriptor
        except BaseException:
            os.close(descriptor)
            raise

    def publish(self, temporary_name: str, final_name: str, descriptor: int) -> None:
        expected = self.staged_identities[temporary_name]
        if self.identity(os.fstat(descriptor)) != expected:
            raise IntakeError(
                f"staged candidate output handle changed before publication: {temporary_name}"
            )
        try:
            staged_identity = self.regular_entry_identity(
                temporary_name,
                "staged candidate output",
            )
        except FileNotFoundError as exception:
            raise IntakeError(
                f"staged candidate output changed before publication: {temporary_name}"
            ) from exception
        if staged_identity != expected:
            raise IntakeError(
                f"staged candidate output changed before publication: {temporary_name}"
            )

        if self.directory_descriptor is None:
            os.link(
                self.path / temporary_name,
                self.path / final_name,
                follow_symlinks=False,
            )
        else:
            os.link(
                temporary_name,
                final_name,
                src_dir_fd=self.directory_descriptor,
                dst_dir_fd=self.directory_descriptor,
                follow_symlinks=False,
            )

        self.published_identities[final_name] = expected
        try:
            published_identity = self.regular_entry_identity(
                final_name,
                "candidate output",
            )
        except FileNotFoundError as exception:
            raise IntakeError(
                f"candidate output changed during publication: {final_name}"
            ) from exception
        if published_identity != expected:
            raise IntakeError(
                f"candidate output changed during publication and was preserved: {final_name}"
            )

        try:
            staged_identity = self.regular_entry_identity(
                temporary_name,
                "staged candidate output",
            )
        except FileNotFoundError as exception:
            raise IntakeError(
                f"staged candidate output changed during publication: {temporary_name}"
            ) from exception
        if staged_identity != expected:
            raise IntakeError(
                "staged candidate output changed during publication and was preserved: "
                f"{temporary_name}"
            )
        self.unlink(temporary_name)
        del self.staged_identities[temporary_name]

    def validate_published(self) -> None:
        for name, expected in self.published_identities.items():
            try:
                current = self.regular_entry_identity(name, "candidate output")
            except FileNotFoundError as exception:
                raise IntakeError(
                    f"candidate output changed after publication: {name}"
                ) from exception
            if current != expected:
                raise IntakeError(f"candidate output changed after publication: {name}")

    def cleanup_tracked(self) -> None:
        staged_preserved, staged_failures = self._cleanup_identities(
            self.staged_identities
        )
        published_preserved, published_failures = self._cleanup_identities(
            self.published_identities
        )
        messages: list[str] = []
        if staged_preserved:
            messages.append(
                "staged candidate output changed before cleanup and was preserved: "
                + ", ".join(sorted(staged_preserved))
            )
        if published_preserved:
            messages.append(
                "candidate output changed before cleanup and was preserved: "
                + ", ".join(sorted(published_preserved))
            )
        failures = [*staged_failures, *published_failures]
        if failures:
            messages.append(
                "candidate output cleanup failed: "
                + ", ".join(sorted(name for name, _ in failures))
            )
        if messages:
            error = IntakeError("; ".join(messages))
            if failures:
                raise error from failures[0][1]
            raise error

    def _cleanup_identities(
        self,
        identities: dict[str, tuple[int, int]],
    ) -> tuple[list[str], list[tuple[str, OSError]]]:
        preserved: list[str] = []
        failures: list[tuple[str, OSError]] = []
        for name, expected in reversed(list(identities.items())):
            try:
                status = self.entry_status(name)
            except FileNotFoundError:
                continue
            except OSError as exception:
                failures.append((name, exception))
                continue
            if not stat.S_ISREG(status.st_mode) or self.identity(status) != expected:
                preserved.append(name)
                continue
            try:
                self.unlink(name)
            except OSError as exception:
                failures.append((name, exception))
        identities.clear()
        return preserved, failures


@contextmanager
def open_validated_output_directory(
    path: Path,
) -> Iterator[ValidatedOutputDirectory]:
    raw = path.expanduser().absolute()
    if not raw.is_dir():
        raise IntakeError(f"output must be an existing directory: {raw}")
    reject_reparse_points(raw)
    resolved = raw.resolve(strict=True)
    directory_descriptor = -1
    lock_descriptor = -1
    output: ValidatedOutputDirectory | None = None
    try:
        if os.name != "nt":
            directory_descriptor = os.open(
                resolved,
                os.O_RDONLY
                | getattr(os, "O_DIRECTORY", 0)
                | getattr(os, "O_CLOEXEC", 0)
                | getattr(os, "O_NOFOLLOW", 0),
            )
            status = os.fstat(directory_descriptor)
        else:
            status = os.stat(resolved, follow_symlinks=False)
        if not stat.S_ISDIR(status.st_mode):
            raise IntakeError(
                f"output must be a regular filesystem directory: {resolved}"
            )
        output = ValidatedOutputDirectory(
            resolved,
            status,
            directory_descriptor if directory_descriptor >= 0 else None,
        )
        output.validate_identity()
        if output.names():
            raise IntakeError(f"output directory must be empty: {resolved}")
        lock_descriptor = output.create_exclusive(
            OUTPUT_LOCK_FILE,
            temporary=os.name == "nt",
        )
        output.validate_identity()
        output.require_names({OUTPUT_LOCK_FILE})
        try:
            yield output
            output.validate_identity()
            output.validate_published()
            output.require_names(
                {OUTPUT_LOCK_FILE, *output.published_identities.keys()}
            )
        except BaseException as exception:
            # The output boundary must roll back on cancellation as well as I/O errors.
            try:
                output.cleanup_tracked()
            except IntakeError as cleanup_error:
                raise cleanup_error from exception
            raise
    finally:
        if lock_descriptor >= 0:
            if os.name != "nt" and output is not None:
                output.unlink(OUTPUT_LOCK_FILE, missing_ok=True)
            os.close(lock_descriptor)
        if directory_descriptor >= 0:
            os.close(directory_descriptor)
