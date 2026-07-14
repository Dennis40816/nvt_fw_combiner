"""Validate owner-approved firmware golden fixture manifests and payloads."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path, PurePosixPath
from typing import Any

from verify_ctrlram_replace_fixture import verify_fixture_manifest

ROOT = Path(__file__).resolve().parents[1]


def validate_golden_fixtures(errors: list[str]) -> None:
    validate_owner_approved_flat_golden_fixture(
        errors,
        manifest_relative="testdata/golden/ab-merge/manifest.json",
        label="AB Merge",
        require_case_approval=True,
    )
    validate_owner_approved_flat_golden_fixture(
        errors,
        manifest_relative="testdata/golden/standard-merge-reference/nt51950/manifest.json",
        label="standard-merge reference",
        require_case_approval=True,
    )
    validate_standard_merge_golden_fixtures(errors)
    validate_ctrlram_replace_golden_fixtures(errors)


def validate_owner_approved_flat_golden_fixture(
    errors: list[str],
    *,
    manifest_relative: str,
    label: str,
    require_case_approval: bool,
) -> None:
    manifest_path = ROOT / manifest_relative
    manifest = load_json(manifest_path, errors)
    if not isinstance(manifest, dict):
        return

    validate_owner_approved_golden_manifest(manifest, errors, label=label, require_source_approval=True)
    golden_root = manifest_path.parent
    declared_bins: set[PurePosixPath] = set()
    cases = manifest.get("cases")
    if not isinstance(cases, list) or not cases:
        errors.append(f"{label} golden manifest must contain cases")
        return

    for index, item in enumerate(cases):
        if not isinstance(item, dict):
            errors.append(f"invalid {label} golden case[{index}]")
            continue
        if require_case_approval and not has_nonempty_string(item, "ownerApproval"):
            errors.append(f"{label} golden case[{index}] has no ownerApproval")
        collect_input_entries(golden_root, item.get("inputs"), declared_bins, errors, label=label, case_index=index)
        collect_expected_output(golden_root, item.get("expectedOutput"), declared_bins, errors, label=label, case_index=index)

    validate_bin_inventory(golden_root, declared_bins, errors, label=label)


def validate_standard_merge_golden_fixtures(errors: list[str]) -> None:
    manifest_path = ROOT / "testdata/golden/standard-merge-gen-flash/manifest.json"
    manifest = load_json(manifest_path, errors)
    if not isinstance(manifest, dict):
        return

    validate_owner_approved_golden_manifest(manifest, errors, label="standard-merge", require_source_approval=False)
    golden_root = manifest_path.parent
    declared_bins: set[PurePosixPath] = set()
    supporting = manifest.get("supportingFiles")
    if isinstance(supporting, dict) and isinstance(supporting.get("test_ic_config"), dict):
        validate_golden_manifest_entry(golden_root, supporting["test_ic_config"], errors, require_bin=False, label="standard-merge")

    cases = manifest.get("cases")
    if not isinstance(cases, list) or not cases:
        errors.append("standard-merge golden manifest must contain cases")
        return
    for index, item in enumerate(cases):
        if not isinstance(item, dict):
            errors.append(f"invalid standard-merge golden case[{index}]")
            continue
        collect_input_entries(golden_root, item.get("inputs"), declared_bins, errors, label="standard-merge", case_index=index)
        collect_expected_output(golden_root, item.get("expectedOutput"), declared_bins, errors, label="standard-merge", case_index=index)

    validate_bin_inventory(golden_root, declared_bins, errors, label="standard-merge")


def validate_ctrlram_replace_golden_fixtures(errors: list[str]) -> None:
    manifest_path = ROOT / "testdata/golden/ctrlram-replace/manifest.json"
    try:
        verify_fixture_manifest(manifest_path)
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        errors.append(f"ctrlram-replace golden manifest contract invalid: {exc}")
    manifest = load_json(manifest_path, errors)
    if not isinstance(manifest, dict):
        return

    validate_owner_approved_golden_manifest(manifest, errors, label="ctrlram-replace", require_source_approval=False)
    golden_root = manifest_path.parent
    declared_bins: set[PurePosixPath] = set()
    cases = manifest.get("cases")
    if not isinstance(cases, list) or not cases:
        errors.append("ctrlram-replace golden manifest must contain cases")
        return

    for index, item in enumerate(cases):
        if not isinstance(item, dict):
            errors.append(f"invalid ctrlram-replace golden case[{index}]")
            continue
        collect_single_entry(golden_root, item.get("base"), declared_bins, errors, label="ctrlram-replace", missing=f"ctrlram-replace golden case[{index}] has no base")
        replacements = item.get("replacementInputs")
        if not isinstance(replacements, list) or not replacements:
            errors.append(f"ctrlram-replace golden case[{index}] has no replacementInputs")
            continue
        for replacement_index, replacement in enumerate(replacements):
            if not isinstance(replacement, dict):
                errors.append(f"invalid ctrlram-replace golden case[{index}].replacementInputs[{replacement_index}]")
                continue
            collect_single_entry(
                golden_root,
                replacement.get("file"),
                declared_bins,
                errors,
                label="ctrlram-replace",
                missing=f"ctrlram-replace golden case[{index}].replacementInputs[{replacement_index}] has no file",
            )
        expected = item.get("expectedOutput")
        if isinstance(expected, dict):
            collect_single_entry(
                golden_root,
                expected,
                declared_bins,
                errors,
                label="ctrlram-replace",
                missing=None,
            )

    validate_bin_inventory(golden_root, declared_bins, errors, label="ctrlram-replace")


def validate_owner_approved_golden_manifest(
    manifest: dict[str, Any], errors: list[str], *, label: str, require_source_approval: bool
) -> None:
    if manifest.get("payloadClass") != "owner-approved-golden-firmware":
        errors.append(f"{label} golden manifest must declare owner-approved-golden-firmware payloadClass")
    if manifest.get("binaryPayloadsIncluded") is not True:
        errors.append(f"{label} golden manifest must explicitly include binaryPayloadsIncluded=true")
    if require_source_approval:
        source = manifest.get("source")
        if not isinstance(source, dict) or not has_nonempty_string(source, "approval"):
            errors.append(f"{label} golden manifest must record source approval")


def collect_input_entries(
    golden_root: Path,
    inputs: Any,
    declared_bins: set[PurePosixPath],
    errors: list[str],
    *,
    label: str,
    case_index: int,
) -> None:
    if not isinstance(inputs, dict) or not inputs:
        errors.append(f"{label} golden case[{case_index}] has no inputs")
        return
    for entry in inputs.values():
        collect_single_entry(
            golden_root,
            entry,
            declared_bins,
            errors,
            label=label,
            missing=f"invalid {label} golden case[{case_index}] input entry",
        )


def collect_expected_output(
    golden_root: Path,
    expected: Any,
    declared_bins: set[PurePosixPath],
    errors: list[str],
    *,
    label: str,
    case_index: int,
) -> None:
    collect_single_entry(
        golden_root,
        expected,
        declared_bins,
        errors,
        label=label,
        missing=f"{label} golden case[{case_index}] has no expectedOutput",
    )


def collect_single_entry(
    golden_root: Path,
    entry: Any,
    declared_bins: set[PurePosixPath],
    errors: list[str],
    *,
    label: str,
    missing: str | None,
) -> None:
    if not isinstance(entry, dict):
        if missing is not None:
            errors.append(missing)
        return
    relative = validate_golden_manifest_entry(golden_root, entry, errors, require_bin=True, label=label)
    if relative is not None:
        declared_bins.add(relative)


def validate_bin_inventory(
    golden_root: Path, declared_bins: set[PurePosixPath], errors: list[str], *, label: str
) -> None:
    actual_bins = {
        PurePosixPath(path.relative_to(golden_root).as_posix())
        for path in golden_root.rglob("*.bin")
        if path.is_file()
    }
    if actual_bins != declared_bins:
        errors.append(
            f"{label} golden BIN manifest drift: "
            f"expected={sorted(path.as_posix() for path in declared_bins)} "
            f"actual={sorted(path.as_posix() for path in actual_bins)}"
        )


def validate_golden_manifest_entry(
    golden_root: Path,
    entry: dict[str, Any],
    errors: list[str],
    *,
    require_bin: bool,
    label: str,
) -> PurePosixPath | None:
    relative_text = entry.get("path")
    expected_size = entry.get("size")
    expected_hash = entry.get("sha256")
    if not isinstance(relative_text, str) or not isinstance(expected_size, int) or not isinstance(expected_hash, str):
        errors.append(f"invalid {label} golden manifest file entry")
        return None
    relative = PurePosixPath(relative_text)
    if relative.is_absolute() or ".." in relative.parts:
        errors.append(f"unsafe {label} golden manifest path: {relative_text}")
        return None
    if require_bin and relative.suffix.lower() != ".bin":
        errors.append(f"{label} golden payload is not a BIN file: {relative_text}")
        return None
    candidate = (golden_root / Path(*relative.parts)).resolve()
    try:
        candidate.relative_to(golden_root.resolve())
    except ValueError:
        errors.append(f"{label} golden manifest path escapes fixture root: {relative_text}")
        return None
    if not candidate.is_file():
        errors.append(f"{label} golden manifest file missing: {candidate.relative_to(ROOT)}")
        return relative
    if candidate.stat().st_size != expected_size:
        errors.append(f"{label} golden size drift: {candidate.relative_to(ROOT)}")
    if sha256(candidate) != expected_hash.lower():
        errors.append(f"{label} golden hash drift: {candidate.relative_to(ROOT)}")
    return relative


def has_nonempty_string(value: dict[str, Any], key: str) -> bool:
    candidate = value.get(key)
    return isinstance(candidate, str) and bool(candidate.strip())


def load_json(path: Path, errors: list[str]) -> Any | None:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        errors.append(f"invalid JSON {path.relative_to(ROOT)}: {exc}")
        return None


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()
