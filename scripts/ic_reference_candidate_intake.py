"""Validate and stage manifest-declared IC reference candidate evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import stat
import tempfile
from datetime import datetime
from pathlib import Path, PurePosixPath
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
INTAKE_TOOL_ID = "ic-reference-intake"
INTAKE_TOOL_VERSION = "0.9.4"

ID_PATTERN = re.compile(r"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$")
MEMBER_ID_PATTERN = re.compile(r"^NT[0-9A-Z-]+$")
SEMVER_PATTERN = re.compile(
    r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)"
    r"(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?"
    r"(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$"
)
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
UTC_TIMESTAMP_PATTERN = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$")
LOCAL_PATH_PATTERN = re.compile(
    r"(?:[A-Za-z]:[\\/]|\\\\|(?<![A-Za-z0-9])/(?:[A-Za-z0-9._~$-]+(?:/|$))|"
    r"(?<![A-Za-z0-9._-])\.\.?[\\/])"
)
SOURCE_PATH_PATTERN = re.compile(
    r"^[A-Za-z0-9~$][A-Za-z0-9._ ~$-]*(?:/[A-Za-z0-9~$][A-Za-z0-9._ ~$-]*)*$"
)
SOURCE_KINDS = {"workbook", "source-code", "firmware", "document", "issue-export", "owner-record"}
FACT_KINDS = {
    "range",
    "locator",
    "metadata-layout",
    "topology",
    "capability",
    "alias",
    "processor",
    "integrity",
    "output-naming",
    "golden",
}
FACT_DISPOSITIONS = {"observed", "accepted", "rejected", "unresolved"}
PROMOTION_IMPACTS = {"none", "blocks-map-resolution", "blocks-execution", "blocks-support"}
REVIEW_DECISIONS = {"accept", "reject", "request-changes"}

WORKFLOWS = (
    "reference-only",
    "standard-merge",
    "dp-replace",
    "ctrlram-replace",
    "general-replace",
)


class CandidateIntakeError(ValueError):
    """Raised for a candidate-intake failure that is safe to display to the caller."""


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_json(path: Path, data: dict[str, Any]) -> None:
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def reject_duplicate_object_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("duplicate JSON key")
        result[key] = value
    return result


def load_request(path: Path) -> dict[str, Any]:
    try:
        document = json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=reject_duplicate_object_keys)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError("cannot read intake request") from exc
    if not isinstance(document, dict):
        raise ValueError("intake request root must be an object")
    return document


def require_exact_keys(
    value: dict[str, Any],
    required: set[str],
    optional: set[str],
    label: str,
) -> None:
    missing = required - set(value)
    unknown = set(value) - required - optional
    if missing:
        raise ValueError(f"{label} is missing required field(s): {', '.join(sorted(missing))}")
    if unknown:
        raise ValueError(f"{label} contains unknown field(s)")


def require_string(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value:
        raise ValueError(f"{label} must be a non-empty string")
    return value


def require_output_text(value: Any, label: str) -> str:
    text = require_string(value, label)
    if LOCAL_PATH_PATTERN.search(text) is not None:
        raise ValueError(f"{label} must not contain local paths")
    return text


def require_id(value: Any, label: str) -> str:
    result = require_string(value, label)
    if ID_PATTERN.fullmatch(result) is None:
        raise ValueError(f"{label} must be a lower-case hyphenated id")
    return result


def require_utc_timestamp(value: Any, label: str) -> str:
    timestamp = require_string(value, label)
    if UTC_TIMESTAMP_PATTERN.fullmatch(timestamp) is None:
        raise ValueError(f"{label} must be an RFC 3339 UTC timestamp ending in Z")
    try:
        datetime.fromisoformat(timestamp.removesuffix("Z") + "+00:00")
    except ValueError as exc:
        raise ValueError(f"{label} must be an ISO-8601 UTC timestamp") from exc
    return timestamp


def require_positive_int(value: Any, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 1:
        raise ValueError(f"{label} must be a positive integer")
    return value


def require_string_array(value: Any, label: str, validator: Any = require_string) -> list[str]:
    if not isinstance(value, list) or not value:
        raise ValueError(f"{label} must be a non-empty array")
    values = [validator(item, f"{label}[{index}]") for index, item in enumerate(value)]
    if len(values) != len(set(values)):
        raise ValueError(f"{label} must not contain duplicates")
    return values


def require_member_id(value: Any, label: str) -> str:
    member_id = require_string(value, label)
    if MEMBER_ID_PATTERN.fullmatch(member_id) is None:
        raise ValueError(f"{label} must be an uppercase NT member id")
    return member_id


def require_semver(value: Any, label: str) -> str:
    version = require_string(value, label)
    if SEMVER_PATTERN.fullmatch(version) is None:
        raise ValueError(f"{label} must be a semantic version")
    return version


def require_sha256(value: Any, label: str) -> str:
    digest = require_string(value, label)
    if SHA256_PATTERN.fullmatch(digest) is None:
        raise ValueError(f"{label} must be a lower-case SHA-256 hex value")
    return digest


def validate_candidate_scope(value: Any) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError("candidateScope must be an object")
    require_exact_keys(
        value,
        {"memberIds", "modeIds", "capacityBytes", "topologyChoices"},
        {"exclusions"},
        "candidateScope",
    )
    member_ids = require_string_array(value["memberIds"], "candidateScope.memberIds", require_member_id)
    mode_ids = require_string_array(value["modeIds"], "candidateScope.modeIds", require_id)
    topology_choices = require_string_array(
        value["topologyChoices"], "candidateScope.topologyChoices", require_id
    )
    capacities = value["capacityBytes"]
    if not isinstance(capacities, list) or not capacities:
        raise ValueError("candidateScope.capacityBytes must be a non-empty array")
    capacity_bytes = [
        require_positive_int(item, f"candidateScope.capacityBytes[{index}]")
        for index, item in enumerate(capacities)
    ]
    if len(capacity_bytes) != len(set(capacity_bytes)):
        raise ValueError("candidateScope.capacityBytes must not contain duplicates")

    result: dict[str, Any] = {
        "memberIds": member_ids,
        "modeIds": mode_ids,
        "capacityBytes": capacity_bytes,
        "topologyChoices": topology_choices,
    }
    if "exclusions" in value:
        result["exclusions"] = require_string_array(
            value["exclusions"], "candidateScope.exclusions", require_output_text
        )
    return result


def normalize_request_source_path(value: Any, label: str) -> PurePosixPath:
    source_path = require_string(value, label)
    if "\\" in source_path or SOURCE_PATH_PATTERN.fullmatch(source_path) is None:
        raise ValueError(f"{label} must use a relative POSIX path")
    path = PurePosixPath(source_path)
    if path.is_absolute() or any(part in {"", ".", ".."} for part in path.parts):
        raise ValueError(f"{label} must stay below --source-root")
    return path


def validate_request_source_artifacts(value: Any, source_root: Path) -> list[dict[str, Any]]:
    if not isinstance(value, list) or not value:
        raise ValueError("sourceArtifacts must be a non-empty array")

    artifact_ids: set[str] = set()
    source_paths: set[PurePosixPath] = set()
    artifacts: list[dict[str, Any]] = []
    for index, raw in enumerate(value):
        label = f"sourceArtifacts[{index}]"
        if not isinstance(raw, dict):
            raise ValueError(f"{label} must be an object")
        require_exact_keys(
            raw,
            {"artifactId", "sourceKind", "logicalName", "sourcePath", "contentHash", "sizeBytes"},
            set(),
            label,
        )
        artifact_id = require_id(raw["artifactId"], f"{label}.artifactId")
        if artifact_id in artifact_ids:
            raise ValueError(f"duplicate artifact id: {artifact_id}")
        artifact_ids.add(artifact_id)

        source_kind = require_string(raw["sourceKind"], f"{label}.sourceKind")
        if source_kind not in SOURCE_KINDS:
            raise ValueError(f"{label}.sourceKind is not recognized")
        relative_path = normalize_request_source_path(raw["sourcePath"], f"{label}.sourcePath")
        if relative_path in source_paths:
            raise ValueError(f"duplicate source path: {relative_path.as_posix()}")
        source_paths.add(relative_path)

        logical_name = require_string(raw["logicalName"], f"{label}.logicalName")
        if logical_name != relative_path.name:
            raise ValueError(f"{label}.logicalName must preserve the source filename")
        declared_hash = require_sha256(raw["contentHash"], f"{label}.contentHash")
        declared_size = require_positive_int(raw["sizeBytes"], f"{label}.sizeBytes")
        source_path = resolve_declared_source_path(source_root, relative_path)
        actual_size, actual_hash = snapshot_identity(source_path)
        if actual_size != declared_size or actual_hash != declared_hash:
            raise ValueError(f"{label} does not match its declared size or SHA-256")
        artifacts.append(
            {
                "artifactId": artifact_id,
                "sourceKind": source_kind,
                "logicalName": logical_name,
                "sourceRoot": source_root,
                "sourceRelativePath": relative_path,
                "contentHash": declared_hash,
                "sizeBytes": declared_size,
            }
        )
    return artifacts


def validate_facts(value: Any, artifact_ids: set[str], scope: dict[str, Any]) -> list[dict[str, Any]]:
    if not isinstance(value, list) or not value:
        raise ValueError("facts must be a non-empty array")
    fact_ids: set[str] = set()
    facts: list[dict[str, Any]] = []
    for index, fact in enumerate(value):
        label = f"facts[{index}]"
        if not isinstance(fact, dict):
            raise ValueError(f"{label} must be an object")
        require_exact_keys(
            fact,
            {"factId", "subject", "factKind", "value", "disposition", "promotionImpact", "citations"},
            {"rationale"},
            label,
        )
        fact_id = require_id(fact["factId"], f"{label}.factId")
        if fact_id in fact_ids:
            raise ValueError(f"duplicate fact id: {fact_id}")
        fact_ids.add(fact_id)
        validate_fact_subject(fact["subject"], label, scope)
        fact_kind = require_string(fact["factKind"], f"{label}.factKind")
        if fact_kind not in FACT_KINDS:
            raise ValueError(f"{label}.factKind is not recognized")
        value_kind = validate_fact_value(fact["value"], label)
        disposition = require_string(fact["disposition"], f"{label}.disposition")
        if disposition not in FACT_DISPOSITIONS:
            raise ValueError(f"{label}.disposition is not recognized")
        promotion_impact = require_string(fact["promotionImpact"], f"{label}.promotionImpact")
        if promotion_impact not in PROMOTION_IMPACTS:
            raise ValueError(f"{label}.promotionImpact is not recognized")
        if fact_kind == "range" and not (
            value_kind == "range"
            or (
                value_kind == "statement"
                and disposition == "unresolved"
                and promotion_impact == "blocks-map-resolution"
            )
        ):
            raise ValueError(
                f"{label} requires a typed range unless it is an unresolved map-blocking statement"
            )
        validate_fact_citations(fact["citations"], label, artifact_ids)
        if "rationale" in fact:
            require_output_text(fact["rationale"], f"{label}.rationale")
        facts.append(fact)
    return facts


def validate_fact_subject(value: Any, label: str, scope: dict[str, Any]) -> None:
    if not isinstance(value, dict):
        raise ValueError(f"{label}.subject must be an object")
    require_exact_keys(value, {"familyId"}, {"memberId", "modeId", "profileId"}, f"{label}.subject")
    require_id(value["familyId"], f"{label}.subject.familyId")
    if "memberId" in value:
        member_id = require_member_id(value["memberId"], f"{label}.subject.memberId")
        if member_id not in scope["memberIds"]:
            raise ValueError(f"{label}.subject.memberId is outside candidateScope")
    if "modeId" in value:
        mode_id = require_id(value["modeId"], f"{label}.subject.modeId")
        if mode_id not in scope["modeIds"]:
            raise ValueError(f"{label}.subject.modeId is outside candidateScope")
    if "profileId" in value:
        require_id(value["profileId"], f"{label}.subject.profileId")


def validate_fact_value(value: Any, label: str) -> str:
    if not isinstance(value, dict):
        raise ValueError(f"{label}.value must be an object")
    kind = require_string(value.get("kind"), f"{label}.value.kind")
    if kind == "range":
        require_exact_keys(value, {"kind", "addressSpaceId", "range"}, set(), f"{label}.value")
        require_id(value["addressSpaceId"], f"{label}.value.addressSpaceId")
        byte_range = value["range"]
        if not isinstance(byte_range, dict):
            raise ValueError(f"{label}.value.range must be an object")
        require_exact_keys(byte_range, {"start", "length"}, set(), f"{label}.value.range")
        if isinstance(byte_range["start"], bool) or not isinstance(byte_range["start"], int) or byte_range["start"] < 0:
            raise ValueError(f"{label}.value.range.start must be a non-negative integer")
        require_positive_int(byte_range["length"], f"{label}.value.range.length")
        return kind
    if kind == "scalar":
        require_exact_keys(value, {"kind", "value"}, set(), f"{label}.value")
        scalar = value["value"]
        if not isinstance(scalar, (bool, int, str)) or isinstance(scalar, float):
            raise ValueError(f"{label}.value.value must be a boolean, integer, or non-empty string")
        if isinstance(scalar, str):
            require_output_text(scalar, f"{label}.value.value")
        return kind
    if kind == "reference":
        require_exact_keys(value, {"kind", "targetId"}, set(), f"{label}.value")
        require_id(value["targetId"], f"{label}.value.targetId")
        return kind
    if kind == "statement":
        require_exact_keys(value, {"kind", "text"}, set(), f"{label}.value")
        require_output_text(value["text"], f"{label}.value.text")
        return kind
    raise ValueError(f"{label}.value.kind is not recognized")


def validate_fact_citations(value: Any, label: str, artifact_ids: set[str]) -> None:
    if not isinstance(value, list) or not value:
        raise ValueError(f"{label}.citations must be a non-empty array")
    citations: set[tuple[str, str]] = set()
    for index, citation in enumerate(value):
        citation_label = f"{label}.citations[{index}]"
        if not isinstance(citation, dict):
            raise ValueError(f"{citation_label} must be an object")
        require_exact_keys(citation, {"artifactId", "location"}, set(), citation_label)
        artifact_id = require_id(citation["artifactId"], f"{citation_label}.artifactId")
        if artifact_id not in artifact_ids:
            raise ValueError(f"{citation_label}.artifactId is not a declared source artifact")
        location = require_output_text(citation["location"], f"{citation_label}.location")
        pair = (artifact_id, location)
        if pair in citations:
            raise ValueError(f"duplicate citation in {label}")
        citations.add(pair)


def validate_reviews(value: Any, fact_ids: set[str]) -> list[dict[str, Any]]:
    if not isinstance(value, list):
        raise ValueError("reviews must be an array")
    review_ids: set[str] = set()
    for index, review in enumerate(value):
        label = f"reviews[{index}]"
        if not isinstance(review, dict):
            raise ValueError(f"{label} must be an object")
        require_exact_keys(
            review,
            {"reviewId", "reviewer", "reviewedAt", "decision", "factIds"},
            set(),
            label,
        )
        review_id = require_id(review["reviewId"], f"{label}.reviewId")
        if review_id in review_ids:
            raise ValueError(f"duplicate review id: {review_id}")
        review_ids.add(review_id)
        require_output_text(review["reviewer"], f"{label}.reviewer")
        require_utc_timestamp(review["reviewedAt"], f"{label}.reviewedAt")
        decision = require_string(review["decision"], f"{label}.decision")
        if decision not in REVIEW_DECISIONS:
            raise ValueError(f"{label}.decision is not recognized")
        review_fact_ids = require_string_array(review["factIds"], f"{label}.factIds", require_id)
        unknown = set(review_fact_ids) - fact_ids
        if unknown:
            raise ValueError(f"{label}.factIds contains unknown fact id(s): {', '.join(sorted(unknown))}")
    return value


def validate_candidate_request(document: dict[str, Any], source_root: Path) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    require_exact_keys(
        document,
        {
            "schemaVersion",
            "requestId",
            "manifestId",
            "manifestVersion",
            "requestedAtUtc",
            "owner",
            "workflow",
            "candidateScope",
            "sourceArtifacts",
            "facts",
            "reviews",
        },
        {"sourceRef", "case"},
        "intake request",
    )
    if document["schemaVersion"] != "1.0":
        raise ValueError("intake request schemaVersion must be 1.0")
    require_id(document["requestId"], "requestId")
    require_id(document["manifestId"], "manifestId")
    require_semver(document["manifestVersion"], "manifestVersion")
    require_utc_timestamp(document["requestedAtUtc"], "requestedAtUtc")
    require_output_text(document["owner"], "owner")
    if "sourceRef" in document:
        require_string(document["sourceRef"], "sourceRef")
    if document["workflow"] not in WORKFLOWS:
        raise ValueError("workflow is not recognized")
    if "case" in document:
        require_id(document["case"], "case")

    scope = validate_candidate_scope(document["candidateScope"])
    artifacts = validate_request_source_artifacts(document["sourceArtifacts"], source_root)
    facts = validate_facts(document["facts"], {artifact["artifactId"] for artifact in artifacts}, scope)
    validate_reviews(document["reviews"], {fact["factId"] for fact in facts})
    return scope, artifacts


def is_reparse_point(path: Path) -> bool:
    try:
        attributes = getattr(path.lstat(), "st_file_attributes", 0)
    except OSError as exc:
        raise CandidateIntakeError("cannot inspect candidate intake path") from exc
    reparse_attribute = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x0400)
    return path.is_symlink() or bool(attributes & reparse_attribute)


def reject_reparse_path(path: Path, label: str) -> None:
    current = path
    while True:
        if is_reparse_point(current):
            raise ValueError(f"{label} contains a symbolic link or reparse point")
        if current.parent == current:
            return
        current = current.parent


def resolve_candidate_source_root(path: Path | None) -> Path:
    if path is None:
        raise ValueError("--source-root is required with --request")
    source_root = path.expanduser()
    if not source_root.is_dir():
        raise FileNotFoundError("source root does not exist")
    reject_reparse_path(source_root, "source root")
    return source_root.resolve()


def resolve_declared_source_path(source_root: Path, relative_path: PurePosixPath) -> Path:
    source_path = source_root.joinpath(*relative_path.parts)
    try:
        resolved = source_path.resolve(strict=True)
        resolved.relative_to(source_root)
    except (OSError, ValueError) as exc:
        raise ValueError(f"declared source path escapes --source-root: {relative_path.as_posix()}") from exc
    reject_reparse_path(source_path, "declared source path")
    if source_path.name.startswith("~$") or source_path.suffix.lower() == ".lock":
        raise ValueError(f"declared source path is an Office or tool lock file: {relative_path.as_posix()}")
    try:
        mode = source_path.stat().st_mode
    except OSError as exc:
        raise ValueError(f"cannot inspect declared source path: {relative_path.as_posix()}") from exc
    if not stat.S_ISREG(mode):
        raise ValueError(f"declared source path is not a regular file: {relative_path.as_posix()}")
    return source_path


def snapshot_identity(path: Path) -> tuple[int, str]:
    return path.stat().st_size, sha256(path)


def resolve_candidate_output_dir(path: Path | None) -> Path:
    if path is None:
        raise ValueError("--output-dir is required with --request")
    output_dir = path.expanduser()
    if output_dir.exists():
        raise CandidateIntakeError("candidate output directory already exists")
    parent = output_dir.parent
    if not parent.is_dir():
        raise FileNotFoundError("candidate output parent does not exist")
    reject_reparse_path(parent, "candidate output parent")
    resolved = parent.resolve() / output_dir.name
    try:
        resolved.relative_to(ROOT.resolve())
    except ValueError:
        return resolved
    raise ValueError("candidate output directory must be outside this repository")


def reject_source_output_overlap(source_root: Path, output_dir: Path) -> None:
    try:
        output_dir.relative_to(source_root)
    except ValueError:
        pass
    else:
        raise ValueError("candidate output directory must not be inside the source root")

    try:
        source_root.relative_to(output_dir)
    except ValueError:
        return
    raise ValueError("candidate output directory must not contain the source root")


def build_candidate_evidence_manifest(
    request: dict[str, Any], artifacts: list[dict[str, Any]]
) -> dict[str, Any]:
    return {
        "schemaVersion": "1.0",
        "manifestId": request["manifestId"],
        "manifestVersion": request["manifestVersion"],
        "status": "candidate",
        "intakeProvenance": {
            "toolId": INTAKE_TOOL_ID,
            "toolVersion": INTAKE_TOOL_VERSION,
            "generatedAt": request["requestedAtUtc"],
            "candidateOnly": True,
        },
        "sourceArtifacts": [
            {
                "artifactId": artifact["artifactId"],
                "sourceKind": artifact["sourceKind"],
                "logicalName": artifact["logicalName"],
                "contentHash": artifact["contentHash"],
                "sizeBytes": artifact["sizeBytes"],
            }
            for artifact in artifacts
        ],
        "facts": request["facts"],
        "reviews": request["reviews"],
    }


def build_candidate_intake_report(
    request: dict[str, Any], scope: dict[str, Any], artifacts: list[dict[str, Any]]
) -> dict[str, Any]:
    return {
        "reportVersion": "1.0",
        "requestId": request["requestId"],
        "owner": request["owner"],
        "workflow": request["workflow"],
        "case": request.get("case"),
        "candidateScope": scope,
        "candidateOnly": True,
        "runtimeRegistration": "not-performed",
        "supportPromotion": "not-performed",
        "rangeInference": "not-performed",
        "schemaAllowlistChange": "not-performed",
        "artifacts": [
            {
                "artifactId": artifact["artifactId"],
                "stagedPath": f"artifacts/{artifact['artifactId']}/{artifact['logicalName']}",
                "contentHash": artifact["contentHash"],
                "sizeBytes": artifact["sizeBytes"],
            }
            for artifact in artifacts
        ],
    }


def write_candidate_next_steps(path: Path, report: dict[str, Any]) -> None:
    lines = [
        "# Candidate IC Intake Next Steps",
        "",
        f"- Request: `{report['requestId']}`",
        f"- Workflow: `{report['workflow']}`",
        f"- Owner: `{report['owner']}`",
        "- Status: candidate only; runtime registration and support promotion were not performed.",
        "",
        "## Required Review",
        "",
        "- Verify each source hash, fact citation, disposition, and promotion blocker.",
        "- Resolve no range, alias, metadata, integrity, or processor fact by inference.",
        "- Materialize a trusted profile bundle only in a separate reviewed change.",
        "- Add runtime registration, profile promotion, and golden evidence only after their normal gates pass.",
    ]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def copy_declared_artifact(artifact: dict[str, Any], staging_root: Path) -> None:
    expected = (artifact["sizeBytes"], artifact["contentHash"])
    source_path = resolve_declared_source_path(artifact["sourceRoot"], artifact["sourceRelativePath"])
    destination = staging_root / "artifacts" / artifact["artifactId"] / artifact["logicalName"]
    destination.parent.mkdir(parents=True, exist_ok=True)
    digest = hashlib.sha256()
    copied_size = 0
    with source_path.open("rb") as source, destination.open("xb") as target:
        initial_stat = os.fstat(source.fileno())
        if not stat.S_ISREG(initial_stat.st_mode):
            raise ValueError(f"declared source is no longer a regular file: {artifact['artifactId']}")
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            copied_size += len(chunk)
            digest.update(chunk)
            target.write(chunk)
        final_stat = os.fstat(source.fileno())
        if (
            initial_stat.st_dev != final_stat.st_dev
            or initial_stat.st_ino != final_stat.st_ino
            or initial_stat.st_size != final_stat.st_size
        ):
            raise ValueError(f"declared source handle changed during copy: {artifact['artifactId']}")
    if (copied_size, digest.hexdigest()) != expected:
        raise ValueError(f"declared source changed during copy: {artifact['artifactId']}")


def stage_manifest_request(args: argparse.Namespace) -> int:
    try:
        return stage_manifest_request_core(args)
    except CandidateIntakeError:
        raise
    except OSError as exc:
        raise CandidateIntakeError("candidate evidence staging failed") from exc


def stage_manifest_request_core(args: argparse.Namespace) -> int:
    if args.source_root is None or args.output_dir is None:
        raise ValueError("--request requires both --source-root and --output-dir")
    if any(
        value is not None
        for value in (args.ic, args.mode, args.case, args.owner, args.source_ref, args.output_root, args.run_id)
    ):
        raise ValueError(
            "--request does not accept legacy --ic/--mode/--case/--owner/--source-ref/--output-root/--run-id options"
        )
    source_root = resolve_candidate_source_root(args.source_root)
    output_dir = resolve_candidate_output_dir(args.output_dir)
    reject_source_output_overlap(source_root, output_dir)
    request = load_request(args.request)
    scope, artifacts = validate_candidate_request(request, source_root)
    evidence_manifest = build_candidate_evidence_manifest(request, artifacts)
    report = build_candidate_intake_report(request, scope, artifacts)
    if args.dry_run:
        print(json.dumps({"evidenceManifest": evidence_manifest, "intakeReport": report}, indent=2, ensure_ascii=False))
        return 0

    temporary_root = Path(tempfile.mkdtemp(prefix=f".{output_dir.name}-", dir=output_dir.parent))
    try:
        for artifact in artifacts:
            copy_declared_artifact(artifact, temporary_root)
        write_json(temporary_root / "evidence-manifest.json", evidence_manifest)
        write_json(temporary_root / "intake-report.json", report)
        write_candidate_next_steps(temporary_root / "NEXT_STEPS.md", report)
        if output_dir.exists():
            raise FileExistsError("candidate output directory already exists")
        temporary_root.rename(output_dir)
    except Exception:
        try:
            shutil.rmtree(temporary_root)
        except OSError as cleanup_error:
            raise CandidateIntakeError(
                f"candidate staging cleanup failed; residual directory name is {temporary_root.name}"
            ) from cleanup_error
        raise

    print(f"Staged {len(artifacts)} declared artifact(s).")
    return 0
