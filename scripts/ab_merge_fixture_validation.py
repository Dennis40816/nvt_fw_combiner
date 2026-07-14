"""AB Merge owner-approved fixture manifest validation."""

from collections.abc import Callable
from pathlib import Path, PurePosixPath
from typing import Any


def validate_ab_merge_golden_fixtures(
    repository_root: Path,
    load_json: Callable[[Path, list[str]], Any],
    validate_entry: Callable[..., PurePosixPath | None],
    errors: list[str],
) -> None:
    """Validate the closed AB fixture set without granting runtime support."""
    manifest_path = repository_root / "testdata/golden/ab-merge/manifest.json"
    manifest = load_json(manifest_path, errors)
    if not isinstance(manifest, dict):
        return

    if manifest.get("payloadClass") != "owner-approved-golden-firmware":
        errors.append("AB merge golden manifest must declare owner-approved-golden-firmware payloadClass")
    if manifest.get("binaryPayloadsIncluded") is not True:
        errors.append("AB merge golden manifest must explicitly include binaryPayloadsIncluded=true")

    golden_root = manifest_path.parent
    declared_bins: set[PurePosixPath] = set()
    cases = manifest.get("cases")
    if not isinstance(cases, list) or not cases:
        errors.append("AB merge golden manifest must contain cases")
        return

    expected_input_ids = {"dp-ab-input", "tp-a-input", "tp-b-input"}
    for index, item in enumerate(cases):
        if not isinstance(item, dict):
            errors.append(f"invalid AB merge golden case[{index}]")
            continue
        if not all(isinstance(item.get(key), str) and item[key] for key in ("caseId", "ic", "profileId", "profileVersion")):
            errors.append(f"AB merge golden case[{index}] lacks identity")
        if not isinstance(item.get("mapCapacity"), int) or item["mapCapacity"] <= 0:
            errors.append(f"AB merge golden case[{index}] lacks a positive mapCapacity")

        inputs = item.get("inputs")
        if not isinstance(inputs, dict) or set(inputs) != expected_input_ids:
            errors.append(f"AB merge golden case[{index}] must declare exactly {sorted(expected_input_ids)}")
        else:
            for input_id, entry in inputs.items():
                if not isinstance(entry, dict):
                    errors.append(f"AB merge golden case[{index}] input {input_id} is invalid")
                    continue
                if not isinstance(entry.get("originalFileName"), str) or not entry["originalFileName"]:
                    errors.append(f"AB merge golden case[{index}] input {input_id} lacks originalFileName")
                relative = validate_entry(golden_root, entry, errors, require_bin=True, label="AB merge")
                if relative is not None:
                    declared_bins.add(relative)

        expected = item.get("expectedOutput")
        if not isinstance(expected, dict):
            errors.append(f"AB merge golden case[{index}] has no expectedOutput")
        else:
            if not isinstance(expected.get("originalFileName"), str) or not expected["originalFileName"]:
                errors.append(f"AB merge golden case[{index}] expectedOutput lacks originalFileName")
            relative = validate_entry(golden_root, expected, errors, require_bin=True, label="AB merge")
            if relative is not None:
                declared_bins.add(relative)

        reference = item.get("referenceParity")
        if not isinstance(reference, dict) or not isinstance(reference.get("status"), str):
            errors.append(f"AB merge golden case[{index}] lacks referenceParity status")

    actual_bins = {
        PurePosixPath(path.relative_to(golden_root).as_posix())
        for path in golden_root.rglob("*.bin")
        if path.is_file()
    }
    if actual_bins != declared_bins:
        errors.append(
            "AB merge golden BIN manifest drift: "
            f"expected={sorted(path.as_posix() for path in declared_bins)} "
            f"actual={sorted(path.as_posix() for path in actual_bins)}"
        )
