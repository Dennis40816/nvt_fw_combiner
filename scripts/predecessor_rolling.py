"""Rolling orchestration for ADR 0078, using the B1 execution and validation owners."""

from __future__ import annotations

import argparse
from dataclasses import asdict, dataclass
import hashlib
import json
from pathlib import Path, PurePosixPath
import subprocess
import sys
import tempfile
from typing import Any, Callable, Mapping, NamedTuple, Protocol, Sequence

from scripts import predecessor_comparison as execution
from scripts import predecessor_validation as validation
from scripts.predecessor_comparison import ScenarioExecution, measured_scopes, informational_differences
from scripts.v0916_parity_certification import (
    MaterializedCanonicalAuthority, ParityError,
    load_json_reject_duplicates,
    materialize_and_validate_canonical_input_authority, resolve_case, write_json_exclusive_atomic,
)


CONTRACT = "docs/contracts/predecessor-comparison-v1.json"
LEDGER = "docs/contracts/predecessor-comparison-scenarios-v1.json"
POLICY = "docs/contracts/canonical-capability-policy-v1.json"
PLAN = "docs/contracts/v0916-parity-certification-v1.json"
INVENTORY_SCHEMA = "docs/contracts/predecessor-published-release-inventory-v1.schema.json"
APPLIED_CONTRACTS = (CONTRACT, CONTRACT.replace(".json", ".schema.json"),
                     "docs/contracts/predecessor-comparison-report-v1.schema.json",
                     "docs/contracts/predecessor-comparison-declaration-v1.schema.json",
                     "docs/contracts/predecessor-comparison-scenarios-v1.schema.json", INVENTORY_SCHEMA, PLAN, POLICY)


def sha256(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


class PublishedReleaseHost(Protocol):
    """Supply the complete published stable inventory; None means unavailable.

    This module never uses the network. A partial inventory must not be returned.
    """
    def complete_published_stable_tags(self) -> Sequence[str] | None: ...
    def report_identity(self) -> Mapping[str, str]: ...


@dataclass(frozen=True)
class FilePublishedReleaseInventory:
    """Read the commander's file; the comparator never acquires publication facts."""

    inventory: Mapping[str, Any]
    identity: Mapping[str, str]

    @classmethod
    def read(cls, path: Path) -> FilePublishedReleaseInventory:
        try:
            payload = path.read_bytes()
            inventory = load_json_reject_duplicates(payload)
            execution._refuse(validation.published_inventory_failures(inventory))
            return cls(inventory, validation.published_inventory_identity(payload, inventory))
        except execution.ExecutionError:
            raise
        except (OSError, ParityError, ValueError, TypeError) as error:
            raise execution.ExecutionError("PREDECESSOR_BASELINE_INVALID", "invalid published release inventory") from error

    def complete_published_stable_tags(self) -> list[str]:
        return [row["tag"] for row in self.inventory["releases"]]

    def report_identity(self) -> Mapping[str, str]:
        return self.identity


class RollingGitHost(execution.GitHost, Protocol):
    def snapshot_reader(self, commit: str) -> Any: ...
    def commit_tree(self, commit: str) -> str: ...
    def stable_tags(self, candidate_commit: str) -> Sequence[validation.RollingTag]: ...


class LocalRollingGitHost(execution.LocalGitHost):
    """Read local tag facts and compose the existing pinned snapshot reader."""

    def commit_tree(self, commit: str) -> str:
        return self._git("rev-parse", "--verify", f"{commit}^{{tree}}")

    def stable_tags(self, candidate_commit: str) -> list[validation.RollingTag]:
        rows = self._git("for-each-ref", "--format=%(refname:strip=2) %(objecttype) %(objectname)", "refs/tags")
        tags = []
        for line in rows.splitlines():
            tag, kind, oid = line.split()
            # Non-stable tags are outside the contract, including release candidates.
            if not validation.stable_tag_version(tag):
                continue
            if kind == "tag":
                oid = self.git_tag_object(f"refs/tags/{tag}")
            commit = self.git_tag_commit(oid)
            ancestor = subprocess.run(["git", "merge-base", "--is-ancestor", commit, candidate_commit],
                                      cwd=self.repository, capture_output=True, check=False)
            if ancestor.returncode not in (0, 1):
                raise execution.ExecutionError("PREDECESSOR_BASELINE_INVALID", "cannot check baseline ancestry")
            tags.append(validation.RollingTag(tag, kind, oid, commit, ancestor.returncode == 0, None))
        return tags


def resolve_rolling_baseline(
    git: RollingGitHost, candidate_commit: str, candidate_version: str, *, baseline_tag: str | None,
    formal: bool, published: PublishedReleaseHost | None,
) -> validation.RollingTag:
    try:
        tags = list(git.stable_tags(candidate_commit))
        inventory = published.complete_published_stable_tags() if formal and published is not None else None
    except (OSError, subprocess.SubprocessError, ValueError) as error:
        raise execution.ExecutionError("PREDECESSOR_BASELINE_INVALID", "baseline host acquisition failed") from error
    baseline, failures = validation.rolling_baseline(tags, candidate_version, given_tag=baseline_tag, formal=formal,
                                                     published_tags=inventory)
    execution._refuse(failures)
    assert baseline is not None
    return baseline


@dataclass(frozen=True)
class Approval:
    boardDecision: str
    role: str
    date: str


@dataclass(frozen=True)
class ArtifactIdentity:
    size: int
    sha256: str


@dataclass(frozen=True)
class DeclaredSide:
    result: str
    output: ArtifactIdentity | None
    precursor: ArtifactIdentity | None
    stage: str | None
    issueCodes: list[str]

    @classmethod
    def read(cls, row: Mapping[str, Any]) -> DeclaredSide:
        value = dict(row)
        for scope in validation.SCOPES:
            value[scope] = None if row[scope] is None else ArtifactIdentity(**row[scope])
        return cls(**value)


@dataclass(frozen=True)
class Attribution:
    start: int
    endExclusive: int
    mechanism: str
    cause: str
    causeVerification: str
    evidence: list[str]


@dataclass(frozen=True)
class DeclaredDifference:
    differentByteCount: int
    rangeCount: int
    rangeListSha256: str
    ranges: list[dict[str, int]]
    attribution: list[Attribution]

    @classmethod
    def read(cls, row: Mapping[str, Any]) -> DeclaredDifference:
        return cls(**{**row, "attribution": [Attribution(**item) for item in row["attribution"]]})


@dataclass(frozen=True)
class DeclarationEntry:
    id: str
    kind: str
    scenarioIds: list[str]
    routeIds: list[str]
    expected: dict[str, DeclaredSide] | None
    differences: dict[str, DeclaredDifference | None] | None
    knownIssue: dict[str, str] | None
    routeWithdrawal: bool
    approval: Approval

    @classmethod
    def read(cls, row: Mapping[str, Any]) -> DeclarationEntry:
        expected = row["expected"]
        differences = row["differences"]
        return cls(**{**row, "approval": Approval(**row["approval"]),
                      "expected": None if expected is None else {key: DeclaredSide.read(value) for key, value in expected.items()},
                      "differences": None if differences is None else
                      {key: None if value is None else DeclaredDifference.read(value) for key, value in differences.items()}})


@dataclass(frozen=True)
class ReleaseDeclaration:
    schemaVersion: str
    kind: str
    candidateVersion: str
    baseline: dict[str, str]
    ledgerSha256: str
    entries: list[DeclarationEntry]

    @classmethod
    def read(cls, payload: bytes) -> ReleaseDeclaration:
        row = load_json_reject_duplicates(payload)
        return cls(**{**row, "entries": [DeclarationEntry.read(entry) for entry in row["entries"]]})


class RollingSources(NamedTuple):
    version: str
    tree: str
    baseline: validation.RollingTag
    contract: Mapping[str, Any]
    ledger: Mapping[str, Any]
    baseline_ledger: Mapping[str, Any]
    policy: Mapping[str, Any]
    plan: Mapping[str, Any]
    declaration: Mapping[str, Any] | None
    changelog: str
    authority: validation.SourceAuthority
    reader: Any
    descriptor: dict[str, Any]


def load_rolling_sources(
    git: RollingGitHost, candidate_commit: str, *, baseline_tag: str | None,
    formal: bool, published: PublishedReleaseHost | None,
) -> RollingSources:
    reader = git.snapshot_reader(candidate_commit)
    files = set(reader.list_files(candidate_commit))

    def raw(path: str) -> bytes:
        return reader.read_file(candidate_commit, path)

    def document(path: str) -> Any:
        return load_json_reject_duplicates(raw(path))

    implementation = ("scripts/predecessor_comparison.py", "scripts/predecessor_rolling.py",
                      "scripts/predecessor_pdb_probe.py",
                      "scripts/predecessor_validation.py", "scripts/predecessor_report_reader.py",
                      "scripts/v0916_parity_certification.py", "scripts/render_release_notes.py",
                      "scripts/canonical_golden_validation.py")
    if formal:
        execution._refuse(validation.comparator_source_failures(
            {path: sha256(raw(path)) for path in implementation},
            {path: sha256((execution.ROOT / path).read_bytes()) for path in implementation}, formal=formal))
    version = raw("VERSION").decode("utf-8").strip()
    baseline = resolve_rolling_baseline(git, candidate_commit, version, baseline_tag=baseline_tag,
                                       formal=formal, published=published)
    before = git.snapshot_reader(baseline.commit)
    before.list_files(baseline.commit)
    baseline_ledger = load_json_reject_duplicates(before.read_file(baseline.commit, LEDGER))
    contract, ledger, policy, plan = (document(path) for path in (CONTRACT, LEDGER, POLICY, PLAN))
    declaration_path = f"{contract['interfaces']['declarationDirectory']}/{version}.json"
    declaration_raw = raw(declaration_path) if declaration_path in files else None
    declaration = None if declaration_raw is None else asdict(ReleaseDeclaration.read(declaration_raw))
    manifest_path = plan["canonicalInputAuthority"]["manifestPath"]
    manifest_raw = raw(manifest_path)
    root_entry = reader.entry(PurePosixPath(manifest_path).parent.as_posix())
    manifest_entry = reader.entry(manifest_path)
    descriptor = {"repositoryCommit": candidate_commit, "manifestPath": manifest_path,
                  "canonicalRootTree": root_entry[2], "manifestBlob": manifest_entry[2],
                  "manifestSize": len(manifest_raw), "manifestRawSha256": sha256(manifest_raw)}
    authority = validation.SourceAuthority(
        candidate_commit, git.commit_tree(candidate_commit), baseline.tag, baseline.tag_object, baseline.commit,
        sha256(Path(execution.__file__).read_bytes()), {path: sha256(raw(path)) for path in APPLIED_CONTRACTS},
        sha256(raw(LEDGER)), None if declaration_raw is None else sha256(declaration_raw),
        published_inventory=None if published is None else published.report_identity())
    return RollingSources(version, authority.candidate_tree, baseline, contract, ledger, baseline_ledger,
                          policy, plan, declaration, raw("CHANGELOG.md").decode("utf-8"), authority, reader, descriptor)


def materialize_rolling_inputs(
    sources: RollingSources, destination: Path,
    materializer: Callable[..., MaterializedCanonicalAuthority] = materialize_and_validate_canonical_input_authority,
) -> tuple[MaterializedCanonicalAuthority, dict[str, Any], dict[str, Any]]:
    authority = materializer({"canonicalInputAuthority": sources.descriptor}, git_reader=sources.reader, destination=destination)
    manifest = load_json_reject_duplicates(authority.files[authority.manifest_relative])
    root = PurePosixPath(authority.manifest_relative).parent
    cases = {row["caseId"]: load_json_reject_duplicates(authority.files[(root / row["manifestPath"]).as_posix()])
             for row in manifest["cases"]}
    execution._refuse(validation.ledger_failures(sources.ledger, policy=sources.policy, manifest=manifest,
                                                case_manifests=cases, plan=sources.plan))
    return authority, manifest, cases


def execute_rolling_side(
    runner: execution.ProcessRunner, executor: execution.Executor, scenario: Mapping[str, Any],
    authority: MaterializedCanonicalAuthority, manifest: Mapping[str, Any],
) -> ScenarioExecution:
    """One invocation of every required stage, stopping on the validator's verdict."""
    case = resolve_case(authority, {**manifest, "__manifestRelative": authority.manifest_relative}, scenario["evidenceCaseId"])
    artifacts = {row["artifactId"]: row for row in case["artifacts"]}
    request = {"workflowId": scenario["workflowId"], "profileId": scenario["cli"]["profile"], "icId": scenario["icId"],
               "cliSelectionToken": scenario["cli"]["selectionToken"]}
    bindings = scenario["inputs"]
    base = scenario["ctrlRamBase"] or {}
    precursor_request = None
    precursor_bindings = []
    if base.get("kind") == "standard-merge":
        precursor_request = {"workflowId": "standard-merge", "profileId": request["profileId"], "icId": request["icId"],
                             "cliSelectionToken": None}
        precursor_bindings = [binding for binding in bindings if binding["slotId"] in ("dp-input", "tp-input")]
        bindings = [binding for binding in bindings if binding["slotId"] not in ("dp-input", "tp-input")]
    return execution.execute_side_stages(runner, executor, request, authority, artifacts, bindings,
                                         precursor_request=precursor_request, precursor_bindings=precursor_bindings)


@dataclass(frozen=True)
class ScenarioReport:
    scenarioId: str
    routeId: str
    inputRevision: int
    outcome: str
    baseline: dict[str, Any]
    candidate: dict[str, Any]
    comparison: dict[str, Any] | None
    precursorComparison: dict[str, Any] | None
    failureCode: str | None
    declarationEntryId: str | None
    informational: list[dict[str, Any]]


def build_scenario_report(
    row: Mapping[str, Any], baseline: ScenarioExecution, candidate: ScenarioExecution,
    evidence: validation.ScopeEvidence, failures: Sequence[validation.Failure], declaration: Mapping[str, Any] | None,
) -> ScenarioReport:
    verdict = validation.rolling_outcome(baseline.result.side, candidate.result.side, evidence, failures)
    entry = validation.declaration_entry_id(validation.OUTCOME_ENTRY_KINDS.get(verdict.outcome), row["scenarioId"], declaration)
    return ScenarioReport(row["scenarioId"], row["routeId"], row["inputRevision"], verdict.outcome,
                          baseline.result.side, candidate.result.side, validation.range_projection(evidence.get("output")),
                          validation.range_projection(evidence.get("precursor")), verdict.failure_code, entry,
                          informational_differences(baseline, candidate))


@dataclass(frozen=True)
class RollingReport:
    schemaVersion: str
    kind: str
    certification: str
    terminal: bool
    mode: str
    formal: bool
    comparator: dict[str, Any]
    candidate: dict[str, Any]
    baseline: dict[str, Any]
    ledgerSha256: str
    declarationSha256: str | None
    publishedInventory: dict[str, str] | None
    environment: dict[str, Any]
    scenarios: list[ScenarioReport]
    coverage: dict[str, Any]
    gate: dict[str, Any]

    def payload(self) -> dict[str, Any]:
        value = asdict(self)
        return {**value, "deterministicSha256": validation.deterministic_report_sha256(value)}


def build_rolling_report(
    sources: RollingSources, baseline: execution.Executor, candidate: execution.Executor,
    environment: dict[str, Any], scenarios: list[ScenarioReport], manifest: Mapping[str, Any],
    cases: Mapping[str, Any], evidence: Mapping[str, validation.ScopeEvidence],
    failures: Sequence[validation.Failure], *, admission: execution.ContractAdmission,
    baseline_identity_report_sha256: str | None = None,
) -> dict[str, Any]:
    authority = sources.authority
    report = RollingReport(
        "1.0", "predecessor-comparison-report", "none", False, "rolling", admission.formal,
        {"scriptSha256": authority.comparator_sha256,
         "contracts": [{"path": path, "sha256": digest} for path, digest in sorted(authority.contracts.items())]},
        {"version": sources.version, "executor": candidate.identity},
        {"kind": "previous-release", "tag": sources.baseline.tag, "executor": baseline.identity},
        authority.ledger_sha256, authority.declaration_sha256, authority.published_inventory, environment, scenarios,
        validation.rolling_coverage(sources.ledger, sources.baseline_ledger, sources.policy, manifest, sources.declaration),
        validation.rolling_gate([]))
    payload = asdict(report)
    if baseline_identity_report_sha256 is not None:
        payload["baselineIdentityReportSha256"] = baseline_identity_report_sha256
    checks = validation.rolling_report_failures(
        payload, ledger=sources.ledger, baseline_ledger=sources.baseline_ledger, policy=sources.policy,
        manifest=manifest, case_manifests=cases, plan=sources.plan, declaration=sources.declaration,
        changelog=sources.changelog, authority=authority, evidence=evidence)
    payload["gate"] = validation.rolling_gate([*failures, *checks])
    payload["deterministicSha256"] = validation.deterministic_report_sha256(payload)
    return payload


def compare_baseline_own_report(baseline: execution.Executor, path: Path, tag: str) -> str:
    """Read once, refuse missing/mismatched identity, and bind the admitted bytes."""
    try:
        raw = path.read_bytes()
        own_report = load_json_reject_duplicates(raw)
    except (OSError, ParityError, ValueError, TypeError) as error:
        raise execution.ExecutionError("PREDECESSOR_BASELINE_IDENTITY_MISSING",
                                       "baseline own report is unavailable or malformed") from error
    execution._refuse(validation.baseline_identity_failures(baseline.identity, own_report, baseline_version=tag[1:]))
    return sha256(raw)


def run_rolling(
    *, git: RollingGitHost, host: execution.ProcessHost, candidate_commit: str,
    baseline_tag: str | None, output_path: Path, temporary_root: Path, settings_folder: Path,
    formal: bool = False, published: PublishedReleaseHost | None = None, baseline_report: Path | None = None,
    materializer: Callable = materialize_and_validate_canonical_input_authority,
) -> dict[str, Any]:
    """Build both 1.x executors, execute each ledger scenario once per side, and write the gate."""
    execution.require_fresh_output(output_path)
    if formal and published is None:
        raise execution.ExecutionError("PREDECESSOR_BASELINE_INVALID", "formal rolling requires --published-release-inventory")
    # Refuse pending formal execution before acquiring source or settings.
    execution.admit_execution_contract(mode="rolling", formal=formal)
    try:
        sources = load_rolling_sources(git, candidate_commit, baseline_tag=baseline_tag, formal=formal, published=published)
        admission = execution.admit_loaded_execution_contract(sources.contract, mode="rolling", formal=formal)
        runner = execution.ProcessRunner(host, temporary_root, settings_folder, admission=admission)
        try:
            authority, manifest, cases = materialize_rolling_inputs(sources, runner.temporary_root / "canonical", materializer)
            baseline = execution.build_1x_executor(git, runner, sources.baseline.commit, sources.contract,
                                                   tag_object=sources.baseline.tag_object)
            baseline_identity_report_sha256 = None if baseline_report is None else compare_baseline_own_report(
                baseline, baseline_report, sources.baseline.tag)
            candidate = execution.build_1x_executor(git, runner, candidate_commit, sources.contract)
            scenarios, evidence, failures = [], {}, []
            for row in sources.ledger["scenarios"]:
                left = execute_rolling_side(runner, baseline, row, authority, manifest)
                right = execute_rolling_side(runner, candidate, row, authority, manifest)
                scopes, capture_failures = measured_scopes(left, right, runner.custody)
                side_failures = [*left.result.failures, *right.result.failures, *capture_failures]
                scenarios.append(build_scenario_report(row, left, right, scopes, side_failures, sources.declaration))
                evidence[row["scenarioId"]] = scopes
                failures.extend(side_failures)
        finally:
            environment, environment_failures = runner.finish()
        report = build_rolling_report(sources, baseline, candidate, environment, scenarios, manifest, cases, evidence,
                                      [*failures, *environment_failures], admission=runner.admission,
                                      baseline_identity_report_sha256=baseline_identity_report_sha256)
        write_json_exclusive_atomic(output_path, report)
        return report
    except execution.ExecutionError:
        raise
    except subprocess.SubprocessError as error:
        raise execution.ExecutionError("PREDECESSOR_SOURCE_MISMATCH", "candidate Git acquisition failed") from error
    except (ParityError, OSError, KeyError, TypeError, ValueError) as error:
        raise execution.ExecutionError("PREDECESSOR_INPUT_INVALID", "rolling source, declaration or artifact acquisition failed") from error


def rolling_main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Predecessor comparison")
    modes = parser.add_subparsers(dest="mode", required=True)
    rolling = modes.add_parser("rolling", help="compare the ledger with the previous stable release")
    rolling.add_argument("--candidate-commit", required=True)
    rolling.add_argument("--baseline-tag", help="required for a local diagnostic run")
    rolling.add_argument("--published-release-inventory", type=Path, help="commander's complete published stable release inventory")
    rolling.add_argument("--baseline-report", type=Path, help="opt in to comparing the rebuild with that release's own predecessor report")
    rolling.add_argument("--output", type=Path, required=True)
    policy = rolling.add_mutually_exclusive_group(required=True)
    policy.add_argument("--formal", action="store_true")
    policy.add_argument("--diagnostic", action="store_true")
    rolling.add_argument("--temporary-root", type=Path, required=True, help="existing root for short, private process paths")
    args = parser.parse_args(argv)
    try:
        execution.require_fresh_output(args.output)
        if args.formal and args.published_release_inventory is None:
            raise execution.ExecutionError("PREDECESSOR_BASELINE_INVALID", "formal rolling requires --published-release-inventory")
        published = None if args.published_release_inventory is None else FilePublishedReleaseInventory.read(args.published_release_inventory)
        execution.admit_execution_contract(mode="rolling", formal=args.formal)
        if not args.formal and args.baseline_tag is None:
            raise execution.ExecutionError("PREDECESSOR_BASELINE_INVALID", "diagnostic requires --baseline-tag")
        with tempfile.TemporaryDirectory(prefix="rolling-", dir=args.temporary_root) as temporary:
            report = run_rolling(git=LocalRollingGitHost(execution.ROOT), host=execution.LocalExecutionHost(),
                                 candidate_commit=args.candidate_commit, baseline_tag=args.baseline_tag,
                                 output_path=args.output, temporary_root=Path(temporary),
                                 settings_folder=execution.local_settings_folder(), formal=args.formal, published=published,
                                 baseline_report=args.baseline_report)
        return 0 if report["gate"]["result"] == "clear" else 1
    except (execution.ExecutionError, OSError, ParityError) as error:
        print(f"{getattr(error, 'code', 'PREDECESSOR_ENVIRONMENT_INVALID')}: {error}", file=sys.stderr)
        return 1
