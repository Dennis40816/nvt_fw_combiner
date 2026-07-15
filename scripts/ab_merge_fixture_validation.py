"""AB Merge owner-approved fixture manifest validation."""

from collections.abc import Callable
from pathlib import Path, PurePosixPath
from typing import Any


EXPECTED_CASES: dict[str, dict[str, Any]] = {
    "nt51929-ab-t05-d06": {
        "ic": "51929",
        "profileId": "nt51929-ab-merge",
        "profileVersion": "0.1.0",
        "mapCapacity": 524288,
        "directMemberIds": ["NT51929"],
        "factScopedAliasMemberIds": ["NT51919"],
        "notEstablishedMemberIds": ["NT51932"],
        "referenceStatus": "full-byte-match",
        "referenceConfiguration": "51929",
        "promotion": "executable-candidate only; firmware-owner review remains required",
    },
    "nt51950-ab-boe-d82t80": {
        "ic": "51950",
        "profileId": "nt51950-ab-merge",
        "profileVersion": "0.1.1",
        "mapCapacity": 524288,
        "directMemberIds": ["NT51950"],
        "factScopedAliasMemberIds": [],
        "notEstablishedMemberIds": ["NT51951"],
        "referenceStatus": (
            "full-byte-match-to-uploaded-python-reference-and-legacy-combiner-1.13.0"
        ),
        "referenceConfiguration": "51950",
        "promotion": (
            "executable candidate only; full-byte Python/Combiner parity is tracked, but "
            "firmware-owner review remains required before runtime exposure"
        ),
    },
    "nt51950-ab-hiway-d82t80": {
        "ic": "51950",
        "profileId": "nt51950-ab-merge",
        "profileVersion": "0.1.1",
        "mapCapacity": 524288,
        "directMemberIds": ["NT51950"],
        "factScopedAliasMemberIds": [],
        "notEstablishedMemberIds": ["NT51951"],
        "referenceStatus": (
            "full-byte-match-to-uploaded-python-reference-and-legacy-combiner-1.13.0"
        ),
        "referenceConfiguration": "51950",
        "promotion": (
            "executable candidate only; full-byte Python/Combiner parity is tracked, but "
            "firmware-owner review remains required before runtime exposure"
        ),
    },
}


def _validate_case_evidence(
    item: dict[str, Any], index: int, errors: list[str]
) -> str | None:
    case_id = item.get("caseId")
    if not isinstance(case_id, str) or not case_id:
        errors.append(f"AB merge golden case[{index}] lacks caseId")
        return None

    expected = EXPECTED_CASES.get(case_id)
    if expected is None:
        errors.append(
            f"AB merge golden manifest contains unexpected caseId '{case_id}'"
        )
        return case_id

    for property_name in ("ic", "profileId", "profileVersion", "mapCapacity"):
        if item.get(property_name) != expected[property_name]:
            errors.append(
                f"AB merge golden case '{case_id}' has unexpected {property_name}: "
                f"expected={expected[property_name]!r} actual={item.get(property_name)!r}"
            )

    applicability = item.get("evidenceApplicability")
    if not isinstance(applicability, dict):
        errors.append(f"AB merge golden case '{case_id}' lacks evidenceApplicability")
    else:
        for property_name in (
            "directMemberIds",
            "factScopedAliasMemberIds",
            "notEstablishedMemberIds",
        ):
            if applicability.get(property_name) != expected[property_name]:
                errors.append(
                    f"AB merge golden case '{case_id}' has unexpected applicability "
                    f"{property_name}: expected={expected[property_name]!r} "
                    f"actual={applicability.get(property_name)!r}"
                )

    reference = item.get("referenceParity")
    if not isinstance(reference, dict):
        errors.append(f"AB merge golden case '{case_id}' lacks referenceParity")
    else:
        expected_reference = {
            "status": expected["referenceStatus"],
            "snapshot": "refcode/ab_code_combiner",
            "configuration": expected["referenceConfiguration"],
            "observedOn": "2026-07-15",
        }
        if reference != expected_reference:
            errors.append(
                f"AB merge golden case '{case_id}' referenceParity drift: "
                f"expected={expected_reference!r} actual={reference!r}"
            )

    if item.get("promotion") != expected["promotion"]:
        errors.append(f"AB merge golden case '{case_id}' promotion gate drift")
    return case_id


def _validate_artifact_provenance(
    entry: dict[str, Any], case_id: str, artifact_id: str, errors: list[str]
) -> None:
    for property_name in ("originalFileName", "sourcePath"):
        if not isinstance(entry.get(property_name), str) or not entry[property_name]:
            errors.append(
                f"AB merge golden case '{case_id}' artifact '{artifact_id}' "
                f"lacks {property_name}"
            )


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
        errors.append(
            "AB merge golden manifest must declare owner-approved-golden-firmware payloadClass"
        )
    if manifest.get("binaryPayloadsIncluded") is not True:
        errors.append(
            "AB merge golden manifest must explicitly include binaryPayloadsIncluded=true"
        )

    source = manifest.get("source")
    if not isinstance(source, dict):
        errors.append("AB merge golden manifest must declare source provenance")
    else:
        for property_name in (
            "archive",
            "archiveSha256",
            "transfer",
            "sourceClassification",
            "approval",
        ):
            if (
                not isinstance(source.get(property_name), str)
                or not source[property_name]
            ):
                errors.append(f"AB merge golden manifest source lacks {property_name}")

    golden_root = manifest_path.parent
    declared_bins: set[PurePosixPath] = set()
    cases = manifest.get("cases")
    if not isinstance(cases, list) or not cases:
        errors.append("AB merge golden manifest must contain cases")
        return

    expected_input_ids = {"dp-ab-input", "tp-a-input", "tp-b-input"}
    seen_case_ids: set[str] = set()
    for index, item in enumerate(cases):
        if not isinstance(item, dict):
            errors.append(f"invalid AB merge golden case[{index}]")
            continue
        case_id = _validate_case_evidence(item, index, errors)
        if case_id is None:
            case_id = f"case[{index}]"
        elif case_id in seen_case_ids:
            errors.append(f"AB merge golden manifest repeats caseId '{case_id}'")
        else:
            seen_case_ids.add(case_id)

        inputs = item.get("inputs")
        if not isinstance(inputs, dict) or set(inputs) != expected_input_ids:
            errors.append(
                f"AB merge golden case[{index}] must declare exactly {sorted(expected_input_ids)}"
            )
        else:
            for input_id, entry in inputs.items():
                if not isinstance(entry, dict):
                    errors.append(
                        f"AB merge golden case[{index}] input {input_id} is invalid"
                    )
                    continue
                _validate_artifact_provenance(entry, case_id, input_id, errors)
                relative = validate_entry(
                    golden_root, entry, errors, require_bin=True, label="AB merge"
                )
                if relative is not None:
                    declared_bins.add(relative)

        expected = item.get("expectedOutput")
        if not isinstance(expected, dict):
            errors.append(f"AB merge golden case[{index}] has no expectedOutput")
        else:
            _validate_artifact_provenance(expected, case_id, "expectedOutput", errors)
            relative = validate_entry(
                golden_root, expected, errors, require_bin=True, label="AB merge"
            )
            if relative is not None:
                declared_bins.add(relative)

    if seen_case_ids != set(EXPECTED_CASES):
        errors.append(
            "AB merge golden case inventory drift: "
            f"expected={sorted(EXPECTED_CASES)} actual={sorted(seen_case_ids)}"
        )

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
