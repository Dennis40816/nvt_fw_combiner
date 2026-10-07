"""v0.9.16 milestone orchestration; execution and proof rules keep their owners."""

from __future__ import annotations

import argparse
from dataclasses import asdict, dataclass
from pathlib import Path
import subprocess
import sys
import tempfile
from typing import Any, Callable, Mapping, NamedTuple, Protocol, Sequence

from scripts import predecessor_comparison as execution
from scripts import predecessor_validation as validation
from scripts.v0916_parity_certification import (
    MaterializedCanonicalAuthority, ParityError, Plan,
    load_and_validate_pinned_plan, load_json_reject_duplicates,
    materialize_and_validate_canonical_input_authority, resolve_canonical_route_input,
    resolve_case, write_json_exclusive_atomic,
)


CONTRACT = "docs/contracts/predecessor-comparison-v1.json"
PLAN = "docs/contracts/v0916-parity-certification-v1.json"
AMENDMENT = "docs/contracts/v0916-parity-1x-amendment-v1.json"
BASELINE_EXECUTOR = validation.BASELINE_EXECUTOR_V2_PATH
APPLIED_CONTRACTS = (CONTRACT, CONTRACT.replace(".json", ".schema.json"),
                     "docs/contracts/predecessor-comparison-report-v1.schema.json",
                     PLAN, PLAN.replace(".json", ".schema.json"),
                     AMENDMENT, AMENDMENT.replace(".json", ".schema.json"), BASELINE_EXECUTOR,
                     BASELINE_EXECUTOR.replace(".json", ".schema.json"),
                     "docs/contracts/v0916-baseline-executor-v1.json")
IMPLEMENTATION = ("scripts/predecessor_comparison.py", "scripts/predecessor_v0916.py",
                  "scripts/predecessor_validation.py", "scripts/predecessor_report_reader.py",
                  "scripts/v0916_parity_certification.py", "scripts/canonical_golden_validation.py",
                  "scripts/predecessor_pdb_probe.py")


class V0916GitHost(execution.GitHost, Protocol):
    def snapshot_reader(self, commit: str) -> Any: ...
    def commit_tree(self, commit: str) -> str: ...


class LocalV0916GitHost(execution.LocalGitHost):
    def commit_tree(self, commit: str) -> str:
        return self._git("rev-parse", "--verify", f"{commit}^{{tree}}")


class V0916Sources(NamedTuple):
    version: str
    contract: Mapping[str, Any]
    plan: Mapping[str, Any]
    amendment: Mapping[str, Any]
    authority: validation.SourceAuthority
    input_reader: Any
    baseline_executor: Mapping[str, Any] | None = None


def load_v0916_sources(git: V0916GitHost, candidate_commit: str, *, formal: bool) -> V0916Sources:
    reader = git.snapshot_reader(candidate_commit)
    reader.list_files(candidate_commit)

    def raw(path: str) -> bytes:
        return reader.read_file(candidate_commit, path)

    plan, amendment = (load_json_reject_duplicates(raw(path)) for path in (PLAN, AMENDMENT))
    execution._refuse(validation.amendment_binding_failures(amendment, plan))
    # Remove the excluded member before any plan loader, materializer or builder.
    plan = {key: value for key, value in plan.items() if key != "candidateAuthority"}
    contract = load_json_reject_duplicates(raw(CONTRACT))
    baseline_executor = execution.load_bound_v0916_executor(
        amendment["baselineExecutor"], raw, contract["executor"]["compilerHost"])
    if formal:
        execution._refuse(validation.comparator_source_failures(
            {path: execution._sha256(raw(path)) for path in IMPLEMENTATION},
            {path: execution._sha256((execution.ROOT / path).read_bytes()) for path in IMPLEMENTATION}, formal=True))
    baseline = plan["baseline"]
    execution._refuse(validation.executor_tag_failures(
        baseline["tagObject"], git.git_tag_object(baseline["tag"]),
        git.git_tag_commit(baseline["tagObject"]), baseline["peeledCommit"]))
    if baseline_executor is not None and (baseline_executor["source"]["tagObject"], baseline_executor["source"]["peeledCommit"]) != (
        baseline["tagObject"], baseline["peeledCommit"]):
        raise execution.ExecutionError("PREDECESSOR_BASELINE_INVALID", "v2 source differs from the plan baseline")
    authority = validation.SourceAuthority(
        candidate_commit, git.commit_tree(candidate_commit), baseline["tag"], baseline["tagObject"],
        baseline["peeledCommit"], execution._sha256(Path(execution.__file__).read_bytes()),
        {path: execution._sha256(raw(path)) for path in APPLIED_CONTRACTS}, amendment_sha256=execution._sha256(raw(AMENDMENT)))
    return V0916Sources(raw("VERSION").decode("utf-8").strip(), contract, plan, amendment, authority,
                        git.snapshot_reader(plan["canonicalInputAuthority"]["repositoryCommit"]), baseline_executor)


def materialize_v0916_inputs(
    sources: V0916Sources, destination: Path, *, materializer: Callable, plan_loader: Callable,
) -> tuple[MaterializedCanonicalAuthority, Plan, Mapping[str, Any], list[validation.RouteDisposition]]:
    authority = materializer(sources.plan, git_reader=sources.input_reader, destination=destination)
    # The historical loader consumes captured plan bytes and the policy from the
    # plan's Git revision. It never consults the candidate's Golden snapshot.
    plan_path = destination.parent / "historical-plan.json"
    write_json_exclusive_atomic(plan_path, sources.plan)
    plan = plan_loader(plan_path, repository_root=destination, git_reader=sources.input_reader)
    policy_raw = sources.input_reader.read_file(sources.plan["canonicalInputAuthority"]["repositoryCommit"],
                                               sources.plan["policyBinding"]["path"])
    execution._refuse(validation.v0916_policy_capture_failures(sources.plan, execution._sha256(policy_raw)))
    policy = load_json_reject_duplicates(policy_raw)
    dispositions, failures = validation.v0916_route_dispositions(sources.plan, sources.amendment, policy)
    execution._refuse(failures)
    return authority, plan, policy, dispositions


class RouteExecution(NamedTuple):
    baseline: execution.ScenarioExecution | None
    candidate: execution.ScenarioExecution
    inputs: Mapping[str, Any]


def execute_v0916_side(
    runner: execution.ProcessRunner, executor: execution.Executor, plan: Plan,
    authority: MaterializedCanonicalAuthority, disposition: validation.RouteDisposition, *, side: str,
) -> tuple[execution.ScenarioExecution, Mapping[str, Any]]:
    role = "baseline-exact" if side == "baseline" else (
        "candidate-tp" if disposition.proof_kind == "tp-prefix-transitive" else "candidate-exact")
    verified = resolve_canonical_route_input(plan, authority, admitted_input_root=runner.temporary_root / side,
                                             route_id=disposition.route_id, execution_role=role)
    request = {**verified.request, "inputIdentityAliases": plan.raw.get("inputIdentityAliases", ())}
    # Use the captured authority with the resolver's exact artifacts and slot
    # identities rather than inventing a second artifact admission path.
    manifest = load_json_reject_duplicates(authority.files[authority.manifest_relative])
    evidence_case = next(row["caseId"] for row in manifest["routeEvidence"] if row["routeId"] == disposition.route_id)
    case = resolve_case(authority, {**manifest, "__manifestRelative": authority.manifest_relative}, evidence_case)
    artifacts = {row["artifactId"]: row for row in case["artifacts"]}
    bindings = []
    for admitted in request["orderedInputs"]:
        # Reconnect the resolver's staged path to its case artifact, using the
        # exact admit_case_inputs filename; no firmware fact is inferred.
        artifact_id = next(key for key in artifacts if Path(admitted["path"]).name == f"{admitted['order']:02d}-{key}.bin")
        bindings.append({"artifactId": artifact_id, "slotId": admitted["slotId"]})
    precursor_request = None
    precursor_bindings = []
    if request.get("baseRecipe") is not None:
        precursor_request = {**request, "workflowId": "standard-merge", "cliSelectionToken": None}
        precursor_bindings = bindings[:2]
        bindings = bindings[2:]
    result = execution.execute_side_stages(
        runner, executor, request, authority, artifacts, bindings, precursor_request=precursor_request,
        precursor_bindings=precursor_bindings, execution_role=role)
    return result, {**request, "canonicalBinding": {"evidenceCaseId": evidence_case, "inputCaseId": case["caseId"],
                                                    "precursorMapVariant": request.get("baseRecipe", {}).get("mapVariant")}}


def read_payloads(runner: execution.ProcessRunner, artifacts: Sequence[tuple[Path, Mapping[str, Any]]]) -> tuple[list[bytes], list[validation.Failure]]:
    payloads, failures = [], []
    with runner.custody([path for path, _ in artifacts]):
        for path, expected in artifacts:
            payload = path.read_bytes()
            failures.extend(validation.scope_capture_failures("output-file-offset", expected, execution._payload_identity(payload)))
            payloads.append(payload)
    return payloads, failures


@dataclass(frozen=True)
class RouteReport:
    planRouteId: str
    planCapabilityFingerprint: str
    proofKind: str
    result: str
    dispositionRow: dict[str, Any] | None
    baseline: dict[str, Any] | None
    candidate: dict[str, Any] | None
    comparison: dict[str, Any] | None
    transitive: dict[str, Any] | None
    failureCode: str | None
    informational: list[dict[str, Any]]


def build_route_report(
    disposition: validation.RouteDisposition, acquired: RouteExecution | None, runner: execution.ProcessRunner,
    routes: Mapping[str, Mapping[str, Any]], executions: Mapping[str, RouteExecution],
) -> tuple[RouteReport, validation.V0916RouteEvidence | None, list[validation.Failure]]:
    row = RouteReport(disposition.route_id, disposition.capability_fingerprint, disposition.proof_kind,
                      "not-covered", None, None, None, None, None, None, [])
    if acquired is None:
        verdict = validation.v0916_route_verdict(asdict(row), disposition, None, routes)
        return row, None, verdict.failures
    left, right, inputs = acquired
    payload = asdict(row)
    payload.update(baseline=None if left is None else left.result.side, candidate=right.result.side,
                   informational=[] if left is None else execution.informational_differences(left, right))
    if disposition.row_member in {"approvedSemanticCorrections", "baselineNotApplicable"}:
        payload["dispositionRow"] = {"source": disposition.row_source, "member": disposition.row_member,
                                     "routeId": disposition.route_id}
    failures = [*(() if left is None else left.result.failures), *right.result.failures]
    scopes = {}
    computed = None
    correction = None
    binding = None
    try:
        if left is not None:
            scopes, capture_failures = execution.measured_scopes(left, right, runner.custody)
            failures.extend(capture_failures)
        if disposition.proof_kind == "exact-output-with-approved-semantic-correction" and all(
            side.result.side["output"] is not None for side in (left, right)
        ):
            outputs = [next(capture for capture in side.captures if capture.record["stage"] == "build") for side in (left, right)]
            values, checks = read_payloads(runner, [(item.output_path, item.output) for item in outputs])
            failures.extend(checks)
            correction = validation.approved_correction_evidence(*values, disposition.row)
        if disposition.proof_kind == "canonical-binding-not-applicable-to-v0916":
            binding = inputs["canonicalBinding"]
        if disposition.proof_kind == "tp-prefix-transitive":
            full_id = disposition.row["fullRouteId"]
            if validation.transitive_blocker(payload, routes.get(full_id)) is None:
                full = executions[full_id]
                outputs = [next(item for item in side.captures if item.record["stage"] == "build")
                           for side in (full.baseline, full.candidate, right)]
                base = next(item for item in outputs[1].inputs if item["slotId"] == "replace-base")
                values, checks = read_payloads(runner, [*((item.output_path, item.output) for item in outputs),
                                                       (Path(base["path"]), base)])
                failures.extend(checks)
                computed = validation.transitive_evidence(*values, disposition.row["tpLength"])
                payload["transitive"] = validation.transitive_projection(disposition.row, computed)
    except (ParityError, OSError):
        failures.append(validation.Failure("PREDECESSOR_REPORT_INVALID", disposition.route_id, "artifact custody failed"))
    evidence = validation.V0916RouteEvidence(scopes, computed, correction, binding)
    if disposition.proof_kind not in {"tp-prefix-transitive", "canonical-binding-not-applicable-to-v0916"}:
        payload["comparison"] = validation.range_projection(scopes.get("output"))
    verdict = validation.v0916_route_verdict(payload, disposition, evidence, routes, failures)
    payload.update(result=verdict.result, failureCode=verdict.failure_code)
    return RouteReport(**payload), evidence, verdict.failures


@dataclass(frozen=True)
class V0916Report:
    schemaVersion: str
    kind: str
    certification: str
    terminal: bool
    mode: str
    formal: bool
    milestone: str | None
    comparator: dict[str, Any]
    candidate: dict[str, Any]
    baseline: dict[str, Any]
    planBinding: dict[str, str]
    amendmentSha256: str
    environment: dict[str, Any]
    routes: list[RouteReport]
    summary: dict[str, int]
    result: str
    failures: list[dict[str, Any]]


def build_v0916_report(
    sources: V0916Sources, baseline: execution.Executor, candidate: execution.Executor,
    environment: dict[str, Any], routes: list[RouteReport], dispositions: Sequence[validation.RouteDisposition],
    evidence: Mapping[str, validation.V0916RouteEvidence | None], failures: Sequence[validation.Failure],
    *, admission: execution.ContractAdmission, milestone: str | None,
) -> dict[str, Any]:
    authority = sources.authority
    results = [route.result for route in routes]
    report = V0916Report(
        "1.0", "predecessor-comparison-report", "none", False, "v0916-1x", admission.formal, milestone,
        {"scriptSha256": authority.comparator_sha256,
         "contracts": [{"path": path, "sha256": digest} for path, digest in sorted(authority.contracts.items())]},
        {"version": sources.version, "executor": candidate.identity},
        {"kind": "v0916", "tag": authority.baseline_tag, "executor": baseline.identity},
        validation.v0916_plan_binding(sources.plan), authority.amendment_sha256, environment, routes,
        validation.v0916_summary(results), validation.v0916_result(results), [])
    payload = asdict(report)
    execution._refuse(validation.v0916_report_failures(payload, dispositions, evidence, authority=authority, plan=sources.plan))
    payload["failures"] = [{"code": item.code, "subject": item.subject, "detail": item.detail}
                           for item in sorted(set(failures))]
    payload["deterministicSha256"] = validation.deterministic_report_sha256(payload)
    return payload


def run_v0916(
    *, git: V0916GitHost, host: execution.ProcessHost, baseline_builder: execution.BaselineExecutorBuilder | None,
    candidate_commit: str, output_path: Path, temporary_root: Path, settings_folder: Path,
    formal: bool = False, milestone: str | None = None,
    materializer: Callable = materialize_and_validate_canonical_input_authority,
    plan_loader: Callable = load_and_validate_pinned_plan,
) -> dict[str, Any]:
    execution.require_fresh_output(output_path)
    execution.admit_execution_contract(mode="v0916-1x", formal=formal)
    execution._refuse(validation.v0916_milestone_failures(formal=formal, milestone=milestone))
    try:
        sources = load_v0916_sources(git, candidate_commit, formal=formal)
        admission = execution.admit_loaded_execution_contract(sources.contract, mode="v0916-1x", formal=formal,
                                                              amendment=sources.amendment)
        if baseline_builder is None or sources.baseline_executor is None:
            raise execution.ExecutionError("PREDECESSOR_CONTRACT_PENDING", "v0.9.16 baseline builder must be supplied")
        runner = execution.ProcessRunner(host, temporary_root, settings_folder, admission=admission)
        try:
            authority, plan, _, dispositions = materialize_v0916_inputs(
                sources, runner.temporary_root / "canonical", materializer=materializer, plan_loader=plan_loader)
            baseline = baseline_builder.build(git, runner, sources.authority.baseline_commit, sources.baseline_executor)
            candidate = execution.build_1x_executor(git, runner, candidate_commit, sources.contract)
            executions = {}
            for disposition in dispositions:
                if disposition.proof_kind == "not-covered":
                    continue
                left = None
                if disposition.proof_kind != "tp-prefix-transitive":
                    left, _ = execute_v0916_side(runner, baseline, plan, authority, disposition, side="baseline")
                right, inputs = execute_v0916_side(runner, candidate, plan, authority, disposition, side="candidate")
                executions[disposition.route_id] = RouteExecution(left, right, inputs)
            routes_by_id, evidence, failures = {}, {}, []
            # Resolve full-route results before dependent TP proofs, while the
            # published report retains the pinned policy's route order.
            for disposition in sorted(dispositions, key=lambda item: item.proof_kind == "tp-prefix-transitive"):
                route, computed, checks = build_route_report(disposition, executions.get(disposition.route_id), runner,
                                                              routes_by_id, executions)
                routes_by_id[disposition.route_id] = asdict(route)
                if computed is not None:
                    evidence[disposition.route_id] = computed
                failures.extend(checks)
        finally:
            environment, environment_failures = runner.finish()
        execution._refuse(environment_failures)
        routes = [RouteReport(**routes_by_id[item.route_id]) for item in dispositions]
        report = build_v0916_report(sources, baseline, candidate, environment, routes, dispositions, evidence, failures,
                                    admission=admission, milestone=milestone)
        write_json_exclusive_atomic(output_path, report)
        return report
    except execution.ExecutionError:
        raise
    except subprocess.SubprocessError as error:
        raise execution.ExecutionError("PREDECESSOR_SOURCE_MISMATCH", "candidate Git acquisition failed") from error
    except (ParityError, OSError, KeyError, TypeError, ValueError, StopIteration) as error:
        raise execution.ExecutionError("PREDECESSOR_INPUT_INVALID", "historical source or input acquisition failed") from error


def v0916_main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="v0.9.16 milestone comparison")
    modes = parser.add_subparsers(dest="mode", required=True)
    mode = modes.add_parser("v0916-1x")
    mode.add_argument("--candidate-commit", required=True)
    mode.add_argument("--output", type=Path, required=True)
    mode.add_argument("--temporary-root", type=Path, required=True)
    mode.add_argument("--milestone", choices=("1.1.13-final-candidate", "1.2.0-release-approval", "before-ro-1-decision"))
    policy = mode.add_mutually_exclusive_group(required=True)
    policy.add_argument("--formal", action="store_true")
    policy.add_argument("--diagnostic", action="store_true")
    args = parser.parse_args(argv)
    try:
        execution.require_fresh_output(args.output)
        execution.admit_execution_contract(mode="v0916-1x", formal=args.formal)
        with tempfile.TemporaryDirectory(prefix="v0916-", dir=args.temporary_root) as temporary:
            report = run_v0916(git=LocalV0916GitHost(execution.ROOT), host=execution.LocalExecutionHost(),
                                baseline_builder=execution.V0916BaselineExecutorBuilder(), candidate_commit=args.candidate_commit, output_path=args.output,
                                temporary_root=Path(temporary), settings_folder=execution.local_settings_folder(),
                                formal=args.formal, milestone=args.milestone)
        return 0 if report["result"] == "consistent" else 1
    except (execution.ExecutionError, OSError, ParityError) as error:
        print(f"{getattr(error, 'code', 'PREDECESSOR_ENVIRONMENT_INVALID')}: {error}", file=sys.stderr)
        return 1
