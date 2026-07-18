"""Validate owner-approved dated golden intake snapshots."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path, PurePosixPath
from typing import Any


EXPECTED_20260718_SOURCE_ARCHIVE = {
    "fileName": "Golden_最後一次_20260718_Reply.7z",
    "sha256": "da32ae0acebcd89a5c2b548cd4e0863620cfc774010751f6e826bc9cbc0f4351",
    "transfer": "HackMD CJK14 numbered-note transfer",
    "inventoryFileName": "Golden_最後一次_20260718_SHA256.csv",
    "inventoryFiles": 117,
    "inventoryBins": 89,
    "inventoryBytes": 12459915,
    "recoveredDateTaipei": "2026-07-18",
}
EXPECTED_20260718_OWNER_APPROVAL = (
    "Repository owner explicitly approved committing all golden payloads to Git on 2026-07-18. "
    "Technical project and AUTO_PRJ data is retained; personal data, placeholders, PDBs, and "
    "unapproved executable copies are excluded."
)
EXPECTED_20260718_EVIDENCE_SCOPE = (
    "Direct exact-case evidence intake only. No additional owner input is required. No support "
    "promotion, range, processor, CRC, or runtime route change is authorized by this manifest."
)
EXPECTED_20260718_OWNER_DECISIONS = [
    "NT51950 currently has no cascade product case; no direct cascade support may be inferred from expected stable ranges.",
    "NT51951 currently has only single product projects; this direct single golden replaces an earlier conflicting package.",
    "NT51950 cascade and NT51951 cascade are excluded from the v0.9.9 release scope rather than treated as missing owner evidence.",
    "General Replace must apply every replacement first and then run one complete postbuild in the same terminal/session.",
    "TP firmware version editing must occur before postbuild so CRC is calculated from the staged value.",
    "NT51931 InsertSID is an out-of-scope pre-step for this V1 retirement and does not block exact CtrlRAM/Postbuild full-byte parity.",
    "NT51951 Combiner 1.11 and 1.13 equivalence is an owner-authorized compatibility hypothesis that must be resolved by direct 1.11-expected versus registered-1.13 parity.",
]
EXPECTED_20260718_CASES: dict[str, dict[str, Any]] = {
    "nt51926-fw200-single-auto-prj-597-20260718": {
        "ic": "NT51926",
        "workflow": "ctrlram-replace",
        "commonFwVersion": "2.0.0",
        "variant": "CSOT Common FW 2.0.0 (owner note D01; input/expected filenames D02; unresolved)",
        "project": "AUTO_PRJ-597",
        "pid": "0x1309 (decoded from expected FWConfig)",
        "topology": "single",
        "icCount": 1,
        "evidenceKind": "direct-owner-golden",
        "currentProfile": {
            "profileId": "nt51926-ctrlram-replace-workbench",
            "profileVersion": "0.5.0",
            "route": "legacy-workbench-v1",
        },
        "targetV2": {"status": "not-materialized", "runtimePromotion": False},
        "baseKind": "standard-merge-dp-and-tp-inputs",
        "engineeringGateIds": [
            "variant-d01-d02-conflict",
            "repository-command-parity",
        ],
        "expectedOutput": "fixtures/20260718/NT51926/replace/ctrlram/2.0.0/single/case-01/expected_output/NT51926_FlashCode_D02TFF_20260718.bin",
        "pathPrefix": "fixtures/20260718/NT51926/replace/ctrlram/2.0.0/single/case-01/",
    },
    "nt51926-fw200-cascade3-auto-prj-597-20260718": {
        "ic": "NT51926",
        "workflow": "ctrlram-replace",
        "commonFwVersion": "2.0.0",
        "variant": "CSOT Common FW 2.0.0 (owner note D01; input/expected filenames D02; unresolved)",
        "project": "AUTO_PRJ-597",
        "pid": "0x1309 (decoded from expected FWConfig)",
        "topology": "cascade",
        "icCount": 3,
        "evidenceKind": "direct-owner-golden",
        "currentProfile": {
            "profileId": "nt51926-ctrlram-replace-workbench",
            "profileVersion": "0.5.0",
            "route": "legacy-workbench-v1",
        },
        "targetV2": {"status": "not-materialized", "runtimePromotion": False},
        "baseKind": "standard-merge-dp-and-tp-inputs",
        "engineeringGateIds": [
            "variant-d01-d02-conflict",
            "repository-command-parity",
        ],
        "expectedOutput": "fixtures/20260718/NT51926/replace/ctrlram/2.0.0/cascade/case-01/expected_output/NT51926_FlashCode_D02T00_20260718.bin",
        "pathPrefix": "fixtures/20260718/NT51926/replace/ctrlram/2.0.0/cascade/case-01/",
    },
    "nt51930-fw130-cascade3-auto-prj-302-inx-20260718": {
        "ic": "NT51930",
        "workflow": "ctrlram-replace",
        "commonFwVersion": "1.3.0",
        "variant": "INX 4080x720 D05 GOP 6MUX",
        "project": "AUTO_PRJ-302",
        "pid": "0x110D",
        "topology": "cascade",
        "icCount": 3,
        "evidenceKind": "direct-owner-golden",
        "currentProfile": {
            "profileId": "nt51930-ctrlram-replace-workbench",
            "profileVersion": "0.5.0",
            "route": "legacy-workbench-v1",
        },
        "targetV2": {"status": "not-materialized", "runtimePromotion": False},
        "baseKind": "standard-merge-dp-and-tp-inputs",
        "engineeringGateIds": [
            "insertsid-out-of-scope-prestep",
            "combiner-hash-only",
            "nf-composite-command-reconstruction",
        ],
        "expectedOutput": "fixtures/20260718/NT51930/replace/ctrlram/1.3.0/cascade/case-01/expected_output/NT51930_FlashCode.bin",
        "pathPrefix": "fixtures/20260718/NT51930/replace/ctrlram/1.3.0/cascade/case-01/",
    },
    "nt51931-fw130-cascade6-auto-prj-158-20260718": {
        "ic": "NT51931",
        "workflow": "ctrlram-replace",
        "commonFwVersion": "1.3.0",
        "variant": "System Massive Product",
        "project": "AUTO_PRJ-158",
        "pid": "0x131B",
        "topology": "cascade",
        "icCount": 6,
        "evidenceKind": "direct-owner-golden",
        "currentProfile": {
            "profileId": "nt51931-ctrlram-replace-workbench",
            "profileVersion": "0.5.0",
            "route": "legacy-workbench-v1-not-available",
        },
        "targetV2": {
            "status": "exact-case-parity-candidate",
            "runtimePromotion": False,
        },
        "baseKind": "direct-reference-flashcode",
        "engineeringGateIds": [
            "combiner-hash-only",
            "insertsid-out-of-scope-prestep",
        ],
        "expectedOutput": "fixtures/20260718/NT51931/replace/ctrlram/1.3.0/cascade/case-01/expected_output/NT51931_FlashCode_D8DT83_20260718.bin",
        "pathPrefix": "fixtures/20260718/NT51931/replace/ctrlram/1.3.0/cascade/case-01/",
    },
    "nt51932-fw200-cascade3-auto-prj-525-20260718": {
        "ic": "NT51932",
        "workflow": "ctrlram-replace",
        "commonFwVersion": "2.0.0",
        "variant": "BOE 36.2 D02",
        "project": "AUTO_PRJ-525",
        "pid": "0x5601",
        "topology": "cascade",
        "icCount": 3,
        "evidenceKind": "direct-owner-golden",
        "currentProfile": {
            "profileId": "nt51932-ctrlram-replace-workbench",
            "profileVersion": "0.5.0",
            "route": "legacy-workbench-v1",
        },
        "targetV2": {"status": "not-materialized", "runtimePromotion": False},
        "baseKind": "standard-merge-dp-and-tp-inputs",
        "engineeringGateIds": [
            "diff-nf-merge-command-reconstruction",
            "nf-composite-not-proven",
        ],
        "expectedOutput": "fixtures/20260718/NT51932/replace/ctrlram/2.0.0/cascade/case-01/expected_output/NT51932_FlashCode_D02T88_20260718.bin",
        "pathPrefix": "fixtures/20260718/NT51932/replace/ctrlram/2.0.0/cascade/case-01/",
    },
    "nt51951-fw200-single-auto-prj-695-20260718": {
        "ic": "NT51951",
        "workflow": "ctrlram-replace",
        "commonFwVersion": "2.0.0",
        "variant": "CSOT 17.3-inch 2880x1620 CCW D06",
        "project": "AUTO_PRJ-695",
        "pid": "0x5901",
        "topology": "single",
        "icCount": 1,
        "evidenceKind": "direct-owner-golden",
        "currentProfile": {
            "profileId": "nt51951-ctrlram-replace-workbench",
            "profileVersion": "0.5.0",
            "route": "legacy-workbench-v1",
        },
        "targetV2": {"status": "not-materialized", "runtimePromotion": False},
        "baseKind": "standard-merge-dp-and-tp-inputs",
        "engineeringGateIds": [
            "combiner-111-113-parity-experiment",
            "registered-113-command-reconstruction",
        ],
        "expectedOutput": "fixtures/20260718/NT51951/replace/ctrlram/2.0.0/single/case-01/expected_output/NT51950TT_FW_D06T03.bin",
        "pathPrefix": "fixtures/20260718/NT51951/replace/ctrlram/2.0.0/single/case-01/",
    },
}
EXPECTED_20260718_TOOL_OBSERVATIONS = [
    {
        "caseId": "nt51930-fw130-cascade3-auto-prj-302-inx-20260718",
        "fileName": "Combiner.exe",
        "size": 34304,
        "sha256": "291c2c1cc5b75c59680818497ddb863718ff1930b1f000c61a27e1c4eac9dec3",
        "versionMetadata": "absent",
        "repositoryRegistration": "none",
        "includedInGolden": False,
        "executionAuthorized": False,
    },
    {
        "caseId": "nt51931-fw130-cascade6-auto-prj-158-20260718",
        "fileName": "Combiner.exe",
        "size": 25088,
        "sha256": "778f6dcec718e809d41c118ca40ce056ac428bc932ed36d851bd842fd612af58",
        "versionMetadata": "absent",
        "repositoryRegistration": "none",
        "includedInGolden": False,
        "executionAuthorized": False,
    },
    {
        "caseId": "nt51932-fw200-cascade3-auto-prj-525-20260718",
        "packageId": "diff-nf-merge",
        "repositoryPackagePath": "external-tools/diff-nf-merge/1.0.0",
        "incomingPackageFilesMatched": True,
        "files": [
            {
                "fileName": "DiffNFMerge.exe",
                "size": 9728,
                "sha256": "f611af7e315d46341e15cd7140eb3962f6ac05d337121e5554022ef5e69a2bbe",
            },
            {
                "fileName": "CommandLine.dll",
                "size": 225280,
                "sha256": "e7d67580a8999d0e5534039f62e0dbc423b2d6dac51c2f5ff6ef199e4a53138d",
            },
            {
                "fileName": "DiffNFMerge.exe.config",
                "size": 174,
                "sha256": "e2f8febf28b49915d142e4b698435d53b7eb3443853bd2892b86fe20e8895620",
            },
        ],
        "includedInGolden": False,
        "runtimeRegistered": False,
        "executionAuthorized": False,
        "commandContract": "unverified",
    },
]


def verify_owner_golden_intake_20260717(golden_root: Path) -> None:
    """Verify every file in the original 2026-07-17 intake manifest."""
    verify_owner_golden_intake(golden_root, "20260717", "0.1")


def verify_owner_golden_intake_20260718(golden_root: Path) -> None:
    """Verify every file and exact-case relationship in the final owner intake."""
    document, declared_files = verify_owner_golden_intake(
        golden_root, "20260718", "0.2"
    )
    verify_20260718_provenance(document)
    verify_exact_cases(
        document,
        declared_files,
        "20260718",
        EXPECTED_20260718_CASES,
    )


def verify_20260718_provenance(document: dict[str, Any]) -> None:
    """Lock the final archive, approval, owner decisions, and tool observations."""
    require(
        document.get("sourceArchive") == EXPECTED_20260718_SOURCE_ARCHIVE,
        "20260718 source archive provenance drifted",
    )
    require(
        document.get("ownerApproval") == EXPECTED_20260718_OWNER_APPROVAL,
        "20260718 owner approval drifted",
    )
    require(
        document.get("evidenceScope") == EXPECTED_20260718_EVIDENCE_SCOPE,
        "20260718 evidence scope drifted",
    )
    require(
        document.get("ownerDecisions") == EXPECTED_20260718_OWNER_DECISIONS,
        "20260718 owner decisions drifted",
    )
    require(
        document.get("externalToolObservations") == EXPECTED_20260718_TOOL_OBSERVATIONS,
        "20260718 external tool observations drifted",
    )


def verify_owner_golden_intake(
    golden_root: Path,
    intake_id: str,
    expected_schema_version: str,
) -> tuple[dict[str, Any], set[PurePosixPath]]:
    """Verify one closed dated inventory and return its parsed declaration."""
    manifest_path = golden_root / f"manifest.{intake_id}.json"
    document = json.loads(manifest_path.read_text(encoding="utf-8"))
    require(
        isinstance(document, dict),
        f"{intake_id} intake manifest root must be an object",
    )
    expected_facts = {
        "schemaVersion": expected_schema_version,
        "payloadClass": "owner-approved-golden-firmware",
        "binaryPayloadsIncluded": True,
        "runtimeSupportPromotion": False,
    }
    for key, expected in expected_facts.items():
        require(
            document.get(key) == expected,
            f"{intake_id} intake manifest must declare {key}={expected!r}",
        )

    declared_files: set[PurePosixPath] = set()
    for collection_name, require_bin in (
        ("payloads", True),
        ("supportingFiles", False),
    ):
        entries = document.get(collection_name)
        require(
            isinstance(entries, list) and bool(entries),
            f"{intake_id} intake manifest must contain {collection_name}",
        )
        for index, entry in enumerate(entries):
            require(
                isinstance(entry, dict),
                f"{intake_id} intake {collection_name}[{index}] must be an object",
            )
            relative = verify_entry(golden_root, entry, require_bin, intake_id)
            require(
                relative not in declared_files,
                f"{intake_id} intake path is duplicated: {relative}",
            )
            declared_files.add(relative)

    fixture_root = golden_root / f"fixtures/{intake_id}"
    actual_files = {
        PurePosixPath(path.relative_to(golden_root).as_posix())
        for path in fixture_root.rglob("*")
        if path.is_file()
    }
    require(
        actual_files == declared_files,
        f"{intake_id} intake manifest inventory does not match fixtures/{intake_id}",
    )
    return document, declared_files


def verify_exact_cases(
    document: dict[str, Any],
    declared_files: set[PurePosixPath],
    intake_id: str,
    expected_cases: dict[str, dict[str, Any]] | None = None,
) -> None:
    """Lock exact case metadata without turning evidence intake into support."""
    cases = document.get("cases")
    require(
        isinstance(cases, list) and bool(cases),
        f"{intake_id} intake cases are required",
    )
    payload_entries = document.get("payloads")
    require(
        isinstance(payload_entries, list), f"{intake_id} intake payloads are required"
    )
    payload_roles = {
        PurePosixPath(entry["path"]): entry["role"]
        for entry in payload_entries
        if isinstance(entry, dict) and isinstance(entry.get("path"), str)
    }
    payload_case_ids = {
        PurePosixPath(entry["path"]): entry.get("caseId")
        for entry in payload_entries
        if isinstance(entry, dict) and isinstance(entry.get("path"), str)
    }
    declared_case_ids: set[str] = set()
    referenced_payloads: set[PurePosixPath] = set()
    for index, item in enumerate(cases):
        label = f"{intake_id} intake case[{index}]"
        require(isinstance(item, dict), f"{label} must be an object")
        case_id = require_text(item.get("caseId"), f"{label}.caseId")
        require(case_id not in declared_case_ids, f"{label}.caseId is duplicated")
        declared_case_ids.add(case_id)
        expected_case = None if expected_cases is None else expected_cases.get(case_id)
        require(
            expected_cases is None or expected_case is not None,
            f"{label}.caseId is not one of the exact approved cases",
        )
        for key in (
            "ic",
            "workflow",
            "commonFwVersion",
            "variant",
            "project",
            "pid",
            "topology",
            "evidenceKind",
            "baseKind",
            "expectedOutput",
        ):
            require_text(item.get(key), f"{label}.{key}")
        require(
            item.get("workflow") == "ctrlram-replace",
            f"{label}.workflow must be ctrlram-replace",
        )
        require(
            item.get("topology") in {"single", "cascade"},
            f"{label}.topology is invalid",
        )
        require(
            isinstance(item.get("icCount"), int) and item["icCount"] > 0,
            f"{label}.icCount must be positive",
        )
        require(
            (item["topology"] == "single" and item["icCount"] == 1)
            or (item["topology"] == "cascade" and item["icCount"] > 1),
            f"{label}.topology and icCount are inconsistent",
        )
        for contract_key in ("currentProfile", "targetV2"):
            require(
                isinstance(item.get(contract_key), dict),
                f"{label}.{contract_key} must be an object",
            )
        for key in ("profileId", "profileVersion", "route"):
            require_text(
                item["currentProfile"].get(key), f"{label}.currentProfile.{key}"
            )
        require_text(item["targetV2"].get("status"), f"{label}.targetV2.status")
        require(
            item["targetV2"].get("runtimePromotion") is False,
            f"{label} must not promote runtime support",
        )
        if expected_case is not None:
            for key, expected in expected_case.items():
                if key != "pathPrefix":
                    require(
                        item.get(key) == expected,
                        f"{label}.{key} drifted from the exact approved case",
                    )

        paths = item.get("payloadPaths")
        require(
            isinstance(paths, list) and bool(paths),
            f"{label}.payloadPaths are required",
        )
        case_paths = [
            PurePosixPath(require_text(path, f"{label}.payloadPaths")) for path in paths
        ]
        require(
            len(case_paths) == len(set(case_paths)),
            f"{label}.payloadPaths contain duplicates",
        )
        require(
            set(case_paths) <= declared_files,
            f"{label}.payloadPaths contain undeclared files",
        )
        require(
            all(payload_case_ids.get(path) == case_id for path in case_paths),
            f"{label}.payloadPaths are attributed to a different caseId",
        )
        if expected_case is not None:
            require(
                all(
                    path.as_posix().startswith(expected_case["pathPrefix"])
                    for path in case_paths
                ),
                f"{label}.payloadPaths escape the exact case root",
            )
        require(
            not (set(case_paths) & referenced_payloads),
            f"{label}.payloadPaths overlap another case",
        )
        referenced_payloads.update(case_paths)
        expected_path = PurePosixPath(item["expectedOutput"])
        require(
            expected_path in case_paths,
            f"{label}.expectedOutput is not one of its payloads",
        )
        require(
            payload_roles.get(expected_path) == "expected-final-output",
            f"{label}.expectedOutput role is invalid",
        )

        gaps = item.get("provenanceGaps")
        require(
            isinstance(gaps, list)
            and bool(gaps)
            and all(isinstance(gap, str) and gap.strip() for gap in gaps),
            f"{label}.provenanceGaps must remain explicit",
        )
        claims = item.get("claims")
        require(isinstance(claims, dict), f"{label}.claims must be an object")
        require(
            claims.get("intakeComplete") is True,
            f"{label} must declare intakeComplete=true",
        )
        require(
            claims.get("fullByteParity") is False,
            f"{label} cannot claim full-byte parity at intake",
        )
        require(
            claims.get("runtimeSupportPromotion") is False,
            f"{label} cannot claim runtime promotion",
        )

    require(
        referenced_payloads == set(payload_roles),
        f"{intake_id} case payload inventory is incomplete",
    )
    if expected_cases is not None:
        require(
            declared_case_ids == set(expected_cases),
            f"{intake_id} exact case inventory drifted",
        )
    for collection_name in ("payloads", "supportingFiles"):
        for entry in document[collection_name]:
            require(
                entry.get("caseId") in declared_case_ids,
                f"{intake_id} {collection_name} caseId is invalid",
            )
            expected_case = (
                None if expected_cases is None else expected_cases[entry["caseId"]]
            )
            if expected_case is not None:
                require(
                    entry["path"].startswith(expected_case["pathPrefix"]),
                    f"{intake_id} {collection_name} path escapes its exact case root",
                )


def verify_entry(
    golden_root: Path,
    entry: dict[str, Any],
    require_bin: bool,
    intake_id: str,
) -> PurePosixPath:
    relative_text = entry.get("path")
    expected_size = entry.get("size")
    expected_hash = entry.get("sha256")
    require(
        isinstance(relative_text, str) and bool(relative_text),
        f"{intake_id} intake path is required",
    )
    require(
        isinstance(expected_size, int) and expected_size >= 0,
        f"{intake_id} intake size is invalid",
    )
    require(
        isinstance(expected_hash, str) and len(expected_hash) == 64,
        f"{intake_id} intake hash is invalid",
    )
    require(
        isinstance(entry.get("role"), str) and bool(entry["role"].strip()),
        f"{intake_id} intake role is required",
    )

    relative = PurePosixPath(relative_text)
    require(
        not relative.is_absolute() and ".." not in relative.parts,
        f"unsafe {intake_id} intake path: {relative}",
    )
    require(
        not require_bin or relative.suffix.lower() == ".bin",
        f"{intake_id} intake payload is not BIN: {relative}",
    )
    require(
        entry.get("originalFileName") == relative.name,
        f"{intake_id} intake original filename drift: {relative}",
    )

    candidate = (golden_root / Path(*relative.parts)).resolve()
    require(
        candidate.is_relative_to(golden_root.resolve()),
        f"{intake_id} intake path escapes fixture root: {relative}",
    )
    require(candidate.is_file(), f"{intake_id} intake file is missing: {relative}")
    require(
        candidate.stat().st_size == expected_size,
        f"{intake_id} intake size drift: {relative}",
    )
    require(
        sha256(candidate) == expected_hash.lower(),
        f"{intake_id} intake hash drift: {relative}",
    )
    return relative


def require_text(value: Any, label: str) -> str:
    require(
        isinstance(value, str) and bool(value.strip()),
        f"{label} must be non-empty text",
    )
    return value


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)
