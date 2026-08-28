#!/usr/bin/env python3
"""Atomically edit the external update-source Registry without rebuilding a release."""

from __future__ import annotations

import argparse
import ctypes
import json
import ntpath
import os
import re
import stat
import tempfile
from contextlib import contextmanager
from ctypes import wintypes
from pathlib import Path
from typing import Iterator

try:
    from update_source_registry_policy import (
        MAXIMUM_REVISION,
        PRODUCTION_REGISTRY_ID,
        load_strict_registry_json as _load_strict_json,
        validate_registry_document as _validate_document,
    )
except ModuleNotFoundError:
    from scripts.update_source_registry_policy import (
        MAXIMUM_REVISION,
        PRODUCTION_REGISTRY_ID,
        load_strict_registry_json as _load_strict_json,
        validate_registry_document as _validate_document,
    )


SECURITY_INFORMATION = 0x00000001 | 0x00000002 | 0x00000004
PROTECTED_DACL_SECURITY_INFORMATION = 0x80000000
UNPROTECTED_DACL_SECURITY_INFORMATION = 0x20000000
SE_DACL_PROTECTED = 0x1000
ERROR_INSUFFICIENT_BUFFER = 122
REPLACEFILE_WRITE_THROUGH = 0x00000001


def _entries(
    latest: str,
    available: list[str],
    deprecated: list[str],
) -> tuple[dict[str, str], ...]:
    proposed = (
        {"status": "latest", "catalogPath": latest},
        *({"status": "available", "catalogPath": path} for path in available),
        *({"status": "deprecated", "catalogPath": path} for path in deprecated),
    )
    _, validated = _validate_document(
        {
            "schemaVersion": 1,
            "registryId": PRODUCTION_REGISTRY_ID,
            "registryRevision": 1,
            "publishedAtUtc": "2026-08-27T00:00:00Z",
            "catalogPublication": {
                "latestVersion": "1.0.0",
                "catalogSchemaVersion": 1,
                "catalogSha256": "0" * 64,
            },
            "entries": list(proposed),
        }
    )
    return validated


def _entry_identity(
    entries: tuple[dict[str, str], ...],
) -> tuple[tuple[str, str], ...]:
    return tuple(
        (entry["status"], ntpath.normcase(entry["catalogPath"])) for entry in entries
    )


def _has_reparse_attribute(path: Path) -> bool:
    attributes = getattr(path.stat(follow_symlinks=False), "st_file_attributes", 0)
    return bool(attributes & stat.FILE_ATTRIBUTE_REPARSE_POINT)


def _reject_reparse_chain(path: Path) -> None:
    current = path
    while True:
        if _has_reparse_attribute(current):
            raise ValueError(f"registry locator contains a reparse point: {current}")
        parent = current.parent
        if parent == current:
            return
        current = parent


def _file_identity(path: Path) -> tuple[int, int]:
    value = path.stat(follow_symlinks=False)
    return value.st_dev, value.st_ino


def _security_descriptor(path: Path) -> bytes:
    if os.name != "nt":
        raise OSError("Registry publication is supported only on Windows")
    advapi32 = ctypes.WinDLL("advapi32", use_last_error=True)
    get_security = advapi32.GetFileSecurityW
    get_security.argtypes = (
        wintypes.LPCWSTR,
        wintypes.DWORD,
        wintypes.LPVOID,
        wintypes.DWORD,
        ctypes.POINTER(wintypes.DWORD),
    )
    get_security.restype = wintypes.BOOL
    required = wintypes.DWORD()
    ctypes.set_last_error(0)
    get_security(str(path), SECURITY_INFORMATION, None, 0, ctypes.byref(required))
    if ctypes.get_last_error() != ERROR_INSUFFICIENT_BUFFER or required.value == 0:
        raise ctypes.WinError(ctypes.get_last_error())
    buffer = ctypes.create_string_buffer(required.value)
    if not get_security(
        str(path),
        SECURITY_INFORMATION,
        buffer,
        required.value,
        ctypes.byref(required),
    ):
        raise ctypes.WinError(ctypes.get_last_error())
    return bytes(buffer.raw[: required.value])


def _apply_security_descriptor(path: Path, descriptor: bytes) -> None:
    advapi32 = ctypes.WinDLL("advapi32", use_last_error=True)
    set_security = advapi32.SetFileSecurityW
    set_security.argtypes = (wintypes.LPCWSTR, wintypes.DWORD, wintypes.LPVOID)
    set_security.restype = wintypes.BOOL
    control = int.from_bytes(descriptor[2:4], byteorder="little")
    protection = (
        PROTECTED_DACL_SECURITY_INFORMATION
        if control & SE_DACL_PROTECTED
        else UNPROTECTED_DACL_SECURITY_INFORMATION
    )
    buffer = ctypes.create_string_buffer(descriptor)
    if not set_security(
        str(path),
        SECURITY_INFORMATION | protection,
        ctypes.cast(buffer, wintypes.LPVOID),
    ):
        raise ctypes.WinError(ctypes.get_last_error())


def _descriptor_sddl(descriptor: bytes) -> str:
    advapi32 = ctypes.WinDLL("advapi32", use_last_error=True)
    convert = advapi32.ConvertSecurityDescriptorToStringSecurityDescriptorW
    convert.argtypes = (
        wintypes.LPVOID,
        wintypes.DWORD,
        wintypes.DWORD,
        ctypes.POINTER(wintypes.LPWSTR),
        ctypes.POINTER(wintypes.DWORD),
    )
    convert.restype = wintypes.BOOL
    buffer = ctypes.create_string_buffer(descriptor)
    output = wintypes.LPWSTR()
    length = wintypes.DWORD()
    if not convert(
        ctypes.cast(buffer, wintypes.LPVOID),
        1,
        SECURITY_INFORMATION,
        ctypes.byref(output),
        ctypes.byref(length),
    ):
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        return ctypes.wstring_at(output)
    finally:
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        local_free = kernel32.LocalFree
        local_free.argtypes = (wintypes.LPVOID,)
        local_free.restype = wintypes.LPVOID
        local_free(ctypes.cast(output, wintypes.LPVOID))


def _security_descriptor_sddl(path: Path) -> str:
    return _descriptor_sddl(_security_descriptor(path))


def _canonical_security_identity(sddl: str) -> str:
    # ReplaceFile may re-mark auto-inheritance provenance (AI/AR/ID). Those
    # markers do not change the owner, group, effective ACEs, or DACL
    # protection; every access/inheritance permission flag remains compared.
    dacl_start = sddl.find("D:")
    ace_start = sddl.find("(", dacl_start)
    if dacl_start < 0 or ace_start < 0:
        raise ValueError("Registry security descriptor must contain an explicit DACL")
    flags = sddl[dacl_start + 2 : ace_start]
    flags = flags.replace("AI", "").replace("AR", "")
    dacl = f"{flags}{sddl[ace_start:]}"

    def normalize_ace(match: re.Match[str]) -> str:
        fields = match.group(1).split(";")
        if len(fields) >= 2:
            fields[1] = fields[1].replace("ID", "")
        return f"({';'.join(fields)})"

    dacl = re.sub(r"\(([^()]*)\)", normalize_ace, dacl)
    return f"{sddl[: dacl_start + 2]}{dacl}"


def _security_descriptor_identity(path: Path) -> str:
    return _canonical_security_identity(_security_descriptor_sddl(path))


def _replace_file(destination: Path, replacement: Path) -> None:
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    replace_file = kernel32.ReplaceFileW
    replace_file.argtypes = (
        wintypes.LPCWSTR,
        wintypes.LPCWSTR,
        wintypes.LPCWSTR,
        wintypes.DWORD,
        wintypes.LPVOID,
        wintypes.LPVOID,
    )
    replace_file.restype = wintypes.BOOL
    if not replace_file(
        str(destination),
        str(replacement),
        None,
        REPLACEFILE_WRITE_THROUGH,
        None,
        None,
    ):
        raise ctypes.WinError(ctypes.get_last_error())


def _stage_bytes(
    registry_path: Path,
    content: bytes,
    descriptor: bytes,
    expected_sddl: str,
) -> Path:
    file_descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{registry_path.name}.",
        suffix=".tmp",
        dir=registry_path.parent,
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(file_descriptor, "wb") as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        _apply_security_descriptor(temporary, descriptor)
        if _security_descriptor_identity(temporary) != expected_sddl:
            raise RuntimeError("staged Registry security descriptor differs")
        return temporary
    except BaseException:
        temporary.unlink(missing_ok=True)
        raise


def _cleanup_orphaned_staging(registry_path: Path, expected_sddl: str) -> None:
    pattern = re.compile(
        rf"^{re.escape(f'.{registry_path.name}.')}[a-z0-9_]{{8}}\.tmp$"
    )
    for candidate in registry_path.parent.glob(f".{registry_path.name}.*.tmp"):
        if not pattern.fullmatch(candidate.name):
            raise RuntimeError(f"unrecognized Registry staging path: {candidate}")
        if _has_reparse_attribute(candidate) or not candidate.is_file():
            raise RuntimeError(f"unsafe Registry staging path: {candidate}")
        try:
            _validate_document(_load_strict_json(candidate.read_bytes()))
            if _security_descriptor_identity(candidate) != expected_sddl:
                raise RuntimeError("security authority differs")
        except BaseException as exception:
            raise RuntimeError(
                f"untrusted Registry staging residue requires operator review: {candidate}"
            ) from exception
        candidate.unlink()


def _restore_original(
    registry_path: Path,
    original: bytes,
    descriptor: bytes,
    expected_sddl: str,
) -> None:
    recovery = _stage_bytes(registry_path, original, descriptor, expected_sddl)
    try:
        _replace_file(registry_path, recovery)
        if (
            registry_path.read_bytes() != original
            or _security_descriptor_identity(registry_path) != expected_sddl
        ):
            raise RuntimeError("original Registry recovery verification failed")
    finally:
        recovery.unlink(missing_ok=True)


def _restore_after_publication_failure(
    registry_path: Path,
    original: bytes,
    descriptor: bytes,
    expected_sddl: str,
    publication_failure: BaseException,
) -> None:
    failure = f"{type(publication_failure).__name__}: {publication_failure}"
    try:
        _restore_original(registry_path, original, descriptor, expected_sddl)
    except BaseException as recovery_failure:
        raise RuntimeError(
            "Registry publication failed "
            f"({failure}); original recovery also failed "
            f"({type(recovery_failure).__name__}: {recovery_failure})"
        ) from recovery_failure
    raise RuntimeError(
        f"Registry publication failed ({failure}); original restored"
    ) from publication_failure


@contextmanager
def _publisher_lock(registry_path: Path) -> Iterator[None]:
    lock_path = registry_path.with_name(f".{registry_path.name}.publisher.lock")
    temporary_flag = getattr(os, "O_TEMPORARY", 0)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    flags |= getattr(os, "O_NOINHERIT", 0)
    flags |= temporary_flag
    try:
        descriptor = os.open(lock_path, flags, 0o600)
    except FileExistsError as exception:
        raise RuntimeError(f"another Registry publisher owns {lock_path}") from exception
    try:
        with os.fdopen(descriptor, "w", encoding="ascii", newline="\n") as stream:
            stream.write(f"pid={os.getpid()}\n")
            stream.flush()
            os.fsync(stream.fileno())
            yield
    finally:
        if temporary_flag == 0:
            lock_path.unlink(missing_ok=True)


def _read_registry(
    registry_path: Path,
) -> tuple[bytes, tuple[int, int], bytes, str]:
    identity_before = _file_identity(registry_path)
    content = registry_path.read_bytes()
    identity_after = _file_identity(registry_path)
    if identity_before != identity_after:
        raise RuntimeError("Registry identity changed during read")
    security = _security_descriptor(registry_path)
    security_sddl = _canonical_security_identity(_descriptor_sddl(security))
    if _file_identity(registry_path) != identity_before:
        raise RuntimeError("Registry identity changed during security inspection")
    return content, identity_before, security, security_sddl


def _updated_publication(
    current_document: dict[str, object],
    latest_version: str | None,
    catalog_schema_version: int | None,
    catalog_sha256: str | None,
) -> dict[str, object]:
    current = current_document["catalogPublication"]
    if not isinstance(current, dict):
        raise ValueError("catalogPublication shape is invalid")
    return {
        "latestVersion": (
            latest_version
            if latest_version is not None
            else current["latestVersion"]
        ),
        "catalogSchemaVersion": (
            catalog_schema_version
            if catalog_schema_version is not None
            else current["catalogSchemaVersion"]
        ),
        "catalogSha256": (
            catalog_sha256
            if catalog_sha256 is not None
            else current["catalogSha256"]
        ),
    }


def _validate_proposed_metadata(
    current_document: dict[str, object],
    published_at_utc: str,
    catalog_publication: dict[str, object],
) -> None:
    candidate = dict(current_document)
    candidate["publishedAtUtc"] = published_at_utc
    candidate["catalogPublication"] = catalog_publication
    _validate_document(candidate)


def _encoded_registry(
    revision: int,
    entries: tuple[dict[str, str], ...],
    registry_id: str,
    published_at_utc: str,
    catalog_publication: dict[str, object],
) -> bytes:
    encoded = (
        json.dumps(
            {
                "schemaVersion": 1,
                "registryId": registry_id,
                "registryRevision": revision,
                "publishedAtUtc": published_at_utc,
                "catalogPublication": catalog_publication,
                "entries": list(entries),
            },
            ensure_ascii=False,
            indent=2,
        )
        + "\n"
    ).encode("utf-8")
    _validate_document(_load_strict_json(encoded))
    return encoded


def _commit_registry_bytes(
    registry_path: Path,
    encoded: bytes,
    original: bytes,
    identity: tuple[int, int],
    security: bytes,
    security_sddl: str,
) -> None:
    staged = _stage_bytes(registry_path, encoded, security, security_sddl)
    try:
        if (
            registry_path.read_bytes() != original
            or _file_identity(registry_path) != identity
            or _security_descriptor_identity(registry_path) != security_sddl
        ):
            raise RuntimeError("Registry changed before the hotfix commit")
        try:
            _replace_file(registry_path, staged)
        except BaseException as replace_failure:
            try:
                original_is_intact = (
                    registry_path.read_bytes() == original
                    and _security_descriptor_identity(registry_path) == security_sddl
                )
            except BaseException as inspection_failure:
                _restore_after_publication_failure(
                    registry_path,
                    original,
                    security,
                    security_sddl,
                    RuntimeError(
                        "ReplaceFile failed and original-state inspection failed: "
                        f"{replace_failure}; {inspection_failure}"
                    ),
                )
            if not original_is_intact:
                _restore_after_publication_failure(
                    registry_path,
                    original,
                    security,
                    security_sddl,
                    replace_failure,
                )
            raise
        try:
            if (
                registry_path.read_bytes() != encoded
                or _security_descriptor_identity(registry_path) != security_sddl
            ):
                raise RuntimeError("published Registry bytes or security differ")
        except BaseException as verification_failure:
            _restore_after_publication_failure(
                registry_path,
                original,
                security,
                security_sddl,
                verification_failure,
            )
    finally:
        staged.unlink(missing_ok=True)


def repair_registry(
    registry_path: Path,
    authoritative_path: Path,
    expected_revision: int | None,
    dry_run: bool,
) -> tuple[int, int]:
    """Replace one stale replica with exact bytes from a newer authoritative replica."""

    registry_path = registry_path.absolute()
    authoritative_path = authoritative_path.absolute()
    for path, label in (
        (registry_path, "registry"),
        (authoritative_path, "authoritative registry"),
    ):
        if not path.is_file():
            raise FileNotFoundError(f"{label} file does not exist: {path}")
        _reject_reparse_chain(path)
    if registry_path == authoritative_path:
        raise ValueError("authoritative Registry must differ from the stale replica")
    if not dry_run and expected_revision is None:
        raise ValueError("--expected-revision is required for a Registry repair")

    authoritative_identity = _file_identity(authoritative_path)
    authoritative = authoritative_path.read_bytes()
    if _file_identity(authoritative_path) != authoritative_identity:
        raise RuntimeError("authoritative Registry identity changed during read")
    authoritative_document = _load_strict_json(authoritative)
    authoritative_revision, _ = _validate_document(authoritative_document)
    assert isinstance(authoritative_document, dict)

    if dry_run:
        current_document = _load_strict_json(registry_path.read_bytes())
        current_revision, _ = _validate_document(current_document)
        assert isinstance(current_document, dict)
        if expected_revision is not None and current_revision != expected_revision:
            raise ValueError(
                f"registry revision changed; expected {expected_revision}, found {current_revision}"
            )
        if current_document["registryId"] != authoritative_document["registryId"]:
            raise ValueError("authoritative Registry identity differs")
        if authoritative_revision <= current_revision:
            raise ValueError("authoritative Registry revision must be newer")
        return current_revision, authoritative_revision

    with _publisher_lock(registry_path):
        _reject_reparse_chain(registry_path)
        original, identity, security, security_sddl = _read_registry(registry_path)
        current_document = _load_strict_json(original)
        current_revision, _ = _validate_document(current_document)
        assert isinstance(current_document, dict)
        _cleanup_orphaned_staging(registry_path, security_sddl)
        if current_revision != expected_revision:
            raise ValueError(
                f"registry revision changed; expected {expected_revision}, found {current_revision}"
            )
        if current_document["registryId"] != authoritative_document["registryId"]:
            raise ValueError("authoritative Registry identity differs")
        if authoritative_revision <= current_revision:
            raise ValueError("authoritative Registry revision must be newer")
        _reject_reparse_chain(authoritative_path)
        if (
            _file_identity(authoritative_path) != authoritative_identity
            or authoritative_path.read_bytes() != authoritative
        ):
            raise RuntimeError("authoritative Registry changed during repair")
        _commit_registry_bytes(
            registry_path,
            authoritative,
            original,
            identity,
            security,
            security_sddl,
        )
    return current_revision, authoritative_revision


def update_registry(
    registry_path: Path,
    latest: str,
    available: list[str],
    deprecated: list[str],
    expected_revision: int | None,
    dry_run: bool,
    latest_version: str | None = None,
    catalog_schema_version: int | None = None,
    catalog_sha256: str | None = None,
    published_at_utc: str | None = None,
) -> tuple[int, int]:
    """Validate and atomically replace one Registry, advancing its revision once."""
    registry_path = registry_path.absolute()
    if not registry_path.is_file():
        raise FileNotFoundError(f"registry file does not exist: {registry_path}")
    _reject_reparse_chain(registry_path)
    proposed_entries = _entries(latest, available, deprecated)
    if not dry_run and expected_revision is None:
        raise ValueError("--expected-revision is required for a Registry write")

    if dry_run:
        original = registry_path.read_bytes()
        current_document = _load_strict_json(original)
        revision, current_entries = _validate_document(current_document)
        assert isinstance(current_document, dict)
        proposed_publication = _updated_publication(
            current_document,
            latest_version,
            catalog_schema_version,
            catalog_sha256,
        )
        proposed_published_at = (
            published_at_utc
            if published_at_utc is not None
            else str(current_document["publishedAtUtc"])
        )
        _validate_proposed_metadata(
            current_document,
            proposed_published_at,
            proposed_publication,
        )
        if expected_revision is not None and revision != expected_revision:
            raise ValueError(
                f"registry revision changed; expected {expected_revision}, found {revision}"
            )
        if (
            _entry_identity(proposed_entries) == _entry_identity(current_entries)
            and proposed_publication == current_document["catalogPublication"]
            and proposed_published_at == current_document["publishedAtUtc"]
        ):
            raise ValueError("Registry Catalog binding did not change")
        if revision == MAXIMUM_REVISION:
            raise ValueError("registry revision cannot advance beyond Int64 maximum")
        return revision, revision + 1

    with _publisher_lock(registry_path):
        _reject_reparse_chain(registry_path)
        original, identity, security, security_sddl = _read_registry(registry_path)
        current_document = _load_strict_json(original)
        revision, current_entries = _validate_document(current_document)
        assert isinstance(current_document, dict)
        proposed_publication = _updated_publication(
            current_document,
            latest_version,
            catalog_schema_version,
            catalog_sha256,
        )
        proposed_published_at = (
            published_at_utc
            if published_at_utc is not None
            else str(current_document["publishedAtUtc"])
        )
        _validate_proposed_metadata(
            current_document,
            proposed_published_at,
            proposed_publication,
        )
        _cleanup_orphaned_staging(registry_path, security_sddl)
        if revision != expected_revision:
            raise ValueError(
                f"registry revision changed; expected {expected_revision}, found {revision}"
            )
        if (
            _entry_identity(proposed_entries) == _entry_identity(current_entries)
            and proposed_publication == current_document["catalogPublication"]
            and proposed_published_at == current_document["publishedAtUtc"]
        ):
            raise ValueError("Registry Catalog binding did not change")
        if revision == MAXIMUM_REVISION:
            raise ValueError("registry revision cannot advance beyond Int64 maximum")
        encoded = _encoded_registry(
            revision + 1,
            proposed_entries,
            str(current_document["registryId"]),
            proposed_published_at,
            proposed_publication,
        )
        _commit_registry_bytes(
            registry_path,
            encoded,
            original,
            identity,
            security,
            security_sddl,
        )
    return revision, revision + 1


def _parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Atomically change external update roots and automatically advance the "
            "Registry revision. No package, catalog, or release checksum is rewritten."
        )
    )
    parser.add_argument("--registry", required=True, type=Path)
    parser.add_argument("--latest")
    parser.add_argument("--available", action="append", default=[])
    parser.add_argument("--deprecated", action="append", default=[])
    parser.add_argument("--expected-revision", type=int)
    parser.add_argument("--latest-version")
    parser.add_argument("--catalog-schema-version", type=int)
    parser.add_argument("--catalog-sha256")
    parser.add_argument("--published-at-utc")
    parser.add_argument("--repair-from", type=Path)
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def main() -> int:
    arguments = _parse_arguments()
    try:
        if arguments.repair_from is not None:
            if (
                arguments.latest is not None
                or arguments.available
                or arguments.deprecated
                or arguments.latest_version is not None
                or arguments.catalog_schema_version is not None
                or arguments.catalog_sha256 is not None
                or arguments.published_at_utc is not None
            ):
                raise ValueError("--repair-from cannot be combined with Catalog edits")
            previous, current = repair_registry(
                arguments.registry,
                arguments.repair_from,
                arguments.expected_revision,
                arguments.dry_run,
            )
        else:
            if arguments.latest is None:
                raise ValueError("--latest is required unless --repair-from is used")
            previous, current = update_registry(
                arguments.registry,
                arguments.latest,
                arguments.available,
                arguments.deprecated,
                arguments.expected_revision,
                arguments.dry_run,
                arguments.latest_version,
                arguments.catalog_schema_version,
                arguments.catalog_sha256,
                arguments.published_at_utc,
            )
    except (FileNotFoundError, OSError, RuntimeError, ValueError) as exception:
        print(f"error: {exception}")
        return 1
    action = "validated" if arguments.dry_run else "updated"
    print(f"Registry {action}; revision {previous} -> {current}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
