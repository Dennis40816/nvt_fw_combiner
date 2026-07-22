"""Validate repository-only golden diagnostic separation."""

from __future__ import annotations

import hashlib
from pathlib import Path, PurePosixPath

try:
    from canonical_golden_validation import (
        DIAGNOSTICS_ROOT,
        DIAGNOSTIC_CTRLRAM_ARTIFACT_ROOT,
        DIAGNOSTIC_CTRLRAM_INVENTORY,
        DIAGNOSTIC_OWNER_HANDOFF_ROOT,
        SHA256_PATTERN,
        _load_object,
        _read_confined_file,
        _relative_path,
    )
except ModuleNotFoundError:
    from scripts.canonical_golden_validation import (
        DIAGNOSTICS_ROOT,
        DIAGNOSTIC_CTRLRAM_ARTIFACT_ROOT,
        DIAGNOSTIC_CTRLRAM_INVENTORY,
        DIAGNOSTIC_OWNER_HANDOFF_ROOT,
        SHA256_PATTERN,
        _load_object,
        _read_confined_file,
        _relative_path,
    )


def validate_diagnostic_golden_separation(
    repository_root: Path, errors: list[str]
) -> None:
    """Validate the closed repository-only diagnostic quarantine inventory."""
    diagnostics_root = repository_root / Path(DIAGNOSTICS_ROOT)
    manifest = _load_object(
        diagnostics_root / "manifest.json",
        diagnostics_root,
        "diagnostic golden manifest",
        errors,
    )
    if manifest is None:
        return
    expected_root_files = {
        PurePosixPath("README.md"),
        PurePosixPath("manifest.json"),
    }
    records = manifest.get("records")
    if not isinstance(records, list):
        errors.append("diagnostic golden manifest records must be an array")
        records = []
    for index, record in enumerate(records):
        label = f"diagnostic golden records[{index}]"
        if not isinstance(record, dict):
            errors.append(f"{label} must be an object")
            continue
        relative_path = _relative_path(record.get("path"), f"{label}.path", errors)
        if relative_path is None:
            continue
        if relative_path in expected_root_files:
            errors.append(
                f"diagnostic golden record is declared more than once: {relative_path}"
            )
        expected_root_files.add(relative_path)
        payload = _read_confined_file(
            diagnostics_root / Path(relative_path),
            diagnostics_root,
            f"diagnostic golden record {relative_path}",
            errors,
        )
        if payload is None:
            continue
        if record.get("size") != len(payload):
            errors.append(f"diagnostic golden record size mismatch for {relative_path}")
        expected_sha = record.get("sha256")
        if (
            not isinstance(expected_sha, str)
            or SHA256_PATTERN.fullmatch(expected_sha) is None
            or expected_sha != hashlib.sha256(payload).hexdigest()
        ):
            errors.append(
                f"diagnostic golden record SHA-256 mismatch for {relative_path}"
            )
    diagnostic_entries = list(diagnostics_root.rglob("*"))
    for path in diagnostic_entries:
        if path.is_symlink():
            errors.append(f"diagnostic golden root cannot contain symlinks: {path}")
    actual_root_files = {
        PurePosixPath(path.relative_to(diagnostics_root).as_posix())
        for path in diagnostic_entries
        if path.is_file() and not path.is_symlink()
    }
    if actual_root_files != expected_root_files:
        errors.append(
            "diagnostic golden root differs from its closed records inventory"
        )
    expected_fields = {
        "schemaVersion": "1.0",
        "payloadClass": "repository-only-diagnostic-evidence",
        "canonicalExpected": False,
        "runtimeSupportPromotion": False,
        "releaseRedistributionApproved": False,
        "physicalPayloadMovement": "frozen-after-9e15bc0f",
    }
    for key, expected in expected_fields.items():
        if manifest.get(key) != expected:
            errors.append(f"diagnostic golden manifest {key} must be {expected!r}")

    quarantine = manifest.get("ctrlRamLegacyQuarantine")
    if not isinstance(quarantine, dict):
        errors.append("diagnostic golden manifest must declare ctrlRamLegacyQuarantine")
        return
    artifact_root_path = _relative_path(
        quarantine.get("artifactRoot"),
        "diagnostic ctrlRamLegacyQuarantine.artifactRoot",
        errors,
    )
    inventory_path = _relative_path(
        quarantine.get("inventoryManifest"),
        "diagnostic ctrlRamLegacyQuarantine.inventoryManifest",
        errors,
    )
    if artifact_root_path is None or inventory_path is None:
        return
    if artifact_root_path != DIAGNOSTIC_CTRLRAM_ARTIFACT_ROOT:
        errors.append(
            "diagnostic CtrlRAM artifactRoot must remain frozen at "
            f"{DIAGNOSTIC_CTRLRAM_ARTIFACT_ROOT}"
        )
    if inventory_path != DIAGNOSTIC_CTRLRAM_INVENTORY:
        errors.append(
            "diagnostic CtrlRAM inventoryManifest must remain frozen at "
            f"{DIAGNOSTIC_CTRLRAM_INVENTORY}"
        )
    artifact_root = repository_root / Path(artifact_root_path)
    inventory = _load_object(
        repository_root / Path(inventory_path),
        repository_root,
        "diagnostic CtrlRAM inventory",
        errors,
    )
    if inventory is None:
        return
    inventory_fields = {
        "payloadClass": "repository-only-diagnostic-evidence",
        "binaryPayloadsIncluded": True,
        "canonicalExpected": False,
        "runtimeSupportPromotion": False,
        "releaseRedistributionApproved": False,
    }
    for key, expected in inventory_fields.items():
        if inventory.get(key) != expected:
            errors.append(f"diagnostic CtrlRAM inventory {key} must be {expected!r}")
    source_archive = inventory.get("sourceArchive")
    source_sha = quarantine.get("sourceArchiveSha256")
    if (
        not isinstance(source_sha, str)
        or SHA256_PATTERN.fullmatch(source_sha) is None
        or not isinstance(source_archive, dict)
        or source_archive.get("sha256") != source_sha
    ):
        errors.append(
            "diagnostic CtrlRAM source archive SHA-256 must match its inventory"
        )

    payloads = inventory.get("payloads")
    supporting_files = inventory.get("supportingFiles")
    if not isinstance(payloads, list) or not isinstance(supporting_files, list):
        errors.append("diagnostic CtrlRAM inventory payload arrays are required")
        return
    if quarantine.get("payloadCount") != len(payloads):
        errors.append("diagnostic CtrlRAM payloadCount does not match its inventory")
    if quarantine.get("supportingFileCount") != len(supporting_files):
        errors.append(
            "diagnostic CtrlRAM supportingFileCount does not match its inventory"
        )

    declared_files: set[PurePosixPath] = set()
    inventory_root = inventory_path.parent
    for index, item in enumerate([*payloads, *supporting_files]):
        label = f"diagnostic CtrlRAM artifacts[{index}]"
        if not isinstance(item, dict):
            errors.append(f"{label} must be an object")
            continue
        relative_path = _relative_path(item.get("path"), f"{label}.path", errors)
        if relative_path is None:
            continue
        repository_path = inventory_root / relative_path
        if repository_path in declared_files:
            errors.append(
                f"diagnostic CtrlRAM path is declared more than once: {repository_path}"
            )
        declared_files.add(repository_path)
        if (
            len(repository_path.parts) <= len(artifact_root_path.parts)
            or repository_path.parts[: len(artifact_root_path.parts)]
            != artifact_root_path.parts
        ):
            errors.append(
                f"{label} is outside the frozen artifact root: {repository_path}"
            )
            continue
        payload = _read_confined_file(
            repository_root / Path(repository_path),
            artifact_root,
            f"diagnostic artifact {repository_path}",
            errors,
        )
        if payload is None:
            continue
        expected_size = item.get("size")
        expected_sha = item.get("sha256")
        if type(expected_size) is not int or expected_size != len(payload):
            errors.append(f"diagnostic artifact size mismatch for {repository_path}")
        if (
            not isinstance(expected_sha, str)
            or SHA256_PATTERN.fullmatch(expected_sha) is None
            or expected_sha != hashlib.sha256(payload).hexdigest()
        ):
            errors.append(f"diagnostic artifact SHA-256 mismatch for {repository_path}")
    artifact_entries = list(artifact_root.rglob("*"))
    for path in artifact_entries:
        if path.is_symlink():
            errors.append(
                f"diagnostic CtrlRAM artifact root cannot contain symlinks: {path}"
            )
    actual_files = {
        PurePosixPath(path.relative_to(repository_root).as_posix())
        for path in artifact_entries
        if path.is_file() and not path.is_symlink()
    }
    if actual_files != declared_files:
        errors.append(
            "diagnostic CtrlRAM artifact root differs from its closed inventory"
        )

    owner_handoff = manifest.get("ownerHandoff")
    if not isinstance(owner_handoff, dict):
        errors.append("diagnostic golden manifest must declare ownerHandoff")
        return
    owner_root_path = _relative_path(
        owner_handoff.get("root"), "diagnostic ownerHandoff.root", errors
    )
    if owner_root_path is None:
        return
    if owner_root_path != DIAGNOSTIC_OWNER_HANDOFF_ROOT:
        errors.append(
            "diagnostic ownerHandoff root must remain frozen at "
            f"{DIAGNOSTIC_OWNER_HANDOFF_ROOT}"
        )
    owner_root = repository_root / Path(owner_root_path)
    try:
        resolved_owner_root = owner_root.resolve(strict=True)
        resolved_repository_root = repository_root.resolve(strict=True)
    except OSError as error:
        errors.append(f"cannot resolve diagnostic ownerHandoff root: {error}")
        return
    if (
        owner_root.is_symlink()
        or resolved_repository_root not in resolved_owner_root.parents
    ):
        errors.append(
            "diagnostic ownerHandoff root must be a physical repository directory"
        )
        return
    owner_entries = list(owner_root.rglob("*"))
    for path in owner_entries:
        if path.is_symlink():
            errors.append(f"diagnostic ownerHandoff cannot contain symlinks: {path}")
    # CASE.md records owner-provided investigation payloads under ignored intake directories.
    # They are private workspace evidence, never repository inventory or release material.
    owner_files = [
        path
        for path in owner_entries
        if path.is_file()
        and not path.is_symlink()
        and "intake" not in path.relative_to(owner_root).parts
    ]
    if owner_handoff.get("fileCount") != len(owner_files):
        errors.append("diagnostic ownerHandoff fileCount does not match its inventory")
    if owner_handoff.get("binaryPayloadsIncluded") is not False:
        errors.append(
            "diagnostic ownerHandoff must declare binaryPayloadsIncluded=false"
        )
    tree_records: list[str] = []
    for path in sorted(
        owner_files, key=lambda item: item.relative_to(owner_root).as_posix()
    ):
        if path.name not in {".gitignore", ".keep"} and path.suffix.lower() != ".md":
            errors.append(
                f"diagnostic ownerHandoff contains a binary or unsupported file: {path}"
            )
        payload = _read_confined_file(
            path, owner_root, f"diagnostic ownerHandoff file {path}", errors
        )
        if payload is not None:
            relative_path = path.relative_to(owner_root).as_posix()
            tree_records.append(
                f"{relative_path}\0{len(payload)}\0{hashlib.sha256(payload).hexdigest()}\n"
            )
    tree_sha = owner_handoff.get("treeSha256")
    if (
        not isinstance(tree_sha, str)
        or SHA256_PATTERN.fullmatch(tree_sha) is None
        or tree_sha != hashlib.sha256("".join(tree_records).encode("utf-8")).hexdigest()
    ):
        errors.append(
            "diagnostic ownerHandoff treeSha256 does not match its closed inventory"
        )
