"""Validate the repository's canonical golden evidence inventory."""

from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path, PurePosixPath
from typing import Any


CANONICAL_ROOT = PurePosixPath("testdata/golden/canonical")
DIAGNOSTICS_ROOT = "testdata/diagnostics/golden-evidence"
DIAGNOSTIC_CTRLRAM_ARTIFACT_ROOT = PurePosixPath(
    "testdata/golden/ctrlram-replace/fixtures/20260717"
)
DIAGNOSTIC_CTRLRAM_INVENTORY = PurePosixPath(
    "testdata/golden/ctrlram-replace/manifest.20260717.json"
)
DIAGNOSTIC_OWNER_HANDOFF_ROOT = PurePosixPath("testdata/golden/owner-handoff")
STANDARD_MERGE_RELEASE_ALLOWLIST = PurePosixPath(
    "testdata/golden/release-standard-merge-v1.json"
)
ROOT_FILES = {PurePosixPath("README.md"), PurePosixPath("manifest.json")}
LEGACY_GOLDEN_ROOTS = {
    PurePosixPath("testdata/golden/ab-merge"),
    PurePosixPath("testdata/golden/standard-merge-gen-flash"),
}
ALLOWED_WORKFLOWS = {
    "ab-merge",
    "ctrlram-replace",
    "dp-replace",
    "standard-merge",
}
ROLE_DIRECTORIES = {
    "expected": "expected",
    "input": "inputs",
    "provenance": "provenance",
}
IC_PATTERN = re.compile(r"NT519[0-9]{2}\Z")
SLUG_PATTERN = re.compile(r"[a-z0-9][a-z0-9.-]*\Z")
TOPOLOGY_PATTERN = re.compile(r"(?:single|cascade-[1-9][0-9]*|topology-unscoped)\Z")
SHA256_PATTERN = re.compile(r"[0-9a-f]{64}\Z")


def _read_confined_file(
    path: Path, confined_root: Path, label: str, errors: list[str]
) -> bytes | None:
    try:
        relative_path = path.relative_to(confined_root)
        resolved_root = confined_root.resolve(strict=True)
    except (OSError, ValueError) as error:
        errors.append(f"cannot resolve {label} root {confined_root}: {error}")
        return None
    current = confined_root
    if current.is_symlink():
        errors.append(f"{label} root cannot be a symlink: {confined_root}")
        return None
    for part in relative_path.parts:
        current /= part
        if current.is_symlink():
            errors.append(f"{label} path cannot contain a symlink: {current}")
            return None
    try:
        resolved_path = path.resolve(strict=True)
    except OSError as error:
        errors.append(f"cannot resolve {label} {path}: {error}")
        return None
    if resolved_root not in resolved_path.parents or not resolved_path.is_file():
        errors.append(f"{label} escaped its confined root or is not a file: {path}")
        return None
    try:
        return resolved_path.read_bytes()
    except OSError as error:
        errors.append(f"cannot read {label} {path}: {error}")
        return None


def _load_object(
    path: Path, confined_root: Path, label: str, errors: list[str]
) -> dict[str, Any] | None:
    payload = _read_confined_file(path, confined_root, label, errors)
    if payload is None:
        return None
    try:
        value = json.loads(payload.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        errors.append(f"invalid JSON in {label} {path}: {error}")
        return None
    if not isinstance(value, dict):
        errors.append(f"{label} must be a JSON object: {path}")
        return None
    return value


def _relative_path(
    value: object, label: str, errors: list[str]
) -> PurePosixPath | None:
    if not isinstance(value, str) or not value:
        errors.append(f"{label} must be a non-empty relative path")
        return None
    path = PurePosixPath(value)
    if (
        path.is_absolute()
        or ".." in path.parts
        or "\\" in value
        or (path.parts and ":" in path.parts[0])
        or str(path) != value
    ):
        errors.append(f"{label} is not a normalized confined path: {value}")
        return None
    return path


def _required_string(
    document: dict[str, Any], key: str, label: str, errors: list[str]
) -> str | None:
    value = document.get(key)
    if not isinstance(value, str) or not value:
        errors.append(f"{label} must contain non-empty string {key}")
        return None
    return value


def _case_directory(
    case: dict[str, Any], label: str, errors: list[str]
) -> PurePosixPath | None:
    ic = _required_string(case, "ic", label, errors)
    workflow = _required_string(case, "workflow", label, errors)
    variant = _required_string(case, "variantOrVersion", label, errors)
    topology = _required_string(case, "topology", label, errors)
    case_id = _required_string(case, "caseId", label, errors)
    if None in (ic, workflow, variant, topology, case_id):
        return None
    if IC_PATTERN.fullmatch(ic) is None:
        errors.append(f"{label} has invalid IC id: {ic}")
    if workflow not in ALLOWED_WORKFLOWS:
        errors.append(f"{label} has unsupported workflow: {workflow}")
    for field, value in (("variantOrVersion", variant), ("caseId", case_id)):
        if SLUG_PATTERN.fullmatch(value) is None:
            errors.append(f"{label} has invalid {field} slug: {value}")
    if TOPOLOGY_PATTERN.fullmatch(topology) is None:
        errors.append(f"{label} has invalid topology: {topology}")
    return PurePosixPath(ic, workflow, variant, topology, case_id)


def _validate_artifact(
    canonical_root: Path,
    case_directory: PurePosixPath,
    artifact: object,
    label: str,
    declared_files: set[PurePosixPath],
    artifact_ids: set[str],
    roles: list[str],
    errors: list[str],
) -> None:
    if not isinstance(artifact, dict):
        errors.append(f"{label} must be an object")
        return
    artifact_id = _required_string(artifact, "artifactId", label, errors)
    role = _required_string(artifact, "role", label, errors)
    relative_path = _relative_path(artifact.get("path"), f"{label}.path", errors)
    if artifact_id is not None:
        if artifact_id in artifact_ids:
            errors.append(f"duplicate artifactId in {label}: {artifact_id}")
        artifact_ids.add(artifact_id)
    if role is not None:
        roles.append(role)
        if role not in ROLE_DIRECTORIES:
            errors.append(f"{label} has unsupported role: {role}")
    if relative_path is None:
        return
    expected_parent = case_directory / ROLE_DIRECTORIES.get(role or "", "invalid")
    if (
        len(relative_path.parts) <= len(expected_parent.parts)
        or relative_path.parts[: len(expected_parent.parts)] != expected_parent.parts
    ):
        errors.append(
            f"{label} path must stay below {expected_parent}: {relative_path}"
        )
    # A single direct case may bind one immutable physical payload to multiple
    # logical argv roles (for example AB TPA and TPB). The case-directory check
    # above still prevents a different case from reaching across case roots.
    declared_files.add(relative_path)
    payload = _read_confined_file(
        canonical_root / Path(relative_path),
        canonical_root,
        f"canonical artifact {relative_path}",
        errors,
    )
    if payload is None:
        return
    expected_size = artifact.get("size")
    if type(expected_size) is not int or expected_size < 0:
        errors.append(f"{label} size must be a non-negative integer: {expected_size}")
    elif expected_size != len(payload):
        errors.append(
            f"canonical artifact size mismatch for {relative_path}: "
            f"expected {expected_size}, actual {len(payload)}"
        )
    expected_sha = artifact.get("sha256")
    actual_sha = hashlib.sha256(payload).hexdigest()
    if (
        not isinstance(expected_sha, str)
        or SHA256_PATTERN.fullmatch(expected_sha) is None
    ):
        errors.append(f"{label} has invalid sha256: {expected_sha}")
    elif expected_sha != actual_sha:
        errors.append(
            f"canonical artifact SHA-256 mismatch for {relative_path}: "
            f"expected {expected_sha}, actual {actual_sha}"
        )
    legacy_paths = artifact.get("legacyPaths")
    if not isinstance(legacy_paths, list) or not legacy_paths:
        errors.append(f"{label} must retain at least one legacyPaths entry")
    else:
        normalized: set[str] = set()
        for index, legacy_path in enumerate(legacy_paths):
            path = _relative_path(legacy_path, f"{label}.legacyPaths[{index}]", errors)
            if path is not None:
                if not str(path).startswith("testdata/golden/"):
                    errors.append(f"legacy path is outside testdata/golden: {path}")
                if str(path).startswith(f"{CANONICAL_ROOT}/"):
                    errors.append(
                        f"legacy path cannot point into canonical root: {path}"
                    )
                normalized.add(str(path))
        if len(normalized) != len(legacy_paths):
            errors.append(f"{label} contains duplicate legacyPaths")


def validate_canonical_golden(repository_root: Path, errors: list[str]) -> None:
    """Validate canonical case manifests, payload hashes, aliases, and closed inventory."""
    for legacy_root in LEGACY_GOLDEN_ROOTS:
        if (repository_root / Path(legacy_root)).exists():
            errors.append(f"canonical migration legacy root must be removed: {legacy_root}")

    canonical_root = repository_root / Path(CANONICAL_ROOT)
    try:
        resolved_repository_root = repository_root.resolve(strict=True)
        resolved_canonical_root = canonical_root.resolve(strict=True)
    except OSError as error:
        errors.append(f"cannot resolve canonical repository roots: {error}")
        return
    if (
        canonical_root.is_symlink()
        or resolved_repository_root not in resolved_canonical_root.parents
    ):
        errors.append(
            "canonical root must be a physical directory inside the repository"
        )
        return
    root_manifest = _load_object(
        canonical_root / "manifest.json",
        canonical_root,
        "canonical manifest",
        errors,
    )
    if root_manifest is None:
        return
    if root_manifest.get("schemaVersion") != "1.0":
        errors.append("canonical manifest schemaVersion must be 1.0")
    if root_manifest.get("payloadClass") != "owner-approved-golden":
        errors.append("canonical manifest payloadClass must be owner-approved-golden")
    if root_manifest.get("binaryPayloadsIncluded") is not True:
        errors.append("canonical manifest must declare binaryPayloadsIncluded=true")
    if root_manifest.get("diagnosticsRoot") != DIAGNOSTICS_ROOT:
        errors.append(f"canonical manifest diagnosticsRoot must be {DIAGNOSTICS_ROOT}")
    case_entries = root_manifest.get("cases")
    if not isinstance(case_entries, list) or not case_entries:
        errors.append("canonical manifest must contain a non-empty cases array")
        return

    declared_files = set(ROOT_FILES)
    direct_source_case_ids: set[str] = set()
    alias_sources: list[tuple[str, str, str]] = []
    direct_source_workflows: dict[str, str] = {}
    case_ids: set[str] = set()
    for index, entry in enumerate(case_entries):
        entry_label = f"canonical cases[{index}]"
        if not isinstance(entry, dict):
            errors.append(f"{entry_label} must be an object")
            continue
        case_id = _required_string(entry, "caseId", entry_label, errors)
        manifest_path = _relative_path(
            entry.get("manifestPath"), f"{entry_label}.manifestPath", errors
        )
        if case_id is None or manifest_path is None:
            continue
        if case_id in case_ids:
            errors.append(f"duplicate canonical caseId: {case_id}")
        case_ids.add(case_id)
        if manifest_path in declared_files:
            errors.append(f"canonical file is declared more than once: {manifest_path}")
        declared_files.add(manifest_path)
        case = _load_object(
            canonical_root / Path(manifest_path),
            canonical_root,
            "canonical case manifest",
            errors,
        )
        if case is None:
            continue
        label = f"canonical case {case_id}"
        if case.get("schemaVersion") != "1.0":
            errors.append(f"{label} schemaVersion must be 1.0")
        if case.get("caseId") != case_id:
            errors.append(f"{label} caseId does not match root manifest")
        _required_string(case, "sourceClassification", label, errors)
        _required_string(case, "ownerApproval", label, errors)
        case_directory = _case_directory(case, label, errors)
        if case_directory is None:
            continue
        expected_manifest_path = case_directory / "provenance/case.json"
        if manifest_path != expected_manifest_path:
            errors.append(
                f"{label} manifestPath must match its canonical facts: "
                f"expected {expected_manifest_path}, actual {manifest_path}"
            )
        direct = case.get("directGolden")
        if not isinstance(direct, bool):
            errors.append(f"{label} must declare boolean directGolden")
            continue
        direct_evidence = case.get("directEvidence", False)
        if not isinstance(direct_evidence, bool):
            errors.append(f"{label} directEvidence must be a boolean when declared")
            continue
        if direct and direct_evidence:
            errors.append(f"{label} cannot be both a direct golden and direct evidence")
            continue
        if direct or direct_evidence:
            direct_source_case_ids.add(case_id)
            workflow = case.get("workflow")
            if isinstance(workflow, str):
                direct_source_workflows[case_id] = workflow
            artifacts = case.get("artifacts")
            if not isinstance(artifacts, list) or not artifacts:
                kind = "direct golden" if direct else "direct evidence"
                errors.append(f"{label} {kind} must contain artifacts")
                continue
            artifact_ids: set[str] = set()
            roles: list[str] = []
            for artifact_index, artifact in enumerate(artifacts):
                _validate_artifact(
                    canonical_root,
                    case_directory,
                    artifact,
                    f"{label}.artifacts[{artifact_index}]",
                    declared_files,
                    artifact_ids,
                    roles,
                    errors,
                )
            if direct and "input" not in roles:
                errors.append(f"{label} direct golden requires input artifacts")
            if direct and roles.count("expected") != 1:
                errors.append(
                    f"{label} direct golden requires exactly one expected artifact"
                )
            if direct_evidence and "input" not in roles:
                errors.append(f"{label} direct evidence requires input artifacts")
            if direct_evidence and "expected" in roles:
                errors.append(
                    f"{label} direct evidence cannot declare an expected artifact"
                )
            if "alias" in case:
                errors.append(f"{label} direct case cannot declare alias")
        else:
            if case.get("artifacts") not in (None, []):
                errors.append(f"{label} alias case cannot contain physical artifacts")
            alias = case.get("alias")
            if not isinstance(alias, dict):
                errors.append(f"{label} must contain alias facts")
                continue
            source_case_id = _required_string(
                alias, "sourceCaseId", f"{label}.alias", errors
            )
            fact_scope = alias.get("factScope")
            evidence_refs = alias.get("evidenceRefs")
            if (
                not isinstance(fact_scope, list)
                or not fact_scope
                or not all(isinstance(value, str) and value for value in fact_scope)
            ):
                errors.append(
                    f"{label}.alias factScope must be a non-empty string array"
                )
            if (
                not isinstance(evidence_refs, list)
                or not evidence_refs
                or not all(isinstance(value, str) and value for value in evidence_refs)
            ):
                errors.append(
                    f"{label}.alias evidenceRefs must be a non-empty string array"
                )
            if source_case_id is not None:
                workflow = case.get("workflow")
                if isinstance(workflow, str):
                    alias_sources.append((case_id, workflow, source_case_id))

    for alias_case_id, alias_workflow, source_case_id in alias_sources:
        if source_case_id not in direct_source_case_ids:
            errors.append(
                f"canonical alias {alias_case_id} must reference a direct canonical evidence case: "
                f"{source_case_id}"
            )
        elif direct_source_workflows.get(source_case_id) != alias_workflow:
            errors.append(
                f"canonical alias {alias_case_id} workflow must match direct source "
                f"{source_case_id}"
            )

    actual_files: set[PurePosixPath] = set()
    for path in canonical_root.rglob("*"):
        if path.is_symlink():
            errors.append(f"canonical root cannot contain symlinks: {path}")
        elif path.is_file():
            actual_files.add(PurePosixPath(path.relative_to(canonical_root).as_posix()))
    if actual_files != declared_files:
        missing = sorted(declared_files - actual_files)
        extra = sorted(actual_files - declared_files)
        if missing:
            errors.append(
                "canonical manifest declares missing files: "
                + ", ".join(str(path) for path in missing)
            )
        if extra:
            errors.append(
                "canonical root contains undeclared files: "
                + ", ".join(str(path) for path in extra)
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
            errors.append(f"diagnostic golden record is declared more than once: {relative_path}")
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
            errors.append(f"diagnostic golden record SHA-256 mismatch for {relative_path}")
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
        errors.append("diagnostic golden root differs from its closed records inventory")
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
        errors.append("diagnostic CtrlRAM source archive SHA-256 must match its inventory")

    payloads = inventory.get("payloads")
    supporting_files = inventory.get("supportingFiles")
    if not isinstance(payloads, list) or not isinstance(supporting_files, list):
        errors.append("diagnostic CtrlRAM inventory payload arrays are required")
        return
    if quarantine.get("payloadCount") != len(payloads):
        errors.append("diagnostic CtrlRAM payloadCount does not match its inventory")
    if quarantine.get("supportingFileCount") != len(supporting_files):
        errors.append("diagnostic CtrlRAM supportingFileCount does not match its inventory")

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
            errors.append(f"diagnostic CtrlRAM path is declared more than once: {repository_path}")
        declared_files.add(repository_path)
        if (
            len(repository_path.parts) <= len(artifact_root_path.parts)
            or repository_path.parts[: len(artifact_root_path.parts)]
            != artifact_root_path.parts
        ):
            errors.append(f"{label} is outside the frozen artifact root: {repository_path}")
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
            errors.append(f"diagnostic CtrlRAM artifact root cannot contain symlinks: {path}")
    actual_files = {
        PurePosixPath(path.relative_to(repository_root).as_posix())
        for path in artifact_entries
        if path.is_file() and not path.is_symlink()
    }
    if actual_files != declared_files:
        errors.append("diagnostic CtrlRAM artifact root differs from its closed inventory")

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
        errors.append("diagnostic ownerHandoff root must be a physical repository directory")
        return
    owner_entries = list(owner_root.rglob("*"))
    for path in owner_entries:
        if path.is_symlink():
            errors.append(f"diagnostic ownerHandoff cannot contain symlinks: {path}")
    owner_files = [
        path for path in owner_entries if path.is_file() and not path.is_symlink()
    ]
    if owner_handoff.get("fileCount") != len(owner_files):
        errors.append("diagnostic ownerHandoff fileCount does not match its inventory")
    if owner_handoff.get("binaryPayloadsIncluded") is not False:
        errors.append("diagnostic ownerHandoff must declare binaryPayloadsIncluded=false")
    tree_records: list[str] = []
    for path in sorted(owner_files, key=lambda item: item.relative_to(owner_root).as_posix()):
        if path.name not in {".gitignore", ".keep"} and path.suffix.lower() != ".md":
            errors.append(f"diagnostic ownerHandoff contains a binary or unsupported file: {path}")
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
        or tree_sha
        != hashlib.sha256("".join(tree_records).encode("utf-8")).hexdigest()
    ):
        errors.append("diagnostic ownerHandoff treeSha256 does not match its closed inventory")


def validate_standard_merge_release_allowlist(
    repository_root: Path, errors: list[str]
) -> None:
    """Validate the explicit case/artifact facts authorized for release packaging."""
    canonical_root = repository_root / Path(CANONICAL_ROOT)
    inventory = _load_object(
        canonical_root / "manifest.json",
        canonical_root,
        "canonical manifest",
        errors,
    )
    allowlist_path = repository_root / Path(STANDARD_MERGE_RELEASE_ALLOWLIST)
    try:
        allowlist = json.loads(allowlist_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        errors.append(f"cannot read Standard Merge release allowlist: {error}")
        return
    if not isinstance(allowlist, dict):
        errors.append("Standard Merge release allowlist must be a JSON object")
        return
    if inventory is None:
        return
    if allowlist.get("schemaVersion") != "1.0":
        errors.append("Standard Merge release allowlist schemaVersion must be 1.0")
    if allowlist.get("workflow") != "standard-merge":
        errors.append("Standard Merge release allowlist workflow must be standard-merge")
    if allowlist.get("releaseStatus") != "human-gated-allowlist":
        errors.append(
            "Standard Merge release allowlist releaseStatus must be human-gated-allowlist"
        )
    inventory_cases = {
        entry.get("caseId"): entry.get("manifestPath")
        for entry in inventory.get("cases", [])
        if isinstance(entry, dict)
    }
    release_cases = allowlist.get("cases")
    if not isinstance(release_cases, list) or not release_cases:
        errors.append("Standard Merge release allowlist must contain cases")
        return
    seen_case_ids: set[str] = set()
    for index, release_case in enumerate(release_cases):
        label = f"Standard Merge release cases[{index}]"
        if not isinstance(release_case, dict):
            errors.append(f"{label} must be an object")
            continue
        case_id = _required_string(release_case, "caseId", label, errors)
        manifest_path = _relative_path(
            release_case.get("manifestPath"), f"{label}.manifestPath", errors
        )
        if case_id is None or manifest_path is None:
            continue
        if case_id in seen_case_ids:
            errors.append(f"duplicate Standard Merge release caseId: {case_id}")
        seen_case_ids.add(case_id)
        if inventory_cases.get(case_id) != str(manifest_path):
            errors.append(
                f"{label} does not match the canonical case manifest path for {case_id}"
            )
            continue
        case = _load_object(
            canonical_root / Path(manifest_path),
            canonical_root,
            "release-selected canonical case",
            errors,
        )
        if case is None:
            continue
        if case.get("workflow") != "standard-merge":
            errors.append(f"{label} selects a non-Standard Merge case")
        if case.get("directEvidence") is True:
            errors.append(f"{label} cannot select direct input evidence for release")
        release_direct = release_case.get("directGolden")
        canonical_direct = case.get("directGolden")
        if type(release_direct) is not bool:
            errors.append(f"{label}.directGolden must be a boolean")
        elif type(canonical_direct) is not bool:
            errors.append(f"{label} canonical directGolden must be a boolean")
        elif release_direct != canonical_direct:
            errors.append(f"{label} directGolden differs from the canonical case")
        canonical_artifacts = {
            artifact.get("artifactId"): artifact
            for artifact in case.get("artifacts", [])
            if isinstance(artifact, dict)
        }
        release_artifacts = release_case.get("artifacts")
        if not isinstance(release_artifacts, list):
            errors.append(f"{label}.artifacts must be an array")
            continue
        release_artifact_ids: set[str] = set()
        for artifact_index, release_artifact in enumerate(release_artifacts):
            artifact_label = f"{label}.artifacts[{artifact_index}]"
            if not isinstance(release_artifact, dict):
                errors.append(f"{artifact_label} must be an object")
                continue
            artifact_id = _required_string(
                release_artifact, "artifactId", artifact_label, errors
            )
            if artifact_id is None:
                continue
            if artifact_id in release_artifact_ids:
                errors.append(f"{label} contains duplicate artifactId {artifact_id}")
            release_artifact_ids.add(artifact_id)
            canonical_artifact = canonical_artifacts.get(artifact_id)
            if canonical_artifact is None:
                errors.append(
                    f"{artifact_label} is not declared by canonical case {case_id}"
                )
                continue
            for field in ("path", "size", "sha256"):
                if release_artifact.get(field) != canonical_artifact.get(field):
                    errors.append(
                        f"{artifact_label}.{field} differs from canonical case {case_id}"
                    )
        if release_artifact_ids != set(canonical_artifacts):
            errors.append(
                f"{label} artifact IDs must exactly match canonical case {case_id}"
            )
