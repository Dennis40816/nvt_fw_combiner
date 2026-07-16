"""Filesystem trust boundary for deterministic candidate-intake output."""

from __future__ import annotations

import hashlib
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
    reject_reparse_points(raw)
    try:
        initial_status = os.stat(raw, follow_symlinks=False)
    except OSError as exception:
        raise IntakeError(
            f"{description} must be an existing directory: {raw}"
        ) from exception
    if not stat.S_ISDIR(initial_status.st_mode):
        raise IntakeError(f"{description} must be an existing directory: {raw}")
    resolved = raw.resolve(strict=True)
    resolved_status = os.stat(resolved, follow_symlinks=False)
    if _identity(initial_status) != _identity(resolved_status):
        raise IntakeError(f"{description} changed while validating: {raw}")
    return resolved


def _identity(status: os.stat_result) -> tuple[int, int]:
    return status.st_dev, status.st_ino


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
        self.published_content: dict[str, tuple[int, str]] = {}
        self.anchor_descriptors: dict[int, tuple[int, int]] = {}
        self.lock_identity: tuple[int, int] | None = None

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

    def active_names(self, *names: str) -> set[str]:
        expected = set(names)
        if self.lock_identity is not None:
            expected.add(OUTPUT_LOCK_FILE)
        return expected

    def create_exclusive(
        self,
        name: str,
        *,
        temporary: bool = False,
        read_write: bool = False,
    ) -> int:
        flags = (
            (os.O_RDWR if read_write else os.O_WRONLY)
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

    def unlink(
        self,
        name: str,
        *,
        missing_ok: bool = False,
        expected_identity: tuple[int, int] | None = None,
    ) -> bool:
        try:
            if expected_identity is not None:
                status = self.entry_status(name)
                if (
                    not stat.S_ISREG(status.st_mode)
                    or self.identity(status) != expected_identity
                ):
                    return False
            if self.directory_descriptor is not None:
                os.unlink(name, dir_fd=self.directory_descriptor)
            else:
                os.unlink(self.path / name)
            return True
        except FileNotFoundError:
            if not missing_ok:
                raise
            return True

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
        return _identity(status)

    def regular_entry_identity(self, name: str, description: str) -> tuple[int, int]:
        status = self.entry_status(name)
        if not stat.S_ISREG(status.st_mode):
            raise IntakeError(f"{description} is not a regular file: {name}")
        return self.identity(status)

    def create_staged(self, name: str) -> int:
        descriptor = self.create_exclusive(
            name,
            temporary=os.name == "nt",
            read_write=True,
        )
        try:
            status = os.fstat(descriptor)
            if not stat.S_ISREG(status.st_mode):
                raise IntakeError(
                    f"staged candidate output is not a regular file: {name}"
                )
            identity = self.identity(status)
            self.staged_identities[name] = identity
            self.anchor_descriptors[descriptor] = identity
            return descriptor
        except BaseException:
            self.anchor_descriptors.pop(descriptor, None)
            os.close(descriptor)
            raise

    def close_anchors(self) -> None:
        descriptors = tuple(self.anchor_descriptors)
        self.anchor_descriptors.clear()
        first_error: OSError | None = None
        for descriptor in reversed(descriptors):
            try:
                os.close(descriptor)
            except OSError as exception:
                first_error = first_error or exception
        if first_error is not None:
            raise first_error

    def publish(
        self,
        temporary_name: str,
        final_name: str,
        descriptor: int,
        expected_content: bytes,
    ) -> None:
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
        content = (len(expected_content), hashlib.sha256(expected_content).hexdigest())
        self._validate_descriptor_content(
            descriptor,
            expected,
            content,
            f"staged candidate output changed before publication: {temporary_name}",
        )

        self.published_identities[final_name] = expected
        self.published_content[final_name] = content
        try:
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
        except OSError:
            # A failed link syscall created no final entry. Cancellation remains
            # tracked because it may arrive after the link became visible.
            del self.published_identities[final_name]
            del self.published_content[final_name]
            raise

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
        self._validate_descriptor_content(
            descriptor,
            expected,
            content,
            f"candidate output bytes changed during publication: {final_name}",
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
        if not self.unlink(temporary_name, expected_identity=expected):
            raise IntakeError(
                "staged candidate output changed during publication and was preserved: "
                f"{temporary_name}"
            )
        del self.staged_identities[temporary_name]

    def validate_published(self) -> None:
        for name, expected in self.published_identities.items():
            descriptor = -1
            try:
                descriptor = self.open_readonly(name)
                opened_status = os.fstat(descriptor)
                try:
                    current = self.regular_entry_identity(name, "candidate output")
                except FileNotFoundError as exception:
                    raise IntakeError(
                        f"candidate output changed after publication: {name}"
                    ) from exception
                if (
                    not stat.S_ISREG(opened_status.st_mode)
                    or self.identity(opened_status) != expected
                    or current != expected
                ):
                    raise IntakeError(
                        f"candidate output changed after publication: {name}"
                    )
                self._validate_descriptor_content(
                    descriptor,
                    expected,
                    self.published_content[name],
                    f"candidate output bytes changed after publication: {name}",
                )
                try:
                    current = self.regular_entry_identity(name, "candidate output")
                except FileNotFoundError as exception:
                    raise IntakeError(
                        f"candidate output changed after publication: {name}"
                    ) from exception
                if current != expected:
                    raise IntakeError(
                        f"candidate output changed after publication: {name}"
                    )
            finally:
                if descriptor >= 0:
                    os.close(descriptor)

    def open_readonly(self, name: str) -> int:
        flags = (
            os.O_RDONLY
            | getattr(os, "O_BINARY", 0)
            | getattr(os, "O_CLOEXEC", 0)
            | getattr(os, "O_NOFOLLOW", 0)
        )
        if self.directory_descriptor is not None:
            return os.open(name, flags, dir_fd=self.directory_descriptor)
        return os.open(self.path / name, flags)

    def _validate_descriptor_content(
        self,
        descriptor: int,
        expected_identity: tuple[int, int],
        expected_content: tuple[int, str],
        message: str,
    ) -> None:
        before = os.fstat(descriptor)
        if (
            not stat.S_ISREG(before.st_mode)
            or self.identity(before) != expected_identity
            or before.st_size != expected_content[0]
        ):
            raise IntakeError(message)
        digest = hashlib.sha256()
        os.lseek(descriptor, 0, os.SEEK_SET)
        for chunk in iter(lambda: os.read(descriptor, 1024 * 1024), b""):
            digest.update(chunk)
        after = os.fstat(descriptor)
        if (
            self.identity(after) != expected_identity
            or after.st_size != expected_content[0]
            or digest.hexdigest() != expected_content[1]
        ):
            raise IntakeError(message)

    def record_lock(self, descriptor: int) -> None:
        status = os.fstat(descriptor)
        if not stat.S_ISREG(status.st_mode):
            raise IntakeError("candidate intake lock is not a regular file")
        self.lock_identity = self.identity(status)

    def validate_lock(self) -> None:
        if self.lock_identity is None:
            raise IntakeError("candidate intake lock was not established")
        try:
            current = self.regular_entry_identity(
                OUTPUT_LOCK_FILE,
                "candidate intake lock",
            )
        except (IntakeError, OSError) as exception:
            raise IntakeError(
                "candidate intake lock changed while running"
            ) from exception
        if current != self.lock_identity:
            raise IntakeError("candidate intake lock changed while running")

    def cleanup_tracked(self) -> None:
        tracked_links: dict[tuple[int, int], int] = {}
        for identity in (
            *self.staged_identities.values(),
            *self.published_identities.values(),
        ):
            tracked_links[identity] = tracked_links.get(identity, 0) + 1
        hardlink_failures = self._anchor_link_failures(tracked_links)
        blocked_identities = {identity for _, _, identity in hardlink_failures}
        staged_preserved, staged_failures = self._cleanup_identities(
            self.staged_identities,
            blocked_identities,
        )
        published_preserved, published_failures = self._cleanup_identities(
            self.published_identities,
            blocked_identities,
        )
        remaining_links: dict[tuple[int, int], int] = {}
        for identity in (
            *self.staged_identities.values(),
            *self.published_identities.values(),
        ):
            remaining_links[identity] = remaining_links.get(identity, 0) + 1
        late_hardlink_failures = [
            failure
            for failure in self._anchor_link_failures(remaining_links)
            if failure[2] not in blocked_identities
        ]
        messages: list[str] = []
        if hardlink_failures:
            messages.append(
                "candidate output cleanup blocked by unexpected hard links: "
                + ", ".join(sorted(name for name, _, _ in hardlink_failures))
            )
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
        anchor_failures = [
            (name, error)
            for name, error, _ in (*hardlink_failures, *late_hardlink_failures)
        ]
        failures = [*staged_failures, *published_failures, *anchor_failures]
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
        blocked_identities: set[tuple[int, int]],
    ) -> tuple[list[str], list[tuple[str, OSError]]]:
        preserved: list[str] = []
        failures: list[tuple[str, OSError]] = []
        for name, expected in reversed(list(identities.items())):
            if expected in blocked_identities:
                continue
            try:
                removed = self.unlink(
                    name,
                    missing_ok=True,
                    expected_identity=expected,
                )
            except OSError as exception:
                failures.append((name, exception))
            else:
                if not removed:
                    preserved.append(name)
            del identities[name]
        return preserved, failures

    def _anchor_link_failures(
        self,
        expected_links: dict[tuple[int, int], int],
    ) -> list[tuple[str, OSError, tuple[int, int]]]:
        failures: list[tuple[str, OSError, tuple[int, int]]] = []
        checked: set[tuple[int, int]] = set()
        for descriptor, expected_identity in self.anchor_descriptors.items():
            if expected_identity in checked:
                continue
            checked.add(expected_identity)
            label = f"<identity:{expected_identity[0]}:{expected_identity[1]}>"
            try:
                status = os.fstat(descriptor)
            except OSError as exception:
                failures.append((label, exception, expected_identity))
                continue
            expected = expected_links.get(expected_identity, 0)
            if self.identity(status) != expected_identity:
                failures.append(
                    (
                        label,
                        OSError("run-owned output handle changed"),
                        expected_identity,
                    )
                )
            elif status.st_nlink > expected:
                failures.append(
                    (
                        label,
                        OSError(
                            "run-owned output has an untracked hard link: "
                            f"expected at most {expected}, found {status.st_nlink}"
                        ),
                        expected_identity,
                    )
                )
        return failures


@contextmanager
def open_validated_output_directory(
    path: Path,
) -> Iterator[ValidatedOutputDirectory]:
    raw = path.expanduser().absolute()
    reject_reparse_points(raw)
    try:
        initial_status = os.stat(raw, follow_symlinks=False)
    except OSError as exception:
        raise IntakeError(f"output must be an existing directory: {raw}") from exception
    if not stat.S_ISDIR(initial_status.st_mode):
        raise IntakeError(f"output must be an existing directory: {raw}")
    resolved = raw.resolve(strict=True)
    resolved_status = os.stat(resolved, follow_symlinks=False)
    if _identity(initial_status) != _identity(resolved_status):
        raise IntakeError(f"output directory changed while validating: {raw}")
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
            status = resolved_status
        if not stat.S_ISDIR(status.st_mode) or _identity(status) != _identity(
            resolved_status
        ):
            raise IntakeError(f"output directory changed while validating: {resolved}")
        output = ValidatedOutputDirectory(
            resolved,
            status,
            directory_descriptor if directory_descriptor >= 0 else None,
        )
        output.validate_identity()
        if output.names():
            raise IntakeError(f"output directory must be empty: {resolved}")
        if os.name == "nt":
            lock_descriptor = output.create_exclusive(
                OUTPUT_LOCK_FILE,
                temporary=True,
            )
            output.record_lock(lock_descriptor)
        output.validate_identity()
        if lock_descriptor >= 0:
            output.validate_lock()
        output.require_names(output.active_names())
        try:
            yield output
            output.validate_identity()
            if lock_descriptor >= 0:
                output.validate_lock()
            output.validate_published()
            output.require_names(output.active_names(*output.published_identities))
        except BaseException as exception:
            # The output boundary must roll back on cancellation as well as I/O errors.
            try:
                output.cleanup_tracked()
            except IntakeError as cleanup_error:
                raise cleanup_error from exception
            raise
    finally:
        try:
            if output is not None:
                output.close_anchors()
        finally:
            try:
                if lock_descriptor >= 0:
                    os.close(lock_descriptor)
            finally:
                if directory_descriptor >= 0:
                    os.close(directory_descriptor)
