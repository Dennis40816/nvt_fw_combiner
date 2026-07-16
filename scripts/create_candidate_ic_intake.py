"""Create deterministic, candidate-only IC intake records from declared evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import secrets
import stat
import sys
from contextlib import contextmanager
from datetime import datetime
from pathlib import Path, PureWindowsPath
from typing import Any, BinaryIO, Iterator

if __package__:
    from .candidate_intake_output import (
        IntakeError,
        ValidatedOutputDirectory,
        open_unix_directory_chain,
        open_validated_output_directory,
        resolve_directory,
    )
else:
    from candidate_intake_output import (
        IntakeError,
        ValidatedOutputDirectory,
        open_unix_directory_chain,
        open_validated_output_directory,
        resolve_directory,
    )

TOOL_ID = "candidate-ic-intake"
TOOL_VERSION = "1.0.0"
OUTPUT_FILES = (
    "candidate-bundle-rows.json",
    "candidate-evidence-manifest.json",
    "missing-evidence.json",
    "validation-report.json",
)
ID_PATTERN = re.compile(r"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$")
MEMBER_ID_PATTERN = re.compile(r"^NT[0-9A-Z-]+$")
HASH_PATTERN = re.compile(r"^[0-9a-f]{64}$")
ParentSnapshots = tuple[tuple[Path, tuple[int, int]], ...]
SEMVER_PATTERN = re.compile(
    r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:[-+][0-9A-Za-z.-]+)?$"
)
SOURCE_KINDS = {
    "workbook",
    "source-code",
    "firmware",
    "document",
    "issue-export",
    "owner-record",
}
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
PROMOTION_IMPACTS = {
    "none",
    "blocks-map-resolution",
    "blocks-execution",
    "blocks-support",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence-manifest", type=Path, required=True)
    parser.add_argument(
        "--source-root",
        type=Path,
        required=True,
        help="Root containing explicitly bound evidence files.",
    )
    parser.add_argument(
        "--artifact",
        action="append",
        default=[],
        metavar="ARTIFACT_ID=RELATIVE_PATH",
        help="Bind one manifest source artifact to one file below --source-root. Repeat as needed.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        required=True,
        help=(
            "Candidate output path: an existing empty directory on Windows; "
            "a nonexistent path under a writable parent on Unix."
        ),
    )
    parser.add_argument(
        "--generated-at", required=True, help="UTC ISO-8601 timestamp ending in Z."
    )
    return parser.parse_args()


def require(value: Any, description: str, predicate: bool) -> Any:
    if not predicate:
        raise IntakeError(description)
    return value


def require_id(value: Any, description: str) -> str:
    return require(
        value,
        description,
        isinstance(value, str) and ID_PATTERN.fullmatch(value) is not None,
    )


def require_object(value: Any, description: str) -> dict[str, Any]:
    return require(value, description, isinstance(value, dict))


def require_keys(
    value: dict[str, Any], allowed: set[str], required: set[str], description: str
) -> None:
    unexpected = sorted(set(value) - allowed)
    missing = sorted(required - set(value))
    if unexpected:
        raise IntakeError(
            f"{description} has unsupported fields: {', '.join(unexpected)}"
        )
    if missing:
        raise IntakeError(
            f"{description} is missing required fields: {', '.join(missing)}"
        )


def read_json(path: Path) -> dict[str, Any]:
    def reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise IntakeError(f"duplicate JSON key '{key}' in {path}")
            result[key] = value
        return result

    try:
        with open_validated_regular_file(path, "evidence manifest") as (
            _,
            stream,
            _,
        ):
            result = json.loads(
                stream.read().decode("utf-8"),
                object_pairs_hook=reject_duplicates,
            )
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exception:
        raise IntakeError(
            f"could not read evidence manifest '{path}': {exception}"
        ) from exception
    return require_object(result, "evidence manifest root must be an object")


def sha256(stream: BinaryIO) -> str:
    digest = hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
        digest.update(chunk)
    return digest.hexdigest()


@contextmanager
def _validated_parent_directory(
    path: Path,
) -> Iterator[tuple[int | None, ParentSnapshots | None]]:
    if os.name == "nt":
        snapshots = _windows_parent_snapshots(path)
        yield None, snapshots
        if _windows_parent_snapshots(path) != snapshots:
            raise IntakeError("input parent component changed while opening")
        return

    descriptor = open_unix_directory_chain(path.parent, "input parent")
    try:
        yield descriptor, None
    finally:
        os.close(descriptor)


def _windows_parent_snapshots(path: Path) -> tuple[tuple[Path, tuple[int, int]], ...]:
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    snapshots: list[tuple[Path, tuple[int, int]]] = []
    for parent in reversed(path.parents):
        try:
            status = os.stat(parent, follow_symlinks=False)
        except OSError as exception:
            raise IntakeError(
                f"input parent component changed while opening: {parent}"
            ) from exception
        if stat.S_ISLNK(status.st_mode) or (
            getattr(status, "st_file_attributes", 0) & reparse_flag
        ):
            raise IntakeError(f"reparse point is not allowed: {parent}")
        if not stat.S_ISDIR(status.st_mode):
            raise IntakeError(f"input parent component is not a directory: {parent}")
        snapshots.append((parent, (status.st_dev, status.st_ino)))
    return tuple(snapshots)


@contextmanager
def open_validated_regular_file(
    path: Path, description: str
) -> Iterator[tuple[Path, BinaryIO, os.stat_result]]:
    raw = Path(os.path.abspath(path.expanduser()))
    flags = (
        os.O_RDONLY
        | getattr(os, "O_BINARY", 0)
        | getattr(os, "O_CLOEXEC", 0)
        | getattr(os, "O_NOFOLLOW", 0)
    )
    descriptor = -1
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    with _validated_parent_directory(raw) as (
        parent_descriptor,
        windows_parent_snapshots,
    ):
        leaf: str | Path = raw if parent_descriptor is None else raw.name
        stat_options = (
            {"follow_symlinks": False}
            if parent_descriptor is None
            else {"dir_fd": parent_descriptor, "follow_symlinks": False}
        )
        try:
            initial_status = os.stat(leaf, **stat_options)
        except OSError as exception:
            raise IntakeError(
                f"{description} must be an existing file: {raw}"
            ) from exception
        if stat.S_ISLNK(initial_status.st_mode) or (
            getattr(initial_status, "st_file_attributes", 0) & reparse_flag
        ):
            raise IntakeError(f"reparse point is not allowed: {raw}")
        if not stat.S_ISREG(initial_status.st_mode):
            raise IntakeError(f"{description} must be an existing file: {raw}")

        validated_identity = (initial_status.st_dev, initial_status.st_ino)
        try:
            if parent_descriptor is None:
                descriptor = os.open(leaf, flags)
            else:
                descriptor = os.open(leaf, flags, dir_fd=parent_descriptor)
            opened_status = os.fstat(descriptor)
            path_status = os.stat(leaf, **stat_options)
            if not stat.S_ISREG(opened_status.st_mode) or not stat.S_ISREG(
                path_status.st_mode
            ):
                raise IntakeError(f"{description} must be a regular filesystem file")
            opened_identity = (opened_status.st_dev, opened_status.st_ino)
            path_identity = (path_status.st_dev, path_status.st_ino)
            if (
                opened_identity != validated_identity
                or path_identity != validated_identity
            ):
                raise IntakeError(
                    f"{description} open handle does not match the validated path"
                )
            if (
                windows_parent_snapshots is not None
                and _windows_parent_snapshots(raw) != windows_parent_snapshots
            ):
                raise IntakeError("input parent component changed while opening")

            with os.fdopen(descriptor, "rb", closefd=True) as stream:
                descriptor = -1
                yield raw, stream, opened_status
        finally:
            if descriptor >= 0:
                os.close(descriptor)


def validate_generated_at(value: str) -> str:
    if not value.endswith("Z"):
        raise IntakeError("--generated-at must be a UTC timestamp ending in Z")
    try:
        datetime.fromisoformat(value.removesuffix("Z") + "+00:00")
    except ValueError as exception:
        raise IntakeError(
            "--generated-at must be an ISO-8601 UTC timestamp"
        ) from exception
    return value


def validate_manifest(
    manifest: dict[str, Any],
) -> tuple[dict[str, dict[str, Any]], list[dict[str, Any]]]:
    required = {
        "schemaVersion",
        "manifestId",
        "manifestVersion",
        "status",
        "sourceArtifacts",
        "facts",
        "reviews",
    }
    require_keys(manifest, required, required, "evidence manifest")
    if manifest["schemaVersion"] != "1.0":
        raise IntakeError(
            "candidate intake accepts evidence manifest schemaVersion 1.0 only"
        )
    require_id(
        manifest["manifestId"], "manifestId must use the evidence-manifest id format"
    )
    if not isinstance(manifest["manifestVersion"], str) or not SEMVER_PATTERN.fullmatch(
        manifest["manifestVersion"]
    ):
        raise IntakeError(
            "manifestVersion must use the evidence-manifest semver format"
        )
    if manifest["status"] != "candidate":
        raise IntakeError(
            "candidate intake accepts only source manifests with status 'candidate'"
        )
    artifacts = validate_artifacts(manifest["sourceArtifacts"])
    return artifacts, validate_facts(manifest["facts"], artifacts, manifest["reviews"])


def validate_artifacts(value: Any) -> dict[str, dict[str, Any]]:
    if not isinstance(value, list) or not value:
        raise IntakeError("sourceArtifacts must be a non-empty array")
    artifacts: dict[str, dict[str, Any]] = {}
    required = {"artifactId", "sourceKind", "logicalName", "contentHash", "sizeBytes"}
    for index, raw in enumerate(value):
        artifact = require_object(raw, f"sourceArtifacts[{index}] must be an object")
        require_keys(
            artifact,
            required | {"repositoryPath"},
            required,
            f"sourceArtifacts[{index}]",
        )
        artifact_id = require_id(
            artifact["artifactId"], f"sourceArtifacts[{index}].artifactId is invalid"
        )
        if artifact_id in artifacts:
            raise IntakeError(
                f"sourceArtifacts contains duplicate artifactId '{artifact_id}'"
            )
        if artifact["sourceKind"] not in SOURCE_KINDS:
            raise IntakeError(f"sourceArtifacts[{index}].sourceKind is invalid")
        if not isinstance(artifact["logicalName"], str) or not artifact["logicalName"]:
            raise IntakeError(
                f"sourceArtifacts[{index}].logicalName must be a non-empty string"
            )
        if not isinstance(artifact["contentHash"], str) or not HASH_PATTERN.fullmatch(
            artifact["contentHash"]
        ):
            raise IntakeError(
                f"sourceArtifacts[{index}].contentHash must be lowercase SHA-256"
            )
        if not isinstance(artifact["sizeBytes"], int) or artifact["sizeBytes"] < 1:
            raise IntakeError(
                f"sourceArtifacts[{index}].sizeBytes must be a positive integer"
            )
        artifacts[artifact_id] = artifact
    return artifacts


def validate_facts(
    value: Any, artifacts: dict[str, dict[str, Any]], reviews: Any
) -> list[dict[str, Any]]:
    if not isinstance(value, list) or not value or not isinstance(reviews, list):
        raise IntakeError("facts must be non-empty and reviews must be an array")
    facts: list[dict[str, Any]] = []
    fact_ids: set[str] = set()
    required = {
        "factId",
        "subject",
        "factKind",
        "value",
        "disposition",
        "promotionImpact",
        "citations",
    }
    for index, raw in enumerate(value):
        fact = require_object(raw, f"facts[{index}] must be an object")
        require_keys(fact, required | {"rationale"}, required, f"facts[{index}]")
        fact_id = require_id(fact["factId"], f"facts[{index}].factId is invalid")
        if fact_id in fact_ids:
            raise IntakeError(f"facts contains duplicate factId '{fact_id}'")
        fact_ids.add(fact_id)
        subject = require_object(
            fact["subject"], f"facts[{index}].subject must be an object"
        )
        require_keys(
            subject,
            {"familyId", "memberId", "modeId", "profileId"},
            {"familyId"},
            f"facts[{index}].subject",
        )
        require_id(subject["familyId"], f"facts[{index}].subject.familyId is invalid")
        if "memberId" in subject:
            require(
                subject["memberId"],
                f"facts[{index}].subject.memberId is invalid",
                isinstance(subject["memberId"], str)
                and MEMBER_ID_PATTERN.fullmatch(subject["memberId"]) is not None,
            )
        for key in ("modeId", "profileId"):
            if key in subject:
                require_id(subject[key], f"facts[{index}].subject.{key} is invalid")
        if fact["factKind"] not in FACT_KINDS or not isinstance(fact["value"], dict):
            raise IntakeError(f"facts[{index}] has an invalid kind or value")
        if (
            fact["disposition"] not in FACT_DISPOSITIONS
            or fact["promotionImpact"] not in PROMOTION_IMPACTS
        ):
            raise IntakeError(
                f"facts[{index}] has invalid disposition or promotionImpact"
            )
        citations = fact["citations"]
        if not isinstance(citations, list) or not citations:
            raise IntakeError(f"facts[{index}].citations must be a non-empty array")
        for citation_index, raw_citation in enumerate(citations):
            citation = require_object(
                raw_citation,
                f"facts[{index}].citations[{citation_index}] must be an object",
            )
            require_keys(
                citation,
                {"artifactId", "location"},
                {"artifactId", "location"},
                f"facts[{index}].citations[{citation_index}]",
            )
            citation_id = require_id(
                citation["artifactId"],
                f"facts[{index}].citations[{citation_index}].artifactId is invalid",
            )
            if (
                citation_id not in artifacts
                or not isinstance(citation["location"], str)
                or not citation["location"]
            ):
                raise IntakeError(
                    f"facts[{index}].citations[{citation_index}] does not identify declared evidence"
                )
        facts.append(fact)
    for index, review in enumerate(reviews):
        if not isinstance(review, dict):
            raise IntakeError(f"reviews[{index}] must be an object")
    return facts


def parse_bindings(
    raw_bindings: list[str], artifacts: dict[str, dict[str, Any]]
) -> dict[str, PureWindowsPath]:
    bindings: dict[str, PureWindowsPath] = {}
    for raw in raw_bindings:
        artifact_id, separator, raw_path = raw.partition("=")
        if not separator or not ID_PATTERN.fullmatch(artifact_id) or not raw_path:
            raise IntakeError("--artifact must use ARTIFACT_ID=RELATIVE_PATH")
        if artifact_id not in artifacts or artifact_id in bindings:
            raise IntakeError(
                f"--artifact must bind each declared artifact at most once: '{artifact_id}'"
            )
        relative = PureWindowsPath(raw_path)
        if (
            relative.is_absolute()
            or relative.drive
            or any(part in {"", ".", ".."} for part in relative.parts)
        ):
            raise IntakeError(
                f"--artifact path for '{artifact_id}' must be a relative path without traversal"
            )
        if relative.name.startswith("~$"):
            raise IntakeError(
                f"--artifact path for '{artifact_id}' is an Office lock file"
            )
        bindings[artifact_id] = relative
    return bindings


def verify_artifacts(
    artifacts: dict[str, dict[str, Any]],
    source_root: Path,
    bindings: dict[str, PureWindowsPath],
) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for artifact_id in sorted(artifacts):
        relative = bindings.get(artifact_id)
        if relative is None:
            rows.append({"artifactId": artifact_id, "status": "not-bound"})
            continue
        path = source_root.joinpath(*relative.parts)
        artifact = artifacts[artifact_id]
        with open_validated_regular_file(path, f"bound artifact '{artifact_id}'") as (
            resolved,
            stream,
            opened_status,
        ):
            try:
                resolved.relative_to(source_root)
            except ValueError as exception:
                raise IntakeError(
                    f"bound artifact '{artifact_id}' escapes --source-root"
                ) from exception
            if opened_status.st_size != artifact["sizeBytes"]:
                raise IntakeError(
                    f"bound artifact '{artifact_id}' size does not match its declared evidence"
                )
            if sha256(stream) != artifact["contentHash"]:
                raise IntakeError(
                    f"bound artifact '{artifact_id}' SHA-256 does not match its declared evidence"
                )
        rows.append({"artifactId": artifact_id, "status": "verified"})
    return rows


def build_outputs(
    manifest: dict[str, Any],
    facts: list[dict[str, Any]],
    artifact_rows: list[dict[str, Any]],
    generated_at: str,
) -> dict[str, dict[str, Any]]:
    candidate_manifest = dict(manifest)
    candidate_manifest["intakeProvenance"] = {
        "toolId": TOOL_ID,
        "toolVersion": TOOL_VERSION,
        "generatedAt": generated_at,
        "candidateOnly": True,
    }
    unresolved = sorted(
        fact["factId"] for fact in facts if fact["disposition"] == "unresolved"
    )
    rejected = sorted(
        fact["factId"] for fact in facts if fact["disposition"] == "rejected"
    )
    blocking = sorted(
        fact["factId"]
        for fact in facts
        if fact["disposition"] == "unresolved" and fact["promotionImpact"] != "none"
    )
    rows: list[dict[str, Any]] = []
    for fact in sorted(facts, key=lambda item: item["factId"]):
        row = {
            "factId": fact["factId"],
            "familyId": fact["subject"]["familyId"],
            "factKind": fact["factKind"],
            "disposition": fact["disposition"],
            "promotionImpact": fact["promotionImpact"],
            "runtimeAuthority": "none",
        }
        for key in ("memberId", "modeId", "profileId"):
            if key in fact["subject"]:
                row[key] = fact["subject"][key]
        rows.append(row)
    return {
        "candidate-evidence-manifest.json": candidate_manifest,
        "candidate-bundle-rows.json": {
            "schemaVersion": "1.0",
            "candidateId": manifest["manifestId"],
            "candidateVersion": manifest["manifestVersion"],
            "evidenceManifest": "candidate-evidence-manifest.json",
            "runtimeRegistration": False,
            "supportPromotion": False,
            "rows": rows,
        },
        "missing-evidence.json": {
            "schemaVersion": "1.0",
            "candidateId": manifest["manifestId"],
            "unboundArtifactIds": sorted(
                row["artifactId"]
                for row in artifact_rows
                if row["status"] == "not-bound"
            ),
            "unresolvedFactIds": unresolved,
            "rejectedFactIds": rejected,
            "blockingUnresolvedFactIds": blocking,
        },
        "validation-report.json": {
            "schemaVersion": "1.0",
            "candidateId": manifest["manifestId"],
            "validationScope": "candidate-only declared evidence intake; trusted profile-bundle validation remains in the V2 materializer/loader",
            "officeReadMode": "byte-read-only; no Office automation or macro execution",
            "runtimeRegistration": False,
            "supportPromotion": False,
            "artifactVerification": artifact_rows,
        },
    }


def write_outputs(
    output: ValidatedOutputDirectory, outputs: dict[str, dict[str, Any]]
) -> None:
    serialized = {
        name: (
            json.dumps(outputs[name], ensure_ascii=False, indent=2, sort_keys=True)
            + "\n"
        ).encode("utf-8")
        for name in OUTPUT_FILES
    }
    staged_outputs: list[tuple[str, str, int]] = []
    try:
        for name in OUTPUT_FILES:
            temporary_name = f".{name}.{secrets.token_hex(16)}.tmp"
            descriptor = output.create_staged(temporary_name)
            staged_outputs.append((name, temporary_name, descriptor))
            with os.fdopen(descriptor, "wb", closefd=False) as stream:
                stream.write(serialized[name])
                stream.flush()
                os.fsync(stream.fileno())
        output.validate_identity()
        output.require_names(output.active_names(*(item[1] for item in staged_outputs)))
        for name, temporary_name, descriptor in staged_outputs:
            output.publish(temporary_name, name, descriptor, serialized[name])
        output.validate_identity()
        output.validate_published()
        output.require_names(output.active_names(*OUTPUT_FILES))
    except BaseException as exception:
        # Never retain staged or published records after an interrupted intake.
        try:
            output.cleanup_tracked()
        except IntakeError as cleanup_error:
            raise cleanup_error from exception
        raise


def main() -> int:
    args = parse_args()
    try:
        manifest = read_json(args.evidence_manifest)
        source_root = resolve_directory(args.source_root, "source root")
        generated_at = validate_generated_at(args.generated_at)
        artifacts, facts = validate_manifest(manifest)
        with open_validated_output_directory(args.output) as output:
            artifact_rows = verify_artifacts(
                artifacts, source_root, parse_bindings(args.artifact, artifacts)
            )
            write_outputs(
                output, build_outputs(manifest, facts, artifact_rows, generated_at)
            )
        print(
            "Created candidate-only intake for "
            f"'{manifest['manifestId']}' in {output.destination_path}"
        )
        return 0
    except (IntakeError, OSError) as exception:
        print(f"ERROR: {exception}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
