"""Trusted path primitives for deterministic candidate intake."""

from __future__ import annotations

import ctypes
import errno
import os
import stat
from pathlib import Path

RENAME_NOREPLACE = 1
DIRECTORY_OPEN_FLAGS = (
    os.O_RDONLY
    | getattr(os, "O_DIRECTORY", 0)
    | getattr(os, "O_CLOEXEC", 0)
    | getattr(os, "O_NOFOLLOW", 0)
)


class IntakeError(ValueError):
    """Raised when declared candidate evidence is unsafe or incomplete."""


def _identity(status: os.stat_result) -> tuple[int, int]:
    return status.st_dev, status.st_ino


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


def open_unix_directory_chain(path: Path, description: str) -> int:
    parts = Path(os.path.abspath(path)).parts
    descriptor = os.open(parts[0], DIRECTORY_OPEN_FLAGS)
    try:
        for component in parts[1:]:
            try:
                next_descriptor = os.open(
                    component,
                    DIRECTORY_OPEN_FLAGS,
                    dir_fd=descriptor,
                )
            except OSError as exception:
                raise IntakeError(
                    f"{description} component changed while opening: {component}"
                ) from exception
            os.close(descriptor)
            descriptor = next_descriptor
        if not stat.S_ISDIR(os.fstat(descriptor).st_mode):
            raise IntakeError(f"{description} is not a directory: {path}")
        return descriptor
    except BaseException:
        os.close(descriptor)
        raise


def _rename_directory_no_replace(
    parent_descriptor: int,
    source_name: str,
    destination_name: str,
) -> None:
    try:
        renameat2 = getattr(ctypes.CDLL(None, use_errno=True), "renameat2")
    except AttributeError as exception:
        raise IntakeError(
            "atomic no-replace directory publication is unavailable on this Unix platform"
        ) from exception
    renameat2.argtypes = (
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_uint,
    )
    renameat2.restype = ctypes.c_int
    if (
        renameat2(
            parent_descriptor,
            os.fsencode(source_name),
            parent_descriptor,
            os.fsencode(destination_name),
            RENAME_NOREPLACE,
        )
        == 0
    ):
        return
    error = ctypes.get_errno()
    if error in (errno.EEXIST, errno.ENOTEMPTY):
        raise IntakeError(
            f"output destination appeared before atomic publication: {destination_name}"
        )
    if error in (errno.ENOSYS, errno.EINVAL, errno.ENOTSUP):
        raise IntakeError(
            "atomic no-replace directory publication is unavailable on this Unix platform"
        )
    raise OSError(error, os.strerror(error), destination_name)


def _directory_entry_matches(
    parent_descriptor: int,
    name: str,
    expected_status: os.stat_result,
) -> bool:
    try:
        current = os.stat(
            name,
            dir_fd=parent_descriptor,
            follow_symlinks=False,
        )
    except FileNotFoundError:
        return False
    return stat.S_ISDIR(current.st_mode) and _identity(current) == _identity(
        expected_status
    )


def _validate_committed_destination(
    path: Path,
    original_parent_descriptor: int,
    expected_status: os.stat_result,
) -> None:
    error = f"output parent changed after atomic publication: {path}"
    current_parent_descriptor = -1
    try:
        current_parent_descriptor = open_unix_directory_chain(
            path.parent,
            "output parent",
        )
    except (IntakeError, OSError) as exception:
        raise IntakeError(error) from exception
    try:
        if _identity(os.fstat(current_parent_descriptor)) != _identity(
            os.fstat(original_parent_descriptor)
        ) or not _directory_entry_matches(
            current_parent_descriptor,
            path.name,
            expected_status,
        ):
            raise IntakeError(error)
    except OSError as exception:
        raise IntakeError(error) from exception
    finally:
        os.close(current_parent_descriptor)
