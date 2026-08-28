"""Fail-closed v0.9.16 parity evidence comparator.

This module orchestrates already-existing NFC CLI/application contracts.  It
does not plan firmware operations, interpret firmware bytes, or infer support.
"""

from __future__ import annotations

import argparse
import ast
import contextlib
import copy
import datetime as dt
import hashlib
import json
import os
import re
import shutil
import stat
import subprocess
import tempfile
import zipfile
from pathlib import Path, PurePosixPath
from typing import Any, Iterator, Mapping, NamedTuple, Sequence
from urllib.parse import quote, urlencode

import yaml

try:
    from scripts.canonical_golden_validation import (
        validate_canonical_golden as _canonical_validator,
    )
except ModuleNotFoundError as error:
    if error.name != "scripts":
        raise
    # `python ./scripts/v0916_parity_certification.py ...` places the scripts
    # directory, rather than the repository root, on sys.path.  This is the
    # exact GitHub Actions invocation and must remain a supported entry point.
    from canonical_golden_validation import (  # type: ignore[no-redef]
        validate_canonical_golden as _canonical_validator,
    )


SHA256_RE = re.compile(r"[0-9a-f]{64}\Z")
SHA1_RE = re.compile(r"[0-9a-f]{40}\Z")
UTC_RE = re.compile(
    r"(?P<date>[0-9]{4}-[0-9]{2}-[0-9]{2})T"
    r"(?P<time>[0-9]{2}:[0-9]{2}:[0-9]{2})"
    r"(?P<fraction>\.[0-9]{1,7})?Z\Z"
)
MAX_SAFE_INTEGER = 9_007_199_254_740_991
MAX_SNAPSHOT_FILES = 20_000
MAX_SNAPSHOT_BYTES = 512 * 1024 * 1024
_validate_canonical_golden = _canonical_validator


class ParityError(RuntimeError):
    """A stable, fail-closed parity gate failure."""

    def __init__(self, code: str, message: str = "", *, details: dict[str, Any] | None = None):
        super().__init__(message or code)
        self.code = code
        self.details = details or {}


def _fail(code: str, message: str = "", **details: Any) -> None:
    raise ParityError(code, message, details=details)


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _artifact(path: Path) -> dict[str, Any]:
    require_local_artifact(path, "artifact")
    payload = path.read_bytes()
    return {"size": len(payload), "sha256": _sha256(payload)}


def _local_artifact(path: Path) -> dict[str, Any]:
    """Bind a local artifact without leaking it into terminal evidence."""

    return {"path": str(path), **_artifact(path)}


def _native_path(path: Path) -> str:
    absolute = str(path.absolute())
    if os.name == "nt" and not absolute.startswith("\\\\?\\"):
        if absolute.startswith("\\\\"):
            return "\\\\?\\UNC\\" + absolute[2:]
        return "\\\\?\\" + absolute
    return absolute


def _decode_git_path(value: str) -> str:
    if not (value.startswith('"') and value.endswith('"')):
        return value
    try:
        decoded = ast.literal_eval(value)
        return decoded.encode("latin-1").decode("utf-8")
    except (SyntaxError, ValueError, UnicodeError):
        _fail("PARITY_AUTHORITY_MISMATCH", "invalid quoted Git path")


def _remove_tree(path: Path) -> None:
    if path.exists():
        def make_writable_and_retry(function: Any, name: str, _: Any) -> None:
            os.chmod(name, stat.S_IWRITE)
            function(name)

        shutil.rmtree(_native_path(path), onerror=make_writable_and_retry)


@contextlib.contextmanager
def controlled_temporary_directory(prefix: str) -> Iterator[Path]:
    path = Path(tempfile.mkdtemp(prefix=prefix))
    try:
        yield path
    finally:
        _remove_tree(path)


def _reject_constant(value: str) -> Any:
    _fail("PARITY_PROVENANCE_INVALID", f"non-I-JSON number {value}")


def load_json_reject_duplicates(source: str | bytes) -> Any:
    def pairs(items: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in items:
            if key in result:
                _fail("PARITY_PROVENANCE_INVALID", f"duplicate property: {key}")
            result[key] = value
        return result

    try:
        value = json.loads(
            source,
            object_pairs_hook=pairs,
            parse_float=lambda _: _fail("PARITY_PROVENANCE_INVALID", "fractions are not admitted"),
            parse_constant=_reject_constant,
        )
    except ParityError:
        raise
    except (UnicodeError, ValueError, TypeError, json.JSONDecodeError) as error:
        _fail("PARITY_PROVENANCE_INVALID", f"invalid JSON: {error}")
    _validate_ijson(value)
    return value


def _validate_ijson(value: Any) -> None:
    if value is None or isinstance(value, bool):
        return
    if isinstance(value, int):
        if abs(value) > MAX_SAFE_INTEGER:
            _fail("PARITY_PROVENANCE_INVALID", "integer is outside I-JSON safe range")
        return
    if isinstance(value, str):
        try:
            value.encode("utf-8")
        except UnicodeEncodeError:
            _fail("PARITY_PROVENANCE_INVALID", "lone surrogate is not I-JSON")
        return
    if isinstance(value, list):
        for item in value:
            _validate_ijson(item)
        return
    if isinstance(value, dict):
        for key, item in value.items():
            if not isinstance(key, str):
                _fail("PARITY_PROVENANCE_INVALID", "object key must be text")
            _validate_ijson(key)
            _validate_ijson(item)
        return
    _fail("PARITY_PROVENANCE_INVALID", f"unsupported JSON value {type(value).__name__}")


def _utf16_key(value: str) -> bytes:
    return value.encode("utf-16-be")


def canonical_json_bytes(value: Any) -> bytes:
    _validate_ijson(value)

    def render(item: Any) -> str:
        if item is None:
            return "null"
        if item is True:
            return "true"
        if item is False:
            return "false"
        if isinstance(item, int):
            return str(item)
        if isinstance(item, str):
            return json.dumps(item, ensure_ascii=False, separators=(",", ":"))
        if isinstance(item, list):
            return "[" + ",".join(render(child) for child in item) + "]"
        if isinstance(item, dict):
            keys = sorted(item, key=_utf16_key)
            return "{" + ",".join(render(key) + ":" + render(item[key]) for key in keys) + "}"
        _fail("PARITY_PROVENANCE_INVALID", "unsupported canonical JSON value")

    return render(value).encode("utf-8")


def canonical_json_sha256(value: Any) -> str:
    return _sha256(canonical_json_bytes(value))


def _unique_sorted(items: Sequence[Any], key, code: str = "PARITY_PROVENANCE_INVALID") -> list[Any]:
    identities = [key(item) for item in items]
    if len(identities) != len(set(identities)):
        _fail(code, "duplicate canonical identity")
    return sorted(items, key=lambda item: tuple(_utf16_key(str(part)) for part in key(item)))


def canonical_receipt_set_sha256(receipts: Sequence[dict[str, Any]]) -> str:
    return canonical_json_sha256(_unique_sorted(receipts, lambda x: (x["routeId"], x["role"])))


def canonical_provenance_subjects_sha256(subjects: Sequence[dict[str, Any]]) -> str:
    return canonical_json_sha256(_unique_sorted(subjects, lambda x: (x["name"],)))


def canonical_route_evidence_sha256(routes: Sequence[dict[str, Any]]) -> str:
    return canonical_json_sha256(_unique_sorted(routes, lambda x: (x["routeId"],)))


def canonical_operator_set_sha256(operators: Sequence[str]) -> str:
    return canonical_json_sha256(_unique_sorted(list(operators), lambda x: (x,)))


def parse_canonical_utc(value: str) -> dt.datetime:
    if not isinstance(value, str) or not UTC_RE.fullmatch(value):
        _fail("PARITY_PROVENANCE_INVALID", "timestamp is not canonical UTC")
    try:
        normalized = value[:-1]
        if "." in normalized:
            head, fraction = normalized.split(".", 1)
            normalized = head + "." + fraction.ljust(6, "0")[:6]
        return dt.datetime.fromisoformat(normalized).replace(tzinfo=dt.timezone.utc)
    except ValueError:
        _fail("PARITY_PROVENANCE_INVALID", "timestamp is not a calendar instant")


def validate_time_order(start: str, end: str, *, error_code: str) -> None:
    try:
        before, after = parse_canonical_utc(start), parse_canonical_utc(end)
    except ParityError:
        _fail(error_code, "invalid timestamp")
    if before > after:
        _fail(error_code, "timestamps are not monotonic")


class Route(NamedTuple):
    route_id: str
    capability_fingerprint: str
    ic_id: str
    workflow_id: str
    ic_count_variant: str
    map_variant: str
    proof_kind: str
    full_route_id: str | None = None
    full_capability_fingerprint: str | None = None
    tp_length: int | None = None


class Plan(NamedTuple):
    raw: dict[str, Any]
    routes: tuple[Route, ...]
    workflow_counts: dict[str, int]
    path: Path


class BaselineAuthority(NamedTuple):
    contract: dict[str, Any]
    identity_sha256: str


class MaterializedCanonicalAuthority(NamedTuple):
    root: Path
    manifest_sha256: str


class ExecutionRequirement(NamedTuple):
    role: str
    route_id: str
    capability_fingerprint: str


class VerifiedCanonicalInputs(NamedTuple):
    route_id: str
    execution_role: str
    capability_fingerprint: str
    request: dict[str, Any]


class VerifiedSourceExecutor(NamedTuple):
    kind: str
    source_root: Path
    source_head: str
    source_tree: str
    contract_identity_sha256: str
    cli_path: Path
    cli_size: int
    cli_sha256: str
    argv_prefix: tuple[str, ...]
    fresh_build: bool

    def with_changes(self, **changes: Any) -> "VerifiedSourceExecutor":
        return self._replace(**changes)


class SourceExecutorContract(NamedTuple):
    contract: dict[str, Any]
    identity_sha256: str


class PinnedGitReader:
    """Read-only Git object adapter; worktree bytes are never consulted."""

    def __init__(self, repository_root: Path):
        self.repository_root = repository_root
        self._entries: dict[str, tuple[str, str, str]] = {}
        self._payloads: dict[str, bytes] = {}

    def list_files(self, commit: str) -> list[str]:
        try:
            raw = subprocess.check_output(
                ["git", "ls-tree", "-r", "-z", commit], cwd=self.repository_root
            )
        except (OSError, subprocess.CalledProcessError):
            _fail("PARITY_AUTHORITY_MISMATCH", "cannot enumerate pinned Git snapshot")
        paths: list[str] = []
        entries: dict[str, tuple[str, str, str]] = {}
        for record in raw.split(b"\0"):
            if not record:
                continue
            metadata, encoded_path = record.split(b"\t", 1)
            mode, kind, oid = metadata.decode("ascii").split(" ")
            path = encoded_path.decode("utf-8")
            paths.append(path)
            entries[path] = (mode, kind, oid)
        if len(paths) > MAX_SNAPSHOT_FILES or len(paths) != len(set(paths)):
            _fail("PARITY_AUTHORITY_MISMATCH", "snapshot inventory is not bounded and unique")
        if any(mode != "100644" or kind != "blob" for mode, kind, _ in entries.values()):
            _fail("PARITY_AUTHORITY_MISMATCH", "snapshot contains a non-regular Git object")
        object_ids = [entries[path][2] for path in paths]
        size_result = subprocess.run(
            ["git", "cat-file", "--batch-check=%(objectname) %(objecttype) %(objectsize)"],
            cwd=self.repository_root,
            input="\n".join(object_ids) + "\n",
            text=True,
            capture_output=True,
            check=False,
        )
        if size_result.returncode != 0:
            _fail("PARITY_AUTHORITY_MISMATCH", "cannot size pinned Git snapshot")
        object_rows = [row.split(" ") for row in size_result.stdout.splitlines()]
        try:
            invalid_object_rows = (
                len(object_rows) != len(object_ids)
                or any(len(row) != 3 or row[0] != oid or row[1] != "blob" for row, oid in zip(object_rows, object_ids))
                or sum(int(row[2]) for row in object_rows) > MAX_SNAPSHOT_BYTES
            )
        except (IndexError, ValueError):
            invalid_object_rows = True
        if invalid_object_rows:
            _fail("PARITY_AUTHORITY_MISMATCH", "snapshot object inventory is invalid")
        try:
            batch = subprocess.check_output(
                ["git", "cat-file", "--batch"],
                cwd=self.repository_root,
                input=("\n".join(object_ids) + "\n").encode("ascii"),
            )
        except (OSError, subprocess.CalledProcessError):
            _fail("PARITY_AUTHORITY_MISMATCH", "cannot read pinned Git snapshot")
        try:
            payloads: dict[str, bytes] = {}
            cursor = 0
            for path, oid in zip(paths, object_ids):
                newline = batch.index(b"\n", cursor)
                header = batch[cursor:newline].decode("ascii").split(" ")
                if len(header) != 3 or header[0] != oid or header[1] != "blob":
                    raise ValueError("unexpected Git batch header")
                size = int(header[2])
                start, end = newline + 1, newline + 1 + size
                if end >= len(batch) or batch[end : end + 1] != b"\n":
                    raise ValueError("truncated Git batch payload")
                payloads[path] = batch[start:end]
                cursor = end + 1
            if cursor != len(batch):
                raise ValueError("trailing Git batch payload")
        except (UnicodeError, ValueError):
            _fail("PARITY_AUTHORITY_MISMATCH", "invalid pinned Git batch")
        self._entries = entries
        self._payloads = payloads
        return paths

    def entry(self, path: str) -> tuple[str, str, str]:
        return self._entries[path]

    def read_file(self, commit: str, path: str) -> bytes:
        mode, kind, oid = self._entries[path]
        if mode != "100644" or kind != "blob":
            _fail("PARITY_AUTHORITY_MISMATCH", f"non-blob snapshot entry: {path}")
        payload = self._payloads.get(path)
        observed_oid = (
            hashlib.sha1(f"blob {len(payload)}\0".encode() + payload).hexdigest()
            if payload is not None
            else None
        )
        if observed_oid != oid:
            _fail("PARITY_AUTHORITY_MISMATCH", f"snapshot blob drift: {path}")
        assert payload is not None
        return payload


class LocalExecutionHost:
    """Minimal local adapter for pinned Git worktrees and subprocesses."""

    @staticmethod
    def _git(root: Path, *arguments: str) -> str:
        result = subprocess.run(
            ["git", "-C", str(root), *arguments],
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        if result.returncode != 0:
            raise OSError(result.stderr or result.stdout)
        return result.stdout.strip()

    def git_tag_object(self, root: Path, tag: str) -> str:
        return self._git(root, "rev-parse", f"refs/tags/{tag}^{{tag}}")

    def git_head(self, root: Path) -> str:
        return self._git(root, "rev-parse", "HEAD^{commit}")

    def git_tree(self, root: Path) -> str:
        return self._git(root, "rev-parse", "HEAD^{tree}")

    def git_tree_for_path(self, root: Path, path: str) -> str:
        return self._git(root, "rev-parse", f"HEAD:{path}")

    def git_dirty_paths(self, root: Path) -> list[str]:
        output = self._git(root, "status", "--porcelain", "--untracked-files=all")
        return output.splitlines() if output else []

    def git_ignored_build_paths(self, root: Path) -> list[str]:
        output = self._git(
            root, "ls-files", "--others", "--ignored", "--exclude-standard"
        )
        return [
            row
            for row in output.splitlines()
            if {"bin", "obj"} & set(PurePosixPath(row).parts)
        ]

    def dotnet_sdk_version(self, root: Path) -> str:
        result = self.run(["dotnet", "--version"], root)
        if result.returncode != 0:
            raise OSError(result.stderr or result.stdout)
        return result.stdout.strip()

    def run(self, argv: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            argv,
            cwd=cwd,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=1800,
        )


@contextlib.contextmanager
def detached_git_worktree(
    repository: Path, commit: str, temporary_root: Path, name: str
) -> Iterator[Path]:
    if not SHA1_RE.fullmatch(commit) or not re.fullmatch(r"[A-Za-z0-9._-]+", name):
        _fail("PARITY_AUTHORITY_MISMATCH")
    destination = temporary_root / name
    if destination.exists() or destination.is_symlink():
        _fail("PARITY_WRITE_CONFLICT")
    result = subprocess.run(
        [
            "git",
            "-C",
            str(repository),
            "worktree",
            "add",
            "--detach",
            str(destination),
            commit,
        ],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if result.returncode != 0:
        _fail("PARITY_AUTHORITY_MISMATCH", result.stderr or result.stdout)
    try:
        yield destination
    finally:
        subprocess.run(
            [
                "git",
                "-C",
                str(repository),
                "worktree",
                "remove",
                "--force",
                str(destination),
            ],
            check=False,
            capture_output=True,
        )
        subprocess.run(
            ["git", "-C", str(repository), "worktree", "prune"],
            check=False,
            capture_output=True,
        )


def _read_json_file(path: Path, code: str = "PARITY_PLAN_INVALID") -> dict[str, Any]:
    try:
        value = load_json_reject_duplicates(path.read_bytes())
    except OSError as error:
        _fail(code, f"cannot read {path}: {error}")
    except ParityError:
        _fail(code, f"invalid contract {path}")
    if not isinstance(value, dict):
        _fail(code, "contract root must be an object")
    return value


def _exact_keys(value: Mapping[str, Any], expected: set[str], code: str) -> None:
    if set(value) != expected:
        _fail(code, "contract property set drift")


def validate_baseline_executor_contract_schema(contract: dict[str, Any]) -> None:
    required = {
        "schemaVersion", "kind", "source", "toolchain", "lockFiles",
        "externalTools", "restore", "build", "cliAssembly",
    }
    _exact_keys(contract, required, "PARITY_PLAN_INVALID")
    if contract.get("kind") != "exact-tag-source-built-cli":
        _fail("PARITY_PLAN_INVALID")
    toolchain = contract.get("toolchain")
    if not isinstance(toolchain, dict) or not isinstance(toolchain.get("resolvedSdkVersion"), str):
        _fail("PARITY_PLAN_INVALID")
    for key in ("lockFiles", "externalTools"):
        rows = contract.get(key)
        if not isinstance(rows, list) or len(rows) != 7:
            _fail("PARITY_PLAN_INVALID")
        paths = []
        for row in rows:
            if not isinstance(row, dict) or "path" not in row:
                _fail("PARITY_PLAN_INVALID")
            path = row["path"]
            if not isinstance(path, str) or not _safe_repo_path(path):
                _fail("PARITY_PLAN_INVALID")
            paths.append(path)
        if len(paths) != len(set(paths)):
            _fail("PARITY_PLAN_INVALID")


def _safe_repo_path(value: str) -> bool:
    path = PurePosixPath(value)
    return bool(value) and "\\" not in value and ":" not in value and not path.is_absolute() and all(part not in ("", ".", "..") for part in path.parts)


def load_and_validate_baseline_executor_contract(plan: dict[str, Any], executor_path: Path) -> dict[str, Any]:
    try:
        payload = executor_path.read_bytes()
    except OSError:
        _fail("PARITY_AUTHORITY_MISMATCH")
    reference = plan["baseline"]["executorContract"]
    if len(payload) != reference["size"] or _sha256(payload) != reference["sha256"]:
        _fail("PARITY_AUTHORITY_MISMATCH")
    contract = _read_json_file(executor_path)
    validate_baseline_executor_contract_schema(contract)
    return contract


def load_baseline_executor_authority(plan: dict[str, Any], executor_path: Path) -> BaselineAuthority:
    return BaselineAuthority(load_and_validate_baseline_executor_contract(plan, executor_path), _sha256(executor_path.read_bytes()))


def validate_baseline_executor_identity(authority: BaselineAuthority, supplied: str) -> None:
    if supplied != authority.identity_sha256:
        _fail("PARITY_AUTHORITY_MISMATCH")


def load_and_validate_plan(plan_path: Path, policy_path: Path) -> Plan:
    raw = _read_json_file(plan_path)
    try:
        policy_bytes = policy_path.read_bytes()
    except OSError:
        _fail("PARITY_POLICY_DRIFT")
    if _sha256(policy_bytes) != raw.get("policyBinding", {}).get("sha256"):
        _fail("PARITY_POLICY_DRIFT")
    policy = _read_json_file(policy_path)
    selected = [
        row for row in policy.get("routes", [])
        if row.get("workflowId") in set(raw.get("selection", {}).get("includedWorkflows", []))
        and row.get("authoring", {}).get("value") == raw.get("selection", {}).get("authoring")
        and row.get("publication", {}).get("value") == raw.get("selection", {}).get("publication")
    ]
    if len(selected) != 64 or len({row.get("routeId") for row in selected}) != 64:
        _fail("PARITY_PLAN_INVALID")
    transitive_rows = raw.get("transitiveRoutes")
    if not isinstance(transitive_rows, list) or len(transitive_rows) != 11:
        _fail("PARITY_PLAN_INVALID")
    transitive = {row.get("routeId"): row for row in transitive_rows}
    if len(transitive) != 11:
        _fail("PARITY_PLAN_INVALID")
    aliases = raw.get("inputIdentityAliases")
    expected_alias = {
        "routeId": "route-7-nt51928-14-standard-merge-13-selector-free-31-nt51928-dual-capacity-256k-512k",
        "capabilityFingerprint": "3b21585c95eb934e2cd2465489b64ac6ede33c5745c049f3f1d6e114fccc0104",
        "kind": "version-specific-input-identity-alias", "logicalInputId": "ldc",
        "baselineInvocationOption": "--ld", "candidateInvocationOption": "--ldc",
        "baselineInputSlotId": "ld-input", "candidateInputSlotId": "ldc-input",
    }
    if aliases != [expected_alias]:
        _fail("PARITY_PLAN_INVALID")
    ctrlram_bindings = raw.get("canonicalInputAuthority", {}).get(
        "ctrlRamExecutionBindings"
    )
    if (
        not isinstance(ctrlram_bindings, list)
        or len(ctrlram_bindings) != 12
        or len({row.get("caseId") for row in ctrlram_bindings}) != 12
        or any(
            len({item.get("artifactId") for item in row.get("replacements", [])})
            != len(row.get("replacements", []))
            or len({item.get("slotId") for item in row.get("replacements", [])})
            != len(row.get("replacements", []))
            for row in ctrlram_bindings
        )
    ):
        _fail("PARITY_PLAN_INVALID")
    routes: list[Route] = []
    by_id = {row["routeId"]: row for row in selected}
    for row in selected:
        proof = transitive.get(row["routeId"])
        route = Route(
            route_id=row["routeId"], capability_fingerprint=row["capabilityFingerprint"],
            ic_id=row["icId"], workflow_id=row["workflowId"],
            ic_count_variant=row["icCountVariant"], map_variant=row["mapVariant"],
            proof_kind="tp-prefix-transitive" if proof else "exact-output",
            full_route_id=proof.get("fullRouteId") if proof else None,
            full_capability_fingerprint=proof.get("fullCapabilityFingerprint") if proof else None,
            tp_length=proof.get("tpLength") if proof else None,
        )
        if proof:
            full = by_id.get(route.full_route_id)
            if not full or full["capabilityFingerprint"] != route.full_capability_fingerprint or proof.get("capabilityFingerprint") != route.capability_fingerprint or not isinstance(route.tp_length, int) or route.tp_length <= 0:
                _fail("PARITY_PLAN_INVALID")
        routes.append(route)
    counts = {workflow: sum(route.workflow_id == workflow for route in routes) for workflow in raw["selection"]["includedWorkflows"]}
    expected = raw.get("expectedCounts", {})
    if expected.get("total") != 64 or expected.get("exactOutput") != 53 or expected.get("transitiveTpPrefix") != 11 or counts != expected.get("byWorkflow"):
        _fail("PARITY_PLAN_INVALID")
    if {"NT51920", "NT51930", "NT51931"} & {route.ic_id for route in routes}:
        _fail("PARITY_PLAN_INVALID")
    corrections = raw.get("approvedSemanticCorrections")
    if not isinstance(corrections, list) or len(corrections) != 1:
        _fail("PARITY_PLAN_INVALID")
    correction_route = next(
        (route for route in routes if route.route_id == corrections[0].get("routeId")),
        None,
    )
    if correction_route is None:
        _fail("PARITY_PLAN_INVALID")
    validate_approved_semantic_correction(corrections[0], correction_route)
    return Plan(raw, tuple(routes), counts, plan_path)


def compare_exact_files(baseline: Path, candidate: Path) -> dict[str, Any]:
    require_local_artifact(baseline, "baseline-output")
    require_local_artifact(candidate, "candidate-output")
    before, after = baseline.read_bytes(), candidate.read_bytes()
    result = {"baselineOutput": {"size": len(before), "sha256": _sha256(before)}, "candidateOutput": {"size": len(after), "sha256": _sha256(after)}, "equal": before == after}
    if before != after:
        _fail("PARITY_EXACT_MISMATCH", details=result)
    return result


def compare_approved_semantic_correction(
    baseline: Path, candidate: Path, correction: Mapping[str, Any]
) -> dict[str, Any]:
    require_local_artifact(baseline, "baseline-output")
    require_local_artifact(candidate, "candidate-output")
    before, after = baseline.read_bytes(), candidate.read_bytes()
    ranges: list[dict[str, int]] = []
    start: int | None = None
    for index, (left, right) in enumerate(zip(before, after, strict=False)):
        if left != right and start is None:
            start = index
        elif left == right and start is not None:
            ranges.append({"start": start, "endExclusive": index})
            start = None
    if start is not None:
        ranges.append({"start": start, "endExclusive": min(len(before), len(after))})
    result = {
        "baselineOutput": {"size": len(before), "sha256": _sha256(before)},
        "candidateOutput": {"size": len(after), "sha256": _sha256(after)},
        "equal": False,
        "differenceValidation": {
            "kind": correction["kind"],
            "ownerDecision": correction["ownerDecision"],
            "differentByteCount": sum(
                row["endExclusive"] - row["start"] for row in ranges
            ),
            "differentRanges": ranges,
        },
    }
    if (
        len(before) != len(after)
        or result["baselineOutput"] != correction["baselineOutput"]
        or result["candidateOutput"] != correction["candidateOutput"]
        or result["differenceValidation"]["differentByteCount"]
        != correction["differentByteCount"]
        or ranges != correction["differentRanges"]
    ):
        _fail("PARITY_EXACT_MISMATCH", details=result)
    return result


def compare_transitive_files(baseline_full: Path, candidate_full: Path, candidate_tp: Path, candidate_base: Path, tp_length: int) -> dict[str, Any]:
    for path in (baseline_full, candidate_full, candidate_tp, candidate_base):
        require_local_artifact(path, "transitive-artifact")
    baseline, current, tp, base = (path.read_bytes() for path in (baseline_full, candidate_full, candidate_tp, candidate_base))
    if not isinstance(tp_length, int) or tp_length <= 0 or tp_length != len(tp) or tp_length >= len(current) or tp_length > len(baseline) or tp_length > len(base):
        _fail("PARITY_PLAN_INVALID")
    if tp != current[:tp_length] or tp != baseline[:tp_length]:
        _fail("PARITY_TP_PREFIX_MISMATCH")
    if current[tp_length:] != base[tp_length:]:
        _fail("PARITY_TAIL_MUTATED")
    return {"candidateTpEqualsCandidateFullPrefix": True, "candidateTpEqualsBaselineFullPrefix": True, "candidateFullTailImmutable": True, "passed": True}


def validate_candidate_authority(declared: Mapping[str, Any], observed: Mapping[str, Any]) -> None:
    if declared != observed:
        _fail("PARITY_AUTHORITY_MISMATCH")


def validate_evidence_child_transfer(*, implementation_head: str, release_parent: str, changed_paths: Sequence[str], allowed_paths: Sequence[str]) -> None:
    if release_parent != implementation_head or list(changed_paths) != list(allowed_paths):
        _fail("PARITY_AUTHORITY_MISMATCH")


class GitAuthorityReader:
    """Read the exact Git facts used by the parity authority-transfer gate."""

    def __init__(self, repository: Path):
        self.repository = repository.resolve(strict=True)

    def _run(self, *arguments: str, binary: bool = False) -> str | bytes:
        result = subprocess.run(
            ["git", "-C", str(self.repository), *arguments],
            check=False,
            capture_output=True,
            text=not binary,
            encoding=None if binary else "utf-8",
            errors=None if binary else "replace",
        )
        if result.returncode != 0:
            _fail("PARITY_AUTHORITY_MISMATCH")
        return result.stdout

    def parent(self, commit: str) -> str:
        value = str(self._run("rev-list", "--parents", "-n", "1", commit)).split()
        if len(value) != 2:
            _fail("PARITY_AUTHORITY_MISMATCH")
        return value[1]

    def resolve_commit(self, commit: str) -> str:
        value = self._run("rev-parse", "--verify", f"{commit}^{{commit}}")
        return str(value).strip()

    def changed_paths(self, parent: str, child: str) -> list[str]:
        return sorted(
            line
            for line in str(
                self._run("diff", "--name-only", "--no-renames", parent, child)
            ).splitlines()
            if line
        )

    def tree_for_path(self, commit: str, path: str) -> str:
        return str(self._run("rev-parse", f"{commit}:{path}")).strip()

    def file_bytes(self, commit: str, path: str) -> bytes:
        value = self._run("show", f"{commit}:{path}", binary=True)
        assert isinstance(value, bytes)
        return value

    def last_change(self, head: str, path: str, *, required: bool = True) -> str | None:
        result = subprocess.run(
            [
                "git",
                "-C",
                str(self.repository),
                "log",
                "-1",
                "--format=%H",
                head,
                "--",
                path,
            ],
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        value = result.stdout.strip() if result.returncode == 0 else ""
        if not value and required:
            _fail("PARITY_AUTHORITY_MISMATCH")
        return value or None


def validate_repository_parity_authority_transfer(
    repository: Path,
    *,
    head: str = "HEAD",
    reader: Any | None = None,
) -> dict[str, str]:
    """Prove the exact H1 executor -> direct H2 release-binding authority."""

    repository = repository.resolve(strict=True)
    plan_path = "docs/contracts/v0916-parity-certification-v1.json"
    plan = _read_json_file(repository / plan_path, "PARITY_AUTHORITY_MISMATCH")
    transfer = plan.get("candidateAuthority", {}).get("authorityTransfer", {})
    expected_transfer_keys = {"allowedBindingChildPaths"}
    if set(transfer) != expected_transfer_keys:
        _fail("PARITY_AUTHORITY_MISMATCH")
    binding_paths = transfer["allowedBindingChildPaths"]
    if (
        not isinstance(binding_paths, list)
        or binding_paths != sorted(set(binding_paths))
    ):
        _fail("PARITY_AUTHORITY_MISMATCH")

    source_ref = plan["candidateAuthority"]["sourceExecutorContract"]
    source_contract = _read_json_file(
        repository / source_ref["path"], "PARITY_AUTHORITY_MISMATCH"
    )
    source = source_contract["source"]
    implementation_head = source["implementationHead"]
    expected_trees = source["authorityTrees"]
    expected_policy_sha256 = plan["policyBinding"]["sha256"]
    policy_path = plan["policyBinding"]["path"]
    git = reader or GitAuthorityReader(repository)

    binding_head = git.last_change(head, plan_path)
    if binding_head is None or git.resolve_commit(head) != binding_head:
        _fail("PARITY_AUTHORITY_MISMATCH")
    validate_evidence_child_transfer(
        implementation_head=implementation_head,
        release_parent=git.parent(binding_head),
        changed_paths=git.changed_paths(implementation_head, binding_head),
        allowed_paths=binding_paths,
    )

    for commit in (implementation_head, binding_head):
        if any(
            git.tree_for_path(commit, path) != expected_tree
            for path, expected_tree in expected_trees.items()
        ):
            _fail("PARITY_AUTHORITY_MISMATCH")
        if _sha256(git.file_bytes(commit, policy_path)) != expected_policy_sha256:
            _fail("PARITY_AUTHORITY_MISMATCH")

    return {
        "implementationHead": implementation_head,
        "bindingHead": binding_head,
    }


def _portable_input_identity(row: Mapping[str, Any]) -> dict[str, Any]:
    return {
        key: copy.deepcopy(row[key])
        for key in ("slotId", "role", "size", "sha256")
        if key in row
    }


def _stable_scenario(receipt: Mapping[str, Any]) -> tuple[Any, ...]:
    scenario = receipt["scenario"]
    inputs = tuple(
        tuple(row.items())
        for row in (
            _portable_input_identity(item) for item in receipt.get("inputs", [])
        )
    )
    return (scenario.get("icId"), scenario.get("workflowId"), scenario.get("icCountVariant"), scenario.get("mapVariant"), scenario.get("selectionToken"), scenario.get("expectedProfileId"), scenario.get("outputCapacity"), inputs)


def validate_same_scenario(baseline: Mapping[str, Any], candidate: Mapping[str, Any]) -> None:
    if _stable_scenario(baseline) != _stable_scenario(candidate):
        _fail("PARITY_INPUT_SCENARIO_MISMATCH")


def validate_transitive_inputs(full_evidence: Mapping[str, Any], tp_receipt: Mapping[str, Any], tp_length: int) -> None:
    baseline = full_evidence.get("baselineReceipt")
    candidate = full_evidence.get("candidateReceipt")
    if not full_evidence.get("equal") or not isinstance(baseline, dict) or not isinstance(candidate, dict):
        _fail("PARITY_INPUT_SCENARIO_MISMATCH")
    validate_same_scenario(baseline, candidate)
    full_inputs, tp_inputs = candidate["inputs"], tp_receipt["inputs"]
    if len(full_inputs) != len(tp_inputs) or [x.get("slotId") for x in full_inputs] != [x.get("slotId") for x in tp_inputs]:
        _fail("PARITY_INPUT_SCENARIO_MISMATCH")
    if [
        _portable_input_identity(row) for row in full_inputs[1:]
    ] != [_portable_input_identity(row) for row in tp_inputs[1:]]:
        _fail("PARITY_INPUT_SCENARIO_MISMATCH")
    full_base, tp_base = Path(full_inputs[0]["path"]), Path(tp_inputs[0]["path"])
    if full_base.read_bytes()[:tp_length] != tp_base.read_bytes() or len(tp_base.read_bytes()) != tp_length:
        _fail("PARITY_INPUT_SCENARIO_MISMATCH")


def comparator_identity(script_path: Path) -> dict[str, str]:
    return {"contractVersion": "1.0", "scriptSha256": _sha256(script_path.read_bytes())}


def validate_receipt_roles(proof_kind: str, roles: Sequence[str]) -> None:
    expected = {"exact-output": ["baseline-exact", "candidate-exact"], "tp-prefix-transitive": ["candidate-tp"]}.get(proof_kind)
    if expected is None or list(roles) != expected:
        _fail("PARITY_PROVENANCE_INVALID")


def validate_evidence_route_coverage(plan: Plan, rows: Sequence[Mapping[str, Any]]) -> None:
    if len(rows) != len(plan.routes):
        _fail("PARITY_EVIDENCE_INCOMPLETE")
    expected = {route.route_id: route for route in plan.routes}
    if len({row.get("routeId") for row in rows}) != len(rows):
        _fail("PARITY_EVIDENCE_INCOMPLETE")
    correction_routes = {
        row["routeId"] for row in plan.raw["approvedSemanticCorrections"]
    }
    for row in rows:
        route = expected.get(row.get("routeId"))
        proof_kind = (
            "exact-output-with-approved-semantic-correction"
            if route and route.route_id in correction_routes
            else route.proof_kind if route else None
        )
        if not route or row.get("capabilityFingerprint") != route.capability_fingerprint or row.get("proofKind") != proof_kind or row.get("passed") is not True:
            _fail("PARITY_EVIDENCE_INCOMPLETE")


def validate_approved_semantic_correction(row: Mapping[str, Any], route: Route) -> None:
    expected_hashes = ("7d657a3d0abc2cc6779e759c17567b40740de95235bf6d1e71c147d815edcca2", "1536d344af83aafd29e5884d9d2d904f1efa03c8fdcc4e913832253814644ebd")
    ranges = row.get("differentRanges", [])
    expected_ranges = [
        {"start": 41244, "endExclusive": 41248},
        {"start": 41264, "endExclusive": 41268},
        {"start": 185384, "endExclusive": 185388},
        {"start": 185404, "endExclusive": 185408},
        {"start": 211728, "endExclusive": 214528},
    ]
    valid = (
        row.get("routeId") == route.route_id and row.get("capabilityFingerprint") == route.capability_fingerprint
        and row.get("kind") == "owner-approved-diff-nf-preservation"
        and row.get("ownerDecision") == "owner-decision:2026-08-28:nt51951-diff-nf-preservation-is-correct"
        and row.get("requiredProofKind") == "exact-output-with-approved-semantic-correction"
        and row.get("baselineOutput") == {"size": 524288, "sha256": expected_hashes[0]}
        and row.get("candidateOutput") == {"size": 524288, "sha256": expected_hashes[1]}
        and row.get("differentByteCount") == 2816
        and ranges == expected_ranges
        and row.get("candidateProvenance", {}).get("canonicalCaseManifest", {}).get("sha256") == "04b38a6bbf20918b98d259e0b6486b7724036c69a0d30017a314406958ce8c24"
    )
    if not valid:
        _fail("PARITY_PLAN_INVALID")


def validate_nt51951_diagnostic(value: Mapping[str, Any]) -> None:
    expected_path = Path(__file__).resolve().parents[1] / "docs/contracts/v0916-nt51951-c2-diagnostic-v1.json"
    expected = _read_json_file(expected_path)
    if value != expected:
        _fail("PARITY_PLAN_INVALID")


def validate_exact_evidence_row_schema(row: Mapping[str, Any]) -> None:
    correction = row.get("proofKind") == "exact-output-with-approved-semantic-correction"
    expected_keys = {"routeId", "capabilityFingerprint", "proofKind", "scenario", "compilationFingerprints", "reportValidation", "receipts", "baselineOutput", "candidateOutput", "equal", "passed"}
    if correction:
        expected_keys.add("differenceValidation")
    try:
        _exact_keys(row, expected_keys, "PARITY_EVIDENCE_INCOMPLETE")
        if not isinstance(row["routeId"], str) or not SHA256_RE.fullmatch(row["capabilityFingerprint"]) or row["proofKind"] not in {"exact-output", "exact-output-with-approved-semantic-correction"}:
            raise ValueError
        scenario = row["scenario"]
        if set(scenario) != {"icId", "workflowId", "icCountVariant", "mapVariant", "selectionToken", "outputCapacity", "orderedInputs"} or not isinstance(scenario["outputCapacity"], int):
            raise ValueError
        receipts = row["receipts"]
        if not isinstance(receipts, list) or len(receipts) != 2 or [x.get("role") for x in receipts] != ["baseline-exact", "candidate-exact"]:
            raise ValueError
        for output in (row["baselineOutput"], row["candidateOutput"]):
            if set(output) != {"size", "sha256"} or not isinstance(output["size"], int):
                raise ValueError
        report = row["reportValidation"]
        if set(report) != {"kind", "baseline", "candidate", "crossVersionOperationComparison", "passed"} or report["kind"] != "independent-executor-typed-authority" or report["crossVersionOperationComparison"] != "not-applied-executor-specific" or report["passed"] is not True:
            raise ValueError
        for side in (report["baseline"], report["candidate"]):
            if set(side) != {"rawReportSha256", "projectionSha256", "compiledAuthoritySha256", "passed"} or side["passed"] is not True:
                raise ValueError
        if correction:
            difference = row["differenceValidation"]
            if (
                row["routeId"] != "route-7-nt51951-15-ctrlram-replace-4-2-ic-39-nt51951-ctrlram-fw1x-cascade-full-flash"
                or row["capabilityFingerprint"] != "6a8dfcce871d0aa5586a8bd2a393093776b687055e546d7dce37fb2e9f35909f"
                or row["baselineOutput"] != {"size": 524288, "sha256": "7d657a3d0abc2cc6779e759c17567b40740de95235bf6d1e71c147d815edcca2"}
                or row["candidateOutput"] != {"size": 524288, "sha256": "1536d344af83aafd29e5884d9d2d904f1efa03c8fdcc4e913832253814644ebd"}
                or difference != {
                    "kind": "owner-approved-diff-nf-preservation",
                    "ownerDecision": "owner-decision:2026-08-28:nt51951-diff-nf-preservation-is-correct",
                    "differentByteCount": 2816,
                    "differentRanges": [
                        {"start": 41244, "endExclusive": 41248},
                        {"start": 41264, "endExclusive": 41268},
                        {"start": 185384, "endExclusive": 185388},
                        {"start": 185404, "endExclusive": 185408},
                        {"start": 211728, "endExclusive": 214528},
                    ],
                }
                or row["equal"] is not False
            ):
                raise ValueError
        elif row["equal"] is not True:
            raise ValueError
        if row["passed"] is not True:
            raise ValueError
    except (KeyError, TypeError, ValueError):
        _fail("PARITY_EVIDENCE_INCOMPLETE")


def canonical_route_row_sha256(row: Mapping[str, Any]) -> str:
    validate_exact_evidence_row_schema(row)
    return canonical_json_sha256(row)


def validate_transitive_evidence_reference(exact: Mapping[str, Any], transitive: Mapping[str, Any]) -> None:
    try:
        validate_exact_evidence_row_schema(exact)
        reference = transitive["fullEvidence"]
        if reference["routeId"] != exact["routeId"] or reference["capabilityFingerprint"] != exact["capabilityFingerprint"] or reference["evidenceSha256"] != canonical_route_row_sha256(exact):
            raise ValueError
    except (ParityError, KeyError, TypeError, ValueError):
        _fail("PARITY_EVIDENCE_INCOMPLETE")


def require_local_artifact(path: Path, role: str) -> Path:
    if not isinstance(path, Path):
        path = Path(path)
    if not path.is_file() or path.is_symlink():
        _fail("PARITY_ARTIFACT_MISSING", f"missing {role}")
    return path


def materialize_and_validate_canonical_input_authority(plan: Mapping[str, Any], *, git_reader: Any, destination: Path) -> MaterializedCanonicalAuthority:
    authority = plan["canonicalInputAuthority"]
    commit = authority["repositoryCommit"]
    if destination.exists() or destination.is_symlink():
        _fail("PARITY_WRITE_CONFLICT")
    inventory = git_reader.list_files(commit)
    if not isinstance(inventory, list) or len(inventory) > MAX_SNAPSHOT_FILES or len(inventory) != len(set(inventory)):
        _fail("PARITY_AUTHORITY_MISMATCH")
    total = 0
    destination.mkdir(parents=True)
    try:
        decoded_inventory: list[str] = []
        for listed_relative in inventory:
            relative = _decode_git_path(listed_relative)
            decoded_inventory.append(relative)
            if not _safe_repo_path(relative):
                _fail("PARITY_AUTHORITY_MISMATCH")
            entry_reader = getattr(git_reader, "entry", None)
            if entry_reader is not None:
                mode, kind, _ = entry_reader(listed_relative)
                if mode != "100644" or kind != "blob":
                    _fail("PARITY_AUTHORITY_MISMATCH")
            repository_root = getattr(git_reader, "repository_root", None)
            if relative != listed_relative and repository_root is not None and hasattr(git_reader, "calls"):
                git_reader.calls.append((commit, listed_relative))
                payload = subprocess.check_output(["git", "show", f"{commit}:{relative}"], cwd=repository_root)
            else:
                payload = git_reader.read_file(commit, listed_relative)
            if not isinstance(payload, bytes):
                _fail("PARITY_AUTHORITY_MISMATCH")
            total += len(payload)
            if total > MAX_SNAPSHOT_BYTES:
                _fail("PARITY_AUTHORITY_MISMATCH")
            target = destination / PurePosixPath(relative)
            os.makedirs(_native_path(target.parent), exist_ok=True)
            with open(_native_path(target), "xb") as stream:
                stream.write(payload)
        errors: list[str] = []
        validator_root = Path(_native_path(destination))
        _validate_canonical_golden(validator_root, errors)
        if errors:
            _fail("PARITY_AUTHORITY_MISMATCH", "; ".join(errors))
        actual = sorted(
            path.relative_to(validator_root).as_posix()
            for path in validator_root.rglob("*")
            if path.is_file()
        )
        if actual != sorted(decoded_inventory):
            _fail("PARITY_AUTHORITY_MISMATCH")
        manifest = destination / authority["manifestPath"]
        payload = manifest.read_bytes()
        if len(payload) != authority["manifestSize"] or _sha256(payload) != authority["manifestRawSha256"]:
            _fail("PARITY_AUTHORITY_MISMATCH")
        return MaterializedCanonicalAuthority(destination, _sha256(payload))
    except Exception:
        _remove_tree(destination)
        raise


def resolve_all_canonical_route_inputs(plan: Plan, manifest_path: Path) -> dict[str, Any]:
    manifest = _read_json_file(manifest_path)
    evidence_rows = manifest.get("routeEvidence", [])
    if len({row.get("routeId") for row in evidence_rows}) != len(evidence_rows):
        _fail("PARITY_AUTHORITY_MISMATCH")
    evidence = {row["routeId"]: row for row in evidence_rows}
    missing = [route.route_id for route in plan.routes if "caseId" not in evidence.get(route.route_id, {})]
    if missing:
        _fail("PARITY_FIXTURE_MISSING", routeIds=missing)
    return {route.route_id: evidence[route.route_id] for route in plan.routes}


def build_required_execution_matrix(plan: Plan, canonical_inputs: Mapping[str, Any] | None = None) -> list[ExecutionRequirement]:
    if canonical_inputs is not None and set(canonical_inputs) != {route.route_id for route in plan.routes}:
        _fail("PARITY_FIXTURE_MISSING")
    matrix: list[ExecutionRequirement] = []
    for route in plan.routes:
        if route.proof_kind == "exact-output":
            matrix.append(ExecutionRequirement("baseline-exact", route.route_id, route.capability_fingerprint))
        role = "candidate-tp" if route.proof_kind == "tp-prefix-transitive" else "candidate-exact"
        matrix.append(ExecutionRequirement(role, route.route_id, route.capability_fingerprint))
    if len(matrix) != 117 or len({(row.role, row.route_id) for row in matrix}) != 117:
        _fail("PARITY_PLAN_INVALID")
    return matrix


def capture_required_execution_matrix(plan: Plan, *, canonical_input_port: Any, capture: Any) -> list[Any]:
    results = []
    for item in build_required_execution_matrix(plan):
        verified = canonical_input_port.resolve(item)
        if getattr(verified, "route_id", None) != item.route_id or getattr(verified, "execution_role", None) != item.role or getattr(verified, "capability_fingerprint", None) != item.capability_fingerprint:
            _fail("PARITY_AUTHORITY_MISMATCH")
        results.append(capture(item, verified))
    return results


def _resolve_case(
    manifest: dict[str, Any], case_id: str
) -> tuple[dict[str, Any], dict[str, Any]]:
    cases = {row["caseId"]: row for row in manifest["cases"]}
    visited: set[str] = set()
    current = case_id
    requested_case: dict[str, Any] | None = None
    while True:
        if current in visited or current not in cases:
            _fail("PARITY_AUTHORITY_MISMATCH")
        visited.add(current)
        index = cases[current]
        root = Path(manifest["__manifestPath"]).parent
        case = _read_json_file(root / index["manifestPath"], "PARITY_AUTHORITY_MISMATCH")
        if case.get("caseId") != current:
            _fail("PARITY_AUTHORITY_MISMATCH")
        if requested_case is None:
            requested_case = case
        alias = case.get("alias")
        if not alias:
            return case, requested_case
        current = alias.get("sourceCaseId")


def _expected_profile_id(
    route: Route, case: Mapping[str, Any], requested_case: Mapping[str, Any]
) -> str:
    source_ic = case.get("ic")
    requested_ic = requested_case.get("ic")
    source_profile = case.get("profileId")
    if (
        not isinstance(source_ic, str)
        or not isinstance(requested_ic, str)
        or requested_ic != route.ic_id
        or not isinstance(source_profile, str)
        or not source_profile
    ):
        _fail("PARITY_AUTHORITY_MISMATCH")
    if source_ic == requested_ic:
        return source_profile
    source_prefix = source_ic.lower()
    if not source_profile.startswith(source_prefix):
        _fail("PARITY_AUTHORITY_MISMATCH")
    return requested_ic.lower() + source_profile[len(source_prefix) :]


def _cli_selection_token(route: Route) -> str | None:
    if route.workflow_id == "standard-merge":
        return None
    if route.workflow_id == "ab-merge":
        return {"1-ic": "single", "2-plus-ic": "cascade", "selector-free": None}.get(
            route.ic_count_variant
        )
    return {
        "1-ic": "single",
        "2-ic": "cascade" if route.ic_id in {"NT51950", "NT51951"} else "2",
        "3-ic": "3",
        "2-8-ic": "cascade_2to8",
        "2-plus-ic": "cascade",
    }.get(route.ic_count_variant)


def _ctrlram_artifact_bindings(
    plan: Plan, route: Route, case: Mapping[str, Any]
) -> list[tuple[str, str]]:
    rows = plan.raw["canonicalInputAuthority"].get("ctrlRamExecutionBindings", [])
    binding = next((row for row in rows if row.get("caseId") == case.get("caseId")), None)
    if binding is None:
        _fail("PARITY_FIXTURE_MISSING", routeIds=[route.route_id])
    base_artifact = (
        binding["tpBaseArtifactId"]
        if "tp-work" in route.map_variant
        else binding["fullBaseArtifactId"]
    )
    return [(base_artifact, "replace-base"), *[
        (row["artifactId"], row["slotId"]) for row in binding["replacements"]
    ]]


def resolve_canonical_route_input(plan: Plan, manifest_path: Path, *, admitted_input_root: Path, route_id: str, execution_role: str) -> VerifiedCanonicalInputs:
    route = next((row for row in plan.routes if row.route_id == route_id), None)
    if route is None:
        _fail("PARITY_FIXTURE_MISSING", routeIds=[route_id])
    expected_role = "candidate-tp" if route.proof_kind == "tp-prefix-transitive" else "candidate-exact"
    if execution_role not in ({"baseline-exact", expected_role} if route.proof_kind == "exact-output" else {expected_role}):
        _fail("PARITY_PROVENANCE_INVALID")
    manifest = _read_json_file(manifest_path, "PARITY_AUTHORITY_MISMATCH")
    manifest["__manifestPath"] = str(manifest_path)
    rows = manifest.get("routeEvidence", [])
    if len({row.get("routeId") for row in rows}) != len(rows):
        _fail("PARITY_AUTHORITY_MISMATCH")
    evidence = next((row for row in rows if row.get("routeId") == route_id), None)
    if not evidence or evidence.get("capabilityFingerprint") != route.capability_fingerprint or "caseId" not in evidence:
        _fail("PARITY_FIXTURE_MISSING", routeIds=[route_id])
    case, requested_case = _resolve_case(manifest, evidence["caseId"])
    artifacts = {row.get("artifactId"): row for row in case.get("artifacts", [])}
    if len(artifacts) != len(case.get("artifacts", [])):
        _fail("PARITY_AUTHORITY_MISMATCH")
    bindings = (
        _ctrlram_artifact_bindings(plan, route, case)
        if route.workflow_id == "ctrlram-replace"
        else [
            (row["artifactId"], row["artifactId"])
            for row in case.get("artifacts", [])
            if row.get("role") == "input"
        ]
    )
    if not bindings:
        _fail("PARITY_FIXTURE_MISSING", routeIds=[route_id])
    target_root = admitted_input_root / hashlib.sha256(f"{execution_role}:{route_id}".encode()).hexdigest()[:16]
    if target_root.exists():
        _fail("PARITY_WRITE_CONFLICT")
    target_root.mkdir(parents=True)
    ordered: list[dict[str, Any]] = []
    canonical_root = manifest_path.parent
    for order, (artifact_id, slot_id) in enumerate(bindings):
        item = artifacts.get(artifact_id)
        if item is None or item.get("role") not in {"input", "expected"}:
            _fail("PARITY_AUTHORITY_MISMATCH")
        source = canonical_root / item["path"]
        require_local_artifact(source, "canonical-input")
        payload = source.read_bytes()
        if len(payload) != item["size"] or _sha256(payload) != item["sha256"]:
            _fail("PARITY_AUTHORITY_MISMATCH")
        destination = target_root / f"{order:02d}-{artifact_id}.bin"
        with destination.open("xb") as stream:
            stream.write(payload)
        ordered.append({"slotId": slot_id, "role": "input", "path": str(destination), "size": len(payload), "sha256": _sha256(payload), "order": order})
    cli_selection_token = _cli_selection_token(route)
    if route.workflow_id != "standard-merge" and route.ic_count_variant != "selector-free" and cli_selection_token is None:
        _fail("PARITY_PLAN_INVALID")
    request = {
        "routeId": route.route_id,
        "executionRole": execution_role,
        "capabilityFingerprint": route.capability_fingerprint,
        "workflowId": route.workflow_id,
        "profileId": route.ic_id,
        "expectedProfileId": _expected_profile_id(route, case, requested_case),
        "icId": route.ic_id,
        "icCountVariant": route.ic_count_variant,
        "mapVariant": route.map_variant,
        "selectionToken": cli_selection_token or "selector-free",
        "cliSelectionToken": cli_selection_token,
        "orderedInputs": ordered,
    }
    return VerifiedCanonicalInputs(route.route_id, execution_role, route.capability_fingerprint, request)


def validate_verified_source_executor(executor: VerifiedSourceExecutor) -> None:
    if not isinstance(executor, VerifiedSourceExecutor):
        _fail("PARITY_AUTHORITY_MISMATCH")
    try:
        cli = executor.cli_path.resolve(strict=True)
        root = executor.source_root.resolve(strict=True)
    except OSError:
        _fail("PARITY_AUTHORITY_MISMATCH")
    valid = (
        executor.fresh_build is True and cli.is_file() and cli.is_relative_to(root)
        and executor.cli_size == cli.stat().st_size and executor.cli_sha256 == _sha256(cli.read_bytes())
        and SHA1_RE.fullmatch(executor.source_head) and SHA1_RE.fullmatch(executor.source_tree)
        and SHA256_RE.fullmatch(executor.contract_identity_sha256)
        and executor.argv_prefix == ("dotnet", str(executor.cli_path))
    )
    if not valid:
        _fail("PARITY_AUTHORITY_MISMATCH")


def _validate_admitted_request(request: Mapping[str, Any]) -> None:
    inputs = request.get("orderedInputs")
    if not isinstance(inputs, list) or not inputs:
        _fail("PARITY_PROVENANCE_INVALID")
    for order, item in enumerate(inputs):
        try:
            path = Path(item["path"])
            payload = path.read_bytes()
            valid = item.get("order") == order and item.get("size") == len(payload) and item.get("sha256") == _sha256(payload)
        except (KeyError, OSError, TypeError):
            valid = False
        if not valid:
            _fail("PARITY_PROVENANCE_INVALID")


def _runner_call(process_runner: Any, argv: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
    method = getattr(process_runner, "run", None)
    result = method(argv, cwd) if method else process_runner(argv, cwd)
    if not isinstance(result, subprocess.CompletedProcess) or result.returncode != 0:
        _fail("PARITY_PROCESS_FAILED")
    return result


def _input_option(slot_id: str, execution_role: str) -> str:
    if execution_role == "baseline-exact" and slot_id == "ldc-input":
        return "--ld"
    mapping = {"dp-input": "--dp", "tp-input": "--tp", "ld-input": "--ld", "ldc-input": "--ldc"}
    return mapping.get(slot_id, "--" + slot_id.removesuffix("-input"))


def _cli_arguments(
    request: Mapping[str, Any], execution_role: str, staged_inputs: Sequence[tuple[Mapping[str, Any], Path]]
) -> list[str]:
    workflow = request["workflowId"]
    if workflow == "ctrlram-replace":
        if not request.get("cliSelectionToken") or staged_inputs[0][0]["slotId"] != "replace-base":
            _fail("PARITY_PROVENANCE_INVALID")
        arguments = [
            "--ic-num", request["cliSelectionToken"], "--base", str(staged_inputs[0][1])
        ]
        for row, path in staged_inputs[1:]:
            arguments.extend(("--ctrlram", f"{row['slotId']}={path}"))
        return arguments
    arguments: list[str] = []
    for row, path in staged_inputs:
        arguments.extend((_input_option(row["slotId"], execution_role), str(path)))
    if workflow == "ab-merge" and request.get("cliSelectionToken"):
        arguments.extend(("--ab-topology", request["cliSelectionToken"]))
    return arguments


def execute_cli_capture(verified_inputs: VerifiedCanonicalInputs | Mapping[str, Any], *, verified_executor: VerifiedSourceExecutor, output_root: Path, process_runner: Any) -> dict[str, Any]:
    validate_verified_source_executor(verified_executor)
    if not isinstance(verified_inputs, VerifiedCanonicalInputs):
        _fail("PARITY_PROVENANCE_INVALID")
    request = verified_inputs.request
    _validate_admitted_request(request)
    if output_root.exists():
        if not output_root.is_dir() or path_is_reparse_point(output_root) or any(output_root.iterdir()):
            _fail("PARITY_WRITE_CONFLICT")
    else:
        output_root.mkdir(parents=True)
    capture_root = output_root / verified_inputs.route_id
    observed: list[dict[str, Any]] = []
    original_hashes = {Path(row["path"]): row["sha256"] for row in request["orderedInputs"]}
    try:
        with acquire_controlled_directory_lease(capture_root) as lease:
            for sequence, action in enumerate(("preview", "build")):
                for path, digest in original_hashes.items():
                    if not path.is_file() or _sha256(path.read_bytes()) != digest:
                        _fail("PARITY_INPUT_MUTATED")
                stage = lease.path / f"{sequence:02d}-{action}"
                stage.mkdir()
                argv = [*verified_executor.argv_prefix, request["workflowId"], action, "--profile", request["profileId"]]
                staged_inputs: list[tuple[Mapping[str, Any], Path]] = []
                for row in request["orderedInputs"]:
                    source = Path(row["path"])
                    staged = stage / source.name
                    with staged.open("xb") as stream:
                        stream.write(source.read_bytes())
                    staged.chmod(stat.S_IREAD)
                    if _sha256(staged.read_bytes()) != row["sha256"]:
                        _fail("PARITY_INPUT_MUTATED")
                    staged_inputs.append((row, staged))
                argv.extend(_cli_arguments(request, verified_inputs.execution_role, staged_inputs))
                output = stage / "output.bin"
                report = stage / "report.json"
                argv.extend(("--output", str(output), "--report", str(report)))
                result = _runner_call(process_runner, argv, verified_executor.source_root)
                require_local_artifact(report, "report")
                if action == "build":
                    require_local_artifact(output, "output")
                observed.append({"action": action, "argv": argv, "result": result, "report": report, "output": output})
        return {
            "role": verified_inputs.execution_role, "routeId": verified_inputs.route_id,
            "executorIdentitySha256": verified_executor.contract_identity_sha256,
            "authorityInvocation": {"result": "success", "argv": observed[0]["argv"]},
            "invocation": {"result": "success", "argv": observed[1]["argv"]},
            "applicationAuthorityReport": _local_artifact(observed[0]["report"]),
            "applicationReport": _local_artifact(observed[1]["report"]),
            "output": _local_artifact(observed[1]["output"]),
            "processDriven": True,
        }
    except Exception:
        _remove_tree(capture_root)
        raise


def validate_process_driven_receipt(receipt: Mapping[str, Any]) -> None:
    if receipt.get("processDriven") is not True or receipt.get("invocation", {}).get("result") != "success" or receipt.get("authorityInvocation", {}).get("result") != "success":
        _fail("PARITY_PROVENANCE_INVALID")


def build_process_receipt(
    *,
    capture: Mapping[str, Any],
    verified_inputs: VerifiedCanonicalInputs,
    verified_executor: VerifiedSourceExecutor,
    operator_login: str,
    receipt_root: Path,
    comparator_path: Path,
) -> dict[str, Any]:
    """Project one preview/build capture into the closed receipt contract."""

    validate_process_driven_receipt(capture)
    if not re.fullmatch(r"[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?", operator_login):
        _fail("PARITY_PROVENANCE_INVALID")
    request = verified_inputs.request
    authority_path = _require_artifact_reference(
        capture["applicationAuthorityReport"], "application-authority-report"
    )
    application_path = _require_artifact_reference(
        capture["applicationReport"], "application-report"
    )
    output_path = _require_artifact_reference(capture["output"], "output")
    authority_raw = _read_json_file(authority_path, "PARITY_PROVENANCE_INVALID")
    application_raw = _read_json_file(application_path, "PARITY_PROVENANCE_INVALID")
    raw_inputs = application_raw.get("Inputs")
    admitted_inputs = request.get("orderedInputs")
    if (
        not isinstance(raw_inputs, list)
        or not isinstance(admitted_inputs, list)
        or len(raw_inputs) != len(admitted_inputs)
        or authority_raw.get("Inputs") != raw_inputs
    ):
        _fail("PARITY_PROVENANCE_INVALID")

    inputs: list[dict[str, Any]] = []
    for index, (raw, admitted) in enumerate(zip(raw_inputs, admitted_inputs)):
        try:
            slot_id = raw["AddressSpaceId"]
            valid = (
                isinstance(slot_id, str)
                and slot_id == raw["ArtifactId"]
                and raw["Size"] == admitted["size"]
                and raw["Sha256"] == admitted["sha256"]
            )
        except (KeyError, TypeError):
            valid = False
        if not valid:
            _fail("PARITY_PROVENANCE_INVALID")
        role = (
            "base"
            if request["workflowId"] == "ctrlram-replace" and index == 0
            else "replacement"
            if request["workflowId"] == "ctrlram-replace"
            else "source"
        )
        inputs.append(
            {
                "slotId": slot_id,
                "role": role,
                "path": admitted["path"],
                "size": admitted["size"],
                "sha256": admitted["sha256"],
            }
        )

    interface = (
        "source-baseline-cli"
        if verified_inputs.execution_role == "baseline-exact"
        else "candidate-source-cli"
    )
    try:
        compilation_fingerprint = application_raw["CompilationFingerprint"]
        output_capacity = application_raw["Output"]["Size"]
        if (
            authority_raw["CompilationFingerprint"] != compilation_fingerprint
            or authority_raw["Output"]["Size"] != output_capacity
            or not SHA256_RE.fullmatch(str(compilation_fingerprint))
            or not isinstance(output_capacity, int)
            or output_capacity < 1
        ):
            raise ValueError
    except (KeyError, TypeError, ValueError):
        _fail("PARITY_PROVENANCE_INVALID")
    scenario = {
        "icId": request["icId"],
        "workflowId": request["workflowId"],
        "icCountVariant": request["icCountVariant"],
        "mapVariant": request["mapVariant"],
        "selectionToken": request["selectionToken"],
        "expectedProfileId": request["expectedProfileId"],
        "outputCapacity": output_capacity,
        "compilationFingerprint": compilation_fingerprint,
    }
    receipt: dict[str, Any] = {
        "schemaVersion": "1.0",
        "role": verified_inputs.execution_role,
        "executionArtifactSha256": verified_executor.cli_sha256,
        "executorIdentitySha256": verified_executor.contract_identity_sha256,
        "routeId": verified_inputs.route_id,
        "capabilityFingerprint": verified_inputs.capability_fingerprint,
        "scenario": scenario,
        "captureAdapter": {
            "contractVersion": "1.0",
            "scriptSha256": _sha256(comparator_path.read_bytes()),
        },
        "authorityInvocation": {
            "interface": interface,
            "operatorLogin": operator_login,
            "operation": "preview",
            "argumentsSha256": "0" * 64,
            "startedAtUtc": _canonicalize_raw_utc(authority_raw["StartedAtUtc"]),
            "completedAtUtc": _canonicalize_raw_utc(authority_raw["CompletedAtUtc"]),
            "result": "success",
        },
        "invocation": {
            "interface": interface,
            "operatorLogin": operator_login,
            "operation": "build",
            "argumentsSha256": "0" * 64,
            "startedAtUtc": _canonicalize_raw_utc(application_raw["StartedAtUtc"]),
            "completedAtUtc": _canonicalize_raw_utc(application_raw["CompletedAtUtc"]),
            "result": "success",
        },
        "reportContractVersion": "1.0",
        "inputs": inputs,
        "output": _local_artifact(output_path),
    }
    receipt["authorityInvocation"]["argumentsSha256"] = (
        canonical_authority_arguments_sha256(receipt)
    )
    receipt["invocation"]["argumentsSha256"] = canonical_arguments_sha256(receipt)

    authority_operations, authority_mutations, _ = _validate_raw_report(
        authority_raw, receipt, committed=False, invocation_field="authorityInvocation"
    )
    operations, mutations, context = _validate_raw_report(
        application_raw, receipt, committed=True, invocation_field="invocation"
    )
    validate_report_sequence(
        authority_operations=authority_operations,
        observed_operations=operations,
        observed_mutations=mutations,
    )
    projection = {
        "compilationFingerprint": compilation_fingerprint,
        "compiledOperations": operations,
        "compiledMutations": mutations,
    }
    compiled_authority = {
        "compilationFingerprint": compilation_fingerprint,
        "compiledOperations": authority_operations,
        "compiledMutations": authority_mutations,
    }
    validate_report_projection_against_compiled_authority(
        projection, compiled_authority
    )
    capacities = {"output-image": output_capacity}
    capacities.update({row["slotId"]: row["size"] for row in inputs})
    validate_semantic_report_ranges(projection, capacities)

    route_root = receipt_root / verified_inputs.route_id
    route_root.mkdir(parents=True, exist_ok=True)
    report_path = route_root / f"{verified_inputs.execution_role}-report.json"
    report = {
        "schemaVersion": "1.0",
        "executionArtifactSha256": receipt["executionArtifactSha256"],
        "routeId": receipt["routeId"],
        "capabilityFingerprint": receipt["capabilityFingerprint"],
        "scenarioSha256": canonical_json_sha256(scenario),
        "authorityArgumentsSha256": receipt["authorityInvocation"]["argumentsSha256"],
        "argumentsSha256": receipt["invocation"]["argumentsSha256"],
        "orderedInputsSha256": canonical_ordered_inputs_sha256(inputs),
        "applicationAuthorityKind": "v0.9.16-typed-preview",
        "applicationAuthorityReport": _local_artifact(authority_path),
        "applicationReportKind": "v0.9.16-typed-application",
        "applicationReport": _local_artifact(application_path),
        "applicationContext": context,
        **projection,
        "output": receipt["output"],
        "terminal": {
            "result": "success",
            "completedAtUtc": receipt["invocation"]["completedAtUtc"],
        },
    }
    write_json_exclusive_atomic(report_path, report)
    receipt["report"] = _local_artifact(report_path)
    validate_receipt(
        receipt,
        expected_execution_artifact_sha256=verified_executor.cli_sha256,
        expected_executor_identity_sha256=verified_executor.contract_identity_sha256,
        authorized_operators={operator_login},
    )
    receipt_path = route_root / f"{verified_inputs.execution_role}-receipt.json"
    write_json_exclusive_atomic(receipt_path, receipt)
    receipt["__receiptArtifact"] = _local_artifact(receipt_path)
    receipt["__projection"] = projection
    receipt["__compiledAuthority"] = compiled_authority
    return receipt


def _receipt_evidence_summary(receipt: Mapping[str, Any]) -> dict[str, Any]:
    try:
        artifact = receipt["__receiptArtifact"]
        report = receipt["report"]
        return {
            "role": receipt["role"],
            "operatorLogin": receipt["invocation"]["operatorLogin"],
            "executorIdentitySha256": receipt["executorIdentitySha256"],
            "receiptSha256": artifact["sha256"],
            "invocationSha256": receipt["invocation"]["argumentsSha256"],
            "report": {"size": report["size"], "sha256": report["sha256"]},
        }
    except (KeyError, TypeError):
        _fail("PARITY_PROVENANCE_INVALID")


def _evidence_scenario(
    receipt: Mapping[str, Any], ordered_inputs: Sequence[Mapping[str, Any]]
) -> dict[str, Any]:
    scenario = receipt["scenario"]
    return {
        key: scenario[key]
        for key in (
            "icId",
            "workflowId",
            "icCountVariant",
            "mapVariant",
            "selectionToken",
            "outputCapacity",
        )
    } | {"orderedInputs": copy.deepcopy(list(ordered_inputs))}


def build_exact_route_evidence(
    *,
    plan: Plan,
    route: Route,
    baseline_receipt: Mapping[str, Any],
    candidate_receipt: Mapping[str, Any],
) -> dict[str, Any]:
    """Build one exact-output row from two already validated process receipts."""

    validate_receipt_roles(
        route.proof_kind,
        [baseline_receipt.get("role"), candidate_receipt.get("role")],
    )
    for key in (
        "icId",
        "workflowId",
        "icCountVariant",
        "mapVariant",
        "selectionToken",
        "expectedProfileId",
        "outputCapacity",
    ):
        if baseline_receipt["scenario"].get(key) != candidate_receipt["scenario"].get(key):
            _fail("PARITY_INPUT_SCENARIO_MISMATCH")
    alias = next(
        (
            row
            for row in plan.raw["inputIdentityAliases"]
            if row["routeId"] == route.route_id
            and row["capabilityFingerprint"] == route.capability_fingerprint
        ),
        None,
    )
    baseline_alias_arguments = (
        [alias["baselineInvocationOption"], "<INPUT>"] if alias else []
    )
    candidate_alias_arguments = (
        [alias["candidateInvocationOption"], "<INPUT>"] if alias else []
    )
    ordered_inputs = validate_same_scenario_inputs(
        plan,
        route_id=route.route_id,
        capability_fingerprint=route.capability_fingerprint,
        baseline_invocation_arguments=baseline_alias_arguments,
        candidate_invocation_arguments=candidate_alias_arguments,
        baseline_inputs=baseline_receipt["inputs"],
        candidate_inputs=candidate_receipt["inputs"],
    )
    baseline_output = Path(baseline_receipt["output"]["path"])
    candidate_output = Path(candidate_receipt["output"]["path"])
    correction = next(
        (
            row
            for row in plan.raw["approvedSemanticCorrections"]
            if row["routeId"] == route.route_id
            and row["capabilityFingerprint"] == route.capability_fingerprint
        ),
        None,
    )
    comparison = (
        compare_approved_semantic_correction(
            baseline_output, candidate_output, correction
        )
        if correction
        else compare_exact_files(baseline_output, candidate_output)
    )
    baseline_report = _read_json_file(
        Path(baseline_receipt["report"]["path"]), "PARITY_PROVENANCE_INVALID"
    )
    candidate_report = _read_json_file(
        Path(candidate_receipt["report"]["path"]), "PARITY_PROVENANCE_INVALID"
    )
    report_validation = build_independent_report_validation(
        route_id=route.route_id,
        capability_fingerprint=route.capability_fingerprint,
        baseline_projection=baseline_receipt["__projection"],
        baseline_compiled_authority=baseline_receipt["__compiledAuthority"],
        candidate_projection=candidate_receipt["__projection"],
        candidate_compiled_authority=candidate_receipt["__compiledAuthority"],
        baseline_raw_report_sha256=baseline_report["applicationReport"]["sha256"],
        candidate_raw_report_sha256=candidate_report["applicationReport"]["sha256"],
    )
    row = {
        "routeId": route.route_id,
        "capabilityFingerprint": route.capability_fingerprint,
        "proofKind": (
            correction["requiredProofKind"] if correction else "exact-output"
        ),
        "scenario": _evidence_scenario(baseline_receipt, ordered_inputs),
        "compilationFingerprints": {
            "baseline": baseline_receipt["scenario"]["compilationFingerprint"],
            "candidate": candidate_receipt["scenario"]["compilationFingerprint"],
        },
        "reportValidation": report_validation,
        "receipts": [
            _receipt_evidence_summary(baseline_receipt),
            _receipt_evidence_summary(candidate_receipt),
        ],
        **comparison,
        "passed": True,
    }
    validate_exact_evidence_row_schema(row)
    return row


def build_transitive_route_evidence(
    *,
    route: Route,
    full_route: Route,
    full_evidence: Mapping[str, Any],
    baseline_full_receipt: Mapping[str, Any],
    candidate_full_receipt: Mapping[str, Any],
    candidate_tp_receipt: Mapping[str, Any],
) -> dict[str, Any]:
    """Build one declared TP-prefix proof from its exact full-route proof."""

    if route.tp_length is None:
        _fail("PARITY_PLAN_INVALID")
    validate_receipt_roles(route.proof_kind, [candidate_tp_receipt.get("role")])
    validate_transitive_inputs(
        {
            "routeId": full_route.route_id,
            "capabilityFingerprint": full_route.capability_fingerprint,
            "proofKind": "exact-output",
            "equal": full_evidence.get("equal"),
            "baselineReceipt": baseline_full_receipt,
            "candidateReceipt": candidate_full_receipt,
        },
        candidate_tp_receipt,
        route.tp_length,
    )
    result = compare_transitive_files(
        Path(baseline_full_receipt["output"]["path"]),
        Path(candidate_full_receipt["output"]["path"]),
        Path(candidate_tp_receipt["output"]["path"]),
        Path(candidate_full_receipt["inputs"][0]["path"]),
        route.tp_length,
    )
    ordered_inputs = [
        _portable_input_identity(row) for row in candidate_tp_receipt["inputs"]
    ]
    full_input_path = Path(candidate_full_receipt["inputs"][0]["path"])
    row = {
        "routeId": route.route_id,
        "capabilityFingerprint": route.capability_fingerprint,
        "proofKind": "tp-prefix-transitive",
        "fullEvidence": {
            "routeId": full_route.route_id,
            "capabilityFingerprint": full_route.capability_fingerprint,
            "evidenceSha256": canonical_route_row_sha256(full_evidence),
        },
        "tpLength": route.tp_length,
        "tpScenario": _evidence_scenario(candidate_tp_receipt, ordered_inputs),
        "candidateCompilationFingerprint": candidate_tp_receipt["scenario"][
            "compilationFingerprint"
        ],
        "receipts": [_receipt_evidence_summary(candidate_tp_receipt)],
        "candidateTpOutput": _artifact(
            Path(candidate_tp_receipt["output"]["path"])
        ),
        "candidateFullInput": _artifact(full_input_path),
        **result,
    }
    validate_transitive_evidence_reference(full_evidence, row)
    return row


def validate_report_sequence(*, authority_operations: Sequence[Mapping[str, Any]], observed_operations: Sequence[Mapping[str, Any]], observed_mutations: Sequence[Mapping[str, Any]]) -> None:
    expected = [(row.get("operationId"), row.get("sequence")) for row in authority_operations]
    operations = [(row.get("operationId"), row.get("sequence")) for row in observed_operations]
    mutations = [(row.get("operationId"), row.get("sequence", index)) for index, row in enumerate(observed_mutations)]
    if operations != expected or mutations != expected[: len(mutations)]:
        _fail("PARITY_PROVENANCE_INVALID")


def canonical_ordered_inputs_sha256(inputs: Sequence[Mapping[str, Any]]) -> str:
    return canonical_json_sha256(list(inputs))


def _invocation_digest(receipt: Mapping[str, Any], field: str) -> str:
    invocation = receipt.get(field, {})
    value = {
        "routeId": receipt.get("routeId"),
        "capabilityFingerprint": receipt.get("capabilityFingerprint"),
        "scenario": receipt.get("scenario"),
        "inputs": receipt.get("inputs"),
        "interface": invocation.get("interface"),
        "operatorLogin": invocation.get("operatorLogin"),
        "operation": invocation.get("operation"),
    }
    return canonical_json_sha256(value)


def canonical_arguments_sha256(receipt: Mapping[str, Any]) -> str:
    return _invocation_digest(receipt, "invocation")


def canonical_authority_arguments_sha256(receipt: Mapping[str, Any]) -> str:
    return _invocation_digest(receipt, "authorityInvocation")


def validate_invocation_authority(receipt: Mapping[str, Any], *, comparator_sha256: str, expected_execution_artifact_sha256: str, expected_executor_identity_sha256: str, authorized_operators: set[str]) -> None:
    invocation = receipt.get("invocation", {})
    adapter = receipt.get("captureAdapter", {})
    if (
        receipt.get("executionArtifactSha256") != expected_execution_artifact_sha256
        or receipt.get("executorIdentitySha256") != expected_executor_identity_sha256
        or adapter != {"contractVersion": "1.0", "scriptSha256": comparator_sha256}
        or invocation.get("operatorLogin") not in authorized_operators
        or invocation.get("argumentsSha256") != canonical_arguments_sha256(receipt)
    ):
        _fail("PARITY_PROVENANCE_INVALID")


def validate_same_scenario_inputs(plan: Plan, *, route_id: str, capability_fingerprint: str, baseline_invocation_arguments: Sequence[str], candidate_invocation_arguments: Sequence[str], baseline_inputs: Sequence[Mapping[str, Any]], candidate_inputs: Sequence[Mapping[str, Any]]) -> list[dict[str, Any]]:
    route = next((row for row in plan.routes if row.route_id == route_id and row.capability_fingerprint == capability_fingerprint), None)
    if route is None:
        _fail("PARITY_PROVENANCE_INVALID")
    alias = next((row for row in plan.raw["inputIdentityAliases"] if row["routeId"] == route_id and row["capabilityFingerprint"] == capability_fingerprint), None)
    baseline = [
        _portable_input_identity(row) for row in baseline_inputs
    ]
    candidate = [
        _portable_input_identity(row) for row in candidate_inputs
    ]
    if alias:
        if list(baseline_invocation_arguments) != [alias["baselineInvocationOption"], "<INPUT>"] or list(candidate_invocation_arguments) != [alias["candidateInvocationOption"], "<INPUT>"]:
            _fail("PARITY_PROVENANCE_INVALID")
        if baseline[-1].get("slotId") != alias["baselineInputSlotId"] or candidate[-1].get("slotId") != alias["candidateInputSlotId"]:
            _fail("PARITY_PROVENANCE_INVALID")
        baseline[-1]["slotId"] = alias["logicalInputId"]
        candidate[-1]["slotId"] = alias["logicalInputId"]
        for row in (baseline[-1], candidate[-1]):
            row["logicalInputId"] = alias["logicalInputId"]
    if baseline != candidate:
        _fail("PARITY_PROVENANCE_INVALID")
    return baseline


def _valid_mutation(mutation: Mapping[str, Any], operations: Sequence[Mapping[str, Any]]) -> bool:
    operation = next((row for row in operations if row.get("operationId") == mutation.get("operationId")), None)
    if operation is None or mutation.get("kind") != operation.get("kind") or not isinstance(mutation.get("reason"), str) or not mutation.get("reason"):
        return False
    target = mutation.get("targetRange")
    if not isinstance(target, dict) or mutation.get("targetSpaceId") != target.get("addressSpace") or target.get("addressSpace") != operation.get("targetSpaceId"):
        return False
    start, end = target.get("start"), target.get("endExclusive")
    changed = mutation.get("changedByteCount")
    if not all(isinstance(x, int) for x in (start, end, changed)) or start < 0 or end <= start or changed < 0 or changed > end - start:
        return False
    if not SHA256_RE.fullmatch(str(mutation.get("beforeSha256", ""))) or not SHA256_RE.fullmatch(str(mutation.get("afterSha256", ""))):
        return False
    operation_range = operation.get("targetRange", {})
    if not (operation_range.get("start", -1) <= start and end <= operation_range.get("endExclusive", -1)):
        return False
    return True


def validate_report_projection_against_compiled_authority(projection: Mapping[str, Any], authority: Mapping[str, Any]) -> None:
    operations = projection.get("compiledOperations")
    expected = authority.get("compiledOperations")
    mutations = projection.get("compiledMutations")
    if not isinstance(operations, list) or not isinstance(expected, list) or not isinstance(mutations, list):
        _fail("PARITY_PROVENANCE_INVALID")
    if projection.get("compilationFingerprint") is not None and projection.get("compilationFingerprint") != authority.get("compilationFingerprint"):
        _fail("PARITY_PROVENANCE_INVALID")
    if operations != expected or len({row.get("operationId") for row in operations}) != len(operations):
        _fail("PARITY_PROVENANCE_INVALID")
    for operation in operations:
        if operation.get("status") != "succeeded" or not operation.get("reason"):
            _fail("PARITY_PROVENANCE_INVALID")
        processor = operation.get("processor")
        commands = operation.get("executedCommands")
        if processor is None:
            if commands != []:
                _fail("PARITY_PROVENANCE_INVALID")
        elif not isinstance(commands, list) or not commands or [row.get("sequence") for row in commands] != list(range(len(commands))):
            _fail("PARITY_PROVENANCE_INVALID")
    if len({row.get("operationId") for row in mutations}) != len(mutations) or any(not _valid_mutation(row, operations) for row in mutations):
        _fail("PARITY_PROVENANCE_INVALID")
    if projection.get("compilationFingerprint") is not None:
        processor_ids = {row.get("operationId") for row in operations if row.get("processor") is not None}
        mutation_ids = {row.get("operationId") for row in mutations}
        if not processor_ids <= mutation_ids:
            _fail("PARITY_PROVENANCE_INVALID")
    expected_mutations = authority.get("compiledMutations", [])
    if expected_mutations and mutations != expected_mutations:
        _fail("PARITY_PROVENANCE_INVALID")
    if not expected_mutations:
        for mutation in mutations:
            operation = next(row for row in operations if row.get("operationId") == mutation.get("operationId"))
            processor = operation.get("processor")
            if processor:
                target = mutation["targetRange"]
                if not any(
                    row.get("addressSpace") == target.get("addressSpace")
                    and row.get("start", -1) <= target.get("start", -1)
                    and target.get("endExclusive", 2**63) <= row.get("endExclusive", -1)
                    for row in processor.get("allowedWriteRanges", [])
                ):
                    _fail("PARITY_PROVENANCE_INVALID")


def build_independent_report_validation(*, route_id: str, capability_fingerprint: str, baseline_projection: Mapping[str, Any], baseline_compiled_authority: Mapping[str, Any], candidate_projection: Mapping[str, Any], candidate_compiled_authority: Mapping[str, Any], baseline_raw_report_sha256: str, candidate_raw_report_sha256: str) -> dict[str, Any]:
    if not route_id or not SHA256_RE.fullmatch(capability_fingerprint) or not SHA256_RE.fullmatch(baseline_raw_report_sha256) or not SHA256_RE.fullmatch(candidate_raw_report_sha256):
        _fail("PARITY_PROVENANCE_INVALID")
    validate_report_projection_against_compiled_authority(baseline_projection, baseline_compiled_authority)
    validate_report_projection_against_compiled_authority(candidate_projection, candidate_compiled_authority)
    return {
        "kind": "independent-executor-typed-authority",
        "baseline": {"rawReportSha256": baseline_raw_report_sha256, "projectionSha256": canonical_json_sha256(baseline_projection), "compiledAuthoritySha256": canonical_json_sha256(baseline_compiled_authority), "passed": True},
        "candidate": {"rawReportSha256": candidate_raw_report_sha256, "projectionSha256": canonical_json_sha256(candidate_projection), "compiledAuthoritySha256": canonical_json_sha256(candidate_compiled_authority), "passed": True},
        "crossVersionOperationComparison": "not-applied-executor-specific", "passed": True,
    }


def _range(value: Mapping[str, Any], *, expected_space: str, capacities: Mapping[str, int]) -> tuple[int, int]:
    if not isinstance(value, dict) or value.get("addressSpace") != expected_space or expected_space not in capacities:
        _fail("PARITY_REPORT_RANGE_INVALID")
    start, end = value.get("start"), value.get("endExclusive")
    if not isinstance(start, int) or isinstance(start, bool) or not isinstance(end, int) or isinstance(end, bool) or start < 0 or end <= start or end > 2**63 - 1 or end > capacities[expected_space]:
        _fail("PARITY_REPORT_RANGE_INVALID")
    return start, end


def _contained(child: tuple[int, int], parent: tuple[int, int]) -> bool:
    return parent[0] <= child[0] and child[1] <= parent[1]


def _non_overlapping(ranges: Sequence[tuple[int, int]]) -> bool:
    ordered = sorted(ranges)
    return all(left[1] <= right[0] for left, right in zip(ordered, ordered[1:]))


def validate_semantic_report_ranges(projection: Mapping[str, Any], capacities: Mapping[str, int]) -> None:
    try:
        operations = projection["compiledOperations"]
        mutations = projection["compiledMutations"]
        target_ranges: list[tuple[int, int]] = []
        by_id: dict[str, tuple[Mapping[str, Any], tuple[int, int]]] = {}
        for operation in operations:
            target = _range(operation["targetRange"], expected_space=operation["targetSpaceId"], capacities=capacities)
            target_ranges.append(target)
            source = operation.get("sourceRange")
            if source is not None:
                source_range = _range(source, expected_space=operation["sourceSpaceId"], capacities=capacities)
                if operation.get("kind") == "CopyRange" and source_range[1] - source_range[0] != target[1] - target[0]:
                    _fail("PARITY_REPORT_RANGE_INVALID")
            by_id[operation["operationId"]] = (operation, target)
            processor = operation.get("processor")
            if processor:
                for field in ("allowedReadRanges", "allowedWriteRanges"):
                    admitted = [_range(row, expected_space=operation["targetSpaceId"], capacities=capacities) for row in processor[field]]
                    if not _non_overlapping(admitted) or any(not _contained(row, target) for row in admitted):
                        _fail("PARITY_REPORT_RANGE_INVALID")
        if not _non_overlapping(target_ranges):
            _fail("PARITY_REPORT_RANGE_INVALID")
        for mutation in mutations:
            operation, target = by_id[mutation["operationId"]]
            outcome = _range(mutation["targetRange"], expected_space=mutation["targetSpaceId"], capacities=capacities)
            if mutation["targetSpaceId"] != operation["targetSpaceId"] or not _contained(outcome, target) or mutation["changedByteCount"] > outcome[1] - outcome[0]:
                _fail("PARITY_REPORT_RANGE_INVALID")
            processor = operation.get("processor")
            if processor:
                allowed = [_range(row, expected_space=operation["targetSpaceId"], capacities=capacities) for row in processor["allowedWriteRanges"]]
                if not any(_contained(outcome, row) for row in allowed):
                    _fail("PARITY_REPORT_RANGE_INVALID")
    except ParityError:
        raise
    except (KeyError, TypeError, ValueError):
        _fail("PARITY_REPORT_RANGE_INVALID")


def _require_artifact_reference(reference: Mapping[str, Any], role: str) -> Path:
    try:
        path = require_local_artifact(Path(reference["path"]), role)
        payload = path.read_bytes()
        if reference.get("size") != len(payload) or reference.get("sha256") != _sha256(payload):
            _fail("PARITY_PROVENANCE_INVALID")
        return path
    except KeyError:
        _fail("PARITY_PROVENANCE_INVALID")


def _pascal_range(raw: Mapping[str, Any], space: str | None) -> dict[str, Any] | None:
    if raw is None:
        return None
    if set(raw) != {"Start", "Length", "EndExclusive"} or raw["Length"] != raw["EndExclusive"] - raw["Start"]:
        _fail("PARITY_PROVENANCE_INVALID")
    return {"addressSpace": space, "start": raw["Start"], "endExclusive": raw["EndExclusive"]}


def _normalize_raw_operation(raw: Mapping[str, Any]) -> dict[str, Any]:
    required = {
        "OperationId", "Sequence", "Kind", "Status", "SourceSpaceId", "SourceRange",
        "TargetSpaceId", "TargetRange", "OverlapPolicy", "ProcessorId", "ToolBindingId",
        "ProcessorAllowedReadRanges", "ProcessorAllowedWriteRanges", "ExecutedCommands",
        "Reason", "Provenance",
    }
    if set(raw) != required:
        _fail("PARITY_PROVENANCE_INVALID")
    target_space = raw["TargetSpaceId"]
    result: dict[str, Any] = {
        "operationId": raw["OperationId"], "sequence": raw["Sequence"], "kind": raw["Kind"],
        "status": str(raw["Status"]).lower(), "sourceSpaceId": raw["SourceSpaceId"],
        "sourceRange": _pascal_range(raw["SourceRange"], raw["SourceSpaceId"]),
        "targetSpaceId": target_space, "targetRange": _pascal_range(raw["TargetRange"], target_space),
        "overlapPolicy": raw["OverlapPolicy"], "processor": None, "executedCommands": [],
        "reason": raw["Reason"],
        "provenance": {"kind": raw["Provenance"]["Kind"], "sourceId": raw["Provenance"]["SourceId"], "sourceVersion": raw["Provenance"]["SourceVersion"]},
    }
    commands = raw["ExecutedCommands"]
    if raw["ProcessorId"] is None:
        if raw["ToolBindingId"] is not None or raw["ProcessorAllowedReadRanges"] or raw["ProcessorAllowedWriteRanges"] or commands:
            _fail("PARITY_PROVENANCE_INVALID")
        return result
    if not isinstance(commands, list) or not commands:
        _fail("PARITY_PROVENANCE_INVALID")
    result["processor"] = {
        "processorId": raw["ProcessorId"], "toolBindingId": raw["ToolBindingId"],
        "allowedReadRanges": [_pascal_range(row, target_space) for row in raw["ProcessorAllowedReadRanges"]],
        "allowedWriteRanges": [_pascal_range(row, target_space) for row in raw["ProcessorAllowedWriteRanges"]],
    }
    seen_commands: set[tuple[str, str, tuple[str, ...]]] = set()
    for sequence, command in enumerate(commands):
        executable = Path(command["ExecutablePath"])
        working = Path(command["WorkingDirectory"])
        arguments = [str(value) for value in command["Arguments"]]
        if executable.parent.name != "external-tools" or not arguments:
            _fail("PARITY_PROVENANCE_INVALID")
        argument_paths = [Path(value) for value in arguments if Path(value).is_absolute()]
        if any(path.parent != working for path in argument_paths):
            _fail("PARITY_PROVENANCE_INVALID")
        identity = (str(executable), str(working), tuple(arguments))
        if identity in seen_commands:
            _fail("PARITY_PROVENANCE_INVALID")
        seen_commands.add(identity)
        package_root = executable.parent.parent
        tokens = []
        for argument in arguments:
            normalized = argument.replace("\\", "/")
            normalized = normalized.replace(str(package_root).replace("\\", "/"), "{package}")
            normalized = normalized.replace(str(working).replace("\\", "/"), "{staging}")
            tokens.append(normalized)
        result["executedCommands"].append({
            "sequence": sequence,
            "executablePackagePath": executable.relative_to(package_root).as_posix(),
            "workingDirectoryKind": "host-created-staging", "argumentCount": len(arguments),
            "canonicalArgumentsSha256": _sha256(json.dumps(tokens, ensure_ascii=False, separators=(",", ":")).encode("utf-8")),
        })
    return result


def _normalize_raw_mutation(raw: Mapping[str, Any]) -> dict[str, Any]:
    required = {"OperationId", "Kind", "TargetSpaceId", "TargetRange", "ChangedByteCount", "BeforeSha256", "AfterSha256", "Reason"}
    if set(raw) != required:
        _fail("PARITY_PROVENANCE_INVALID")
    return {
        "operationId": raw["OperationId"], "kind": raw["Kind"], "targetSpaceId": raw["TargetSpaceId"],
        "targetRange": _pascal_range(raw["TargetRange"], raw["TargetSpaceId"]),
        "changedByteCount": raw["ChangedByteCount"], "beforeSha256": raw["BeforeSha256"],
        "afterSha256": raw["AfterSha256"], "reason": raw["Reason"],
    }


def _canonicalize_raw_utc(value: str) -> str:
    if value.endswith("+00:00"):
        value = value[:-6] + "Z"
    parse_canonical_utc(value)
    return value


def _validate_raw_report(raw: Mapping[str, Any], receipt: Mapping[str, Any], *, committed: bool, invocation_field: str) -> tuple[list[dict[str, Any]], list[dict[str, Any]], dict[str, Any]]:
    required = {"RunId", "ProfileId", "ProfileVersion", "IcId", "ModeId", "ExperienceId", "CompositionKind", "StartedAtUtc", "CompletedAtUtc", "Inputs", "Operations", "Mutations", "Issues", "Output", "OutputDifferences", "CompilationFingerprint", "Validations", "OutputNaming"}
    if set(raw) != required:
        _fail("PARITY_PROVENANCE_INVALID")
    scenario, invocation = receipt["scenario"], receipt[invocation_field]
    workflow = scenario["workflowId"]
    expected_kind = "Replace" if workflow == "ctrlram-replace" else "Merge"
    inputs = [{"addressSpaceId": row["AddressSpaceId"], "artifactId": row["ArtifactId"], "size": row["Size"], "sha256": row["Sha256"]} for row in raw["Inputs"]]
    expected_inputs = [{"addressSpaceId": row["slotId"], "artifactId": row["slotId"], "size": row["size"], "sha256": row["sha256"]} for row in receipt["inputs"]]
    context = {
        "icId": raw["IcId"], "modeId": raw["ModeId"], "experienceId": raw["ExperienceId"],
        "compositionKind": raw["CompositionKind"], "startedAtUtc": _canonicalize_raw_utc(raw["StartedAtUtc"]),
        "completedAtUtc": _canonicalize_raw_utc(raw["CompletedAtUtc"]), "orderedInputs": inputs,
        "outputCommitted": raw["Output"]["Committed"], "issueCount": len(raw["Issues"]),
    }
    valid = (
        raw["ProfileId"] == scenario["expectedProfileId"]
        and isinstance(raw["ProfileVersion"], str) and bool(raw["ProfileVersion"])
        and raw["IcId"] == scenario["icId"] and raw["ModeId"] == workflow and raw["ExperienceId"] == workflow
        and raw["CompositionKind"] == expected_kind and context["startedAtUtc"] == invocation["startedAtUtc"]
        and context["completedAtUtc"] == invocation["completedAtUtc"] and inputs == expected_inputs
        and raw["Output"]["Committed"] is committed and not raw["Issues"]
        and raw["CompilationFingerprint"] == scenario["compilationFingerprint"]
        and raw["Output"]["Size"] == scenario["outputCapacity"]
        and raw["Output"]["Size"] == receipt["output"]["size"] and raw["Output"]["Sha256"] == receipt["output"]["sha256"]
    )
    if not valid:
        _fail("PARITY_PROVENANCE_INVALID")
    operations = [_normalize_raw_operation(row) for row in raw["Operations"]]
    mutations = [_normalize_raw_mutation(row) for row in raw["Mutations"]]
    if not committed and mutations:
        _fail("PARITY_PROVENANCE_INVALID")
    return operations, mutations, context


def load_and_validate_receipt(path: Path) -> dict[str, Any]:
    require_local_artifact(path, "receipt")
    return _read_json_file(path, "PARITY_PROVENANCE_INVALID")


def validate_receipt(receipt: Mapping[str, Any], *, expected_execution_artifact_sha256: str, expected_executor_identity_sha256: str, authorized_operators: set[str]) -> None:
    validate_invocation_authority(
        receipt,
        comparator_sha256=receipt.get("captureAdapter", {}).get("scriptSha256", ""),
        expected_execution_artifact_sha256=expected_execution_artifact_sha256,
        expected_executor_identity_sha256=expected_executor_identity_sha256,
        authorized_operators=authorized_operators,
    )
    authority_invocation = receipt.get("authorityInvocation", {})
    if (
        authority_invocation.get("operatorLogin") not in authorized_operators
        or authority_invocation.get("argumentsSha256") != canonical_authority_arguments_sha256(receipt)
        or authority_invocation.get("operation") != "preview"
        or receipt.get("invocation", {}).get("operation") != "build"
        or authority_invocation.get("interface") != receipt.get("invocation", {}).get("interface")
        or authority_invocation.get("result") != "success" or receipt.get("invocation", {}).get("result") != "success"
    ):
        _fail("PARITY_PROVENANCE_INVALID")
    validate_time_order(authority_invocation["startedAtUtc"], authority_invocation["completedAtUtc"], error_code="PARITY_PROVENANCE_INVALID")
    validate_time_order(receipt["invocation"]["startedAtUtc"], receipt["invocation"]["completedAtUtc"], error_code="PARITY_PROVENANCE_INVALID")
    if parse_canonical_utc(authority_invocation["completedAtUtc"]) >= parse_canonical_utc(receipt["invocation"]["completedAtUtc"]):
        _fail("PARITY_PROVENANCE_INVALID")
    for row in receipt.get("inputs", []):
        _require_artifact_reference(row, "input")
    output_path = _require_artifact_reference(receipt["output"], "output")
    report_path = _require_artifact_reference(receipt["report"], "report")
    report = _read_json_file(report_path, "PARITY_PROVENANCE_INVALID")
    if (
        report.get("executionArtifactSha256") != receipt["executionArtifactSha256"]
        or report.get("routeId") != receipt["routeId"] or report.get("capabilityFingerprint") != receipt["capabilityFingerprint"]
        or report.get("scenarioSha256") != canonical_json_sha256(receipt["scenario"])
        or report.get("argumentsSha256") != receipt["invocation"]["argumentsSha256"]
        or report.get("authorityArgumentsSha256") != authority_invocation["argumentsSha256"]
        or report.get("orderedInputsSha256") != canonical_ordered_inputs_sha256(receipt["inputs"])
        or report.get("output") != receipt["output"]
        or report.get("terminal", {}).get("result") != "success"
        or report.get("terminal", {}).get("completedAtUtc") != receipt["invocation"]["completedAtUtc"]
    ):
        _fail("PARITY_PROVENANCE_INVALID")
    raw_path = _require_artifact_reference(report["applicationReport"], "application-report")
    authority_path = _require_artifact_reference(report["applicationAuthorityReport"], "application-authority-report")
    raw = _read_json_file(raw_path, "PARITY_PROVENANCE_INVALID")
    authority_raw = _read_json_file(authority_path, "PARITY_PROVENANCE_INVALID")
    operations, mutations, context = _validate_raw_report(raw, receipt, committed=True, invocation_field="invocation")
    authority_operations, authority_mutations, _ = _validate_raw_report(authority_raw, receipt, committed=False, invocation_field="authorityInvocation")
    if context != report.get("applicationContext") or operations != report.get("compiledOperations") or mutations != report.get("compiledMutations") or authority_mutations:
        _fail("PARITY_PROVENANCE_INVALID")
    validate_report_projection_against_compiled_authority(
        {"compilationFingerprint": report.get("compilationFingerprint"), "compiledOperations": operations, "compiledMutations": mutations},
        {"compilationFingerprint": receipt["scenario"]["compilationFingerprint"], "compiledOperations": authority_operations, "compiledMutations": []},
    )
    if _sha256(output_path.read_bytes()) != raw["Output"]["Sha256"]:
        _fail("PARITY_PROVENANCE_INVALID")


def _verify_declared_file(root: Path, reference: Mapping[str, Any]) -> Path:
    relative = reference.get("path")
    if not isinstance(relative, str) or not _safe_repo_path(relative):
        _fail("PARITY_AUTHORITY_MISMATCH")
    path = root / PurePosixPath(relative)
    if not path.is_file() or path.is_symlink():
        _fail("PARITY_AUTHORITY_MISMATCH")
    payload = path.read_bytes()
    if reference.get("size") != len(payload) or reference.get("sha256") != _sha256(payload):
        _fail("PARITY_AUTHORITY_MISMATCH")
    return path


def _run_verified_build(host: Any, root: Path, contract: Mapping[str, Any], *, candidate: bool) -> None:
    restore = list(contract["restore"]["arguments"])
    build = list(contract["build"]["arguments"])
    if candidate:
        build = [f"-p:PathMap={root}=/_/src" if value == "-p:PathMap={sourceRoot}=/_/src" else value for value in build]
    for argv in (restore, build):
        result = host.run(argv, root)
        if result.returncode != 0:
            _fail("PARITY_AUTHORITY_MISMATCH")


def verify_source_baseline_executor(root: Path, contract: Mapping[str, Any], host: Any, *, executor_identity_sha256: str) -> VerifiedSourceExecutor:
    source = contract.get("source", {})
    try:
        valid = (
            host.git_tag_object(root, source["tag"]) == source["tagObject"]
            and host.git_head(root) == source["peeledCommit"] and host.git_tree(root) == source["sourceTree"]
            and not host.git_dirty_paths(root) and host.dotnet_sdk_version(root) == contract["toolchain"]["resolvedSdkVersion"]
            and SHA256_RE.fullmatch(executor_identity_sha256)
        )
    except (KeyError, OSError):
        valid = False
    if not valid:
        _fail("PARITY_AUTHORITY_MISMATCH")
    references = [contract["toolchain"]["globalJson"], *contract["lockFiles"], *contract["externalTools"]]
    for reference in references:
        _verify_declared_file(root, reference)
    _run_verified_build(host, root, contract, candidate=False)
    cli = _verify_declared_file(root, contract["cliAssembly"])
    return VerifiedSourceExecutor("exact-tag-source-built-cli", root, source["peeledCommit"], source["sourceTree"], executor_identity_sha256, cli, cli.stat().st_size, _sha256(cli.read_bytes()), ("dotnet", str(cli)), True)


def _validate_candidate_contract(contract: Mapping[str, Any]) -> None:
    try:
        if contract["kind"] != "candidate-source-built-cli" or contract["freshBuild"] != {
            "sourceMaterialization": "detached-git-worktree", "emptyDestinationRequired": True,
            "rejectIgnoredBuildOutputsBeforeRestore": True, "forbiddenPathSegments": ["bin", "obj"],
        }:
            _fail("PARITY_AUTHORITY_MISMATCH")
        args = contract["build"]["arguments"]
        if "-p:ContinuousIntegrationBuild=true" not in args or "-p:PathMap={sourceRoot}=/_/src" not in args:
            _fail("PARITY_AUTHORITY_MISMATCH")
        for reference in [contract["toolchain"]["globalJson"], *contract["lockFiles"], *contract["externalTools"], contract["cliAssembly"]]:
            if not _safe_repo_path(reference["path"]):
                _fail("PARITY_AUTHORITY_MISMATCH")
    except (KeyError, TypeError):
        _fail("PARITY_AUTHORITY_MISMATCH")


def load_and_validate_candidate_source_executor_contract(path: Path, reference: Mapping[str, Any]) -> SourceExecutorContract:
    require_local_artifact(path, "candidate-source-executor-contract")
    payload = path.read_bytes()
    if reference.get("size") != len(payload) or reference.get("sha256") != _sha256(payload):
        _fail("PARITY_AUTHORITY_MISMATCH")
    contract = _read_json_file(path, "PARITY_AUTHORITY_MISMATCH")
    _validate_candidate_contract(contract)
    return SourceExecutorContract(contract, _sha256(payload))


def verify_candidate_source_executor(root: Path, validated: SourceExecutorContract, host: Any) -> VerifiedSourceExecutor:
    contract = validated.contract
    source = contract["source"]
    try:
        valid = (
            host.git_head(root) == source["implementationHead"] and host.git_tree(root) == source["implementationTree"]
            and not host.git_dirty_paths(root) and not host.git_ignored_build_paths(root)
            and host.dotnet_sdk_version(root) == contract["toolchain"]["resolvedSdkVersion"]
            and all(host.git_tree_for_path(root, path) == tree for path, tree in source["authorityTrees"].items())
        )
    except (KeyError, OSError):
        valid = False
    if not valid:
        _fail("PARITY_AUTHORITY_MISMATCH")
    for reference in [contract["toolchain"]["globalJson"], *contract["lockFiles"], *contract["externalTools"]]:
        _verify_declared_file(root, reference)
    _run_verified_build(host, root, contract, candidate=True)
    cli = _verify_declared_file(root, contract["cliAssembly"])
    return VerifiedSourceExecutor(contract["kind"], root, source["implementationHead"], source["implementationTree"], validated.identity_sha256, cli, cli.stat().st_size, _sha256(cli.read_bytes()), ("dotnet", str(cli)), True)


def validate_candidate_source_executor_identity(*, candidate_authority: Mapping[str, Any], candidate_source_contract: Mapping[str, Any], candidate_build: Mapping[str, Any], receipt_executor_identities: Sequence[str], comparison_identity_sha256: str, evidence_identity_sha256: str) -> None:
    expected = candidate_authority.get("sourceExecutorContract", {}).get("sha256")
    source = candidate_source_contract.get("source", {})
    if (
        candidate_authority.get("implementationHead") != source.get("implementationHead")
        or candidate_authority.get("implementationTree") != source.get("implementationTree")
        or candidate_build.get("candidateSourceExecutorIdentitySha256") != expected
        or len(receipt_executor_identities) != 64 or any(value != expected for value in receipt_executor_identities)
        or comparison_identity_sha256 != expected or evidence_identity_sha256 != expected
    ):
        _fail("PARITY_AUTHORITY_MISMATCH")


def validate_candidate_package(package: Path, expected_head: str) -> dict[str, Any]:
    require_local_artifact(package, "candidate-zip")
    try:
        with zipfile.ZipFile(package) as archive:
            entries = [info for info in archive.infolist() if PurePosixPath(info.filename).name == "RELEASE-MANIFEST.json"]
            if len(entries) != 1:
                _fail("PARITY_PACKAGE_MISMATCH")
            raw = archive.read(entries[0])
        try:
            manifest = load_json_reject_duplicates(raw)
        except ParityError:
            _fail("PARITY_PACKAGE_MISMATCH")
        if not isinstance(manifest, dict) or manifest.get("sourceCommit") != expected_head:
            _fail("PARITY_PACKAGE_MISMATCH")
        return manifest
    except ParityError:
        raise
    except (OSError, ValueError, zipfile.BadZipFile, KeyError, UnicodeError):
        _fail("PARITY_PACKAGE_MISMATCH")


def validate_protected_workflow_semantics(workflow: bytes | Mapping[str, Any], contract: Mapping[str, Any]) -> None:
    try:
        value = yaml.safe_load(workflow) if isinstance(workflow, bytes) else copy.deepcopy(workflow)
        expected_jobs: dict[str, Any] = {}
        for job_id, declared in contract["jobs"].items():
            job = {
                "name": declared["name"], "needs": declared["needs"], "if": declared["if"],
                "runs-on": declared["runsOn"], "timeout-minutes": declared["timeoutMinutes"],
                "permissions": declared["permissions"], "steps": declared["steps"],
            }
            if declared["environment"] is not None:
                job["environment"] = declared["environment"]
            expected_jobs[job_id] = job
        top_on = value.get("on") if "on" in value else value.get(True)
        if not isinstance(top_on, dict) or set(top_on) != {contract["trigger"]} or value.get("permissions") != contract["topLevelPermissions"]:
            _fail("PARITY_WORKFLOW_MISMATCH")
        jobs = value.get("jobs", {})
        if jobs.get("candidate", {}).get("name") != "release / candidate":
            _fail("PARITY_WORKFLOW_MISMATCH")
        parity_jobs = {key: jobs[key] for key in jobs if key.startswith("v0916-parity-")}
        if parity_jobs != expected_jobs:
            _fail("PARITY_WORKFLOW_MISMATCH")
        promotion_contract = contract["promotionGate"]
        promotion = jobs.get(promotion_contract["jobId"], {})
        governed_promotion_names = {
            step["name"] for step in promotion_contract["steps"]
        }
        governed_promotion_steps = [
            step
            for step in promotion.get("steps", [])
            if step.get("name") in governed_promotion_names
        ]
        if (
            promotion.get("needs") != promotion_contract["needs"]
            or promotion.get("if") != promotion_contract["if"]
            or governed_promotion_steps != promotion_contract["steps"]
        ):
            _fail("PARITY_WORKFLOW_MISMATCH")
        allowed_pins = set(contract["actionPins"].values())
        for job in [*parity_jobs.values(), {"steps": governed_promotion_steps}]:
            for step in job["steps"]:
                if "uses" in step and step["uses"] not in allowed_pins:
                    _fail("PARITY_WORKFLOW_MISMATCH")
                if step.get("continue-on-error"):
                    _fail("PARITY_WORKFLOW_MISMATCH")
    except ParityError:
        raise
    except (KeyError, TypeError, yaml.YAMLError):
        _fail("PARITY_WORKFLOW_MISMATCH")


def _git_blob_sha(payload: bytes) -> str:
    return hashlib.sha1(f"blob {len(payload)}\0".encode() + payload).hexdigest()


def verify_protected_candidate_build(
    *,
    repository_root: Path,
    local_assets: Mapping[str, Path],
    declared: Mapping[str, Any],
    firmware_executor_head: str,
    firmware_executor_tree: str,
    package_source_head: str,
    package_source_tree: str,
    process_runner: Any,
    github_reader: Any,
    artifact_download_root: Path,
    workflow_semantic_contract: Mapping[str, Any],
) -> dict[str, Any]:
    expected_roles = {"package", "sbom", "provenance", "notes", "manifest", "checksums"}
    if set(local_assets) != expected_roles:
        _fail("PARITY_PACKAGE_MISMATCH")
    refs = {
        "sbom": "candidateSbom", "provenance": "candidateProvenance", "notes": "releaseNotes",
        "manifest": "candidateManifest", "checksums": "assetChecksums",
    }
    for role, path in local_assets.items():
        require_local_artifact(path, role)
        if role in refs:
            reference = declared[refs[role]]
            payload = path.read_bytes()
            if reference.get("size") != len(payload) or reference.get("sha256") != _sha256(payload):
                _fail("PARITY_PACKAGE_MISMATCH")
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", str(declared.get("artifactDigest", ""))):
        _fail("PARITY_AUTHORITY_MISMATCH")
    try:
        content = github_reader.get_workflow_content(declared["repository"], declared["workflowPath"], declared["workflowCommitSha"])
        if content.get("type") != "file" or content.get("path") != declared["workflowPath"] or content.get("encoding") != "base64" or content.get("sha") != declared["workflowBlobSha"]:
            _fail("PARITY_AUTHORITY_MISMATCH")
        import base64
        workflow_bytes = base64.b64decode(content["content"], validate=True)
        if _git_blob_sha(workflow_bytes) != declared["workflowBlobSha"] or _sha256(workflow_bytes) != declared["workflowRawSha256"]:
            _fail("PARITY_AUTHORITY_MISMATCH")
        validate_protected_workflow_semantics(workflow_bytes, workflow_semantic_contract)
        run = github_reader.get_workflow_run(declared["repository"], declared["runId"])
        repository = run.get("repository", {})
        head_repository = run.get("head_repository", {})
        workflow_sha = declared["workflowCommitSha"]
        run_is_usable = (
            run.get("status") == "completed" and run.get("conclusion") == "success"
        ) or (
            run.get("status") == "in_progress" and run.get("conclusion") is None
        )
        if (
            run.get("id") != declared["runId"] or run.get("head_sha") != workflow_sha
            or run.get("head_branch") != "main" or not run_is_usable
            or repository.get("full_name") != declared["repository"] or repository.get("id") != head_repository.get("id")
            or head_repository.get("full_name") != declared["repository"]
        ):
            _fail("PARITY_AUTHORITY_MISMATCH")
        artifact = github_reader.get_artifact(declared["repository"], declared["artifactId"])
        owner = artifact.get("workflow_run", {})
        if (
            artifact.get("id") != declared["artifactId"] or artifact.get("name") != declared["artifactName"]
            or artifact.get("digest") != declared["artifactDigest"] or artifact.get("expired") is not False
            or owner.get("id") != declared["runId"] or owner.get("head_sha") != workflow_sha or owner.get("head_branch") != "main"
            or owner.get("repository_id") != repository.get("id") or owner.get("head_repository_id") != head_repository.get("id")
        ):
            _fail("PARITY_AUTHORITY_MISMATCH")
        archive = github_reader.download_artifact(declared["repository"], declared["artifactId"])
    except ParityError:
        raise
    except (OSError, KeyError, ValueError, TypeError):
        _fail("PARITY_AUTHORITY_MISMATCH")
    expected_names = {path.name for path in local_assets.values()}
    extracted_root = artifact_download_root / f"artifact-{declared['artifactId']}"
    try:
        artifact_download_root.mkdir(parents=True, exist_ok=False)
        extracted = extract_verified_github_artifact(
            archive, github_digest=declared["artifactDigest"], destination=extracted_root,
            allowed_entries=expected_names, max_entry_bytes=512 * 1024 * 1024,
            max_total_bytes=1024 * 1024 * 1024, max_total_compressed_bytes=1024 * 1024 * 1024,
            max_compression_ratio=1000,
        )
        for role, local in local_assets.items():
            remote = extracted[local.name]
            if local.read_bytes() != remote.read_bytes():
                _fail("PARITY_PACKAGE_MISMATCH")
        validate_candidate_package(
            extracted[local_assets["package"].name], package_source_head
        )
        provenance = _read_json_file(extracted[local_assets["provenance"].name], "PARITY_PACKAGE_MISMATCH")
        if canonical_provenance_subjects_sha256(provenance.get("subjects", [])) != declared["provenanceSubjectsSha256"]:
            _fail("PARITY_PACKAGE_MISMATCH")
        candidate_verifier = repository_root / "scripts/release_promotion_policy.py"
        package_verifier = repository_root / "scripts/smoke-release.ps1"
        if _sha256(candidate_verifier.read_bytes()) != declared["candidateVerifierSha256"] or _sha256(package_verifier.read_bytes()) != declared["packageVerifierSha256"]:
            _fail("PARITY_AUTHORITY_MISMATCH")
        commands = [
            [os.sys.executable, str(candidate_verifier), "verify-manifest", "--asset-dir", str(extracted_root), "--manifest", str(extracted[local_assets["manifest"].name]), "--source-sha", package_source_head, "--source-tree", package_source_tree, "--run-id", str(declared["runId"]), "--workflow-sha", workflow_sha, "--workflow-ref", declared["workflowRef"]],
            ["pwsh", "-NoProfile", "-File", str(package_verifier), "-PackagePath", str(extracted[local_assets["package"].name]), "-SkipUiLaunch"],
        ]
        for argv in commands:
            method = getattr(process_runner, "run", None)
            result = method(argv, repository_root) if method else process_runner(argv, repository_root)
            if result.returncode != 0:
                _fail("PARITY_PACKAGE_MISMATCH")
        return {
            "artifactWorkflowRun": {
                "id": run["id"],
                "headSha": run["head_sha"],
                "headBranch": run["head_branch"],
                "repository": repository["full_name"],
                "repositoryId": repository["id"],
                "headRepositoryId": head_repository["id"],
            },
            "firmwareExecutorAuthority": {
                "head": firmware_executor_head,
                "tree": firmware_executor_tree,
            },
            "packageSourceAuthority": {
                "head": package_source_head,
                "tree": package_source_tree,
            },
            "passed": True,
        }
    except FileExistsError:
        _fail("PARITY_WRITE_CONFLICT")
    finally:
        if artifact_download_root.exists():
            shutil.rmtree(artifact_download_root, ignore_errors=True)


def validate_comparison_schema(comparison: Mapping[str, Any]) -> None:
    required = {"schemaVersion", "planSha256", "policySha256", "comparator", "candidateAuthority", "baselineExecutor", "baselineReleaseReference", "candidatePackage", "candidateBuild", "routeEvidenceSha256", "receiptSetSha256", "executedAtUtc", "routes", "verdict"}
    try:
        if set(comparison) != required or comparison["schemaVersion"] != "1.0" or comparison["verdict"] != "provisional":
            raise ValueError
        routes = comparison["routes"]
        if not isinstance(routes, list) or len(routes) != 64 or len({row.get("routeId") for row in routes}) != 64:
            raise ValueError
        exact = [row for row in routes if row.get("proofKind") == "exact-output"]
        corrections = [
            row
            for row in routes
            if row.get("proofKind")
            == "exact-output-with-approved-semantic-correction"
        ]
        transitive = [row for row in routes if row.get("proofKind") == "tp-prefix-transitive"]
        if len(exact) != 52 or len(corrections) != 1 or len(transitive) != 11 or any(row.get("passed") is not True for row in routes):
            raise ValueError
        for row in [*exact, *corrections]:
            validate_exact_evidence_row_schema(row)
        by_id = {row["routeId"]: row for row in [*exact, *corrections]}
        for row in transitive:
            full = by_id.get(row.get("fullEvidence", {}).get("routeId"))
            if full is None:
                raise ValueError
            validate_transitive_evidence_reference(full, row)
        receipts = [{"routeId": row["routeId"], "role": receipt["role"], "receiptSha256": receipt["receiptSha256"]} for row in routes for receipt in row["receipts"]]
        if comparison["routeEvidenceSha256"] != canonical_route_evidence_sha256(routes) or comparison["receiptSetSha256"] != canonical_receipt_set_sha256(receipts):
            raise ValueError
        parse_canonical_utc(comparison["executedAtUtc"])
    except ParityError:
        raise
    except (KeyError, TypeError, ValueError):
        _fail("PARITY_EVIDENCE_INCOMPLETE")


def infer_firmware_owner_from_github(_: Mapping[str, Any]) -> None:
    _fail("PARITY_OWNER_APPROVAL_REQUIRED", "GitHub deployment creator is not firmware-owner authority")


class ExternalJsonFirmwareOwnerVerifier:
    """Consume, but never manufacture, an external verifier's closed record."""

    def __init__(self, verifier_id: str):
        self.verifier_id = verifier_id

    def verify(
        self, attestation: Mapping[str, Any], verification_record: bytes
    ) -> dict[str, Any]:
        del attestation
        document = load_json_reject_duplicates(verification_record)
        expected_document_keys = {
            "schemaVersion",
            "kind",
            "verifierId",
            "verification",
        }
        if (
            not isinstance(document, dict)
            or set(document) != expected_document_keys
            or document.get("schemaVersion") != "1.0"
            or document.get("kind") != "external-firmware-owner-verification"
            or document.get("verifierId") != self.verifier_id
            or not isinstance(document.get("verification"), dict)
        ):
            raise ValueError("external firmware-owner verification record is invalid")
        observed = copy.deepcopy(document["verification"])
        expected_keys = {
            "attestationId",
            "firmwareOwnerId",
            "attestationSha256",
            "comparisonSha256",
            "comparisonArtifactId",
            "comparisonArtifactDigest",
            "planSha256",
            "policySha256",
            "implementationHead",
            "implementationTree",
            "candidatePackageSha256",
            "candidateManifestSha256",
            "candidateArtifactDigest",
            "routeEvidenceSha256",
            "receiptSetSha256",
            "verifiedAtUtc",
            "authorizedOperators",
        }
        if set(observed) != expected_keys:
            raise ValueError("external firmware-owner verification fields are invalid")
        observed["verificationRecordSha256"] = _sha256(verification_record)
        return observed


class GhCliProtectedApprovalReader:
    """Read immutable GitHub Actions authority through the runner's `gh` CLI."""

    def _json(self, endpoint: str) -> dict[str, Any]:
        result = subprocess.run(
            ["gh", "api", endpoint],
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        if result.returncode != 0:
            raise OSError(result.stderr or result.stdout)
        value = load_json_reject_duplicates(result.stdout)
        if not isinstance(value, dict):
            raise ValueError("GitHub API response is not an object")
        return value

    def get_workflow_content(
        self, repository: str, path: str, ref: str
    ) -> dict[str, Any]:
        return self._json(
            f"repos/{repository}/contents/{quote(path)}?{urlencode({'ref': ref})}"
        )

    def get_workflow_run(self, repository: str, run_id: int) -> dict[str, Any]:
        raw = self._json(f"repos/{repository}/actions/runs/{run_id}")
        return {
            key: raw.get(key)
            for key in (
                "id",
                "run_attempt",
                "head_sha",
                "head_branch",
                "event",
                "status",
                "conclusion",
                "created_at",
                "updated_at",
            )
        } | {
            "repository": {
                "id": raw.get("repository", {}).get("id"),
                "full_name": raw.get("repository", {}).get("full_name"),
            },
            "head_repository": {
                "id": raw.get("head_repository", {}).get("id"),
                "full_name": raw.get("head_repository", {}).get("full_name"),
            },
        }

    def get_workflow_job(self, repository: str, job_id: int) -> dict[str, Any]:
        raw = self._json(f"repos/{repository}/actions/jobs/{job_id}")
        return {
            key: raw.get(key)
            for key in (
                "id",
                "run_id",
                "run_attempt",
                "head_sha",
                "head_branch",
                "name",
                "status",
                "conclusion",
                "started_at",
                "completed_at",
                "html_url",
            )
        }

    def get_deployment(self, repository: str, deployment_id: int) -> dict[str, Any]:
        raw = self._json(f"repos/{repository}/deployments/{deployment_id}")
        return {
            key: raw.get(key)
            for key in ("id", "sha", "ref", "environment", "created_at")
        }

    def get_deployment_statuses(
        self, repository: str, deployment_id: int
    ) -> list[dict[str, Any]]:
        # `gh api` returns an array for this endpoint. Keep a separate bounded
        # reader so the ordinary object-only helper stays fail closed.
        result = subprocess.run(
            ["gh", "api", f"repos/{repository}/deployments/{deployment_id}/statuses"],
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        if result.returncode != 0:
            raise OSError(result.stderr or result.stdout)
        rows = load_json_reject_duplicates(result.stdout)
        if not isinstance(rows, list):
            raise ValueError("GitHub deployment statuses are not an array")
        return [
            {
                key: row.get(key)
                for key in (
                    "id",
                    "state",
                    "created_at",
                    "updated_at",
                    "log_url",
                )
            }
            | {"creator": row.get("creator")}
            for row in rows
            if isinstance(row, dict)
        ]

    def get_artifact(self, repository: str, artifact_id: int) -> dict[str, Any]:
        raw = self._json(f"repos/{repository}/actions/artifacts/{artifact_id}")
        owner = raw.get("workflow_run", {})
        return {
            key: raw.get(key)
            for key in ("id", "name", "digest", "expired", "created_at")
        } | {
            "workflow_run": {
                "id": owner.get("id"),
                "repository_id": owner.get("repository_id"),
                "head_repository_id": owner.get("head_repository_id"),
                "head_branch": owner.get("head_branch"),
                "head_sha": owner.get("head_sha"),
            }
        }

    def download_artifact(self, repository: str, artifact_id: int) -> Any:
        result = subprocess.run(
            ["gh", "api", f"repos/{repository}/actions/artifacts/{artifact_id}/zip"],
            check=False,
            capture_output=True,
        )
        if result.returncode != 0:
            raise OSError(result.stderr.decode("utf-8", errors="replace"))
        return __import__("io").BytesIO(result.stdout)

    def list_workflow_jobs(self, repository: str, run_id: int) -> list[dict[str, Any]]:
        value = self._json(
            f"repos/{repository}/actions/runs/{run_id}/jobs?per_page=100"
        )
        rows = value.get("jobs")
        if not isinstance(rows, list):
            raise ValueError("GitHub workflow jobs are invalid")
        return rows

    def list_run_artifacts(
        self, repository: str, run_id: int
    ) -> list[dict[str, Any]]:
        value = self._json(
            f"repos/{repository}/actions/runs/{run_id}/artifacts?per_page=100"
        )
        rows = value.get("artifacts")
        if not isinstance(rows, list):
            raise ValueError("GitHub run artifacts are invalid")
        return rows

    def list_deployments(
        self, repository: str, *, sha: str, environment: str
    ) -> list[dict[str, Any]]:
        endpoint = (
            f"repos/{repository}/deployments?"
            + urlencode({"sha": sha, "environment": environment, "per_page": 100})
        )
        result = subprocess.run(
            ["gh", "api", endpoint],
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        if result.returncode != 0:
            raise OSError(result.stderr or result.stdout)
        rows = load_json_reject_duplicates(result.stdout)
        if not isinstance(rows, list):
            raise ValueError("GitHub deployments are invalid")
        return rows


def discover_protected_run(
    *,
    reader: Any,
    repository: str,
    run_id: int,
    workflow_sha: str,
) -> dict[str, Any]:
    """Discover the completed compare/attestation facts for finalization."""

    workflow_path = ".github/workflows/release.yml"
    run = reader.get_workflow_run(repository, run_id)
    if run.get("head_sha") != workflow_sha or run.get("head_branch") != "main":
        _fail("PARITY_AUTHORITY_MISMATCH")

    jobs = reader.list_workflow_jobs(repository, run_id)
    matching_jobs = [
        row
        for row in jobs
        if row.get("name") == "release / v0.9.16 parity attestation"
    ]
    if len(matching_jobs) != 1:
        _fail("PARITY_AUTHORITY_MISMATCH")
    job = reader.get_workflow_job(repository, int(matching_jobs[0]["id"]))

    artifact_names = {
        "comparisonArtifact": (
            f"v0916-parity-comparison-{run_id}",
            "comparison.json",
        ),
        "attestationArtifact": (
            f"v0916-parity-attestation-{run_id}",
            "owner-attestation.json",
        ),
        "verificationArtifact": (
            f"v0916-parity-verification-{run_id}",
            "external-verification.json",
        ),
    }
    listed_artifacts = reader.list_run_artifacts(repository, run_id)
    artifacts: dict[str, dict[str, Any]] = {}
    for key, (name, member_name) in artifact_names.items():
        matches = [row for row in listed_artifacts if row.get("name") == name]
        if len(matches) != 1:
            _fail("PARITY_AUTHORITY_MISMATCH")
        artifact = reader.get_artifact(repository, int(matches[0]["id"]))
        owner = artifact.get("workflow_run", {})
        artifacts[key] = {
            "id": artifact["id"],
            "name": artifact["name"],
            "digest": artifact["digest"],
            "memberName": member_name,
            "createdAtUtc": artifact["created_at"],
            "workflowRun": {
                "id": owner["id"],
                "repositoryId": owner["repository_id"],
                "headRepositoryId": owner["head_repository_id"],
                "headBranch": owner["head_branch"],
                "headSha": owner["head_sha"],
            },
        }

    deployments = reader.list_deployments(
        repository, sha=workflow_sha, environment="firmware-parity"
    )
    matching_deployments: list[tuple[dict[str, Any], dict[str, Any]]] = []
    for row in deployments:
        if not isinstance(row, dict) or not isinstance(row.get("id"), int):
            continue
        deployment = reader.get_deployment(repository, int(row["id"]))
        statuses = reader.get_deployment_statuses(repository, int(row["id"]))
        status_matches = [
            status
            for status in statuses
            if status.get("state") == "success"
            and status.get("log_url") == job.get("html_url")
        ]
        if len(status_matches) == 1:
            matching_deployments.append((deployment, status_matches[0]))
    if len(matching_deployments) != 1:
        _fail("PARITY_AUTHORITY_MISMATCH")
    deployment, deployment_status = matching_deployments[0]

    workflow_content = reader.get_workflow_content(
        repository, workflow_path, workflow_sha
    )
    workflow_contract_path = (
        Path(__file__).resolve().parents[1]
        / "docs/contracts/v0916-parity-workflow-v1.json"
    )
    return {
        "repository": repository,
        "repositoryId": run["repository"]["id"],
        "headRepositoryId": run["head_repository"]["id"],
        "workflowPath": workflow_path,
        "workflowRef": "refs/heads/main",
        "workflowCommitSha": workflow_sha,
        "workflowBlobSha": workflow_content["sha"],
        "workflowRawSha256": _sha256(
            __import__("base64").b64decode(workflow_content["content"], validate=True)
        ),
        "workflowSemanticContractSha256": _sha256(
            workflow_contract_path.read_bytes()
        ),
        "workflowRun": {
            "id": run["id"],
            "runAttempt": run["run_attempt"],
            "headSha": run["head_sha"],
            "headBranch": run["head_branch"],
            "event": run["event"],
            "status": run["status"],
            "conclusion": run["conclusion"],
            "repositoryId": run["repository"]["id"],
            "headRepositoryId": run["head_repository"]["id"],
            "createdAtUtc": run["created_at"],
            "updatedAtUtc": run["updated_at"],
        },
        "attestationJob": {
            "id": job["id"],
            "runId": job["run_id"],
            "runAttempt": job["run_attempt"],
            "headSha": job["head_sha"],
            "headBranch": job["head_branch"],
            "name": job["name"],
            "status": job["status"],
            "conclusion": job["conclusion"],
            "startedAtUtc": job["started_at"],
            "completedAtUtc": job["completed_at"],
            "htmlUrl": job["html_url"],
        },
        "deployment": {
            "id": deployment["id"],
            "sha": deployment["sha"],
            "ref": deployment["ref"],
            "environment": deployment["environment"],
            "createdAtUtc": deployment["created_at"],
        },
        "deploymentStatus": {
            "id": deployment_status["id"],
            "state": deployment_status["state"],
            "createdAtUtc": deployment_status["created_at"],
            "updatedAtUtc": deployment_status["updated_at"],
            "logUrl": deployment_status["log_url"],
        },
        **artifacts,
    }


def _read_single_member_archive(stream: Any, digest: str, member: str) -> bytes:
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", digest):
        _fail("PARITY_AUTHORITY_MISMATCH")
    chunks = []
    while True:
        chunk = stream.read(65536)
        if not chunk:
            break
        chunks.append(chunk)
    payload = b"".join(chunks)
    if _sha256(payload) != digest.removeprefix("sha256:"):
        _fail("PARITY_AUTHORITY_MISMATCH")
    try:
        with zipfile.ZipFile(__import__("io").BytesIO(payload)) as archive:
            infos = archive.infolist()
            if len(infos) != 1 or infos[0].filename != member or infos[0].file_size > 64 * 1024 * 1024:
                _fail("PARITY_AUTHORITY_MISMATCH")
            return archive.read(infos[0])
    except ParityError:
        raise
    except (OSError, ValueError, zipfile.BadZipFile):
        _fail("PARITY_AUTHORITY_MISMATCH")


def _github_owner_matches(owner: Mapping[str, Any], *, run_id: int, head: str, repository_id: int, head_repository_id: int) -> bool:
    return owner == {"id": run_id, "repository_id": repository_id, "head_repository_id": head_repository_id, "head_branch": "main", "head_sha": head}


def validate_external_owner_material(
    *,
    comparison_bytes: bytes,
    attestation_bytes: bytes,
    verification_record: bytes,
    firmware_owner_verifier: Any,
) -> dict[str, Any]:
    comparison = load_json_reject_duplicates(comparison_bytes)
    attestation = load_json_reject_duplicates(attestation_bytes)
    if not isinstance(comparison, dict) or not isinstance(attestation, dict):
        _fail("PARITY_OWNER_APPROVAL_REQUIRED")
    validate_comparison_schema(comparison)
    try:
        if set(attestation) != {
            "schemaVersion",
            "authority",
            "binding",
            "authorizedOperators",
            "issuedAtUtc",
            "verdict",
        } or attestation["schemaVersion"] != "1.0" or attestation["verdict"] != "approved":
            raise ValueError
        authority = attestation["authority"]
        if set(authority) != {
            "kind",
            "attestationId",
            "firmwareOwnerId",
            "issuedAtUtc",
            "verificationRequired",
        } or authority["kind"] != "external-firmware-owner-attestation" or authority["verificationRequired"] is not True:
            raise ValueError
        if authority["issuedAtUtc"] != attestation["issuedAtUtc"]:
            raise ValueError
        parse_canonical_utc(attestation["issuedAtUtc"])
        binding = attestation["binding"]
        expected_binding = {
            "comparisonSha256": _sha256(comparison_bytes),
            "comparisonArtifactId": binding["comparisonArtifactId"],
            "comparisonArtifactDigest": binding["comparisonArtifactDigest"],
            "planSha256": comparison["planSha256"],
            "policySha256": comparison["policySha256"],
            "implementationHead": comparison["candidateAuthority"]["implementationHead"],
            "implementationTree": comparison["candidateAuthority"]["implementationTree"],
            "candidatePackageSha256": comparison["candidatePackage"]["sha256"],
            "candidateManifestSha256": comparison["candidateBuild"]["candidateManifest"]["sha256"],
            "candidateArtifactDigest": comparison["candidateBuild"]["artifactDigest"],
            "routeEvidenceSha256": comparison["routeEvidenceSha256"],
            "receiptSetSha256": comparison["receiptSetSha256"],
        }
        if (
            binding != expected_binding
            or not isinstance(binding["comparisonArtifactId"], int)
            or binding["comparisonArtifactId"] < 1
            or not re.fullmatch(
                r"sha256:[0-9a-f]{64}", binding["comparisonArtifactDigest"]
            )
            or not isinstance(attestation["authorizedOperators"], list)
            or not attestation["authorizedOperators"]
            or len(set(attestation["authorizedOperators"]))
            != len(attestation["authorizedOperators"])
        ):
            raise ValueError
    except (KeyError, TypeError, ValueError):
        _fail("PARITY_OWNER_APPROVAL_REQUIRED")

    if firmware_owner_verifier is None:
        _fail("PARITY_OWNER_APPROVAL_REQUIRED")
    try:
        observed = firmware_owner_verifier.verify(attestation, verification_record)
    except Exception:
        _fail("PARITY_OWNER_APPROVAL_REQUIRED")
    expected_verification = {
        "attestationId": authority["attestationId"],
        "firmwareOwnerId": authority["firmwareOwnerId"],
        "attestationSha256": _sha256(attestation_bytes),
        "verificationRecordSha256": _sha256(verification_record),
        **expected_binding,
        "authorizedOperators": attestation["authorizedOperators"],
    }
    if (
        not isinstance(observed, dict)
        or any(
            observed.get(key) != value
            for key, value in expected_verification.items()
        )
        or set(observed) != {*expected_verification, "verifiedAtUtc"}
    ):
        _fail("PARITY_OWNER_APPROVAL_REQUIRED")
    parse_canonical_utc(observed["verifiedAtUtc"])
    return observed


def finalize_evidence(finalize_path: Path, *, github_reader: Any, firmware_owner_verifier: Any) -> dict[str, Any]:
    finalize = _read_json_file(finalize_path, "PARITY_EVIDENCE_INCOMPLETE")
    comparison_path = _require_artifact_reference(finalize["comparison"], "comparison")
    attestation_path = _require_artifact_reference(finalize["firmwareOwnerAttestation"], "firmware-owner-attestation")
    verification_path = _require_artifact_reference(finalize["approvalAuthority"]["verificationRecord"], "verification-record")
    comparison_bytes, attestation_bytes = comparison_path.read_bytes(), attestation_path.read_bytes()
    comparison = load_json_reject_duplicates(comparison_bytes)
    attestation = load_json_reject_duplicates(attestation_bytes)
    validate_comparison_schema(comparison)
    declared = finalize["protectedRun"]
    repository = declared["repository"]
    workflow_head = declared["workflowCommitSha"]
    binding = attestation.get("binding", {})
    if (
        binding.get("comparisonArtifactId")
        != declared.get("comparisonArtifact", {}).get("id")
        or binding.get("comparisonArtifactDigest")
        != declared.get("comparisonArtifact", {}).get("digest")
    ):
        _fail("PARITY_AUTHORITY_MISMATCH")
    try:
        workflow_content = github_reader.get_workflow_content(repository, declared["workflowPath"], workflow_head)
        import base64
        workflow_bytes = base64.b64decode(workflow_content["content"], validate=True)
        if workflow_content.get("sha") != declared["workflowBlobSha"] or _git_blob_sha(workflow_bytes) != declared["workflowBlobSha"] or _sha256(workflow_bytes) != declared["workflowRawSha256"]:
            _fail("PARITY_AUTHORITY_MISMATCH")
        workflow_contract = _read_json_file(Path(__file__).resolve().parents[1] / "docs/contracts/v0916-parity-workflow-v1.json")
        workflow_contract_sha256 = _sha256(
            (
                Path(__file__).resolve().parents[1]
                / "docs/contracts/v0916-parity-workflow-v1.json"
            ).read_bytes()
        )
        if declared.get("workflowSemanticContractSha256") != workflow_contract_sha256:
            _fail("PARITY_AUTHORITY_MISMATCH")
        validate_protected_workflow_semantics(workflow_bytes, workflow_contract)
        expected_run = declared["workflowRun"]
        run = github_reader.get_workflow_run(repository, expected_run["id"])
        terminal_or_finalizing = (
            (run["status"] == "completed" and run["conclusion"] == "success")
            or (run["status"] == "in_progress" and run["conclusion"] is None)
        )
        stable_run = {
            "id": run.get("id"),
            "runAttempt": run.get("run_attempt"),
            "headSha": run.get("head_sha"),
            "headBranch": run.get("head_branch"),
            "event": run.get("event"),
            "repositoryId": run.get("repository", {}).get("id"),
            "headRepositoryId": run.get("head_repository", {}).get("id"),
            "createdAtUtc": run.get("created_at"),
        }
        expected_stable_run = {
            key: expected_run[key]
            for key in (
                "id",
                "runAttempt",
                "headSha",
                "headBranch",
                "event",
                "repositoryId",
                "headRepositoryId",
                "createdAtUtc",
            )
        }
        if (
            stable_run != expected_stable_run
            or run.get("repository", {}).get("full_name") != repository
            or run.get("head_repository", {}).get("full_name") != repository
            or run["head_sha"] != workflow_head
            or run["head_branch"] != "main"
            or not terminal_or_finalizing
            or parse_canonical_utc(run["updated_at"])
            < parse_canonical_utc(expected_run["updatedAtUtc"])
        ):
            _fail("PARITY_AUTHORITY_MISMATCH")
        expected_job = declared["attestationJob"]
        job = github_reader.get_workflow_job(repository, expected_job["id"])
        expected_job_raw = {
            "id": expected_job["id"], "run_id": expected_job["runId"], "run_attempt": expected_job["runAttempt"], "head_sha": expected_job["headSha"], "head_branch": expected_job["headBranch"],
            "name": expected_job["name"], "status": expected_job["status"], "conclusion": expected_job["conclusion"],
            "started_at": expected_job["startedAtUtc"], "completed_at": expected_job["completedAtUtc"], "html_url": expected_job["htmlUrl"],
        }
        if job != expected_job_raw or job["run_id"] != run["id"] or job["run_attempt"] != run["run_attempt"] or job["head_sha"] != workflow_head or job["head_branch"] != "main" or job["name"] != "release / v0.9.16 parity attestation" or job["status"] != "completed" or job["conclusion"] != "success":
            _fail("PARITY_AUTHORITY_MISMATCH")
        expected_deployment = declared["deployment"]
        deployment = github_reader.get_deployment(repository, expected_deployment["id"])
        expected_deployment_raw = {"id": expected_deployment["id"], "sha": expected_deployment["sha"], "ref": expected_deployment["ref"], "environment": expected_deployment["environment"], "created_at": expected_deployment["createdAtUtc"]}
        if deployment != expected_deployment_raw or deployment["sha"] != workflow_head or deployment["ref"] != "main" or deployment["environment"] != "firmware-parity":
            _fail("PARITY_AUTHORITY_MISMATCH")
        statuses = github_reader.get_deployment_statuses(repository, deployment["id"])
        expected_status = declared["deploymentStatus"]
        matches = [row for row in statuses if row.get("id") == expected_status["id"]]
        if len(matches) != 1:
            _fail("PARITY_AUTHORITY_MISMATCH")
        status = matches[0]
        expected_status_raw = {"id": expected_status["id"], "state": expected_status["state"], "created_at": expected_status["createdAtUtc"], "updated_at": expected_status["updatedAtUtc"], "log_url": expected_status["logUrl"], "creator": status.get("creator")}
        if status != expected_status_raw or status["state"] != "success" or status["log_url"] != job["html_url"]:
            _fail("PARITY_AUTHORITY_MISMATCH")
        downloaded: dict[str, bytes] = {}
        verification_bytes = verification_path.read_bytes()
        for key, local_bytes in (
            ("comparisonArtifact", comparison_bytes),
            ("attestationArtifact", attestation_bytes),
            ("verificationArtifact", verification_bytes),
        ):
            expected_artifact = declared[key]
            artifact = github_reader.get_artifact(repository, expected_artifact["id"])
            owner = artifact.get("workflow_run", {})
            if (
                artifact.get("id") != expected_artifact["id"] or artifact.get("name") != expected_artifact["name"]
                or artifact.get("digest") != expected_artifact["digest"] or artifact.get("expired") is not False
                or artifact.get("created_at") != expected_artifact["createdAtUtc"]
                or not _github_owner_matches(owner, run_id=run["id"], head=workflow_head, repository_id=run["repository"]["id"], head_repository_id=run["head_repository"]["id"])
            ):
                _fail("PARITY_AUTHORITY_MISMATCH")
            raw = _read_single_member_archive(github_reader.download_artifact(repository, artifact["id"]), artifact["digest"], expected_artifact["memberName"])
            if raw != local_bytes:
                _fail("PARITY_AUTHORITY_MISMATCH")
            downloaded[key] = raw
    except ParityError:
        raise
    except (OSError, KeyError, ValueError, TypeError):
        _fail("PARITY_AUTHORITY_MISMATCH")
    # Comparison precedes deployment; deployment precedes the protected job;
    # attestation is emitted by that job and status is terminal for that job.
    comparison_time = parse_canonical_utc(declared["comparisonArtifact"]["createdAtUtc"])
    deployment_time = parse_canonical_utc(declared["deployment"]["createdAtUtc"])
    job_start = parse_canonical_utc(declared["attestationJob"]["startedAtUtc"])
    attestation_time = parse_canonical_utc(declared["attestationArtifact"]["createdAtUtc"])
    job_end = parse_canonical_utc(declared["attestationJob"]["completedAtUtc"])
    status_time = parse_canonical_utc(declared["deploymentStatus"]["createdAtUtc"])
    if not (comparison_time < deployment_time <= job_start <= attestation_time <= job_end <= status_time):
        _fail("PARITY_AUTHORITY_MISMATCH")
    observed = validate_external_owner_material(
        comparison_bytes=comparison_bytes,
        attestation_bytes=attestation_bytes,
        verification_record=verification_path.read_bytes(),
        firmware_owner_verifier=firmware_owner_verifier,
    )
    evidence = copy.deepcopy(comparison)
    evidence["comparison"] = _artifact(comparison_path)
    evidence["verdict"] = "pass"
    evidence["protectedRun"] = copy.deepcopy(declared)
    evidence["firmwareOwnerApproval"] = {
        "attestation": _artifact(attestation_path),
        "authority": {
            "kind": "external-firmware-owner-verification",
            "verifierId": finalize["approvalAuthority"]["verifierId"],
            "verificationRecord": _artifact(verification_path),
            "attestationId": observed["attestationId"],
            "firmwareOwnerId": observed["firmwareOwnerId"],
            "verifiedAtUtc": observed["verifiedAtUtc"],
        },
    }
    return evidence


def validate_terminal_evidence(evidence: Mapping[str, Any]) -> None:
    expected_extra = {
        "comparison",
        "protectedRun",
        "firmwareOwnerApproval",
    }
    comparison_keys = {
        "schemaVersion",
        "planSha256",
        "policySha256",
        "comparator",
        "candidateAuthority",
        "baselineExecutor",
        "baselineReleaseReference",
        "candidatePackage",
        "candidateBuild",
        "routeEvidenceSha256",
        "receiptSetSha256",
        "executedAtUtc",
        "routes",
        "verdict",
    }
    if set(evidence) != comparison_keys | expected_extra or evidence.get("verdict") != "pass":
        _fail("PARITY_EVIDENCE_INCOMPLETE")
    provisional = {key: copy.deepcopy(evidence[key]) for key in comparison_keys}
    provisional["verdict"] = "provisional"
    validate_comparison_schema(provisional)
    for key in ("comparison",):
        reference = evidence[key]
        if (
            not isinstance(reference, dict)
            or set(reference) != {"size", "sha256"}
            or not isinstance(reference["size"], int)
            or reference["size"] < 1
            or not SHA256_RE.fullmatch(str(reference["sha256"]))
        ):
            _fail("PARITY_EVIDENCE_INCOMPLETE")
    approval = evidence["firmwareOwnerApproval"]
    try:
        if set(approval) != {"attestation", "authority"}:
            raise ValueError
        authority = approval["authority"]
        if set(authority) != {
            "kind",
            "verifierId",
            "verificationRecord",
            "attestationId",
            "firmwareOwnerId",
            "verifiedAtUtc",
        } or authority["kind"] != "external-firmware-owner-verification":
            raise ValueError
        parse_canonical_utc(authority["verifiedAtUtc"])
        for reference in (approval["attestation"], authority["verificationRecord"]):
            if (
                set(reference) != {"size", "sha256"}
                or not isinstance(reference["size"], int)
                or reference["size"] < 1
                or not SHA256_RE.fullmatch(str(reference["sha256"]))
            ):
                raise ValueError
    except (KeyError, TypeError, ValueError):
        _fail("PARITY_EVIDENCE_INCOMPLETE")


def path_is_reparse_point(path: Path) -> bool:
    try:
        return path.is_symlink() or bool(path.lstat().st_file_attributes & stat.FILE_ATTRIBUTE_REPARSE_POINT)
    except (AttributeError, OSError):
        return path.is_symlink()


class _DirectoryLease:
    def __init__(self, path: Path, handle: int | None = None):
        self.path, self._handle = path, handle

    def create_exclusive_file(self, relative: str):
        if not _safe_repo_path(relative) or "/" in relative:
            _fail("PARITY_WRITE_CONFLICT")
        return (self.path / relative).open("xb")

    def close(self) -> None:
        if self._handle is not None:
            import ctypes
            ctypes.windll.kernel32.CloseHandle(self._handle)
            self._handle = None

    def __enter__(self) -> "_DirectoryLease":
        return self

    def __exit__(self, *_: Any) -> None:
        self.close()


def acquire_controlled_directory_lease(destination: Path) -> _DirectoryLease:
    if destination.exists() or destination.is_symlink():
        _fail("PARITY_WRITE_CONFLICT")
    destination.parent.mkdir(parents=True, exist_ok=True)
    try:
        handle: int | None = None
        if os.name == "nt":
            import ctypes
            from ctypes import wintypes

            class UnicodeString(ctypes.Structure):
                _fields_ = [
                    ("Length", wintypes.USHORT),
                    ("MaximumLength", wintypes.USHORT),
                    ("Buffer", wintypes.LPWSTR),
                ]

            class ObjectAttributes(ctypes.Structure):
                _fields_ = [
                    ("Length", wintypes.ULONG),
                    ("RootDirectory", wintypes.HANDLE),
                    ("ObjectName", ctypes.POINTER(UnicodeString)),
                    ("Attributes", wintypes.ULONG),
                    ("SecurityDescriptor", wintypes.LPVOID),
                    ("SecurityQualityOfService", wintypes.LPVOID),
                ]

            class IoStatusBlock(ctypes.Structure):
                _fields_ = [
                    ("Status", wintypes.LPVOID),
                    ("Information", ctypes.c_size_t),
                ]

            absolute = str(destination.resolve(strict=False))
            native = "\\??\\UNC\\" + absolute[2:] if absolute.startswith("\\\\") else "\\??\\" + absolute
            buffer = ctypes.create_unicode_buffer(native)
            name = UnicodeString(
                len(native.encode("utf-16-le")),
                len(native.encode("utf-16-le")) + 2,
                ctypes.cast(buffer, wintypes.LPWSTR),
            )
            attributes = ObjectAttributes(
                ctypes.sizeof(ObjectAttributes),
                None,
                ctypes.pointer(name),
                0x00000040,  # OBJ_CASE_INSENSITIVE
                None,
                None,
            )
            status = IoStatusBlock()
            leased_handle = wintypes.HANDLE()
            nt_create_file = ctypes.windll.ntdll.NtCreateFile
            nt_create_file.restype = wintypes.LONG
            result = nt_create_file(
                ctypes.byref(leased_handle),
                0x00100181,  # SYNCHRONIZE | FILE_LIST_DIRECTORY | READ/WRITE_ATTRIBUTES
                ctypes.byref(attributes),
                ctypes.byref(status),
                None,
                0x00000080,  # FILE_ATTRIBUTE_NORMAL
                0x00000001 | 0x00000002,  # no FILE_SHARE_DELETE
                2,  # FILE_CREATE: create and fail if it already exists
                0x00000001 | 0x00000020 | 0x00200000,
                None,
                0,
            )
            if result < 0:
                raise OSError(f"NtCreateFile failed with NTSTATUS 0x{result & 0xffffffff:08x}")
            handle = leased_handle.value
        else:
            destination.mkdir()
        if path_is_reparse_point(destination):
            raise OSError("reparse staging directory")
        return _DirectoryLease(destination, handle)
    except Exception as error:
        shutil.rmtree(destination, ignore_errors=True)
        if isinstance(error, ParityError):
            raise
        _fail("PARITY_WRITE_CONFLICT", str(error))


def _default_classifier(path: Path) -> str:
    if path_is_reparse_point(path):
        return "reparse-point"
    if path.is_dir():
        return "directory"
    if path.exists():
        return "other"
    return "missing"


def prepare_extraction_destination(destination: Path, *, allowed_root: Path | None = None, path_classifier: Any = None) -> Path:
    classifier = path_classifier or _default_classifier
    if destination.exists() or classifier(destination) == "reparse-point":
        _fail("PARITY_WRITE_CONFLICT")
    if allowed_root is not None:
        try:
            destination.resolve(strict=False).relative_to(allowed_root.resolve(strict=True))
        except (OSError, ValueError):
            _fail("PARITY_WRITE_CONFLICT")
        current = destination.parent
        root = allowed_root.resolve(strict=True)
        while current.resolve(strict=False) != root:
            if classifier(current) == "reparse-point":
                _fail("PARITY_WRITE_CONFLICT")
            if current == current.parent:
                _fail("PARITY_WRITE_CONFLICT")
            current = current.parent
    destination.mkdir(parents=True)
    return destination


def prepare_capture_paths(capture_root: Path, route_id: str) -> dict[str, Path]:
    if not re.fullmatch(r"[A-Za-z0-9._-]+", route_id):
        _fail("PARITY_WRITE_CONFLICT")
    root = capture_root / route_id
    if root.exists():
        _fail("PARITY_WRITE_CONFLICT")
    root.mkdir()
    return {"root": root, "output": root / "output.bin", "report": root / "report.json"}


def write_json_exclusive_atomic(path: Path, value: Any) -> None:
    if path.exists() or path.is_symlink():
        _fail("PARITY_WRITE_CONFLICT")
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    try:
        with temporary.open("xb") as stream:
            stream.write(canonical_json_bytes(value) + b"\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    except FileExistsError:
        _fail("PARITY_WRITE_CONFLICT")
    finally:
        if temporary.exists():
            temporary.unlink()


@contextlib.contextmanager
def capture_workspace(output_root: Path, route_id: str) -> Iterator[Path]:
    if not output_root.is_dir() or not re.fullmatch(r"[A-Za-z0-9._-]+", route_id):
        _fail("PARITY_WRITE_CONFLICT")
    workspace = Path(tempfile.mkdtemp(prefix=f"{route_id}-", dir=output_root))
    try:
        yield workspace
    finally:
        shutil.rmtree(workspace, ignore_errors=True)


def extract_verified_github_artifact(stream: Any, *, github_digest: str, destination: Path, allowed_entries: set[str], max_entry_bytes: int, max_total_bytes: int, max_total_compressed_bytes: int, max_compression_ratio: int) -> dict[str, Path]:
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", github_digest):
        _fail("PARITY_PACKAGE_MISMATCH")
    return _extract_verified_github_artifact_payload(
        stream,
        github_digest=github_digest,
        destination=destination,
        allowed_entries=allowed_entries,
        max_entry_bytes=max_entry_bytes,
        max_total_bytes=max_total_bytes,
        max_total_compressed_bytes=max_total_compressed_bytes,
        max_compression_ratio=max_compression_ratio,
    )


def discover_candidate_build_declaration(
    *,
    repository_root: Path,
    candidate_artifact_dir: Path,
    candidate_source_identity_sha256: str,
    github_reader: Any,
    repository: str,
    run_id: int,
    workflow_sha: str,
) -> tuple[dict[str, Path], dict[str, Any], dict[str, Any]]:
    """Bind the downloaded six-file candidate surface to its same-run artifact."""

    if (
        repository != "Dennis40816/nvt_fw_combiner"
        or run_id < 1
        or not SHA1_RE.fullmatch(workflow_sha)
        or not SHA256_RE.fullmatch(candidate_source_identity_sha256)
        or not candidate_artifact_dir.is_dir()
        or candidate_artifact_dir.is_symlink()
    ):
        _fail("PARITY_AUTHORITY_MISMATCH")
    files = [
        path
        for path in candidate_artifact_dir.iterdir()
        if path.is_file() and not path.is_symlink()
    ]
    manifests = [
        path
        for path in files
        if re.fullmatch(r"NvtFwCombiner-v1\.0\.0-candidate\.json", path.name)
    ]
    if len(files) != 6 or len(manifests) != 1:
        _fail("PARITY_PACKAGE_MISMATCH")
    manifest_path = manifests[0]
    manifest = _read_json_file(manifest_path, "PARITY_PACKAGE_MISMATCH")
    try:
        version = manifest["version"]
        package_source_head = manifest["sourceSha"]
        package_source_tree = manifest["sourceTree"]
        if (
            version != "1.0.0"
            or manifest["tag"] != "v1.0.0"
            or manifest["candidateRunId"] != str(run_id)
            or manifest["workflowSha"] != workflow_sha
            or manifest["workflowRef"] != "refs/heads/main"
            or not SHA1_RE.fullmatch(package_source_head)
            or not SHA1_RE.fullmatch(package_source_tree)
        ):
            raise ValueError
        payload_assets = manifest["assets"]
        if not isinstance(payload_assets, list) or len(payload_assets) != 3:
            raise ValueError
        by_name = {row["name"]: row for row in payload_assets}
        if len(by_name) != 3:
            raise ValueError
        package_name = f"NvtFwCombiner-v{version}-win-x64.zip"
        sbom_name = f"NvtFwCombiner-v{version}-win-x64.spdx.json"
        provenance_name = f"NvtFwCombiner-v{version}-win-x64.provenance.json"
        notes_name = manifest["releaseNotes"]["name"]
        checksums_name = f"NvtFwCombiner-v{version}-assets.sha256"
        expected_names = {
            package_name,
            sbom_name,
            provenance_name,
            notes_name,
            manifest_path.name,
            checksums_name,
        }
        if {path.name for path in files} != expected_names:
            raise ValueError
    except (KeyError, TypeError, ValueError):
        _fail("PARITY_PACKAGE_MISMATCH")
    paths = {path.name: path for path in files}
    local_assets = {
        "package": paths[package_name],
        "sbom": paths[sbom_name],
        "provenance": paths[provenance_name],
        "notes": paths[notes_name],
        "manifest": manifest_path,
        "checksums": paths[checksums_name],
    }
    for name, declared in by_name.items():
        path = paths.get(name)
        if (
            path is None
            or declared.get("size") != path.stat().st_size
            or declared.get("sha256") != _sha256(path.read_bytes())
        ):
            _fail("PARITY_PACKAGE_MISMATCH")
    notes = manifest.get("releaseNotes", {})
    if (
        notes.get("size") != local_assets["notes"].stat().st_size
        or notes.get("sha256") != _sha256(local_assets["notes"].read_bytes())
    ):
        _fail("PARITY_PACKAGE_MISMATCH")

    artifact_name = f"stable-candidate-{run_id}-{package_source_head}"
    try:
        run = github_reader.get_workflow_run(repository, run_id)
        matches = [
            row
            for row in github_reader.list_run_artifacts(repository, run_id)
            if row.get("name") == artifact_name
        ]
        if len(matches) != 1:
            _fail("PARITY_AUTHORITY_MISMATCH")
        artifact = github_reader.get_artifact(repository, int(matches[0]["id"]))
        content = github_reader.get_workflow_content(
            repository, ".github/workflows/release.yml", workflow_sha
        )
        import base64

        workflow_bytes = base64.b64decode(content["content"], validate=True)
    except ParityError:
        raise
    except (OSError, KeyError, TypeError, ValueError):
        _fail("PARITY_AUTHORITY_MISMATCH")
    if (
        run.get("id") != run_id
        or run.get("head_sha") != workflow_sha
        or run.get("head_branch") != "main"
        or run.get("repository", {}).get("full_name") != repository
        or run.get("head_repository", {}).get("full_name") != repository
        or not (
            (run.get("status") == "completed" and run.get("conclusion") == "success")
            or (run.get("status") == "in_progress" and run.get("conclusion") is None)
        )
        or artifact.get("id") != matches[0].get("id")
        or artifact.get("name") != artifact_name
        or not re.fullmatch(r"sha256:[0-9a-f]{64}", str(artifact.get("digest", "")))
        or artifact.get("expired") is not False
        or not _github_owner_matches(
            artifact.get("workflow_run", {}),
            run_id=run_id,
            head=workflow_sha,
            repository_id=run.get("repository", {}).get("id"),
            head_repository_id=run.get("head_repository", {}).get("id"),
        )
        or content.get("type") != "file"
        or content.get("path") != ".github/workflows/release.yml"
        or content.get("encoding") != "base64"
        or content.get("sha") != _git_blob_sha(workflow_bytes)
    ):
        _fail("PARITY_AUTHORITY_MISMATCH")
    provenance = _read_json_file(
        local_assets["provenance"], "PARITY_PACKAGE_MISMATCH"
    )
    declared = {
        "repository": repository,
        "workflowPath": ".github/workflows/release.yml",
        "workflowRef": "refs/heads/main",
        "workflowCommitSha": workflow_sha,
        "workflowBlobSha": content["sha"],
        "workflowRawSha256": _sha256(workflow_bytes),
        "workflowSemanticContractSha256": _sha256(
            (
                repository_root
                / "docs/contracts/v0916-parity-workflow-v1.json"
            ).read_bytes()
        ),
        "runId": run_id,
        "artifactId": artifact["id"],
        "artifactName": artifact_name,
        "artifactDigest": artifact["digest"],
        "candidateManifest": _artifact(local_assets["manifest"]),
        "candidateSbom": _artifact(local_assets["sbom"]),
        "candidateProvenance": _artifact(local_assets["provenance"]),
        "releaseNotes": _artifact(local_assets["notes"]),
        "assetChecksums": _artifact(local_assets["checksums"]),
        "candidateSourceExecutorIdentitySha256": candidate_source_identity_sha256,
        "provenanceSubjectsSha256": canonical_provenance_subjects_sha256(
            provenance.get("subjects", [])
        ),
        "candidateVerifierSha256": _sha256(
            (repository_root / "scripts/release_promotion_policy.py").read_bytes()
        ),
        "packageVerifierSha256": _sha256(
            (repository_root / "scripts/smoke-release.ps1").read_bytes()
        ),
    }
    package = {
        "name": package_name,
        **_artifact(local_assets["package"]),
        "version": version,
        "sourceCommit": package_source_head,
    }
    return local_assets, declared, {
        "manifest": manifest,
        "package": package,
        "packageSourceHead": package_source_head,
        "packageSourceTree": package_source_tree,
    }


def _extract_verified_github_artifact_payload(
    stream: Any,
    *,
    github_digest: str,
    destination: Path,
    allowed_entries: set[str],
    max_entry_bytes: int,
    max_total_bytes: int,
    max_total_compressed_bytes: int,
    max_compression_ratio: int,
) -> dict[str, Path]:
    chunks = []
    while True:
        chunk = stream.read(65536)
        if not chunk:
            break
        chunks.append(chunk)
    archive_bytes = b"".join(chunks)
    if _sha256(archive_bytes) != github_digest.removeprefix("sha256:"):
        _fail("PARITY_PACKAGE_MISMATCH")
    try:
        with zipfile.ZipFile(__import__("io").BytesIO(archive_bytes)) as archive:
            infos = archive.infolist()
            names = [info.filename for info in infos]
            if len(names) != len({name.casefold() for name in names}) or set(names) != allowed_entries:
                _fail("PARITY_PACKAGE_MISMATCH")
            if sum(info.file_size for info in infos) > max_total_bytes or sum(info.compress_size for info in infos) > max_total_compressed_bytes:
                _fail("PARITY_PACKAGE_MISMATCH")
            for info in infos:
                path = PurePosixPath(info.filename.replace("\\", "/"))
                unix_mode = info.external_attr >> 16
                if (not _safe_repo_path(path.as_posix()) or len(path.parts) != 1 or ":" in info.filename or info.file_size > max_entry_bytes or (info.compress_size == 0 and info.file_size > 0) or (info.compress_size and info.file_size / info.compress_size > max_compression_ratio) or stat.S_ISLNK(unix_mode)):
                    _fail("PARITY_PACKAGE_MISMATCH")
            prepare_extraction_destination(destination)
            extracted: dict[str, Path] = {}
            try:
                for info in infos:
                    target = destination / info.filename
                    with target.open("xb") as output:
                        with archive.open(info) as source:
                            while chunk := source.read(65536):
                                output.write(chunk)
                    extracted[info.filename] = target
                return extracted
            except Exception:
                shutil.rmtree(destination, ignore_errors=True)
                raise
    except ParityError:
        raise
    except (OSError, ValueError, RuntimeError, zipfile.BadZipFile):
        if destination.exists():
            shutil.rmtree(destination, ignore_errors=True)
        _fail("PARITY_PACKAGE_MISMATCH")


def _compare_command(args: argparse.Namespace) -> int:
    repository_root = Path(__file__).resolve().parents[1]
    plan_path = Path(args.plan).resolve(strict=True)
    plan = load_and_validate_plan(
        plan_path,
        repository_root / "docs/contracts/canonical-capability-policy-v1.json",
    )
    output_root = Path(args.output_root).resolve(strict=False)
    if output_root.exists() and any(output_root.iterdir()):
        _fail("PARITY_WRITE_CONFLICT")
    output_root.mkdir(parents=True, exist_ok=True)
    candidate_artifact_dir = Path(args.candidate_artifact_dir).resolve(strict=False)

    # Canonical firmware bytes, admitted copies, worktrees, reports, receipts,
    # and outputs stay outside the uploaded payload-free output directory.
    with controlled_temporary_directory("nfc-v0916-run-") as temporary_root:
        snapshot = temporary_root / "canonical-authority"
        materialize_and_validate_canonical_input_authority(
            plan.raw,
            git_reader=PinnedGitReader(repository_root),
            destination=snapshot,
        )
        manifest_path = snapshot / plan.raw["canonicalInputAuthority"]["manifestPath"]
        canonical_inputs = resolve_all_canonical_route_inputs(plan, manifest_path)
        build_required_execution_matrix(plan, canonical_inputs=canonical_inputs)
        operator_login = os.environ.get("GITHUB_ACTOR", "")
        repository = os.environ.get("GITHUB_REPOSITORY", "")
        workflow_sha = os.environ.get("GITHUB_WORKFLOW_SHA", "")
        try:
            run_id = int(os.environ.get("GITHUB_RUN_ID", ""))
        except ValueError:
            _fail("PARITY_AUTHORITY_MISMATCH")

        candidate_contract_path = (
            repository_root
            / plan.raw["candidateAuthority"]["sourceExecutorContract"]["path"]
        )
        candidate_contract = load_and_validate_candidate_source_executor_contract(
            candidate_contract_path,
            plan.raw["candidateAuthority"]["sourceExecutorContract"],
        )
        baseline_contract_path = (
            repository_root / plan.raw["baseline"]["executorContract"]["path"]
        )
        baseline_authority = load_baseline_executor_authority(
            plan.raw, baseline_contract_path
        )
        github_reader = GhCliProtectedApprovalReader()
        host = LocalExecutionHost()
        local_assets, candidate_build_declared, candidate_asset_identity = (
            discover_candidate_build_declaration(
                repository_root=repository_root,
                candidate_artifact_dir=candidate_artifact_dir,
                candidate_source_identity_sha256=candidate_contract.identity_sha256,
                github_reader=github_reader,
                repository=repository,
                run_id=run_id,
                workflow_sha=workflow_sha,
            )
        )
        authority_transfer = validate_repository_parity_authority_transfer(
            repository_root,
            head=candidate_asset_identity["packageSourceHead"],
        )
        if (
            authority_transfer["implementationHead"]
            != candidate_contract.contract["source"]["implementationHead"]
            or authority_transfer["bindingHead"]
            != candidate_asset_identity["packageSourceHead"]
        ):
            _fail("PARITY_AUTHORITY_MISMATCH")
        workflow_semantic_contract = _read_json_file(
            repository_root
            / plan.raw["candidateAuthority"]["protectedBuild"][
                "workflowSemanticContract"
            ]["path"],
            "PARITY_AUTHORITY_MISMATCH",
        )
        package_proof = verify_protected_candidate_build(
            repository_root=repository_root,
            local_assets=local_assets,
            declared=candidate_build_declared,
            firmware_executor_head=candidate_contract.contract["source"][
                "implementationHead"
            ],
            firmware_executor_tree=candidate_contract.contract["source"][
                "implementationTree"
            ],
            package_source_head=candidate_asset_identity["packageSourceHead"],
            package_source_tree=candidate_asset_identity["packageSourceTree"],
            process_runner=host,
            github_reader=github_reader,
            artifact_download_root=temporary_root / "candidate-artifact-proof",
            workflow_semantic_contract=workflow_semantic_contract,
        )

        baseline_commit = baseline_authority.contract["source"]["peeledCommit"]
        candidate_commit = candidate_contract.contract["source"][
            "implementationHead"
        ]
        with detached_git_worktree(
            repository_root, baseline_commit, temporary_root, "baseline-source"
        ) as baseline_root, detached_git_worktree(
            repository_root, candidate_commit, temporary_root, "candidate-source"
        ) as candidate_root:
            baseline_executor = verify_source_baseline_executor(
                baseline_root,
                baseline_authority.contract,
                host,
                executor_identity_sha256=baseline_authority.identity_sha256,
            )
            candidate_executor = verify_candidate_source_executor(
                candidate_root, candidate_contract, host
            )
            receipts: dict[tuple[str, str], dict[str, Any]] = {}

            class CanonicalInputPort:
                def resolve(self, item: ExecutionRequirement) -> VerifiedCanonicalInputs:
                    return resolve_canonical_route_input(
                        plan,
                        manifest_path,
                        admitted_input_root=temporary_root / "admitted-inputs",
                        route_id=item.route_id,
                        execution_role=item.role,
                    )

            def capture(
                item: ExecutionRequirement, verified: VerifiedCanonicalInputs
            ) -> dict[str, Any]:
                executor = (
                    baseline_executor
                    if item.role == "baseline-exact"
                    else candidate_executor
                )
                capture_root = (
                    temporary_root
                    / "captures"
                    / hashlib.sha256(
                        f"{item.role}:{item.route_id}".encode("utf-8")
                    ).hexdigest()[:20]
                )
                process_capture = execute_cli_capture(
                    verified,
                    verified_executor=executor,
                    output_root=capture_root,
                    process_runner=host,
                )
                receipt = build_process_receipt(
                    capture=process_capture,
                    verified_inputs=verified,
                    verified_executor=executor,
                    operator_login=operator_login,
                    receipt_root=temporary_root / "receipts",
                    comparator_path=Path(__file__).resolve(),
                )
                receipts[(item.role, item.route_id)] = receipt
                return receipt

            captured = capture_required_execution_matrix(
                plan,
                canonical_input_port=CanonicalInputPort(),
                capture=capture,
            )
            if len(captured) != 117 or len(receipts) != 117:
                _fail("PARITY_EVIDENCE_INCOMPLETE")

            route_by_id = {route.route_id: route for route in plan.routes}
            exact_rows: dict[str, dict[str, Any]] = {}
            for route in plan.routes:
                if route.proof_kind != "exact-output":
                    continue
                exact_rows[route.route_id] = build_exact_route_evidence(
                    plan=plan,
                    route=route,
                    baseline_receipt=receipts[("baseline-exact", route.route_id)],
                    candidate_receipt=receipts[("candidate-exact", route.route_id)],
                )
            rows: list[dict[str, Any]] = []
            for route in plan.routes:
                if route.proof_kind == "exact-output":
                    rows.append(exact_rows[route.route_id])
                    continue
                full_route = route_by_id.get(route.full_route_id or "")
                full_evidence = exact_rows.get(route.full_route_id or "")
                if full_route is None or full_evidence is None:
                    _fail("PARITY_PLAN_INVALID")
                rows.append(
                    build_transitive_route_evidence(
                        route=route,
                        full_route=full_route,
                        full_evidence=full_evidence,
                        baseline_full_receipt=receipts[
                            ("baseline-exact", full_route.route_id)
                        ],
                        candidate_full_receipt=receipts[
                            ("candidate-exact", full_route.route_id)
                        ],
                        candidate_tp_receipt=receipts[
                            ("candidate-tp", route.route_id)
                        ],
                    )
                )
            validate_evidence_route_coverage(plan, rows)
            candidate_receipt_identities = [
                receipt["executorIdentitySha256"]
                for (role, _), receipt in receipts.items()
                if role.startswith("candidate-")
            ]
            candidate_build = {
                key: copy.deepcopy(candidate_build_declared[key])
                for key in (
                    "repository",
                    "workflowPath",
                    "workflowRef",
                    "workflowCommitSha",
                    "workflowBlobSha",
                    "workflowRawSha256",
                    "workflowSemanticContractSha256",
                    "runId",
                    "artifactId",
                    "artifactName",
                    "artifactDigest",
                    "candidateManifest",
                    "candidateSourceExecutorIdentitySha256",
                    "provenanceSubjectsSha256",
                    "candidateVerifierSha256",
                    "packageVerifierSha256",
                )
            }
            candidate_build["artifactWorkflowRun"] = package_proof[
                "artifactWorkflowRun"
            ]
            validate_candidate_source_executor_identity(
                candidate_authority={
                    "implementationHead": candidate_executor.source_head,
                    "implementationTree": candidate_executor.source_tree,
                    "sourceExecutorContract": {
                        "sha256": candidate_contract.identity_sha256
                    },
                },
                candidate_source_contract=candidate_contract.contract,
                candidate_build=candidate_build,
                receipt_executor_identities=candidate_receipt_identities,
                comparison_identity_sha256=candidate_contract.identity_sha256,
                evidence_identity_sha256=candidate_contract.identity_sha256,
            )
            receipt_set = [
                {
                    "routeId": row["routeId"],
                    "role": receipt["role"],
                    "receiptSha256": receipt["receiptSha256"],
                }
                for row in rows
                for receipt in row["receipts"]
            ]
            executed_at = dt.datetime.now(dt.timezone.utc).replace(
                microsecond=0
            ).isoformat().replace("+00:00", "Z")
            source = candidate_contract.contract["source"]
            comparison = {
                "schemaVersion": "1.0",
                "planSha256": _sha256(plan_path.read_bytes()),
                "policySha256": plan.raw["policyBinding"]["sha256"],
                "comparator": comparator_identity(Path(__file__).resolve()),
                "candidateAuthority": {
                    "implementationHead": source["implementationHead"],
                    "implementationTree": source["implementationTree"],
                    "authorityTrees": copy.deepcopy(source["authorityTrees"]),
                    "policySha256": plan.raw["policyBinding"]["sha256"],
                    "sourceExecutorContract": {
                        "size": candidate_contract_path.stat().st_size,
                        "sha256": candidate_contract.identity_sha256,
                    },
                    "authorityTransfer": copy.deepcopy(
                        plan.raw["candidateAuthority"]["authorityTransfer"]
                    ),
                },
                "baselineExecutor": {
                    "kind": baseline_executor.kind,
                    "tagObject": plan.raw["baseline"]["tagObject"],
                    "peeledCommit": baseline_executor.source_head,
                    "sourceTree": baseline_executor.source_tree,
                    "resolvedSdkVersion": baseline_authority.contract["toolchain"][
                        "resolvedSdkVersion"
                    ],
                    "contract": {
                        "size": baseline_contract_path.stat().st_size,
                        "sha256": baseline_authority.identity_sha256,
                    },
                    "cliAssembly": {
                        "size": baseline_executor.cli_size,
                        "sha256": baseline_executor.cli_sha256,
                    },
                },
                "baselineReleaseReference": {
                    "name": plan.raw["baseline"]["releaseReference"]["packageName"],
                    "size": plan.raw["baseline"]["releaseReference"]["packageSize"],
                    "sha256": plan.raw["baseline"]["releaseReference"]["packageSha256"],
                    "purpose": plan.raw["baseline"]["releaseReference"]["purpose"],
                },
                "candidatePackage": candidate_asset_identity["package"],
                "candidateBuild": candidate_build,
                "routeEvidenceSha256": canonical_route_evidence_sha256(rows),
                "receiptSetSha256": canonical_receipt_set_sha256(receipt_set),
                "executedAtUtc": executed_at,
                "routes": rows,
                "verdict": "provisional",
            }
            validate_comparison_schema(comparison)
            comparison_path = output_root / "comparison.json"
            write_json_exclusive_atomic(comparison_path, comparison)
            print(str(comparison_path))
            return 0


def _verify_candidate_command(args: argparse.Namespace) -> int:
    repository_root = Path(__file__).resolve().parents[1]
    run = _read_json_file(Path(args.run).resolve(strict=True), "PARITY_PLAN_INVALID")
    try:
        if run["schemaVersion"] != "1.0":
            raise ValueError
        candidate_authority = run["candidateAuthority"]
        candidate_build = run["candidateBuild"]
        source_reference = candidate_authority["sourceExecutorContract"]
        source_contract_path = _require_artifact_reference(
            source_reference, "candidate-source-executor-contract"
        )
        source_contract = load_and_validate_candidate_source_executor_contract(
            source_contract_path,
            {"size": source_reference["size"], "sha256": source_reference["sha256"]},
        )
        source = source_contract.contract["source"]
        if (
            candidate_authority["implementationHead"] != source["implementationHead"]
            or candidate_authority["implementationTree"] != source["implementationTree"]
            or candidate_authority["authorityTrees"] != source["authorityTrees"]
            or candidate_build["candidateSourceExecutorIdentitySha256"]
            != source_contract.identity_sha256
        ):
            raise ValueError
        manifest_path = _require_artifact_reference(
            candidate_build["candidateManifest"], "candidate-manifest"
        )
        manifest = _read_json_file(manifest_path, "PARITY_PACKAGE_MISMATCH")
        local_assets = {
            "package": require_local_artifact(
                Path(run["candidatePackage"]), "candidate-package"
            ),
            "sbom": _require_artifact_reference(
                candidate_build["candidateSbom"], "candidate-sbom"
            ),
            "provenance": _require_artifact_reference(
                candidate_build["candidateProvenance"], "candidate-provenance"
            ),
            "notes": _require_artifact_reference(
                candidate_build["releaseNotes"], "release-notes"
            ),
            "manifest": manifest_path,
            "checksums": _require_artifact_reference(
                candidate_build["assetChecksums"], "asset-checksums"
            ),
        }
        output_root = Path(run["outputRoot"]).resolve(strict=False)
        if output_root.exists() and any(output_root.iterdir()):
            _fail("PARITY_WRITE_CONFLICT")
        package_source_head = manifest["sourceSha"]
        package_source_tree = manifest["sourceTree"]
    except ParityError:
        raise
    except (KeyError, TypeError, ValueError, OSError):
        _fail("PARITY_PLAN_INVALID")
    workflow_contract = _read_json_file(
        repository_root / "docs/contracts/v0916-parity-workflow-v1.json",
        "PARITY_AUTHORITY_MISMATCH",
    )
    authority_transfer = validate_repository_parity_authority_transfer(
        repository_root,
        head=package_source_head,
    )
    if (
        authority_transfer["implementationHead"] != source["implementationHead"]
        or authority_transfer["bindingHead"] != package_source_head
    ):
        _fail("PARITY_AUTHORITY_MISMATCH")
    verify_protected_candidate_build(
        repository_root=repository_root,
        local_assets=local_assets,
        declared=candidate_build,
        firmware_executor_head=source["implementationHead"],
        firmware_executor_tree=source["implementationTree"],
        package_source_head=package_source_head,
        package_source_tree=package_source_tree,
        process_runner=LocalExecutionHost(),
        github_reader=GhCliProtectedApprovalReader(),
        artifact_download_root=output_root / "candidate-artifact-proof",
        workflow_semantic_contract=workflow_contract,
    )
    print("canonical candidate manifest verified")
    print("canonical release smoke verified")
    return 0


def _validate_owner_material_command(args: argparse.Namespace) -> int:
    comparison_path = Path(args.comparison).resolve(strict=True)
    attestation_path = Path(args.attestation).resolve(strict=True)
    verification_path = Path(args.verification_record).resolve(strict=True)
    validate_external_owner_material(
        comparison_bytes=comparison_path.read_bytes(),
        attestation_bytes=attestation_path.read_bytes(),
        verification_record=verification_path.read_bytes(),
        firmware_owner_verifier=ExternalJsonFirmwareOwnerVerifier(args.verifier_id),
    )
    print("external firmware-owner material validated")
    return 0


def _finalize_protected_command(args: argparse.Namespace) -> int:
    comparison_path = Path(args.comparison).resolve(strict=True)
    attestation_path = Path(args.attestation).resolve(strict=True)
    verification_path = Path(args.verification_record).resolve(strict=True)
    output = Path(args.output).resolve(strict=False)
    if output.exists() or output.is_symlink():
        _fail("PARITY_WRITE_CONFLICT")
    reader = GhCliProtectedApprovalReader()
    protected_run = discover_protected_run(
        reader=reader,
        repository=args.repository,
        run_id=int(args.run_id),
        workflow_sha=args.workflow_sha,
    )
    request = {
        "schemaVersion": "1.0",
        "comparison": _local_artifact(comparison_path),
        "firmwareOwnerAttestation": _local_artifact(attestation_path),
        "protectedRun": protected_run,
        "approvalAuthority": {
            "kind": "external-firmware-owner-verification",
            "verifierId": args.verifier_id,
            "verificationRecord": _local_artifact(verification_path),
        },
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix="nfc-v0916-finalize-", dir=output.parent
    ) as temporary:
        request_path = Path(temporary) / "finalize.json"
        write_json_exclusive_atomic(request_path, request)
        evidence = finalize_evidence(
            request_path,
            github_reader=reader,
            firmware_owner_verifier=ExternalJsonFirmwareOwnerVerifier(
                args.verifier_id
            ),
        )
    validate_terminal_evidence(evidence)
    write_json_exclusive_atomic(output, evidence)
    print(str(output))
    return 0


def _validate_terminal_command(args: argparse.Namespace) -> int:
    evidence_path = Path(args.evidence).resolve(strict=True)
    evidence = _read_json_file(evidence_path, "PARITY_EVIDENCE_INCOMPLETE")
    validate_terminal_evidence(evidence)
    print("terminal v0.9.16 parity evidence validated")
    return 0


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)
    compare = subparsers.add_parser("compare")
    compare.add_argument("--plan", required=True)
    compare.add_argument("--candidate-artifact-dir", required=True)
    compare.add_argument("--output-root", required=True)
    compare.set_defaults(handler=_compare_command)
    verify = subparsers.add_parser("verify-candidate")
    verify.add_argument("--run", required=True)
    verify.set_defaults(handler=_verify_candidate_command)
    owner = subparsers.add_parser("validate-owner-material")
    owner.add_argument("--comparison", required=True)
    owner.add_argument("--attestation", required=True)
    owner.add_argument("--verification-record", required=True)
    owner.add_argument("--verifier-id", required=True)
    owner.set_defaults(handler=_validate_owner_material_command)
    finalize = subparsers.add_parser("finalize-protected")
    finalize.add_argument("--comparison", required=True)
    finalize.add_argument("--attestation", required=True)
    finalize.add_argument("--verification-record", required=True)
    finalize.add_argument("--verifier-id", required=True)
    finalize.add_argument("--repository", required=True)
    finalize.add_argument("--run-id", required=True)
    finalize.add_argument("--workflow-sha", required=True)
    finalize.add_argument("--output", required=True)
    finalize.set_defaults(handler=_finalize_protected_command)
    terminal = subparsers.add_parser("validate-terminal")
    terminal.add_argument("--evidence", required=True)
    terminal.set_defaults(handler=_validate_terminal_command)
    args = parser.parse_args(argv)
    handler = getattr(args, "handler", None)
    if handler is None:
        _fail("PARITY_EVIDENCE_INCOMPLETE", "candidate verification requires an admitted run adapter")
    return handler(args)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ParityError as error:
        print(f"{error.code}: {error}", file=os.sys.stderr)
        raise SystemExit(2)
