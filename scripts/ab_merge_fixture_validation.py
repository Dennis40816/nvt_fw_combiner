"""AB Merge owner-approved fixture manifest validation."""

from collections.abc import Callable
from pathlib import Path, PurePosixPath
from typing import Any


EXPECTED_CASES: dict[str, dict[str, Any]] = {
    "nt51929-ab-t05-d06": {
        "ic": "NT51929",
        "variantOrVersion": "t05-d06",
        "profileId": "nt51929-ab-merge",
        "profileVersion": "0.2.0",
        "mapCapacity": 524288,
        "directMemberIds": ["NT51929"],
        "factScopedAliasMemberIds": ["NT51919", "NT51932"],
        "notEstablishedMemberIds": [],
        "referenceStatus": "full-byte-match",
        "referenceConfiguration": "51929",
        "promotion": (
            "supported runtime pilot for NT51919, NT51929, and NT51932 under "
            "the owner-approved fixed AB plan; NT51950/NT51951 remain excluded"
        ),
    },
    "nt51950-ab-boe-d82t80": {
        "ic": "NT51950",
        "variantOrVersion": "boe-d82t80",
        "profileId": "nt51950-ab-merge",
        "profileVersion": "0.1.1",
        "mapCapacity": 524288,
        "directMemberIds": ["NT51950"],
        "factScopedAliasMemberIds": ["NT51951"],
        "notEstablishedMemberIds": [],
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
        "ic": "NT51950",
        "variantOrVersion": "hiway-d82t80",
        "profileId": "nt51950-ab-merge",
        "profileVersion": "0.1.1",
        "mapCapacity": 524288,
        "directMemberIds": ["NT51950"],
        "factScopedAliasMemberIds": ["NT51951"],
        "notEstablishedMemberIds": [],
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

EXPECTED_ALIASES = {
    "nt51919-ab-t05-d06-alias": {
        "ic": "NT51919",
        "variantOrVersion": "t05-d06",
        "sourceCaseId": "nt51929-ab-t05-d06",
        "ownerApproval": "Firmware owner approved the NT51929 AB evidence as fact-scoped NT51919 family evidence on 2026-07-18.",
        "factScope": [
            "NT51929-family AB composition facts for the referenced direct case",
            "full-DP initializer and TPA/TPB logical input roles",
            "referenced direct-case output bytes only; not a direct NT51919 product golden",
        ],
    },
    "nt51932-ab-t05-d06-alias": {
        "ic": "NT51932",
        "variantOrVersion": "t05-d06",
        "sourceCaseId": "nt51929-ab-t05-d06",
        "ownerApproval": "Firmware owner approved the NT51929 AB evidence as fact-scoped NT51932 family evidence on 2026-07-18.",
        "factScope": [
            "NT51929-family AB composition facts for the referenced direct case",
            "full-DP initializer and TPA/TPB logical input roles",
            "referenced direct-case output bytes only; NT51932 named-configuration parity remains separate",
        ],
    },
    "nt51951-ab-boe-d82t80-workflow-alias": {
        "ic": "NT51951",
        "variantOrVersion": "boe-d82t80",
        "sourceCaseId": "nt51950-ab-boe-d82t80",
        "ownerApproval": "Firmware owner approved NT51950 AB evidence as NT51951 workflow-logic evidence on 2026-07-18.",
        "factScope": [
            "full-DP initializer",
            "NT51950BASED_MERGE_AB_MODE command family and no map.txt requirement",
            "Combiner-owned AB header CRC behavior",
            "referenced NT51950 direct-case bytes only; NT51951 0x80000 placement remains synthetic topology evidence",
        ],
    },
    "nt51951-ab-hiway-d82t80-workflow-alias": {
        "ic": "NT51951",
        "variantOrVersion": "hiway-d82t80",
        "sourceCaseId": "nt51950-ab-hiway-d82t80",
        "ownerApproval": "Firmware owner approved NT51950 AB evidence as NT51951 workflow-logic evidence on 2026-07-18.",
        "factScope": [
            "full-DP initializer",
            "NT51950BASED_MERGE_AB_MODE command family and no map.txt requirement",
            "Combiner-owned AB header CRC behavior",
            "referenced NT51950 direct-case bytes only; NT51951 0x80000 placement remains synthetic topology evidence",
        ],
    },
}

ALIAS_EVIDENCE_REFS = [
    "docs/governance/v0.9.9.5-canonical-golden-tool-consolidation-plan.md",
    "docs/architecture/supported-ic-matrix.md",
]

NT51929_CTRLRAM_FIRST_HALF_EVIDENCE = {
    "status": "fact-scoped-allowed-diff-parity",
    "sourceArtifact": "expected-output",
    "sourceRange": {
        "start": 0,
        "length": 262144,
        "sha256": "e257e734a63d0d8a0e471bc7b541366578b9b56c94dd914197508d5af1127c12",
    },
    "sameProductTpArtifact": "tp-a-input",
    "sameProductRegions": [
        {
            "name": "NF CtrlRAM",
            "start": 130048,
            "length": 8080,
            "sha256": "417e04c58e2a587eeb02eb4c873d23cc8ae0478c0807c6b171c4cd6b301d42de",
        },
        {
            "name": "Normal CtrlRAM",
            "start": 138128,
            "length": 18944,
            "sha256": "9ec62a8200f7f305ab729b18b77e747572af493e2fdc32e7bbfb91c02c221c66",
        },
        {
            "name": "VN CtrlRAM",
            "start": 157072,
            "length": 6496,
            "sha256": "c90ced38fc2cc8e62cd0275050f7bfcc05e707eaaffd9704533f9a26a49de64d",
        },
    ],
    "legacyCombinerObservation": {
        "toolBindingId": "legacy-combiner-1.13.0",
        "toolSha256": "ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf",
        "processorId": "nfc.nt51929.ctrlram-postbuild-v1",
        "icNum": "single",
        "commandFamily": "NT51932BASED_NORMAL_MODE CRC8",
        "changedRanges": [
            {"start": 28928, "length": 4, "classification": "Header CRC"},
            {"start": 28952, "length": 3, "classification": "Header CRC"},
            {"start": 163824, "length": 4, "classification": "Header Copy CRC"},
            {"start": 163848, "length": 4, "classification": "Header Copy CRC"},
        ],
        "changedByteCount": 15,
    },
    "allowedDiffPolicy": ["Header CRC", "Header Copy CRC"],
    "standaloneSingleGolden": False,
    "fullByteParity": False,
    "runtimePromotion": (
        "Owner accepted the fixed no-processor NT51919/NT51929/NT51932 pilot on "
        "2026-07-22; the recorded legacy comparison differs only in the classified "
        "Header CRC and Header Copy CRC ranges and remains audit evidence rather "
        "than runtime processor authority."
    ),
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

    for property_name in (
        "ic",
        "variantOrVersion",
        "profileId",
        "profileVersion",
        "mapCapacity",
    ):
        if item.get(property_name) != expected[property_name]:
            errors.append(
                f"AB merge golden case '{case_id}' has unexpected {property_name}: "
                f"expected={expected[property_name]!r} actual={item.get(property_name)!r}"
            )

    if item.get("workflow") != "ab-merge" or item.get("directGolden") is not True:
        errors.append(f"AB merge golden case '{case_id}' must remain a direct AB case")
    if item.get("topology") != "topology-unscoped":
        errors.append(f"AB merge golden case '{case_id}' topology drift")

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
    ctrlram_evidence = item.get("ctrlRamFirstHalfSelfReplacementEvidence")
    expected_ctrlram_evidence = (
        NT51929_CTRLRAM_FIRST_HALF_EVIDENCE if case_id == "nt51929-ab-t05-d06" else None
    )
    if ctrlram_evidence != expected_ctrlram_evidence:
        errors.append(
            f"AB merge golden case '{case_id}' CtrlRAM first-half evidence drift"
        )
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
    """Validate the closed AB fixture set and its per-family runtime support state."""
    manifest_path = repository_root / "testdata/golden/canonical/manifest.json"
    manifest = load_json(manifest_path, errors)
    if not isinstance(manifest, dict):
        return

    source_collections = manifest.get("sourceCollections")
    ab_sources = (
        [
            collection.get("source")
            for collection in source_collections
            if isinstance(collection, dict)
            and collection.get("legacyRoot") == "testdata/golden/ab-merge"
        ]
        if isinstance(source_collections, list)
        else []
    )
    source = ab_sources[0] if len(ab_sources) == 1 else None
    if not isinstance(source, dict):
        errors.append("canonical golden manifest must declare one AB source provenance")
    else:
        for property_name in (
            "archive",
            "archiveSha256",
            "transfer",
            "sourceClassification",
            "approval",
            "evidenceApplicabilityApproval",
        ):
            if (
                not isinstance(source.get(property_name), str)
                or not source[property_name]
            ):
                errors.append(f"canonical AB source lacks {property_name}")

    golden_root = manifest_path.parent
    declared_bins: set[PurePosixPath] = set()
    inventory_cases = manifest.get("cases")
    if not isinstance(inventory_cases, list) or not inventory_cases:
        errors.append("canonical golden manifest must contain cases")
        return

    expected_input_ids = {"dp-ab-input", "tp-a-input", "tp-b-input"}
    seen_direct_ids: set[str] = set()
    seen_alias_ids: set[str] = set()
    for index, inventory_entry in enumerate(inventory_cases):
        if not isinstance(inventory_entry, dict):
            continue
        manifest_path_text = inventory_entry.get("manifestPath")
        if not isinstance(manifest_path_text, str) or "/ab-merge/" not in manifest_path_text:
            continue
        case = load_json(golden_root / Path(manifest_path_text), errors)
        if not isinstance(case, dict) or case.get("workflow") != "ab-merge":
            errors.append(f"invalid canonical AB case at {manifest_path_text}")
            continue
        case_id = case.get("caseId")
        if not isinstance(case_id, str):
            errors.append(f"canonical AB case[{index}] lacks caseId")
            continue

        if case.get("directGolden") is False:
            expected_alias = EXPECTED_ALIASES.get(case_id)
            if expected_alias is None:
                errors.append(f"unexpected canonical AB alias '{case_id}'")
                continue
            seen_alias_ids.add(case_id)
            for property_name in ("ic", "variantOrVersion"):
                if case.get(property_name) != expected_alias[property_name]:
                    errors.append(
                        f"canonical AB alias '{case_id}' has unexpected {property_name}"
                    )
            if case.get("sourceClassification") != "owner-approved-fact-scoped-alias":
                errors.append(f"canonical AB alias '{case_id}' sourceClassification drift")
            if case.get("ownerApproval") != expected_alias["ownerApproval"]:
                errors.append(f"canonical AB alias '{case_id}' ownerApproval drift")
            alias = case.get("alias")
            if not isinstance(alias, dict) or alias.get("sourceCaseId") != expected_alias["sourceCaseId"]:
                errors.append(f"canonical AB alias '{case_id}' source drift")
            elif alias.get("factScope") != expected_alias["factScope"]:
                errors.append(f"canonical AB alias '{case_id}' factScope drift")
            elif alias.get("evidenceRefs") != ALIAS_EVIDENCE_REFS:
                errors.append(f"canonical AB alias '{case_id}' evidenceRefs drift")
            if case.get("topology") != "topology-unscoped" or "artifacts" in case:
                errors.append(f"canonical AB alias '{case_id}' shape drift")
            continue

        validated_case_id = _validate_case_evidence(case, index, errors)
        if validated_case_id is None:
            continue
        if validated_case_id in seen_direct_ids:
            errors.append(f"canonical AB inventory repeats direct caseId '{validated_case_id}'")
        else:
            seen_direct_ids.add(validated_case_id)

        artifacts = case.get("artifacts")
        if not isinstance(artifacts, list):
            errors.append(f"canonical AB case '{case_id}' lacks artifacts")
            continue
        artifacts_by_id = {
            artifact.get("artifactId"): artifact
            for artifact in artifacts
            if isinstance(artifact, dict)
        }
        if set(artifacts_by_id) != expected_input_ids | {"expected-output"}:
            errors.append(
                f"canonical AB case '{case_id}' must declare exactly the three logical inputs and expected-output"
            )
        for artifact_id, artifact in artifacts_by_id.items():
            expected_role = "expected" if artifact_id == "expected-output" else "input"
            if artifact.get("role") != expected_role:
                errors.append(f"canonical AB artifact '{case_id}/{artifact_id}' role drift")
            _validate_artifact_provenance(artifact, case_id, artifact_id, errors)
            relative = validate_entry(
                golden_root, artifact, errors, require_bin=True, label="AB merge"
            )
            if relative is not None:
                declared_bins.add(relative)

    if seen_direct_ids != set(EXPECTED_CASES):
        errors.append(
            "canonical AB direct case inventory drift: "
            f"expected={sorted(EXPECTED_CASES)} actual={sorted(seen_direct_ids)}"
        )
    if seen_alias_ids != set(EXPECTED_ALIASES):
        errors.append(
            "canonical AB alias inventory drift: "
            f"expected={sorted(EXPECTED_ALIASES)} actual={sorted(seen_alias_ids)}"
        )

    actual_bins = {
        PurePosixPath(path.relative_to(golden_root).as_posix())
        for path in golden_root.glob("*/ab-merge/**/*.bin")
        if path.is_file()
    }
    if actual_bins != declared_bins:
        errors.append(
            "AB merge golden BIN manifest drift: "
            f"expected={sorted(path.as_posix() for path in declared_bins)} "
            f"actual={sorted(path.as_posix() for path in actual_bins)}"
        )
