"""Single semantic validator of the predecessor comparison (ADR 0078).

The report and declaration schemas hold the rules that can be decided inside
one document. This module holds every rule that spans documents or compares
values: the coverage ledger against the capability policy, the canonical
manifest, the case manifests and the ADR 0057 plan; a rolling report against
the ledger and the baseline tag's ledger; a release declaration against the
ledger, the CHANGELOG and the run; the v0.9.16 1.x mode against the plan and
the amendment; and the values inside a report that a schema cannot compare.

Every function takes documents and computed evidence that the caller has
already loaded or measured (case manifests as a mapping, byte comparisons as
complete range lists, transitive proofs as the ADR 0057 primitive's own
result) and does no Git, file or process access. Every check returns failures
with the contract's codes and never raises for a finding.
"""

from __future__ import annotations

import re
from typing import Any, Iterable, Mapping, NamedTuple, Sequence

try:
    from scripts.render_release_notes import render_release_notes
    from scripts.v0916_parity_certification import (
        ParityError,
        canonical_json_sha256,
        cli_selection_token,
        compare_transitive_payloads,
    )
except ModuleNotFoundError as error:
    if error.name != "scripts":
        raise
    # `python ./scripts/predecessor_comparison.py` puts the scripts directory,
    # not the repository root, on sys.path.
    from render_release_notes import render_release_notes  # type: ignore[no-redef]
    from v0916_parity_certification import (  # type: ignore[no-redef]
        ParityError,
        canonical_json_sha256,
        cli_selection_token,
        compare_transitive_payloads,
    )


REPORTED_RANGE_LIMIT = 32
SCOPES = ("output", "precursor")
UNIVERSE_PUBLICATION = frozenset({"candidate", "supported"})
OUTCOME_ENTRY_KINDS = {
    "different": "byte-difference",
    "baseline-rejects": "baseline-rejects",
    "candidate-rejects": "candidate-rejects",
    "both-reject": "both-reject",
}
COVERAGE_ENTRY_KINDS = frozenset({"accepted-gap", "input-revision", "scenario-retired"})
# Stages before a Standard Merge precursor exists; a side stopped at one has none.
PRECURSOR_STAGES = frozenset({"precursor-preview", "precursor-build"})
PROCESS_FAILURE_ISSUE_CODES = frozenset({"external-tool.process.failed", "external-tool.process.start-failed"})
# The codes of the shared execution failures (contract section "Shared
# execution"; the report schema's scenarioFailureCode): an executor, the
# environment, a staged input, a process, or a report or per-side safety check.
# A v0.9.16 route is invalid only with one of them.
EXECUTION_FAILURE_CODES = frozenset(
    {
        "PREDECESSOR_ENVIRONMENT_INVALID",
        "PREDECESSOR_EXECUTOR_INVALID",
        "PREDECESSOR_INPUT_INVALID",
        "PREDECESSOR_PROCESS_FAILED",
        "PREDECESSOR_REPORT_INVALID",
    }
)
TRANSITIVE_CHECKS = (
    "candidateTpEqualsCandidateFullPrefix",
    "candidateTpEqualsBaselineFullPrefix",
    "candidateFullTailImmutable",
)
ENTRY_ID = re.compile(r"RP-(?P<version>(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*))-(?P<number>[0-9]{2,})\Z")

# Computed evidence of one scenario or route: for each scope both sides can
# be compared in, the complete differing range list, or None when the bytes
# are equal. A scope a side lacks is absent from the mapping.
ScopeEvidence = Mapping[str, "Sequence[Mapping[str, int]] | None"]


class Failure(NamedTuple):
    """One finding, with a failure code of the predecessor comparison contract."""

    code: str
    subject: str
    detail: str


def _failure(code: str, subject: str, detail: str) -> Failure:
    return Failure(f"PREDECESSOR_{code}", subject, detail)


class _RouteKey(NamedTuple):
    ic_id: str
    workflow_id: str
    ic_count_variant: str
    map_variant: str


def route_key(route: Mapping[str, Any]) -> tuple[str, str, str, str]:
    """The route axes that identify a route in the policy and a scenario in the ledger."""

    return (route["icId"], route["workflowId"], route["icCountVariant"], route["mapVariant"])


def universe_routes(policy: Mapping[str, Any]) -> set[str]:
    """Every `available` route published as `supported` or `candidate`."""

    return {
        route["routeId"]
        for route in policy["routes"]
        if route["authoring"]["value"] == "available" and route["publication"]["value"] in UNIVERSE_PUBLICATION
    }


def expected_cli(scenario: Mapping[str, Any]) -> dict[str, Any]:
    """The CLI profile and selection the ADR 0057 owner derives for a scenario's route axes."""

    token = cli_selection_token(
        _RouteKey(scenario["icId"], scenario["workflowId"], scenario["icCountVariant"], scenario["mapVariant"])
    )
    option = None
    if token is not None:
        option = "--ic-num" if scenario["workflowId"] == "ctrlram-replace" else "--ab-topology"
    return {"profile": scenario["icId"], "selectionOption": option, "selectionToken": token}


def _identity(value: Mapping[str, Any] | None) -> dict[str, Any] | None:
    return None if value is None else {"size": value["size"], "sha256": value["sha256"]}


def _duplicates(values: Iterable[str]) -> list[str]:
    seen: set[str] = set()
    repeated: set[str] = set()
    for value in values:
        (repeated if value in seen else seen).add(value)
    return sorted(repeated)


# ---------------------------------------------------------------------------
# Coverage ledger
# ---------------------------------------------------------------------------


def scenario_binding_failures(
    scenario: Mapping[str, Any], *, routes_by_key: Mapping[tuple[str, str, str, str], Any], universe: set[str]
) -> list[Failure]:
    """A scenario names a current universe route and the selection the CLI derives."""

    subject = scenario["scenarioId"]
    failures: list[Failure] = []
    route = routes_by_key.get(route_key(scenario))
    if route is None or route["routeId"] != scenario["routeId"]:
        failures.append(_failure("INPUT_INVALID", subject, f"route axes do not name {scenario['routeId']}"))
    if scenario["routeId"] not in universe:
        failures.append(_failure("INPUT_INVALID", subject, "route is outside the universe"))
    if scenario["cli"] != expected_cli(scenario):
        failures.append(_failure("INPUT_INVALID", subject, "CLI selection differs from the derived selection"))
    if subject != ":".join([*route_key(scenario), scenario["evidenceCaseId"]]):
        failures.append(_failure("INPUT_INVALID", subject, "identifier differs from its route axes and case"))
    return failures


def scenario_input_failures(
    scenario: Mapping[str, Any],
    *,
    evidence: Mapping[str, Any],
    case_manifests: Mapping[str, Mapping[str, Any]],
    plan: Mapping[str, Any],
    routes_by_key: Mapping[tuple[str, str, str, str], Any],
) -> list[Failure]:
    """A scenario's binding, inputs and CtrlRAM base come from the active Golden and the reviewed bindings."""

    subject = scenario["scenarioId"]
    failures: list[Failure] = []
    evidence_case = case_manifests.get(scenario["evidenceCaseId"])
    input_case = case_manifests.get(scenario["inputCaseId"])
    if evidence_case is None or input_case is None:
        return [_failure("INPUT_INVALID", subject, "evidence or input case is not in the canonical manifest")]
    named = evidence.get(scenario["routeId"], {}).get("caseId")
    if scenario["binding"] == "route-evidence" and named != scenario["evidenceCaseId"]:
        failures.append(_failure("INPUT_INVALID", subject, f"route evidence names {named}"))
    if scenario["binding"] == "case" and named is not None:
        failures.append(_failure("INPUT_INVALID", subject, f"a case binding hides route evidence {named}"))
    alias = evidence_case.get("alias")
    if (alias["sourceCaseId"] if alias else scenario["evidenceCaseId"]) != scenario["inputCaseId"]:
        failures.append(_failure("INPUT_INVALID", subject, "input case is not the resolved case"))
    case_inputs = [item for item in input_case["artifacts"] if item["role"] == "input"]
    artifacts = {item["artifactId"]: item for item in case_inputs}
    if [item["order"] for item in scenario["inputs"]] != list(range(len(scenario["inputs"]))):
        failures.append(_failure("INPUT_INVALID", subject, "input order is not contiguous from 0"))
    for item in scenario["inputs"]:
        artifact = artifacts.get(item["artifactId"])
        if artifact is None or (artifact["size"], artifact["sha256"]) != (item["size"], item["sha256"]):
            failures.append(_failure("INPUT_INVALID", subject, f"input {item['artifactId']} differs from its case"))
    pairs = [(item["artifactId"], item["slotId"]) for item in scenario["inputs"]]
    if scenario["workflowId"] != "ctrlram-replace":
        if scenario["ctrlRamBase"] is not None:
            failures.append(_failure("INPUT_INVALID", subject, "CtrlRAM base on a scenario of another workflow"))
        if pairs != [(item["artifactId"], item["artifactId"]) for item in case_inputs]:
            failures.append(_failure("INPUT_INVALID", subject, "inputs are not the case inputs in order"))
        return failures
    authority = plan["canonicalInputAuthority"]
    binding = next(
        (row for row in authority["ctrlRamExecutionBindings"] if row["caseId"] == scenario["inputCaseId"]), None
    )
    base = next((row for row in authority["ctrlRamBaseRoutes"] if row["routeId"] == scenario["planRouteId"]), None)
    if binding is None or base is None:
        # Without a reviewed CtrlRAM binding the slots and base kind cannot be admitted.
        return [*failures, _failure("INPUT_INVALID", subject, "no reviewed CtrlRAM binding for this scenario")]
    replacements = [(item["artifactId"], item["slotId"]) for item in binding["replacements"]]
    if base["kind"] == "tp-input":
        expected_base: dict[str, Any] = {"kind": "tp-input"}
        expected_pairs = [(binding["tpBaseArtifactId"], "replace-base"), *replacements]
    else:
        expected_base = {"kind": "standard-merge", "standardMergeMapVariant": base["standardMergeMapVariant"]}
        recipe = binding["fullBaseRecipe"]
        expected_pairs = [(recipe["dpArtifactId"], "dp-input"), (recipe["tpArtifactId"], "tp-input"), *replacements]
        precursor = (scenario["icId"], "standard-merge", "selector-free", base["standardMergeMapVariant"])
        if precursor not in routes_by_key:
            failures.append(_failure("INPUT_INVALID", subject, f"precursor route {precursor} is not in the policy"))
    if scenario["ctrlRamBase"] != expected_base:
        failures.append(_failure("INPUT_INVALID", subject, "CtrlRAM base differs from the reviewed binding"))
    if pairs != expected_pairs:
        failures.append(_failure("INPUT_INVALID", subject, "CtrlRAM slots differ from the reviewed binding"))
    return failures


def debt_set_failures(ledger: Mapping[str, Any], plan: Mapping[str, Any]) -> list[Failure]:
    """The debt set is exactly the plan's unbound route ids, in order; nothing else inherits it."""

    expected = sorted(plan["canonicalInputAuthority"]["currentlyMissingRouteIds"])
    actual = ledger["debtSet"]["routeIds"]
    if actual == expected:
        return []
    return [
        _failure(
            "COVERAGE_UNDISPOSED",
            "debtSet",
            f"differs from the plan: missing {sorted(set(expected) - set(actual))}, "
            f"extra {sorted(set(actual) - set(expected))}",
        )
    ]


def coverage_failures(ledger: Mapping[str, Any], *, universe: set[str], evidence: Mapping[str, Any]) -> list[Failure]:
    """Partition and completeness of the coverage ledger over the universe."""

    failures: list[Failure] = []
    covered = {row["routeId"] for row in ledger["scenarios"]}
    debt = set(ledger["debtSet"]["routeIds"])
    accepted = {row["routeId"] for row in ledger["acceptedGaps"]}
    pending = set(ledger["pendingAcceptedGaps"]["routeIds"])
    for label, left, right in (
        ("debt set and accepted gaps", debt, accepted),
        ("debt set and pending gaps", debt, pending),
        ("accepted and pending gaps", accepted, pending),
        ("compared routes and gaps", covered, accepted | pending),
    ):
        for route_id in sorted(left & right):
            failures.append(_failure("COVERAGE_UNDISPOSED", route_id, f"listed in both {label}"))
    for route_id in sorted(universe - covered - debt - accepted - pending):
        failures.append(_failure("COVERAGE_UNDISPOSED", route_id, "neither compared, owed nor disposed"))
    for route_id in sorted((accepted | pending) - universe):
        failures.append(_failure("COVERAGE_UNDISPOSED", route_id, "gap outside the universe"))
    declared = {(row["routeId"], row["evidenceCaseId"]) for row in ledger["scenarios"] if row["binding"] == "route-evidence"}
    for route_id in sorted(universe):
        case_id = evidence.get(route_id, {}).get("caseId")
        if case_id and (route_id, case_id) not in declared:
            failures.append(_failure("COVERAGE_INCOMPLETE", route_id, f"route evidence {case_id} has no scenario"))
    return failures


def report_of_record_blockers(ledger: Mapping[str, Any]) -> list[Failure]:
    """Ledger states under which no run may be a report of record."""

    return [
        _failure("COVERAGE_UNDISPOSED", route_id, "pending gap approval")
        for route_id in ledger["pendingAcceptedGaps"]["routeIds"]
    ]


def ledger_failures(
    ledger: Mapping[str, Any],
    *,
    policy: Mapping[str, Any],
    manifest: Mapping[str, Any],
    case_manifests: Mapping[str, Mapping[str, Any]],
    plan: Mapping[str, Any],
) -> list[Failure]:
    """Every ledger rule against the candidate's policy and manifest and the ADR 0057 plan."""

    routes_by_key = {route_key(route): route for route in policy["routes"]}
    universe = universe_routes(policy)
    evidence = {row["routeId"]: row for row in manifest["routeEvidence"]}
    failures = [
        _failure("INPUT_INVALID", scenario_id, "scenario listed twice")
        for scenario_id in _duplicates(row["scenarioId"] for row in ledger["scenarios"])
    ]
    for scenario in ledger["scenarios"]:
        failures.extend(scenario_binding_failures(scenario, routes_by_key=routes_by_key, universe=universe))
        failures.extend(
            scenario_input_failures(
                scenario, evidence=evidence, case_manifests=case_manifests, plan=plan, routes_by_key=routes_by_key
            )
        )
    failures.extend(debt_set_failures(ledger, plan))
    failures.extend(coverage_failures(ledger, universe=universe, evidence=evidence))
    return failures


def coverage_changes(ledger: Mapping[str, Any], baseline_ledger: Mapping[str, Any]) -> list[dict[str, Any]]:
    """Coverage changes since the baseline tag's ledger, in a stable order.

    An accepted gap is a change when its row is new or differs from the
    baseline's row for the route: a new gap, or the renewal of a gap under
    this release's approval (1.1.12 board decision 60, 1.1.13 decision 96).
    """

    current = {row["scenarioId"]: row for row in ledger["scenarios"]}
    before = {row["scenarioId"]: row for row in baseline_ledger["scenarios"]}
    changes = [{"kind": "scenario-retired", "subject": scenario_id} for scenario_id in sorted(set(before) - set(current))]
    changes.extend(
        {"kind": "input-revision", "subject": scenario_id}
        for scenario_id in sorted(set(before) & set(current))
        if current[scenario_id]["inputs"] != before[scenario_id]["inputs"]
        or current[scenario_id]["inputRevision"] != before[scenario_id]["inputRevision"]
    )
    changes.extend({"kind": "scenario-added", "subject": scenario_id} for scenario_id in sorted(set(current) - set(before)))
    accepted_before = {row["routeId"]: row for row in baseline_ledger["acceptedGaps"]}
    changes.extend(
        {"kind": "accepted-gap", "subject": row["routeId"]}
        for row in sorted(ledger["acceptedGaps"], key=lambda item: item["routeId"])
        if accepted_before.get(row["routeId"]) != row
    )
    return changes


def accepted_gap_scope_failures(ledger: Mapping[str, Any], candidate_version: str) -> list[Failure]:
    """An accepted gap covers only the release that approved it.

    Decision 60 declares a route without comparable inputs like a byte
    difference, with an approval, an entry and a CHANGELOG id each time, and
    decision 96 limits its approval to 1.1.13. A gap row carried from an
    earlier release therefore approves nothing for this one.
    """

    return [
        _failure(
            "COVERAGE_UNDISPOSED",
            row["routeId"],
            f"accepted gap approved in {row['approvedInVersion']} does not cover {candidate_version}",
        )
        for row in ledger["acceptedGaps"]
        if row["approvedInVersion"] != candidate_version
    ]


def monotonicity_failures(ledger: Mapping[str, Any], baseline_ledger: Mapping[str, Any]) -> list[Failure]:
    """An input revision increments the revision, and every removal leaves a retirement record."""

    failures: list[Failure] = []
    current = {row["scenarioId"]: row for row in ledger["scenarios"]}
    before = {row["scenarioId"]: row for row in baseline_ledger["scenarios"]}
    retired = {row["scenarioId"] for row in ledger["retiredScenarios"]}
    for scenario_id in sorted(set(before) & set(current)):
        old, new = before[scenario_id], current[scenario_id]
        if old["inputs"] != new["inputs"] and new["inputRevision"] <= old["inputRevision"]:
            failures.append(_failure("COVERAGE_UNDISPOSED", scenario_id, "inputs replaced without a new input revision"))
        if new["inputRevision"] < old["inputRevision"]:
            failures.append(_failure("COVERAGE_UNDISPOSED", scenario_id, "input revision went back"))
    for scenario_id in sorted(set(before) - set(current) - retired):
        failures.append(_failure("COVERAGE_UNDISPOSED", scenario_id, "scenario removed without a retirement record"))
    return failures


# ---------------------------------------------------------------------------
# Byte comparison evidence
# ---------------------------------------------------------------------------


def range_projection(ranges: Sequence[Mapping[str, int]] | None) -> dict[str, Any] | None:
    """The report comparison of a complete computed range list: totals, digest and the first 32 ranges."""

    if ranges is None:
        return None
    return {
        "differentByteCount": sum(item["endExclusive"] - item["start"] for item in ranges),
        "rangeCount": len(ranges),
        "rangeListSha256": canonical_json_sha256([dict(item) for item in ranges]),
        "ranges": [dict(item) for item in ranges[:REPORTED_RANGE_LIMIT]],
        "rangesTruncated": len(ranges) > REPORTED_RANGE_LIMIT,
    }


def _merged(spans: Iterable[tuple[int, int]]) -> list[tuple[int, int]]:
    result: list[tuple[int, int]] = []
    for start, end in sorted(spans):
        if result and start <= result[-1][1]:
            result[-1] = (result[-1][0], max(result[-1][1], end))
        else:
            result.append((start, end))
    return result


def range_list_failures(ranges: Sequence[Mapping[str, int]], subject: str, code: str = "REPORT_INVALID") -> list[Failure]:
    """Ranges are non-empty, half-open, ascending and disjoint."""

    spans = [(item["start"], item["endExclusive"]) for item in ranges]
    if not spans or any(start >= end for start, end in spans) or any(left[1] > right[0] for left, right in zip(spans, spans[1:])):
        return [_failure(code, subject, "ranges are not non-empty, half-open, ascending and disjoint")]
    return []


def scope_evidence_failures(
    subject: str,
    baseline: Mapping[str, Any] | None,
    candidate: Mapping[str, Any] | None,
    comparisons: Mapping[str, Mapping[str, Any] | None],
    evidence: ScopeEvidence | None,
    reported_scopes: Sequence[str] = SCOPES,
) -> list[Failure]:
    """Each scope both sides can be compared in has computed evidence that agrees with the identities and the report.

    `comparisons` maps each scope in `reported_scopes` to the report's
    comparison object. A scope is comparable when both sides carry that
    artifact; its evidence is then required, `None` exactly when the
    identities are equal, and a reported comparison must be its projection.
    A scope a side lacks has neither evidence nor a comparison.
    """

    failures: list[Failure] = []
    evidence = evidence or {}
    for scope in SCOPES:
        member = "comparison" if scope == "output" else "precursorComparison"
        for name, side in (("baseline", baseline), ("candidate", candidate)):
            if side is not None and scope not in side:
                failures.append(_failure("REPORT_INVALID", subject, f"{name} side is missing {scope}"))
        left = None if baseline is None else _identity(baseline.get(scope))
        right = None if candidate is None else _identity(candidate.get(scope))
        comparable = left is not None and right is not None
        if not comparable:
            if scope in evidence:
                failures.append(_failure("REPORT_INVALID", subject, f"{scope} evidence for a side without that artifact"))
            if comparisons.get(scope) is not None:
                failures.append(_failure("REPORT_INVALID", subject, f"{member} for a side without that artifact"))
            continue
        if scope not in evidence:
            failures.append(_failure("REPORT_INVALID", subject, f"{scope} comparison evidence is missing"))
            continue
        ranges = evidence[scope]
        if (left == right) != (ranges is None):
            failures.append(_failure("REPORT_INVALID", subject, f"{scope} identities and computed ranges disagree"))
        if ranges is not None:
            failures.extend(range_list_failures(ranges, f"{subject}:{scope}"))
        if scope in reported_scopes and comparisons.get(scope) != range_projection(ranges):
            failures.append(_failure("REPORT_INVALID", subject, f"{member} is not the projection of the computed ranges"))
    return failures


def declared_side(side: Mapping[str, Any]) -> dict[str, Any]:
    """The side outcome a declaration binds for one report side."""

    return {
        "result": "rejected" if side["status"] == "rejected" else "output",
        "output": _identity(side["output"]),
        "precursor": _identity(side["precursor"]),
        "stage": side["stoppedAt"],
        "issueCodes": sorted({issue["code"] for issue in side["issues"] if issue["severity"] == "error"}),
    }


def process_failure_issue_failures(subject: str, sides: Iterable[Mapping[str, Any] | None]) -> list[Failure]:
    """A process-failure issue at any severity is never an approvable product result.

    An `invalid` side is the faithful report of such a failure and is not repeated here.
    """

    codes = {
        issue["code"]
        for side in sides if side is not None and side.get("status") != "invalid"
        for issue in side.get("issues", ())
        if issue["code"] in PROCESS_FAILURE_ISSUE_CODES
    }
    return [_failure("PROCESS_FAILED", subject, f"process-failure issue {code}") for code in sorted(codes)]


# ---------------------------------------------------------------------------
# Rolling report
# ---------------------------------------------------------------------------


def scenario_value_failures(scenario: Mapping[str, Any], evidence: ScopeEvidence | None) -> list[Failure]:
    """Values of one rolling scenario result that a schema cannot compare, against its computed evidence."""

    subject = scenario["scenarioId"]
    failures = process_failure_issue_failures(subject, (scenario["baseline"], scenario["candidate"]))
    if scenario["outcome"] == "invalid":
        return failures
    baseline, candidate = scenario["baseline"], scenario["candidate"]
    failures.extend(scope_evidence_failures(
        subject,
        baseline,
        candidate,
        {"output": scenario["comparison"], "precursor": scenario["precursorComparison"]},
        evidence,
    ))
    differs = any((evidence or {}).get(scope) is not None for scope in SCOPES)
    if scenario["outcome"] == "equal" and differs:
        failures.append(_failure("REPORT_INVALID", subject, "an equal outcome with differing bytes"))
    if scenario["outcome"] == "different" and not differs:
        failures.append(_failure("REPORT_INVALID", subject, "a different outcome without differing bytes"))
    return failures


def artifact_presence_failures(scenario: Mapping[str, Any], ledger_row: Mapping[str, Any]) -> list[Failure]:
    """Each side carries the artifacts its ledger scenario obliges it to build.

    A scenario whose declared CtrlRAM base is `standard-merge` builds a
    precursor on each side before its output, so a side with an output, or
    rejected at `preview` or `build`, carries one, and a side rejected at a
    precursor stage carries none. Any other scenario has no precursor and no
    precursor stage. A failed side is left to its failure code.
    """

    if scenario["outcome"] == "invalid":
        return []
    merge = (ledger_row["ctrlRamBase"] or {}).get("kind") == "standard-merge"
    failures: list[Failure] = []
    for name in ("baseline", "candidate"):
        side = scenario[name]
        if side is None or side["status"] == "invalid":
            continue
        if not merge and side["stoppedAt"] in PRECURSOR_STAGES:
            failures.append(_failure("REPORT_INVALID", scenario["scenarioId"], f"{name} stopped at a precursor stage without a Standard Merge base"))
        built = merge and side["stoppedAt"] not in PRECURSOR_STAGES
        if (side["precursor"] is not None) != built:
            failures.append(
                _failure("REPORT_INVALID", scenario["scenarioId"], f"{name} precursor does not match the ledger's CtrlRAM base and its stage")
            )
    return failures


def rolling_coverage_report_failures(
    report: Mapping[str, Any],
    *,
    ledger: Mapping[str, Any],
    baseline_ledger: Mapping[str, Any],
    policy: Mapping[str, Any],
    manifest: Mapping[str, Any],
) -> list[Failure]:
    """A rolling report runs each ledger scenario exactly once and reports the ledger's coverage."""

    failures: list[Failure] = []
    active = {row["scenarioId"]: row for row in ledger["scenarios"]}
    reported = [row["scenarioId"] for row in report["scenarios"]]
    for scenario_id in _duplicates(reported):
        failures.append(_failure("REPORT_INVALID", scenario_id, "scenario reported more than once"))
    for scenario_id in sorted(set(active) - set(reported)):
        failures.append(_failure("REPORT_INVALID", scenario_id, "ledger scenario missing from the report"))
    for scenario_id in sorted(set(reported) - set(active)):
        failures.append(_failure("REPORT_INVALID", scenario_id, "reported scenario is not in the ledger"))
    for row in report["scenarios"]:
        declared = active.get(row["scenarioId"])
        if declared is not None and (row["routeId"], row["inputRevision"]) != (declared["routeId"], declared["inputRevision"]):
            failures.append(_failure("REPORT_INVALID", row["scenarioId"], "route or input revision differs from the ledger"))
    universe = universe_routes(policy)
    covered = {row["routeId"] for row in ledger["scenarios"]}
    debt = set(ledger["debtSet"]["routeIds"])
    accepted = {row["routeId"] for row in ledger["acceptedGaps"]}
    pending = set(ledger["pendingAcceptedGaps"]["routeIds"])
    expected_counts = {
        "universe": len(universe),
        "coveredRoutes": len(covered),
        "scenarios": len(active),
        "debtSetInUniverse": len(debt & universe),
        "acceptedGaps": len(accepted),
        "pendingAcceptedGaps": len(pending),
    }
    coverage = report["coverage"]
    for key, value in expected_counts.items():
        if coverage[key] != value:
            failures.append(_failure("REPORT_INVALID", f"coverage.{key}", f"reports {coverage[key]}, ledger gives {value}"))
    kinds = {row["routeId"]: row["kind"] for row in manifest["routeEvidence"]}
    expected_not_covered = []
    for route_id in sorted(universe - covered):
        reason = "debt-set" if route_id in debt else "accepted-gap" if route_id in accepted else "pending-gap" if route_id in pending else None
        if reason is not None:
            expected_not_covered.append({"routeId": route_id, "reason": reason, "evidenceKind": kinds.get(route_id, "missing")})
    if sorted(coverage["notCovered"], key=lambda row: row["routeId"]) != expected_not_covered:
        failures.append(_failure("REPORT_INVALID", "coverage.notCovered", "differs from the ledger's routes that no scenario compares"))
    expected_changes = [(row["kind"], row["subject"]) for row in coverage_changes(ledger, baseline_ledger)]
    if sorted((row["kind"], row["subject"]) for row in coverage["changesSinceBaseline"]) != sorted(expected_changes):
        failures.append(_failure("REPORT_INVALID", "coverage.changesSinceBaseline", "differs from the ledger changes since the baseline"))
    return failures


# ---------------------------------------------------------------------------
# Declaration
# ---------------------------------------------------------------------------


def declaration_failures(
    declaration: Mapping[str, Any],
    *,
    ledger: Mapping[str, Any],
    ledger_sha256: str,
    candidate_version: str,
    baseline_tag: str,
    baseline_tag_object: str,
    changelog: str,
) -> list[Failure]:
    """A declaration names this release, its baseline and ledger, and each id in the CHANGELOG section."""

    failures: list[Failure] = []
    if declaration["candidateVersion"] != candidate_version:
        failures.append(_failure("SOURCE_MISMATCH", "declaration", "declaration is for another version"))
    if declaration["baseline"] != {"tag": baseline_tag, "tagObject": baseline_tag_object}:
        failures.append(_failure("BASELINE_INVALID", "declaration", "declaration names another baseline"))
    if declaration["ledgerSha256"] != ledger_sha256:
        failures.append(_failure("SOURCE_MISMATCH", "declaration", "declaration binds another ledger"))
    known = {row["scenarioId"] for row in ledger["scenarios"]} | {row["scenarioId"] for row in ledger["retiredScenarios"]}
    for entry_id in _duplicates(entry["id"] for entry in declaration["entries"]):
        failures.append(_failure("STALE_DECLARATION", entry_id, "entry id used twice"))
    try:
        notes = render_release_notes(changelog, candidate_version)
    except ValueError as error:
        notes = ""
        failures.append(_failure("RELEASE_NOTE_MISSING", candidate_version, str(error)))
    for entry in declaration["entries"]:
        for side in (entry.get("expected") or {}).values():
            for code in sorted(set(side["issueCodes"]) & PROCESS_FAILURE_ISSUE_CODES):
                failures.append(_failure("PROCESS_FAILED", entry["id"], f"process-failure issue {code} cannot be declared"))
        match = ENTRY_ID.fullmatch(entry["id"])
        if match is None or match["version"] != candidate_version:
            failures.append(_failure("STALE_DECLARATION", entry["id"], "entry id does not name this version"))
        for scenario_id in entry["scenarioIds"]:
            if scenario_id not in known:
                failures.append(_failure("STALE_DECLARATION", entry["id"], f"unknown scenario {scenario_id}"))
        if notes and re.search(rf"(?<![0-9A-Za-z.-]){re.escape(entry['id'])}(?![0-9])", notes) is None:
            failures.append(_failure("RELEASE_NOTE_MISSING", entry["id"], "id absent from the CHANGELOG section"))
    return failures


def reproduction_failures(entry: Mapping[str, Any], scenario: Mapping[str, Any], evidence: ScopeEvidence | None) -> list[Failure]:
    """An outcome entry reproduces one scenario exactly: both sides and each compared scope's complete ranges."""

    subject = f"{entry['id']}:{scenario['scenarioId']}"
    if OUTCOME_ENTRY_KINDS.get(scenario["outcome"]) != entry["kind"]:
        return [_failure("STALE_DECLARATION", subject, f"a {entry['kind']} entry for a {scenario['outcome']} outcome")]
    failures = [
        _failure("STALE_DECLARATION", subject, f"{side} outcome is not reproduced")
        for side in ("baseline", "candidate")
        if entry["expected"][side] != declared_side(scenario[side])
    ]
    differences = entry["differences"] or {"output": None, "precursor": None}
    computed = evidence or {}
    for scope in SCOPES:
        declared = differences[scope]
        observed = computed.get(scope)
        if (declared is None) != (observed is None) or (
            declared is not None and [dict(item) for item in declared["ranges"]] != [dict(item) for item in observed]
        ):
            failures.append(_failure("STALE_DECLARATION", subject, f"{scope} difference is not reproduced"))
        if declared is None:
            continue
        totals = range_projection(declared["ranges"])
        assert totals is not None
        if any(declared[key] != totals[key] for key in ("differentByteCount", "rangeCount", "rangeListSha256")):
            failures.append(_failure("STALE_DECLARATION", subject, f"{scope} totals or digest do not match its ranges"))
        failures.extend(range_list_failures(declared["ranges"], f"{subject}:{scope}", "STALE_DECLARATION"))
        ranges = [(item["start"], item["endExclusive"]) for item in declared["ranges"]]
        spans = [(item["start"], item["endExclusive"]) for item in declared["attribution"]]
        overlapping = sum(end - start for start, end in spans) != sum(end - start for start, end in _merged(spans))
        if overlapping or _merged(spans) != _merged(ranges):
            failures.append(_failure("STALE_DECLARATION", subject, f"{scope} attribution does not cover exactly its ranges"))
    return failures


def declared_change_failures(
    report: Mapping[str, Any],
    declaration: Mapping[str, Any] | None,
    evidence: Mapping[str, ScopeEvidence | None],
    *,
    ledger: Mapping[str, Any],
    baseline_ledger: Mapping[str, Any],
    candidate_version: str,
) -> list[Failure]:
    """Every change of the run is declared exactly once, by subject, and every declared subject happened.

    Changes are the outcomes other than `equal` and `invalid`, keyed by
    scenario, and the coverage changes other than added scenarios, keyed by
    scenario or, for an accepted gap, by route. `evidence` maps each scenario
    id to its computed scope evidence.
    """

    entries = list((declaration or {"entries": []})["entries"])
    failures: list[Failure] = []
    scenarios = {row["scenarioId"]: row for row in report["scenarios"]}
    routes = {row["scenarioId"]: row["routeId"] for row in (*baseline_ledger["scenarios"], *ledger["scenarios"])}
    expected: set[tuple[str, str]] = {
        (OUTCOME_ENTRY_KINDS[row["outcome"]], scenario_id)
        for scenario_id, row in scenarios.items()
        if row["outcome"] in OUTCOME_ENTRY_KINDS
    }
    changes = [row for row in coverage_changes(ledger, baseline_ledger) if row["kind"] != "scenario-added"]
    expected |= {(row["kind"], row["subject"]) for row in changes}
    declared: dict[tuple[str, str], list[Mapping[str, Any]]] = {}
    for entry in entries:
        subjects = entry["routeIds"] if entry["kind"] == "accepted-gap" else entry["scenarioIds"]
        for subject in subjects:
            declared.setdefault((entry["kind"], subject), []).append(entry)
        if entry["kind"] != "accepted-gap":
            expected_routes = sorted({routes.get(subject, "") for subject in entry["scenarioIds"]})
            if sorted(entry["routeIds"]) != expected_routes:
                failures.append(_failure("STALE_DECLARATION", entry["id"], "route ids differ from the routes of its scenarios"))
    for key in sorted(expected):
        matching = declared.get(key, [])
        if len(matching) != 1:
            failures.append(_failure("UNDECLARED_CHANGE", key[1], f"{key[0]} is declared by {len(matching)} entries, not one"))
    for key in sorted(set(declared) - expected):
        for entry in declared[key]:
            failures.append(_failure("STALE_DECLARATION", f"{entry['id']}:{key[1]}", f"declares a {key[0]} that did not happen"))
    for scenario_id, row in scenarios.items():
        if row["outcome"] not in OUTCOME_ENTRY_KINDS:
            if row["declarationEntryId"] is not None:
                failures.append(_failure("STALE_DECLARATION", scenario_id, "an entry id on an unchanged or invalid scenario"))
            continue
        matching = declared.get((OUTCOME_ENTRY_KINDS[row["outcome"]], scenario_id), [])
        if len(matching) == 1:
            if row["declarationEntryId"] != matching[0]["id"]:
                failures.append(_failure("STALE_DECLARATION", scenario_id, "report names another entry than the one declaring it"))
            failures.extend(reproduction_failures(matching[0], row, evidence.get(scenario_id)))
    change_ids = {
        (row["kind"], row["subject"]): row["declarationEntryId"] for row in report["coverage"]["changesSinceBaseline"]
    }
    for row in changes:
        matching = declared.get((row["kind"], row["subject"]), [])
        entry_id = matching[0]["id"] if len(matching) == 1 else None
        if change_ids.get((row["kind"], row["subject"])) != entry_id:
            failures.append(_failure("STALE_DECLARATION", row["subject"], f"report names another entry for its {row['kind']}"))
        if entry_id is None or row["kind"] == "input-revision":
            continue
        entry = matching[0]
        ledger_rows = ledger["acceptedGaps"] if row["kind"] == "accepted-gap" else ledger["retiredScenarios"]
        key = "routeId" if row["kind"] == "accepted-gap" else "scenarioId"
        version_key = "approvedInVersion" if row["kind"] == "accepted-gap" else "retiredInVersion"
        ledger_row = next((item for item in ledger_rows if item[key] == row["subject"]), None)
        if ledger_row is None or (ledger_row["declarationEntryId"], ledger_row[version_key], ledger_row["approval"]) != (
            entry["id"],
            candidate_version,
            entry["approval"],
        ):
            failures.append(_failure("STALE_DECLARATION", row["subject"], f"ledger {row['kind']} record does not cite its entry"))
    return failures


# ---------------------------------------------------------------------------
# Source binding
# ---------------------------------------------------------------------------


class SourceAuthority(NamedTuple):
    """What a report must bind, measured by the comparator from Git and the files it applied."""

    candidate_commit: str
    candidate_tree: str
    baseline_tag: str
    baseline_tag_object: str
    baseline_commit: str
    comparator_sha256: str
    contracts: Mapping[str, str]
    ledger_sha256: str | None = None
    declaration_sha256: str | None = None
    amendment_sha256: str | None = None


def source_binding_failures(report: Mapping[str, Any], authority: SourceAuthority) -> list[Failure]:
    """A report binds the exact source, baseline, comparator and applied documents."""

    failures: list[Failure] = []
    candidate = report["candidate"]["executor"]
    if (candidate["commit"], candidate["tree"]) != (authority.candidate_commit, authority.candidate_tree):
        failures.append(_failure("SOURCE_MISMATCH", "candidate", "report does not come from the candidate source"))
    baseline = report["baseline"]
    if (baseline["tag"], baseline["executor"]["tagObject"], baseline["executor"]["commit"]) != (
        authority.baseline_tag,
        authority.baseline_tag_object,
        authority.baseline_commit,
    ):
        failures.append(_failure("BASELINE_INVALID", "baseline", "report names another baseline"))
    if report["comparator"]["scriptSha256"] != authority.comparator_sha256:
        failures.append(_failure("SOURCE_MISMATCH", "comparator", "report comes from another comparator"))
    listed = report["comparator"]["contracts"]
    applied = {row["path"]: row["sha256"] for row in listed}
    if applied != dict(authority.contracts) or len(applied) != len(listed):
        failures.append(_failure("SOURCE_MISMATCH", "contracts", "report binds other contract identities"))
    if report["mode"] == "rolling":
        bound = (("ledgerSha256", authority.ledger_sha256), ("declarationSha256", authority.declaration_sha256))
    else:
        bound = (("amendmentSha256", authority.amendment_sha256),)
    for member, expected in bound:
        if report[member] != expected:
            failures.append(_failure("SOURCE_MISMATCH", member, f"report binds another {member}"))
    return failures


def rolling_report_failures(
    report: Mapping[str, Any],
    *,
    ledger: Mapping[str, Any],
    baseline_ledger: Mapping[str, Any],
    policy: Mapping[str, Any],
    manifest: Mapping[str, Any],
    case_manifests: Mapping[str, Mapping[str, Any]],
    plan: Mapping[str, Any],
    declaration: Mapping[str, Any] | None,
    changelog: str,
    authority: SourceAuthority,
    evidence: Mapping[str, ScopeEvidence | None],
) -> list[Failure]:
    """Every semantic rule of a rolling report; the gate is clear only when this returns nothing."""

    failures = ledger_failures(ledger, policy=policy, manifest=manifest, case_manifests=case_manifests, plan=plan)
    failures.extend(monotonicity_failures(ledger, baseline_ledger))
    failures.extend(report_of_record_blockers(ledger))
    failures.extend(
        rolling_coverage_report_failures(report, ledger=ledger, baseline_ledger=baseline_ledger, policy=policy, manifest=manifest)
    )
    failures.extend(accepted_gap_scope_failures(ledger, report["candidate"]["version"]))
    active = {row["scenarioId"]: row for row in ledger["scenarios"]}
    for row in report["scenarios"]:
        failures.extend(scenario_value_failures(row, evidence.get(row["scenarioId"])))
        if row["scenarioId"] in active:
            failures.extend(artifact_presence_failures(row, active[row["scenarioId"]]))
        if row["outcome"] == "invalid":
            failures.append(_failure(row["failureCode"].removeprefix("PREDECESSOR_"), row["scenarioId"], "scenario is invalid"))
    if declaration is None and report["formal"]:
        failures.append(_failure("UNDECLARED_CHANGE", "declaration", "no declaration for this release"))
    if declaration is not None:
        failures.extend(
            declaration_failures(
                declaration,
                ledger=ledger,
                ledger_sha256=authority.ledger_sha256 or "",
                candidate_version=report["candidate"]["version"],
                baseline_tag=authority.baseline_tag,
                baseline_tag_object=authority.baseline_tag_object,
                changelog=changelog,
            )
        )
    failures.extend(
        declared_change_failures(
            report,
            declaration,
            evidence,
            ledger=ledger,
            baseline_ledger=baseline_ledger,
            candidate_version=report["candidate"]["version"],
        )
    )
    failures.extend(source_binding_failures(report, authority))
    return failures


def rolling_gate(failures: Sequence[Failure]) -> dict[str, Any]:
    """The rolling gate: clear only without any failure; failures sorted for a deterministic report."""

    ordered = sorted(set(failures), key=lambda failure: (failure.code, failure.subject, failure.detail))
    return {
        "result": "blocked" if ordered else "clear",
        "failures": [{"code": failure.code, "subject": failure.subject, "detail": failure.detail} for failure in ordered],
    }


# ---------------------------------------------------------------------------
# v0.9.16 1.x mode
# ---------------------------------------------------------------------------


class RouteDisposition(NamedTuple):
    """How the v0.9.16 1.x mode proves one plan route, from the plan and the amendment."""

    route_id: str
    capability_fingerprint: str
    proof_kind: str
    row: Mapping[str, Any] | None
    row_source: str | None
    row_member: str | None


def plan_selected_routes(plan: Mapping[str, Any], pinned_policy: Mapping[str, Any]) -> list[dict[str, Any]]:
    """The plan's routes, selected from the policy it pins, in policy order."""

    selection = plan["selection"]
    return [
        route
        for route in pinned_policy["routes"]
        if route["authoring"]["value"] == selection["authoring"]
        and route["publication"]["value"] == selection["publication"]
        and route["workflowId"] in selection["includedWorkflows"]
    ]


def amendment_binding_failures(amendment: Mapping[str, Any], plan: Mapping[str, Any]) -> list[Failure]:
    """The amendment binds the plan without its candidateAuthority member, and the plan's baseline."""

    binding = amendment["plan"]
    observed = {
        "withoutCandidateAuthorityJcsSha256": canonical_json_sha256(
            {key: value for key, value in plan.items() if key != "candidateAuthority"}
        ),
        "canonicalInputAuthorityJcsSha256": canonical_json_sha256(plan["canonicalInputAuthority"]),
        "policySha256": plan["policyBinding"]["sha256"],
        "baselineTagObject": plan["baseline"]["tagObject"],
        "baselinePeeledCommit": plan["baseline"]["peeledCommit"],
    }
    return [
        _failure("AMENDMENT_MISMATCH", key, "the amendment binds another plan")
        for key, value in observed.items()
        if binding[key] != value
    ]


def v0916_route_dispositions(
    plan: Mapping[str, Any], amendment: Mapping[str, Any], pinned_policy: Mapping[str, Any]
) -> tuple[list[RouteDisposition], list[Failure]]:
    """Each plan route's proof kind and approved row; an ambiguous or unbound row fails closed."""

    routes = plan_selected_routes(plan, pinned_policy)
    fingerprints = {route["routeId"]: route["capabilityFingerprint"] for route in routes}
    missing = set(plan["canonicalInputAuthority"]["currentlyMissingRouteIds"])
    transitive = {row["routeId"]: row for row in plan["transitiveRoutes"]}
    rows: dict[str, list[tuple[str, str, Mapping[str, Any]]]] = {}
    for source, document, member in (
        ("plan", plan, "approvedSemanticCorrections"),
        ("amendment", amendment, "approvedSemanticCorrections"),
        ("amendment", amendment, "baselineNotApplicable"),
    ):
        for row in document[member]:
            rows.setdefault(row["routeId"], []).append((source, member, row))
    failures: list[Failure] = []
    for route_id, listed in sorted(rows.items()):
        if len(listed) > 1 or route_id in transitive:
            failures.append(_failure("AMENDMENT_MISMATCH", route_id, "more than one disposition for one route"))
        for _, _, row in listed:
            if fingerprints.get(route_id) != row["capabilityFingerprint"]:
                failures.append(_failure("AMENDMENT_MISMATCH", route_id, "row does not name a plan route and fingerprint"))
            if route_id in missing:
                failures.append(_failure("AMENDMENT_MISMATCH", route_id, "row names a route without canonical input"))
    dispositions: list[RouteDisposition] = []
    for route in routes:
        route_id = route["routeId"]
        source = member = None
        row: Mapping[str, Any] | None = None
        if route_id in missing:
            proof_kind = "not-covered"
        elif route_id in transitive:
            proof_kind, row, source, member = "tp-prefix-transitive", transitive[route_id], "plan", "transitiveRoutes"
        elif route_id in rows:
            source, member, row = rows[route_id][0]
            proof_kind = (
                "exact-output-with-approved-semantic-correction"
                if member == "approvedSemanticCorrections"
                else "canonical-binding-not-applicable-to-v0916"
            )
        else:
            proof_kind = "exact-output"
        dispositions.append(RouteDisposition(route_id, route["capabilityFingerprint"], proof_kind, row, source, member))
    return dispositions, failures


class TransitiveEvidence(NamedTuple):
    """The actual result of the ADR 0057 `compare_transitive_payloads` for one route.

    `tp_length` is the length the primitive was given. `checks` is the
    mapping it returned when it passed; `failure_code` is the code of the
    `ParityError` it raised otherwise.
    """

    tp_length: int
    checks: Mapping[str, bool] | None
    failure_code: str | None


def transitive_evidence(
    baseline_full: bytes, candidate_full: bytes, candidate_tp: bytes, candidate_base: bytes, tp_length: int
) -> TransitiveEvidence:
    """Run `compare_transitive_payloads` on bytes the caller read and keep its result or its failure code."""

    try:
        result = compare_transitive_payloads(baseline_full, candidate_full, candidate_tp, candidate_base, tp_length)
    except ParityError as error:
        return TransitiveEvidence(tp_length, None, error.code)
    return TransitiveEvidence(tp_length, {check: result[check] for check in TRANSITIVE_CHECKS}, None)


def _transitive_checks_agree(reported: Mapping[str, Any], evidence: TransitiveEvidence) -> bool:
    """Whether the reported checks are what the primitive's result fixes.

    A pass fixes all three checks. The primitive checks the lengths, then both
    TP prefixes, then the tail, and stops at the first failure: a mutated
    tail fixes both prefix checks true and the tail false, a prefix mismatch
    fixes one prefix check false, and any other failure fixes one check false.
    """

    values = tuple(reported.get(check) for check in TRANSITIVE_CHECKS)
    if evidence.checks is not None:
        return values == tuple(evidence.checks[check] for check in TRANSITIVE_CHECKS)
    if evidence.failure_code == "PARITY_TAIL_MUTATED":
        return values == (True, True, False)
    if evidence.failure_code == "PARITY_TP_PREFIX_MISMATCH":
        return values[0] is False or values[1] is False
    return False in values


class V0916RouteEvidence(NamedTuple):
    """Computed evidence of one v0.9.16 route.

    `scopes` is the scope evidence of the route's two sides; `transitive` is
    the primitive's result for a transitive route (see `transitive_evidence`).
    """

    scopes: ScopeEvidence
    transitive: TransitiveEvidence | None = None


def _transitive_blocker(route: Mapping[str, Any], full_route: Mapping[str, Any] | None) -> str | None:
    """Why a transitive proof cannot run, or None when the three outputs it reads exist.

    The proof reads the candidate TP output and both outputs of the full
    route. `rejected`: a typed product rejection left one of them missing.
    `invalid`: a side failed otherwise, or the full route has no side.
    """

    sides = [route["candidate"]]
    sides += [None, None] if full_route is None else [full_route["baseline"], full_route["candidate"]]
    if all(side is not None and side["status"] == "output" for side in sides):
        return None
    if any(side is None or side["status"] == "invalid" for side in sides):
        return "invalid"
    return "rejected"


def _transitive_route_failures(
    route: Mapping[str, Any],
    row: Mapping[str, Any],
    evidence: V0916RouteEvidence | None,
    full_route: Mapping[str, Any] | None,
) -> list[Failure]:
    """A TP-prefix transitive route against its plan row, its full route and the primitive's result.

    A proof that cannot run is not asked for bytes: the route is `invalid`
    when a side failed and otherwise `inconsistent` with
    `PREDECESSOR_UNAPPROVED_DIFFERENCE`, and it reports and carries no proof.
    A proof that can run needs the primitive's result, run with the plan's
    TP length; a passing proof also fixes the TP output size.
    """

    subject = route["planRouteId"]
    failures: list[Failure] = []
    computed = None if evidence is None else evidence.transitive
    blocker = _transitive_blocker(route, full_route)
    if blocker is not None:
        # An invalid route's code is checked with every invalid route.
        expected = "invalid" if blocker == "invalid" else "inconsistent"
        if route["result"] != expected or (
            expected == "inconsistent" and route["failureCode"] != "PREDECESSOR_UNAPPROVED_DIFFERENCE"
        ):
            failures.append(_failure("REPORT_INVALID", subject, f"a transitive route whose proof cannot run is {expected}"))
        if route["transitive"] is not None:
            failures.append(_failure("REPORT_INVALID", subject, "reports a transitive proof that could not run"))
        if computed is not None:
            failures.append(_failure("REPORT_INVALID", subject, "transitive evidence for a proof that could not run"))
        return failures
    if evidence is not None:
        failures.extend(
            scope_evidence_failures(
                subject, route["baseline"], route["candidate"], {"output": route["comparison"]}, evidence.scopes, ("output",)
            )
        )
    reported = route["transitive"] or {}
    if (reported.get("fullRouteId"), reported.get("tpLength")) != (row.get("fullRouteId"), row.get("tpLength")):
        failures.append(_failure("REPORT_INVALID", subject, "transitive proof names another full route or TP length than the plan"))
    if computed is None:
        failures.append(_failure("REPORT_INVALID", subject, "transitive evidence is missing"))
    else:
        if computed.tp_length != row.get("tpLength"):
            failures.append(_failure("REPORT_INVALID", subject, "transitive evidence was computed with another TP length than the plan"))
        if not _transitive_checks_agree(reported, computed):
            failures.append(_failure("REPORT_INVALID", subject, "transitive checks differ from the computed proof"))
        if computed.checks is not None and len({route["candidate"]["output"]["size"], computed.tp_length, row.get("tpLength")}) != 1:
            failures.append(_failure("REPORT_INVALID", subject, "a passing proof whose TP output size is not the plan and proof TP length"))
    passed = computed is not None and computed.checks is not None and all(computed.checks[check] for check in TRANSITIVE_CHECKS)
    if route["result"] == "consistent" and (not passed or (full_route or {}).get("result") != "consistent"):
        failures.append(_failure("UNAPPROVED_DIFFERENCE", subject, "a consistent transitive route without a passing proof and full route"))
    return failures


def _v0916_route_failures(
    route: Mapping[str, Any],
    disposition: RouteDisposition,
    evidence: V0916RouteEvidence | None,
    routes: Mapping[str, Mapping[str, Any]],
) -> list[Failure]:
    subject = route["planRouteId"]
    failures = process_failure_issue_failures(subject, (route["baseline"], route["candidate"]))
    if route["result"] == "not-covered":
        return failures
    # A route is invalid when a side fails for a reason that is not a product
    # result, with the codes of the shared execution failures (contract).
    if route["result"] == "invalid" and route["failureCode"] not in EXECUTION_FAILURE_CODES:
        failures.append(_failure("REPORT_INVALID", subject, "an invalid route without a shared execution failure code"))
    if disposition.proof_kind == "tp-prefix-transitive":
        row = disposition.row or {}
        return failures + _transitive_route_failures(route, row, evidence, routes.get(row.get("fullRouteId", "")))
    if route["result"] == "invalid":
        return failures
    if evidence is None:
        return [_failure("REPORT_INVALID", subject, "computed evidence for a run route is missing")]
    # A v0.9.16 route reports only its output comparison; its consistency is its
    # proof kind's (a precursor enters only through a not-applicable row).
    failures.extend(
        scope_evidence_failures(
            subject, route["baseline"], route["candidate"], {"output": route["comparison"]}, evidence.scopes, ("output",)
        )
    )
    output_differs = evidence.scopes.get("output") is not None
    baseline_output = (route["baseline"] or {}).get("output")
    candidate_output = (route["candidate"] or {}).get("output")
    if disposition.proof_kind == "exact-output" and (route["baseline"] is None or route["candidate"] is None):
        failures.append(_failure("REPORT_INVALID", subject, "an exact-output route is missing a report side"))
    if disposition.proof_kind == "exact-output" and route["result"] == "consistent" and output_differs:
        failures.append(_failure("UNAPPROVED_DIFFERENCE", subject, "a consistent exact-output route with differing bytes"))
    if (
        disposition.proof_kind == "exact-output"
        and not output_differs
        and baseline_output is not None
        and _identity(baseline_output) == _identity(candidate_output)
        and route["result"] != "consistent"
    ):
        failures.append(_failure("REPORT_INVALID", subject, "an exact-output route with equal outputs must be consistent"))
    if disposition.proof_kind == "exact-output-with-approved-semantic-correction" and route["result"] == "consistent":
        row = disposition.row or {}
        observed = {
            "baselineOutput": _identity(route["baseline"]["output"]),
            "candidateOutput": _identity(route["candidate"]["output"]),
            "differentRanges": None if not output_differs else [dict(item) for item in evidence.scopes["output"]],
        }
        observed["differentByteCount"] = None if route["comparison"] is None else route["comparison"]["differentByteCount"]
        for key, value in observed.items():
            if value != row.get(key):
                failures.append(_failure("AMENDMENT_MISMATCH", subject, f"{key} does not reproduce the approved row"))
    if disposition.proof_kind == "canonical-binding-not-applicable-to-v0916" and route["result"] == "consistent":
        row = disposition.row or {}
        baseline, candidate = route["baseline"], route["candidate"]
        observed_rows = {
            "precursor": (_identity(baseline["precursor"]), _identity(candidate["precursor"])),
            "stage": baseline["stoppedAt"],
            "issueCodes": sorted({issue["code"] for issue in baseline["issues"] if issue["severity"] == "error"}),
            "output": _identity(candidate["output"]),
        }
        expected_rows = {
            "precursor": (row["expectedBaseline"]["precursorOutput"], row["expectedCandidate"]["precursorOutput"]),
            "stage": row["expectedBaseline"]["rejectingStage"],
            "issueCodes": sorted(row["expectedBaseline"]["issueCodes"]),
            "output": row["expectedCandidate"]["output"],
        }
        for key, value in observed_rows.items():
            if value != expected_rows[key]:
                failures.append(_failure("AMENDMENT_MISMATCH", subject, f"{key} does not reproduce the approved row"))
    return failures


def v0916_result(route_results: Sequence[str]) -> str:
    """Any invalid route makes the run invalid; otherwise any inconsistent route makes it inconsistent."""

    if "invalid" in route_results:
        return "invalid"
    if "inconsistent" in route_results:
        return "inconsistent"
    return "consistent"


def v0916_report_failures(
    report: Mapping[str, Any],
    dispositions: Sequence[RouteDisposition],
    evidence: Mapping[str, V0916RouteEvidence | None],
) -> list[Failure]:
    """A v0.9.16 1.x report covers each plan route once with its proof, row, evidence, summary and result."""

    failures: list[Failure] = []
    expected = {row.route_id: row for row in dispositions}
    reported = [route["planRouteId"] for route in report["routes"]]
    for route_id in _duplicates(reported):
        failures.append(_failure("REPORT_INVALID", route_id, "route reported more than once"))
    for route_id in sorted(set(expected) - set(reported)):
        failures.append(_failure("REPORT_INVALID", route_id, "plan route missing from the report"))
    for route_id in sorted(set(reported) - set(expected)):
        failures.append(_failure("REPORT_INVALID", route_id, "reported route is not a plan route"))
    routes = {route["planRouteId"]: route for route in report["routes"]}
    results = {route_id: route["result"] for route_id, route in routes.items()}
    for route in report["routes"]:
        disposition = expected.get(route["planRouteId"])
        if disposition is None:
            continue
        subject = route["planRouteId"]
        if (
            route["planCapabilityFingerprint"] != disposition.capability_fingerprint
            or route["proofKind"] != disposition.proof_kind
        ):
            failures.append(_failure("REPORT_INVALID", subject, "proof kind or fingerprint differs from the plan"))
            continue
        declared_row = {"source": disposition.row_source, "member": disposition.row_member, "routeId": subject}
        if disposition.row_member in {"approvedSemanticCorrections", "baselineNotApplicable"} and (
            route["dispositionRow"] != declared_row
        ):
            failures.append(_failure("REPORT_INVALID", subject, "disposition row differs from the plan and amendment"))
        failures.extend(_v0916_route_failures(route, disposition, evidence.get(subject), routes))
    values = list(results.values())
    summary = {
        "consistent": values.count("consistent"),
        "inconsistent": values.count("inconsistent"),
        "invalid": values.count("invalid"),
        "notCovered": values.count("not-covered"),
    }
    if report["summary"] != summary:
        failures.append(_failure("REPORT_INVALID", "summary", "summary differs from the route results"))
    if report["result"] != v0916_result(values):
        failures.append(_failure("REPORT_INVALID", "result", "result differs from the route results"))
    return failures
