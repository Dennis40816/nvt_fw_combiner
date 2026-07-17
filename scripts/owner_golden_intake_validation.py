"""Validate the owner-approved 2026-07-17 golden intake snapshot."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path, PurePosixPath
from typing import Any


def verify_owner_golden_intake_20260717(golden_root: Path) -> None:
    """Verify every payload and supporting file in the intake manifest."""
    manifest_path = golden_root / "manifest.20260717.json"
    document = json.loads(manifest_path.read_text(encoding="utf-8"))
    require(isinstance(document, dict), "20260717 intake manifest root must be an object")
    expected_facts = {
        "schemaVersion": "0.1",
        "payloadClass": "owner-approved-golden-firmware",
        "binaryPayloadsIncluded": True,
        "runtimeSupportPromotion": False,
    }
    for key, expected in expected_facts.items():
        require(
            document.get(key) == expected,
            f"20260717 intake manifest must declare {key}={expected!r}",
        )

    declared_files: set[PurePosixPath] = set()
    for collection_name, require_bin in (("payloads", True), ("supportingFiles", False)):
        entries = document.get(collection_name)
        require(
            isinstance(entries, list) and bool(entries),
            f"20260717 intake manifest must contain {collection_name}",
        )
        for index, entry in enumerate(entries):
            require(
                isinstance(entry, dict),
                f"20260717 intake {collection_name}[{index}] must be an object",
            )
            relative = verify_entry(golden_root, entry, require_bin)
            require(
                relative not in declared_files,
                f"20260717 intake path is duplicated: {relative}",
            )
            declared_files.add(relative)

    fixture_root = golden_root / "fixtures/20260717"
    actual_files = {
        PurePosixPath(path.relative_to(golden_root).as_posix())
        for path in fixture_root.rglob("*")
        if path.is_file()
    }
    require(
        actual_files == declared_files,
        "20260717 intake manifest inventory does not match fixtures/20260717",
    )


def verify_entry(
    golden_root: Path,
    entry: dict[str, Any],
    require_bin: bool,
) -> PurePosixPath:
    relative_text = entry.get("path")
    expected_size = entry.get("size")
    expected_hash = entry.get("sha256")
    require(isinstance(relative_text, str) and bool(relative_text), "20260717 intake path is required")
    require(isinstance(expected_size, int) and expected_size >= 0, "20260717 intake size is invalid")
    require(isinstance(expected_hash, str) and len(expected_hash) == 64, "20260717 intake hash is invalid")
    require(isinstance(entry.get("role"), str) and bool(entry["role"].strip()), "20260717 intake role is required")

    relative = PurePosixPath(relative_text)
    require(not relative.is_absolute() and ".." not in relative.parts, f"unsafe 20260717 intake path: {relative}")
    require(not require_bin or relative.suffix.lower() == ".bin", f"20260717 intake payload is not BIN: {relative}")
    require(entry.get("originalFileName") == relative.name, f"20260717 intake original filename drift: {relative}")

    candidate = (golden_root / Path(*relative.parts)).resolve()
    require(candidate.is_relative_to(golden_root.resolve()), f"20260717 intake path escapes fixture root: {relative}")
    require(candidate.is_file(), f"20260717 intake file is missing: {relative}")
    require(candidate.stat().st_size == expected_size, f"20260717 intake size drift: {relative}")
    require(sha256(candidate) == expected_hash.lower(), f"20260717 intake hash drift: {relative}")
    return relative


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)
