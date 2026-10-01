"""Behavioral tests for the predecessor comparison's single semantic validator.

Every rule is tested on a fresh, complete and valid document set built from
the committed ledger, capability policy, canonical manifest and case
manifests, the ADR 0057 plan, its pinned policy and the amendment, with
synthetic payload-free run values. Each case first shows that the valid set
passes, then changes one fact and expects the failure of the rule under test,
named by its code and detail, and a blocked gate. No firmware byte is read.
"""

from __future__ import annotations

import copy
import json
import subprocess
import unittest
from typing import Any, Callable

from scripts import predecessor_validation as validation
from scripts.render_release_notes import REQUIRED_FEATURE_FIELDS
from tests.scripts.v0916_parity_test_support import MODULE, ROOT

CONTRACTS = ROOT / "docs" / "contracts"
CANONICAL = ROOT / "testdata" / "golden" / "canonical"
SUCCESSOR_ROUTE = "route-7-nt51950-8-ab-merge-9-2-plus-ic-23-nt51950-ab-cascade-maps"
VERSION = "1.1.13"
APPROVAL = {"boardDecision": "1.1.13 board decision 96", "role": "firmware-owner", "date": "2026-09-27"}


def load_json(path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def git_json(commit: str, relative: str) -> Any:
    return json.loads(subprocess.check_output(["git", "cat-file", "blob", f"{commit}:{relative}"], cwd=ROOT))


class Sources:
    """The committed documents, loaded once and deep-copied into every world."""

    ledger = load_json(CONTRACTS / "predecessor-comparison-scenarios-v1.json")
    policy = load_json(CONTRACTS / "canonical-capability-policy-v1.json")
    manifest = load_json(CANONICAL / "manifest.json")
    plan = load_json(CONTRACTS / "v0916-parity-certification-v1.json")
    amendment = load_json(CONTRACTS / "v0916-parity-1x-amendment-v1.json")
    case_manifests = {row["caseId"]: load_json(CANONICAL / row["manifestPath"]) for row in manifest["cases"]}
    pinned_policy = git_json(
        plan["canonicalInputAuthority"]["repositoryCommit"], plan["policyBinding"]["path"]
    )


def codes(failures: list[validation.Failure]) -> set[str]:
    return {failure.code for failure in failures}


def changelog(ids: list[str]) -> str:
    fields = "\n".join(f"{field} example." for field in REQUIRED_FEATURE_FIELDS)
    return (
        f"# Changelog\n\n## [{VERSION}]\n\n### Summary\n\nExample release.\n\n### Product changes\n\n"
        f"#### 1. Example change\n\n{fields}\n- Declared: {' '.join(ids)}.\n\n### Security\n\nNone.\n\n"
        "### Known issues\n\nNone.\n\n### Upgrade and rollback\n\nReplace the package.\n\n"
        "### Downloads and integrity\n\nChecksums.\n"
    )


def artifact(character: str) -> dict[str, Any]:
    return {"size": 65536, "sha256": character * 64}


def output_side(output: str, precursor: str | None = None) -> dict[str, Any]:
    return {
        "status": "output",
        "stoppedAt": None,
        "issues": [{"code": "input.address-space.truncated", "severity": "warning", "source": "report"}],
        "output": artifact(output),
        "precursor": None if precursor is None else artifact(precursor),
    }


def rejected_side(precursor: str | None = None) -> dict[str, Any]:
    return {
        "status": "rejected",
        "stoppedAt": "preview",
        "issues": [{"code": "profile.v2.compile.map-selection-invalid", "severity": "error", "source": "report"}],
        "output": None,
        "precursor": None if precursor is None else artifact(precursor),
    }


def spans(count: int, offset: int) -> list[dict[str, int]]:
    return [{"start": offset + 4 * index, "endExclusive": offset + 4 * index + 2} for index in range(count)]


def difference(ranges: list[dict[str, int]]) -> dict[str, Any]:
    totals = validation.range_projection(ranges)
    return {
        "differentByteCount": totals["differentByteCount"],
        "rangeCount": totals["rangeCount"],
        "rangeListSha256": totals["rangeListSha256"],
        "ranges": [dict(item) for item in ranges],
        "attribution": [
            {
                **item,
                "mechanism": "writes-different-bytes",
                "cause": "example",
                "causeVerification": "not-independently-verified",
                "evidence": [],
            }
            for item in ranges
        ],
    }


def rolling_world() -> dict[str, Any]:
    """A complete, valid rolling run: decision 96's gaps, one retirement, and three declared changes."""

    baseline_ledger = copy.deepcopy(Sources.ledger)
    ledger = copy.deepcopy(Sources.ledger)
    entries: list[dict[str, Any]] = []

    def entry(kind: str, scenario_ids: list[str], route_ids: list[str], **values: Any) -> dict[str, Any]:
        row = {
            "id": f"RP-{VERSION}-{len(entries) + 1:02d}",
            "kind": kind,
            "scenarioIds": scenario_ids,
            "routeIds": route_ids,
            "expected": values.get("expected"),
            "differences": values.get("differences"),
            "knownIssue": values.get("knownIssue"),
            "routeWithdrawal": False,
            "approval": dict(APPROVAL),
        }
        entries.append(row)
        return row

    for route_id in ledger["pendingAcceptedGaps"]["routeIds"]:
        gap = entry("accepted-gap", [], [route_id])
        ledger["acceptedGaps"].append(
            {
                "routeId": route_id,
                "approvedInVersion": VERSION,
                "reason": "no canonical input (decision 96)",
                "approval": dict(APPROVAL),
                "declarationEntryId": gap["id"],
            }
        )
    ledger["pendingAcceptedGaps"]["routeIds"] = []
    routes = [row["routeId"] for row in ledger["scenarios"]]
    retired = next(row for row in ledger["scenarios"] if row["origin"] == "decision-64" and routes.count(row["routeId"]) > 1)
    ledger["scenarios"].remove(retired)
    retirement = entry("scenario-retired", [retired["scenarioId"]], [retired["routeId"]])
    ledger["retiredScenarios"].append(
        {
            "scenarioId": retired["scenarioId"],
            "retiredInVersion": VERSION,
            "reason": "example retirement",
            "approval": dict(APPROVAL),
            "declarationEntryId": retirement["id"],
        }
    )

    full_flash = [row for row in ledger["scenarios"] if (row["ctrlRamBase"] or {}).get("kind") == "standard-merge"]
    plain = next(row for row in ledger["scenarios"] if row["ctrlRamBase"] is None)
    plain_equal = next(row for row in ledger["scenarios"] if row["ctrlRamBase"] is None and row is not plain)
    precursor_only, rejects, merge_equal = full_flash[0], full_flash[1], full_flash[2]
    scenarios: list[dict[str, Any]] = []
    evidence: dict[str, Any] = {}
    for row in ledger["scenarios"]:
        has_precursor = row in full_flash
        scenario = {
            "scenarioId": row["scenarioId"],
            "routeId": row["routeId"],
            "inputRevision": row["inputRevision"],
            "outcome": "equal",
            "baseline": output_side("a", "b" if has_precursor else None),
            "candidate": output_side("a", "b" if has_precursor else None),
            "comparison": None,
            "precursorComparison": None,
            "failureCode": None,
            "declarationEntryId": None,
            "informational": [],
        }
        scope: dict[str, Any] = {"output": None}
        if has_precursor:
            scope["precursor"] = None
        if row is plain:
            scope["output"] = spans(33, 0)
            scenario.update(outcome="different", candidate=output_side("d"))
        elif row is precursor_only:
            scope["precursor"] = spans(1, 8192)
            scenario.update(outcome="different", candidate=output_side("a", "c"))
        elif row is rejects:
            scope = {"precursor": spans(2, 16384)}
            scenario.update(outcome="baseline-rejects", baseline=rejected_side("b"), candidate=output_side("a", "c"))
        scenario["comparison"] = validation.range_projection(scope.get("output"))
        scenario["precursorComparison"] = validation.range_projection(scope.get("precursor"))
        scenarios.append(scenario)
        evidence[row["scenarioId"]] = scope
    by_id = {row["scenarioId"]: row for row in scenarios}
    for row, kind in ((plain, "byte-difference"), (precursor_only, "byte-difference"), (rejects, "baseline-rejects")):
        scenario = by_id[row["scenarioId"]]
        scope = evidence[row["scenarioId"]]
        declared = entry(
            kind,
            [row["scenarioId"]],
            [row["routeId"]],
            expected={
                "baseline": validation.declared_side(scenario["baseline"]),
                "candidate": validation.declared_side(scenario["candidate"]),
            },
            differences={
                "output": None if scope.get("output") is None else difference(scope["output"]),
                "precursor": None if scope.get("precursor") is None else difference(scope["precursor"]),
            },
        )
        scenario["declarationEntryId"] = declared["id"]
    changes = []
    for change in validation.coverage_changes(ledger, baseline_ledger):
        matching = [
            item["id"]
            for item in entries
            if item["kind"] == change["kind"]
            and change["subject"] in (item["routeIds"] if item["kind"] == "accepted-gap" else item["scenarioIds"])
        ]
        changes.append({**change, "declarationEntryId": matching[0] if matching else None})
    universe = validation.universe_routes(Sources.policy)
    covered = {row["routeId"] for row in ledger["scenarios"]}
    debt = set(ledger["debtSet"]["routeIds"])
    accepted = {row["routeId"] for row in ledger["acceptedGaps"]}
    kinds = {row["routeId"]: row["kind"] for row in Sources.manifest["routeEvidence"]}
    report = {
        "mode": "rolling",
        "formal": True,
        "candidate": {"version": VERSION, "executor": {"commit": "1" * 40, "tree": "2" * 40}},
        "baseline": {"tag": "v1.1.12", "executor": {"tagObject": "3" * 40, "commit": "4" * 40}},
        "comparator": {
            "scriptSha256": "a" * 64,
            "contracts": [{"path": "docs/contracts/predecessor-comparison-v1.json", "sha256": "b" * 64}],
        },
        "ledgerSha256": "c" * 64,
        "declarationSha256": "d" * 64,
        "scenarios": scenarios,
        "coverage": {
            "universe": len(universe),
            "coveredRoutes": len(covered),
            "scenarios": len(ledger["scenarios"]),
            "debtSetInUniverse": len(debt & universe),
            "acceptedGaps": len(accepted),
            "pendingAcceptedGaps": 0,
            "notCovered": [
                {"routeId": route_id, "reason": "debt-set" if route_id in debt else "accepted-gap", "evidenceKind": kinds.get(route_id, "missing")}
                for route_id in sorted(universe - covered)
            ],
            "changesSinceBaseline": changes,
        },
    }
    declaration = {
        "candidateVersion": VERSION,
        "baseline": {"tag": "v1.1.12", "tagObject": "3" * 40},
        "ledgerSha256": "c" * 64,
        "entries": entries,
    }
    authority = validation.SourceAuthority(
        candidate_commit="1" * 40,
        candidate_tree="2" * 40,
        baseline_tag="v1.1.12",
        baseline_tag_object="3" * 40,
        baseline_commit="4" * 40,
        comparator_sha256="a" * 64,
        contracts={"docs/contracts/predecessor-comparison-v1.json": "b" * 64},
        ledger_sha256="c" * 64,
        declaration_sha256="d" * 64,
    )
    return {
        "report": report,
        "ledger": ledger,
        "baseline_ledger": baseline_ledger,
        "policy": Sources.policy,
        "manifest": Sources.manifest,
        "case_manifests": Sources.case_manifests,
        "plan": Sources.plan,
        "declaration": declaration,
        "changelog": changelog([item["id"] for item in entries]),
        "authority": authority,
        "evidence": evidence,
        "names": {
            "plain": plain["scenarioId"],
            "precursor_only": precursor_only["scenarioId"],
            "rejects": rejects["scenarioId"],
            "retired": retired["scenarioId"],
            "equal": plain_equal["scenarioId"],
            "merge_equal": merge_equal["scenarioId"],
        },
    }


def rolling_failures(world: dict[str, Any]) -> list[validation.Failure]:
    arguments = {key: value for key, value in world.items() if key != "names"}
    report = arguments.pop("report")
    return validation.rolling_report_failures(report, **arguments)


def scenario(world: dict[str, Any], name: str) -> dict[str, Any]:
    target = world["names"][name]
    return next(row for row in world["report"]["scenarios"] if row["scenarioId"] == target)


def entry_for(world: dict[str, Any], name: str) -> dict[str, Any]:
    target = world["names"][name]
    return next(row for row in world["declaration"]["entries"] if target in row["scenarioIds"])


class RollingValidationTests(unittest.TestCase):
    """Each rolling rule on a fresh valid world: the valid world passes, one change fails its rule."""

    def test_process_start_failure_cannot_be_approved_as_a_rejection(self) -> None:
        world = rolling_world()
        self.assertEqual([], rolling_failures(world))
        row = scenario(world, "rejects")
        row["baseline"]["issues"][0]["code"] = "external-tool.process.start-failed"
        entry_for(world, "rejects")["expected"]["baseline"] = validation.declared_side(row["baseline"])
        self.assertIn("PREDECESSOR_PROCESS_FAILED", codes(rolling_failures(world)))
        self.assertEqual("blocked", validation.rolling_gate(rolling_failures(world))["result"])

    def test_process_failures_at_any_severity_block_output_sides(self) -> None:
        for code in validation.PROCESS_FAILURE_ISSUE_CODES:
            for severity in ("error", "warning", "info", "unspecified"):
                with self.subTest(code=code, severity=severity):
                    world = rolling_world()
                    self.assertEqual([], rolling_failures(world))
                    scenario(world, "equal")["candidate"]["issues"] = [
                        {"code": code, "severity": severity, "source": "report"}
                    ]
                    self.assertIn("PREDECESSOR_PROCESS_FAILED", codes(rolling_failures(world)))

    def assert_each_mutation_fails(self, cases: dict[str, tuple[Callable[[dict[str, Any]], None], str, str]]) -> None:
        for label, (mutate, code, fragment) in cases.items():
            with self.subTest(case=label):
                world = rolling_world()
                self.assertEqual([], rolling_failures(world))
                mutate(world)
                failures = rolling_failures(world)
                self.assertTrue(
                    any(failure.code == code and fragment in failure.detail for failure in failures),
                    f"{label}: expected {code} '{fragment}', got {failures}",
                )
                self.assertEqual("blocked", validation.rolling_gate(failures)["result"])

    def test_the_valid_world_passes_and_its_gate_is_clear(self) -> None:
        world = rolling_world()
        self.assertEqual([], rolling_failures(world))
        self.assertEqual({"result": "clear", "failures": []}, validation.rolling_gate(rolling_failures(world)))
        self.assertEqual(11, sum(1 for row in world["declaration"]["entries"] if row["kind"] == "accepted-gap"))
        self.assertTrue(scenario(world, "plain")["comparison"]["rangesTruncated"])

    def test_report_covers_each_ledger_scenario_and_the_ledger_coverage(self) -> None:
        def missing(world):
            world["report"]["scenarios"].remove(scenario(world, "equal"))

        def duplicated(world):
            world["report"]["scenarios"].append(copy.deepcopy(scenario(world, "equal")))

        def foreign(world):
            extra = copy.deepcopy(scenario(world, "equal"))
            extra["scenarioId"] += "-foreign"
            world["report"]["scenarios"].append(extra)
            world["evidence"][extra["scenarioId"]] = world["evidence"][world["names"]["equal"]]

        cases = {
            "missing-equal-scenario": (missing, "PREDECESSOR_REPORT_INVALID", "missing from the report"),
            "duplicated-scenario": (duplicated, "PREDECESSOR_REPORT_INVALID", "more than once"),
            "scenario-not-in-ledger": (foreign, "PREDECESSOR_REPORT_INVALID", "not in the ledger"),
            "wrong-route": (
                lambda world: scenario(world, "equal").update(routeId=SUCCESSOR_ROUTE),
                "PREDECESSOR_REPORT_INVALID",
                "route or input revision",
            ),
            "wrong-input-revision": (
                lambda world: scenario(world, "equal").update(inputRevision=2),
                "PREDECESSOR_REPORT_INVALID",
                "route or input revision",
            ),
            "wrong-count": (
                lambda world: world["report"]["coverage"].update(coveredRoutes=world["report"]["coverage"]["coveredRoutes"] - 1),
                "PREDECESSOR_REPORT_INVALID",
                "ledger gives",
            ),
            "forged-debt-reason": (
                lambda world: next(
                    row for row in world["report"]["coverage"]["notCovered"] if row["reason"] == "accepted-gap"
                ).update(reason="debt-set"),
                "PREDECESSOR_REPORT_INVALID",
                "no scenario compares",
            ),
            "deleted-coverage-change": (
                lambda world: world["report"]["coverage"]["changesSinceBaseline"].pop(),
                "PREDECESSOR_REPORT_INVALID",
                "changes since the baseline",
            ),
        }
        self.assert_each_mutation_fails(cases)

    def test_scope_evidence_and_rejected_outcomes(self) -> None:
        def unreported_rejection_precursor(world):
            scenario(world, "rejects")["precursorComparison"] = None

        def undeclared_rejection_precursor(world):
            entry_for(world, "rejects")["differences"] = None

        def rejection_attribution_gap(world):
            entry_for(world, "rejects")["differences"]["precursor"]["attribution"].pop()

        def rejection_range_drift(world):
            declared = entry_for(world, "rejects")["differences"]["precursor"]
            declared["ranges"][0]["endExclusive"] += 1

        def evidence_missing(world):
            del world["evidence"][world["names"]["precursor_only"]]["precursor"]

        def equal_with_bytes(world):
            world["evidence"][world["names"]["equal"]]["output"] = spans(1, 0)
            scenario(world, "equal")["comparison"] = validation.range_projection(spans(1, 0))

        def one_side_precursor(world):
            scenario(world, "equal")["candidate"]["precursor"] = artifact("e")

        cases = {
            "rejection-precursor-difference-not-reported": (
                unreported_rejection_precursor,
                "PREDECESSOR_REPORT_INVALID",
                "precursorComparison is not the projection",
            ),
            "rejection-precursor-difference-not-declared": (
                undeclared_rejection_precursor,
                "PREDECESSOR_STALE_DECLARATION",
                "precursor difference is not reproduced",
            ),
            "rejection-precursor-attribution-gap": (
                rejection_attribution_gap,
                "PREDECESSOR_STALE_DECLARATION",
                "attribution does not cover",
            ),
            "rejection-precursor-range-drift": (
                rejection_range_drift,
                "PREDECESSOR_STALE_DECLARATION",
                "precursor difference is not reproduced",
            ),
            "comparison-evidence-missing": (evidence_missing, "PREDECESSOR_REPORT_INVALID", "evidence is missing"),
            "equal-outcome-with-differing-bytes": (equal_with_bytes, "PREDECESSOR_REPORT_INVALID", "equal outcome"),
            "precursor-on-one-side": (one_side_precursor, "PREDECESSOR_REPORT_INVALID", "precursor does not match the ledger"),
        }
        self.assert_each_mutation_fails(cases)

    def test_complete_ranges_beyond_the_reported_32(self) -> None:
        def run_33rd_range(world):
            ranges = world["evidence"][world["names"]["plain"]]["output"]
            ranges[32]["endExclusive"] += 1
            scenario(world, "plain")["comparison"] = validation.range_projection(ranges)

        def declared_first_32_only(world):
            declared = entry_for(world, "plain")["differences"]["output"]
            declared["ranges"] = declared["ranges"][:32]

        def report_first_32(world):
            scenario(world, "plain")["comparison"]["ranges"][5]["start"] += 1

        def output_hash_drift(world):
            entry_for(world, "plain")["expected"]["candidate"]["output"]["sha256"] = "e" * 64

        cases = {
            "run-33rd-range": (run_33rd_range, "PREDECESSOR_STALE_DECLARATION", "output difference is not reproduced"),
            "declared-first-32-only": (declared_first_32_only, "PREDECESSOR_STALE_DECLARATION", "output difference is not reproduced"),
            "report-first-32": (report_first_32, "PREDECESSOR_REPORT_INVALID", "comparison is not the projection"),
            "declared-output-hash": (output_hash_drift, "PREDECESSOR_STALE_DECLARATION", "candidate outcome is not reproduced"),
        }
        self.assert_each_mutation_fails(cases)

    def test_range_values_and_declared_totals(self) -> None:
        def computed(world):
            return world["evidence"][world["names"]["plain"]]["output"]

        def declared(world):
            return entry_for(world, "plain")["differences"]["output"]

        def unsorted_computed(world):
            ranges = computed(world)
            ranges[0], ranges[1] = ranges[1], ranges[0]
            scenario(world, "plain")["comparison"] = validation.range_projection(ranges)

        def overlapping_computed(world):
            ranges = computed(world)
            ranges[0]["endExclusive"] = ranges[1]["start"] + 1
            scenario(world, "plain")["comparison"] = validation.range_projection(ranges)

        def overlapping_declared(world):
            ranges = declared(world)["ranges"]
            ranges[0]["endExclusive"] = ranges[1]["start"] + 1

        cases = {
            "unsorted-computed-ranges": (unsorted_computed, "PREDECESSOR_REPORT_INVALID", "ranges are not non-empty"),
            "overlapping-computed-ranges": (overlapping_computed, "PREDECESSOR_REPORT_INVALID", "ranges are not non-empty"),
            "overlapping-declared-ranges": (overlapping_declared, "PREDECESSOR_STALE_DECLARATION", "ranges are not non-empty"),
            "declared-byte-count": (
                lambda world: declared(world).update(differentByteCount=declared(world)["differentByteCount"] + 1),
                "PREDECESSOR_STALE_DECLARATION",
                "totals or digest",
            ),
            "declared-digest": (
                lambda world: declared(world).update(rangeListSha256="0" * 64),
                "PREDECESSOR_STALE_DECLARATION",
                "totals or digest",
            ),
            "reported-byte-count": (
                lambda world: scenario(world, "plain")["comparison"].update(differentByteCount=1),
                "PREDECESSOR_REPORT_INVALID",
                "comparison is not the projection",
            ),
        }
        self.assert_each_mutation_fails(cases)

    def test_artifacts_follow_the_ledger(self) -> None:
        def reject_before_the_precursor(world):
            row = scenario(world, "rejects")
            row.update(baseline={**rejected_side(), "stoppedAt": "precursor-build"}, precursorComparison=None)
            world["evidence"][row["scenarioId"]] = {}
            entry_for(world, "rejects").update(
                expected={"baseline": validation.declared_side(row["baseline"]), "candidate": validation.declared_side(row["candidate"])},
                differences=None,
            )

        # A Standard Merge side rejected before its precursor exists carries none.
        world = rolling_world()
        reject_before_the_precursor(world)
        self.assertEqual([], rolling_failures(world))

        def omit_both_precursors(world):
            row = scenario(world, "merge_equal")
            row["baseline"]["precursor"] = row["candidate"]["precursor"] = None
            del world["evidence"][row["scenarioId"]]["precursor"]

        def precursors_without_merge(world):
            row = scenario(world, "equal")
            row["baseline"]["precursor"] = artifact("b")
            row["candidate"]["precursor"] = artifact("b")
            world["evidence"][row["scenarioId"]]["precursor"] = None

        def rejected_after_the_precursor_without_one(world):
            scenario(world, "rejects")["baseline"]["precursor"] = None

        def precursor_stage_without_merge(world):
            scenario(world, "equal").update(outcome="baseline-rejects", baseline={**rejected_side(), "stoppedAt": "precursor-preview"})

        cases = {
            "both-sides-omit-the-merge-precursor": (omit_both_precursors, "PREDECESSOR_REPORT_INVALID", "precursor does not match the ledger"),
            "precursors-without-a-merge-base": (precursors_without_merge, "PREDECESSOR_REPORT_INVALID", "precursor does not match the ledger"),
            "rejected-at-preview-without-a-precursor": (
                rejected_after_the_precursor_without_one,
                "PREDECESSOR_REPORT_INVALID",
                "precursor does not match the ledger",
            ),
            "precursor-stage-without-a-merge-base": (
                precursor_stage_without_merge,
                "PREDECESSOR_REPORT_INVALID",
                "stopped at a precursor stage",
            ),
        }
        self.assert_each_mutation_fails(cases)

    def test_gap_approvals_cover_one_release(self) -> None:
        def earlier_approval(world):
            row = world["ledger"]["acceptedGaps"][0]
            old = {
                **copy.deepcopy(row),
                "approvedInVersion": "1.1.12",
                "approval": {**APPROVAL, "boardDecision": "1.1.12 board decision 60"},
                "declarationEntryId": "RP-1.1.12-01",
            }
            world["baseline_ledger"]["acceptedGaps"].append(old)
            return row, old

        # A renewal under this release's approval is a declared change that reproduces.
        world = rolling_world()
        earlier_approval(world)
        self.assertEqual([], rolling_failures(world))

        def carried_approval(world):
            row, old = earlier_approval(world)
            world["ledger"]["acceptedGaps"][0] = copy.deepcopy(old)
            entries = world["declaration"]["entries"]
            entries.remove(next(item for item in entries if row["routeId"] in item["routeIds"]))
            world["changelog"] = changelog([item["id"] for item in entries])
            changes = world["report"]["coverage"]["changesSinceBaseline"]
            changes.remove(next(item for item in changes if item["subject"] == row["routeId"]))

        def renewal_cites_the_earlier_entry(world):
            row, _ = earlier_approval(world)
            row["declarationEntryId"] = "RP-1.1.12-01"

        cases = {
            "earlier-approval-carried": (carried_approval, "PREDECESSOR_COVERAGE_UNDISPOSED", "does not cover 1.1.13"),
            "renewal-cites-the-earlier-entry": (renewal_cites_the_earlier_entry, "PREDECESSOR_STALE_DECLARATION", "does not cite its entry"),
        }
        self.assert_each_mutation_fails(cases)

    def test_declaration_subjects_and_ledger_references(self) -> None:
        def stale_subject(world):
            declared = entry_for(world, "plain")
            declared["scenarioIds"].append(world["names"]["equal"])
            declared["routeIds"] = sorted({*declared["routeIds"], scenario(world, "equal")["routeId"]})

        def gap_without_change(world):
            gap = next(row for row in world["declaration"]["entries"] if row["kind"] == "accepted-gap")
            gap["routeIds"].append(SUCCESSOR_ROUTE.replace("23", "99"))

        def gap_cites_other_entry(world):
            world["ledger"]["acceptedGaps"][0]["declarationEntryId"] = "RP-1.1.13-98"

        def gap_other_version(world):
            world["ledger"]["acceptedGaps"][0]["approvedInVersion"] = "1.1.14"

        def gap_other_approval(world):
            world["ledger"]["acceptedGaps"][0]["approval"] = {**APPROVAL, "boardDecision": "1.1.13 board decision 1"}

        def retirement_cites_other_entry(world):
            world["ledger"]["retiredScenarios"][0]["declarationEntryId"] = "RP-1.1.13-98"

        def declared_twice(world):
            copy_of = copy.deepcopy(entry_for(world, "plain"))
            copy_of["id"] = "RP-1.1.13-97"
            world["declaration"]["entries"].append(copy_of)
            world["changelog"] = changelog([row["id"] for row in world["declaration"]["entries"]])

        def undeclared_difference(world):
            world["declaration"]["entries"].remove(entry_for(world, "plain"))

        def report_names_other_entry(world):
            scenario(world, "plain")["declarationEntryId"] = entry_for(world, "precursor_only")["id"]

        def wrong_route_ids(world):
            entry_for(world, "plain")["routeIds"] = [SUCCESSOR_ROUTE]

        def change_names_other_entry(world):
            world["report"]["coverage"]["changesSinceBaseline"][0]["declarationEntryId"] = "RP-1.1.13-98"

        cases = {
            "valid-and-stale-subject-in-one-entry": (stale_subject, "PREDECESSOR_STALE_DECLARATION", "did not happen"),
            "gap-route-without-change": (gap_without_change, "PREDECESSOR_STALE_DECLARATION", "did not happen"),
            "gap-cites-other-entry": (gap_cites_other_entry, "PREDECESSOR_STALE_DECLARATION", "does not cite its entry"),
            "gap-other-version": (gap_other_version, "PREDECESSOR_STALE_DECLARATION", "does not cite its entry"),
            "gap-other-approval": (gap_other_approval, "PREDECESSOR_STALE_DECLARATION", "does not cite its entry"),
            "retirement-cites-other-entry": (retirement_cites_other_entry, "PREDECESSOR_STALE_DECLARATION", "does not cite its entry"),
            "declared-twice": (declared_twice, "PREDECESSOR_UNDECLARED_CHANGE", "declared by 2 entries"),
            "undeclared-difference": (undeclared_difference, "PREDECESSOR_UNDECLARED_CHANGE", "declared by 0 entries"),
            "report-names-other-entry": (report_names_other_entry, "PREDECESSOR_STALE_DECLARATION", "another entry than"),
            "entry-route-ids": (wrong_route_ids, "PREDECESSOR_STALE_DECLARATION", "route ids differ"),
            "change-names-other-entry": (change_names_other_entry, "PREDECESSOR_STALE_DECLARATION", "another entry for its"),
        }
        self.assert_each_mutation_fails(cases)

    def test_declaration_identity_release_notes_and_source(self) -> None:
        def missing_note(world):
            world["changelog"] = changelog([world["declaration"]["entries"][0]["id"]])

        cases = {
            "missing-release-note-id": (missing_note, "PREDECESSOR_RELEASE_NOTE_MISSING", "absent from the CHANGELOG"),
            "declaration-other-baseline": (
                lambda world: world["declaration"]["baseline"].update(tag="v1.1.11"),
                "PREDECESSOR_BASELINE_INVALID",
                "another baseline",
            ),
            "declaration-other-ledger": (
                lambda world: world["declaration"].update(ledgerSha256="9" * 64),
                "PREDECESSOR_SOURCE_MISMATCH",
                "another ledger",
            ),
            "declaration-other-version": (
                lambda world: world["declaration"].update(candidateVersion="1.1.14"),
                "PREDECESSOR_SOURCE_MISMATCH",
                "another version",
            ),
            "formal-run-without-declaration": (
                lambda world: world.update(declaration=None),
                "PREDECESSOR_UNDECLARED_CHANGE",
                "no declaration",
            ),
            "report-from-other-commit": (
                lambda world: world["report"]["candidate"]["executor"].update(commit="9" * 40),
                "PREDECESSOR_SOURCE_MISMATCH",
                "candidate source",
            ),
            "report-other-baseline": (
                lambda world: world["report"]["baseline"]["executor"].update(tagObject="9" * 40),
                "PREDECESSOR_BASELINE_INVALID",
                "another baseline",
            ),
            "report-other-contract": (
                lambda world: world["report"]["comparator"]["contracts"][0].update(sha256="9" * 64),
                "PREDECESSOR_SOURCE_MISMATCH",
                "contract identities",
            ),
            "report-other-declaration": (
                lambda world: world["report"].update(declarationSha256="9" * 64),
                "PREDECESSOR_SOURCE_MISMATCH",
                "declarationSha256",
            ),
            "invalid-scenario": (
                lambda world: scenario(world, "equal").update(outcome="invalid", failureCode="PREDECESSOR_PROCESS_FAILED"),
                "PREDECESSOR_PROCESS_FAILED",
                "scenario is invalid",
            ),
        }
        self.assert_each_mutation_fails(cases)

    def test_pending_gaps_block_every_report_of_record(self) -> None:
        def reopen_gap(world):
            gap = world["ledger"]["acceptedGaps"].pop()
            world["ledger"]["pendingAcceptedGaps"]["routeIds"].append(gap["routeId"])

        self.assert_each_mutation_fails(
            {"pending-gap": (reopen_gap, "PREDECESSOR_COVERAGE_UNDISPOSED", "pending gap approval")}
        )

    def test_ledger_rules(self) -> None:
        def first(world, predicate):
            return next(row for row in world["ledger"]["scenarios"] if predicate(row))

        cases = {
            "input-sha256": (
                lambda world: first(world, lambda row: row["workflowId"] == "ctrlram-replace")["inputs"][0].update(sha256="0" * 64),
                "PREDECESSOR_INPUT_INVALID",
                "differs from its case",
            ),
            "selection-token": (
                lambda world: first(world, lambda row: row["cli"]["selectionToken"] == "single")["cli"].update(selectionToken="cascade"),
                "PREDECESSOR_INPUT_INVALID",
                "CLI selection",
            ),
            "case-claims-route-evidence": (
                lambda world: first(world, lambda row: row["binding"] == "case").update(binding="route-evidence"),
                "PREDECESSOR_INPUT_INVALID",
                "route evidence names",
            ),
            "ctrlram-base-kind": (
                lambda world: first(world, lambda row: (row["ctrlRamBase"] or {}).get("kind") == "standard-merge").update(
                    ctrlRamBase={"kind": "tp-input"}
                ),
                "PREDECESSOR_INPUT_INVALID",
                "CtrlRAM base differs",
            ),
            "shortened-debt-set": (
                lambda world: world["ledger"]["debtSet"]["routeIds"].pop(),
                "PREDECESSOR_COVERAGE_UNDISPOSED",
                "differs from the plan",
            ),
            "successor-in-debt-set": (
                lambda world: world["ledger"]["debtSet"]["routeIds"].__setitem__(-1, SUCCESSOR_ROUTE),
                "PREDECESSOR_COVERAGE_UNDISPOSED",
                "differs from the plan",
            ),
            "removed-without-retirement": (
                lambda world: world["ledger"]["retiredScenarios"].clear(),
                "PREDECESSOR_COVERAGE_UNDISPOSED",
                "without a retirement record",
            ),
            "inputs-replaced-without-revision": (
                lambda world: first(world, lambda row: True)["inputs"][0].update(size=1),
                "PREDECESSOR_COVERAGE_UNDISPOSED",
                "without a new input revision",
            ),
        }
        self.assert_each_mutation_fails(cases)

    def test_gate_is_deterministic(self) -> None:
        failures = [
            validation.Failure("PREDECESSOR_UNDECLARED_CHANGE", "b", ""),
            validation.Failure("PREDECESSOR_COVERAGE_UNDISPOSED", "a", "x"),
            validation.Failure("PREDECESSOR_UNDECLARED_CHANGE", "b", ""),
        ]
        gate = validation.rolling_gate(failures)
        self.assertEqual(["PREDECESSOR_COVERAGE_UNDISPOSED", "PREDECESSOR_UNDECLARED_CHANGE"], [row["code"] for row in gate["failures"]])
        self.assertEqual(gate, validation.rolling_gate(list(reversed(failures))))

    def test_range_digest_is_the_parity_jcs_digest(self) -> None:
        ranges = spans(3, 0)
        self.assertEqual(MODULE.canonical_json_sha256(ranges), validation.range_projection(ranges)["rangeListSha256"])


def transitive_proof(checks: tuple[bool, bool, bool] = (True, True, True)) -> dict[str, bool]:
    return dict(zip(validation.TRANSITIVE_CHECKS, checks, strict=True))


def computed_transitive(tp_length: int, change: str | None = None) -> validation.TransitiveEvidence:
    """Run the ADR 0057 primitive on synthetic bytes: a passing proof, or one changed input."""

    tp = bytes([1]) * tp_length
    base = bytes([0]) * tp_length + bytes([2]) * 16
    candidate_full = tp + bytes([2]) * 16
    baseline_full = tp + bytes([3]) * 16
    if change == "tail":
        candidate_full = candidate_full[:-1] + bytes([9])
    if change == "baseline-prefix":
        baseline_full = bytes([9]) + baseline_full[1:]
    return validation.transitive_evidence(baseline_full, candidate_full, tp, base, tp_length)


def v0916_world() -> dict[str, Any]:
    """A complete, valid v0.9.16 1.x report over the plan's 64 routes with its computed evidence."""

    dispositions, failures = validation.v0916_route_dispositions(Sources.plan, Sources.amendment, Sources.pinned_policy)
    assert failures == []
    routes: list[dict[str, Any]] = []
    evidence: dict[str, Any] = {}
    for row in dispositions:
        route = {
            "planRouteId": row.route_id,
            "planCapabilityFingerprint": row.capability_fingerprint,
            "proofKind": row.proof_kind,
            "result": "consistent",
            "dispositionRow": None,
            "baseline": None,
            "candidate": None,
            "comparison": None,
            "transitive": None,
            "failureCode": None,
        }
        if row.proof_kind == "not-covered":
            route["result"] = "not-covered"
        elif row.proof_kind == "exact-output":
            route.update(baseline=output_side("a"), candidate=output_side("a"))
            evidence[row.route_id] = validation.V0916RouteEvidence({"output": None})
        elif row.proof_kind == "tp-prefix-transitive":
            route.update(
                candidate={**output_side("a"), "output": {"size": row.row["tpLength"], "sha256": "a" * 64}},
                transitive={"fullRouteId": row.row["fullRouteId"], "tpLength": row.row["tpLength"], **transitive_proof()},
            )
            evidence[row.route_id] = validation.V0916RouteEvidence({}, computed_transitive(row.row["tpLength"]))
        elif row.proof_kind == "exact-output-with-approved-semantic-correction":
            ranges = [dict(item) for item in row.row["differentRanges"]]
            route.update(
                baseline={**output_side("a"), "output": dict(row.row["baselineOutput"])},
                candidate={**output_side("a"), "output": dict(row.row["candidateOutput"])},
                comparison=validation.range_projection(ranges),
                dispositionRow={"source": row.row_source, "member": row.row_member, "routeId": row.route_id},
            )
            evidence[row.route_id] = validation.V0916RouteEvidence({"output": ranges})
        else:
            expected_baseline, expected_candidate = row.row["expectedBaseline"], row.row["expectedCandidate"]
            route.update(
                baseline={
                    **rejected_side(),
                    "precursor": dict(expected_baseline["precursorOutput"]),
                    "stoppedAt": expected_baseline["rejectingStage"],
                },
                candidate={
                    **output_side("a"),
                    "precursor": dict(expected_candidate["precursorOutput"]),
                    "output": dict(expected_candidate["output"]),
                },
                dispositionRow={"source": row.row_source, "member": row.row_member, "routeId": row.route_id},
            )
            evidence[row.route_id] = validation.V0916RouteEvidence({"precursor": None})
        routes.append(route)
    report = {
        "routes": routes,
        "summary": {"consistent": 37, "inconsistent": 0, "invalid": 0, "notCovered": 27},
        "result": "consistent",
    }
    return {"report": report, "dispositions": dispositions, "evidence": evidence}


def recount(world: dict[str, Any]) -> None:
    results = [route["result"] for route in world["report"]["routes"]]
    world["report"]["summary"] = {
        "consistent": results.count("consistent"),
        "inconsistent": results.count("inconsistent"),
        "invalid": results.count("invalid"),
        "notCovered": results.count("not-covered"),
    }
    world["report"]["result"] = validation.v0916_result(results)


def v0916_failures(world: dict[str, Any]) -> list[validation.Failure]:
    return validation.v0916_report_failures(world["report"], world["dispositions"], world["evidence"])


def route_of(world: dict[str, Any], proof_kind: str, source: str | None = None, index: int = 0) -> dict[str, Any]:
    matching = [
        route
        for route in world["report"]["routes"]
        if route["proofKind"] == proof_kind and (source is None or (route["dispositionRow"] or {}).get("source") == source)
    ]
    return matching[index]


def independent_exact_route(world: dict[str, Any]) -> dict[str, Any]:
    full_ids = {(route["transitive"] or {}).get("fullRouteId") for route in world["report"]["routes"]}
    return next(
        route for route in world["report"]["routes"]
        if route["proofKind"] == "exact-output" and route["planRouteId"] not in full_ids
    )


class V0916ModeValidationTests(unittest.TestCase):
    """The v0.9.16 1.x mode on a fresh valid report: each change fails its own rule."""

    def test_start_failure_is_not_a_consistent_output_or_approved_rejection(self) -> None:
        for proof_kind, side_name in (("exact-output", "candidate"), ("canonical-binding-not-applicable-to-v0916", "baseline")):
            with self.subTest(proof_kind=proof_kind):
                world = v0916_world()
                self.assertEqual([], v0916_failures(world))
                route_of(world, proof_kind)[side_name]["issues"][0]["code"] = "external-tool.process.start-failed"
                self.assertIn("PREDECESSOR_PROCESS_FAILED", codes(v0916_failures(world)))

    def test_runnable_transitive_proof_cannot_be_missing_even_on_invalid_route(self) -> None:
        world = v0916_world()
        self.assertEqual([], v0916_failures(world))
        route = route_of(world, "tp-prefix-transitive")
        route.update(result="invalid", failureCode="PREDECESSOR_REPORT_INVALID", transitive=None)
        world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({})
        found = v0916_failures(world)
        self.assertIn("PREDECESSOR_REPORT_INVALID", codes(found))
        self.assertTrue(any(failure.detail == "transitive evidence is missing" for failure in found))

    def test_amendment_binds_the_plan(self) -> None:
        self.assertEqual([], validation.amendment_binding_failures(Sources.amendment, Sources.plan))
        changed = copy.deepcopy(Sources.plan)
        changed["baseline"]["tagObject"] = "0" * 40
        self.assertEqual({"PREDECESSOR_AMENDMENT_MISMATCH"}, codes(validation.amendment_binding_failures(Sources.amendment, changed)))

    def test_each_plan_route_gets_one_proof_kind(self) -> None:
        dispositions, failures = validation.v0916_route_dispositions(Sources.plan, Sources.amendment, Sources.pinned_policy)
        self.assertEqual([], failures)
        counts: dict[str, int] = {}
        for row in dispositions:
            counts[row.proof_kind] = counts.get(row.proof_kind, 0) + 1
        self.assertEqual(
            {
                "not-covered": 27,
                "tp-prefix-transitive": 4,
                "exact-output-with-approved-semantic-correction": 2,
                "canonical-binding-not-applicable-to-v0916": 1,
                "exact-output": 30,
            },
            counts,
        )

    def test_ambiguous_or_unbound_rows_fail_closed(self) -> None:
        missing_route = sorted(Sources.plan["canonicalInputAuthority"]["currentlyMissingRouteIds"])[0]
        plan_row = Sources.plan["approvedSemanticCorrections"][0]

        def fingerprint(amendment):
            amendment["approvedSemanticCorrections"][0]["capabilityFingerprint"] = "0" * 64

        def duplicate_of_the_plan_row(amendment):
            row = copy.deepcopy(amendment["approvedSemanticCorrections"][0])
            row.update(routeId=plan_row["routeId"], capabilityFingerprint=plan_row["capabilityFingerprint"])
            amendment["approvedSemanticCorrections"].append(row)

        def route_without_input(amendment):
            amendment["baselineNotApplicable"][0]["routeId"] = missing_route

        for label, mutate, fragment in (
            ("fingerprint", fingerprint, "does not name a plan route"),
            ("duplicate-of-the-plan-row", duplicate_of_the_plan_row, "more than one disposition"),
            ("route-without-canonical-input", route_without_input, "without canonical input"),
        ):
            with self.subTest(case=label):
                amendment = copy.deepcopy(Sources.amendment)
                mutate(amendment)
                _, failures = validation.v0916_route_dispositions(Sources.plan, amendment, Sources.pinned_policy)
                self.assertTrue(any(fragment in failure.detail for failure in failures), failures)

    def test_each_route_rule_fails_on_its_own_change(self) -> None:
        def exact_hash_without_evidence(world):
            route_of(world, "exact-output")["candidate"]["output"]["sha256"] = "e" * 64

        def exact_consistent_with_bytes(world):
            route = route_of(world, "exact-output")
            route["candidate"]["output"]["sha256"] = "e" * 64
            world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({"output": spans(1, 0)})
            route["comparison"] = validation.range_projection(spans(1, 0))

        def exact_inconsistent_without_bytes(world):
            independent_exact_route(world).update(result="inconsistent", failureCode="PREDECESSOR_UNAPPROVED_DIFFERENCE")
            recount(world)

        def transitive_full_route(world):
            route_of(world, "tp-prefix-transitive")["transitive"]["fullRouteId"] = SUCCESSOR_ROUTE

        def transitive_tp_length(world):
            route_of(world, "tp-prefix-transitive")["transitive"]["tpLength"] += 1

        def transitive_reported_true_computed_false(world):
            route = route_of(world, "tp-prefix-transitive")
            computed = computed_transitive(route["transitive"]["tpLength"], "tail")
            world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({}, computed)

        def transitive_consistent_with_failing_proof(world):
            route = route_of(world, "tp-prefix-transitive")
            route["transitive"].update(transitive_proof((True, False, True)))
            computed = computed_transitive(route["transitive"]["tpLength"], "baseline-prefix")
            world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({}, computed)

        def transitive_evidence_other_length(world):
            route = route_of(world, "tp-prefix-transitive")
            computed = computed_transitive(route["transitive"]["tpLength"] + 1)
            world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({}, computed)

        def transitive_full_route_inconsistent(world):
            transitive = route_of(world, "tp-prefix-transitive")
            full = next(route for route in world["report"]["routes"] if route["planRouteId"] == transitive["transitive"]["fullRouteId"])
            full.update(result="inconsistent", failureCode="PREDECESSOR_UNAPPROVED_DIFFERENCE")
            world["report"].update(
                summary={"consistent": 36, "inconsistent": 1, "invalid": 0, "notCovered": 27}, result="inconsistent"
            )

        def transitive_evidence_missing(world):
            route = route_of(world, "tp-prefix-transitive")
            world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({})

        def route_evidence_missing(world):
            del world["evidence"][route_of(world, "exact-output")["planRouteId"]]

        def correction_hash(world):
            route_of(world, "exact-output-with-approved-semantic-correction", "amendment")["candidate"]["output"]["sha256"] = "0" * 64

        def correction_range(world):
            route = route_of(world, "exact-output-with-approved-semantic-correction", "plan")
            ranges = world["evidence"][route["planRouteId"]].scopes["output"]
            ranges[-1]["endExclusive"] += 4
            route["comparison"] = validation.range_projection(ranges)

        def rejection_code(world):
            route_of(world, "canonical-binding-not-applicable-to-v0916")["baseline"]["issues"][0]["code"] = "profile.v2.compile.other"

        def not_applicable_output(world):
            route_of(world, "canonical-binding-not-applicable-to-v0916")["candidate"]["output"]["sha256"] = "0" * 64

        def not_applicable_precursor(world):
            route = route_of(world, "canonical-binding-not-applicable-to-v0916")
            route["candidate"]["precursor"]["sha256"] = "0" * 64
            world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({"precursor": spans(1, 0)})

        def transitive_output_size(world):
            route_of(world, "tp-prefix-transitive")["candidate"]["output"]["size"] = 1

        def proof_kind(world):
            route_of(world, "tp-prefix-transitive")["proofKind"] = "exact-output"

        def missing_route(world):
            world["report"]["routes"].remove(route_of(world, "exact-output"))

        def duplicated_route(world):
            world["report"]["routes"].append(copy.deepcopy(route_of(world, "not-covered")))

        def summary(world):
            world["report"]["summary"]["consistent"] = 36

        def result(world):
            route_of(world, "exact-output").update(result="inconsistent", failureCode="PREDECESSOR_UNAPPROVED_DIFFERENCE")
            world["report"]["summary"] = {"consistent": 36, "inconsistent": 1, "invalid": 0, "notCovered": 27}

        cases = {
            "exact-output-hash-without-evidence": (exact_hash_without_evidence, "PREDECESSOR_REPORT_INVALID", "identities and computed ranges disagree"),
            "exact-output-consistent-with-bytes": (exact_consistent_with_bytes, "PREDECESSOR_UNAPPROVED_DIFFERENCE", "exact-output route with differing bytes"),
            "exact-output-inconsistent-without-bytes": (exact_inconsistent_without_bytes, "PREDECESSOR_REPORT_INVALID", "exact-output route with equal outputs must be consistent"),
            "transitive-full-route": (transitive_full_route, "PREDECESSOR_REPORT_INVALID", "another full route or TP length"),
            "transitive-tp-length": (transitive_tp_length, "PREDECESSOR_REPORT_INVALID", "another full route or TP length"),
            "transitive-reported-true-computed-false": (transitive_reported_true_computed_false, "PREDECESSOR_REPORT_INVALID", "differ from the computed proof"),
            "transitive-consistent-with-failing-proof": (transitive_consistent_with_failing_proof, "PREDECESSOR_UNAPPROVED_DIFFERENCE", "without a passing proof"),
            "transitive-full-route-inconsistent": (transitive_full_route_inconsistent, "PREDECESSOR_UNAPPROVED_DIFFERENCE", "without a passing proof"),
            "transitive-evidence-missing": (transitive_evidence_missing, "PREDECESSOR_REPORT_INVALID", "transitive evidence is missing"),
            "transitive-evidence-other-tp-length": (transitive_evidence_other_length, "PREDECESSOR_REPORT_INVALID", "another TP length than the plan"),
            "route-evidence-missing": (route_evidence_missing, "PREDECESSOR_REPORT_INVALID", "computed evidence for a run route is missing"),
            "correction-hash": (correction_hash, "PREDECESSOR_AMENDMENT_MISMATCH", "candidateOutput"),
            "correction-range": (correction_range, "PREDECESSOR_AMENDMENT_MISMATCH", "differentRanges"),
            "not-applicable-issue-code": (rejection_code, "PREDECESSOR_AMENDMENT_MISMATCH", "issueCodes"),
            "not-applicable-output": (not_applicable_output, "PREDECESSOR_AMENDMENT_MISMATCH", "output"),
            "not-applicable-precursor": (not_applicable_precursor, "PREDECESSOR_AMENDMENT_MISMATCH", "precursor does not reproduce"),
            "transitive-output-size-only": (transitive_output_size, "PREDECESSOR_REPORT_INVALID", "TP output size"),
            "proof-kind": (proof_kind, "PREDECESSOR_REPORT_INVALID", "proof kind or fingerprint"),
            "missing-route": (missing_route, "PREDECESSOR_REPORT_INVALID", "missing from the report"),
            "duplicated-route": (duplicated_route, "PREDECESSOR_REPORT_INVALID", "more than once"),
            "summary": (summary, "PREDECESSOR_REPORT_INVALID", "summary differs"),
            "result": (result, "PREDECESSOR_REPORT_INVALID", "result differs"),
        }
        for label, (mutate, code, fragment) in cases.items():
            with self.subTest(case=label):
                world = v0916_world()
                self.assertEqual([], v0916_failures(world))
                mutate(world)
                failures = v0916_failures(world)
                self.assertTrue(
                    any(failure.code == code and fragment in failure.detail for failure in failures),
                    f"{label}: expected {code} '{fragment}', got {failures}",
                )

    def test_exact_output_result_follows_evidence_in_both_directions(self) -> None:
        for differs in (False, True):
            with self.subTest(differs=differs):
                world = v0916_world()
                route = independent_exact_route(world)
                if differs:
                    route["candidate"]["output"]["sha256"] = "e" * 64
                    ranges = spans(1, 0)
                    world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({"output": ranges})
                    route.update(
                        comparison=validation.range_projection(ranges),
                        result="inconsistent",
                        failureCode="PREDECESSOR_UNAPPROVED_DIFFERENCE",
                    )
                recount(world)
                self.assertEqual([], v0916_failures(world))

                route.update(
                    result="consistent" if differs else "inconsistent",
                    failureCode=None if differs else "PREDECESSOR_UNAPPROVED_DIFFERENCE",
                )
                recount(world)
                expected = "PREDECESSOR_UNAPPROVED_DIFFERENCE" if differs else "PREDECESSOR_REPORT_INVALID"
                self.assertEqual({expected}, codes(v0916_failures(world)))

    def test_exact_output_missing_side_returns_report_failures(self) -> None:
        for side in ("baseline", "candidate"):
            for partial in (False, True):
                for retain_evidence in (False, True):
                    with self.subTest(side=side, partial=partial, retain_evidence=retain_evidence):
                        world = v0916_world()
                        self.assertEqual([], v0916_failures(world))
                        route = independent_exact_route(world)
                        route[side] = {} if partial else None
                        route.update(result="inconsistent", failureCode="PREDECESSOR_UNAPPROVED_DIFFERENCE")
                        if not retain_evidence:
                            world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({})
                        recount(world)

                        failures = v0916_failures(world)
                        self.assertEqual({"PREDECESSOR_REPORT_INVALID"}, codes(failures))
                        self.assertTrue(all(failure.subject == route["planRouteId"] for failure in failures))
                        if retain_evidence:
                            self.assertTrue(any("output evidence for a side without that artifact" in failure.detail
                                                for failure in failures))

    def test_transitive_evidence_is_the_primitive_result(self) -> None:
        self.assertEqual(validation.TransitiveEvidence(16, transitive_proof(), None), computed_transitive(16))
        self.assertEqual("PARITY_TAIL_MUTATED", computed_transitive(16, "tail").failure_code)
        self.assertEqual("PARITY_TP_PREFIX_MISMATCH", computed_transitive(16, "baseline-prefix").failure_code)

        # A failed proof, reported as the primitive fixes it, is a valid inconsistent route.
        for change, checks in (("tail", (True, True, False)), ("baseline-prefix", (True, False, True))):
            with self.subTest(change=change):
                world = v0916_world()
                route = route_of(world, "tp-prefix-transitive")
                route.update(result="inconsistent", failureCode="PREDECESSOR_UNAPPROVED_DIFFERENCE")
                route["transitive"].update(transitive_proof(checks))
                computed = computed_transitive(route["transitive"]["tpLength"], change)
                world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({}, computed)
                world["report"].update(
                    summary={"consistent": 36, "inconsistent": 1, "invalid": 0, "notCovered": 27}, result="inconsistent"
                )
                self.assertEqual([], v0916_failures(world))
                route["transitive"].update(transitive_proof())
                found = [(failure.code, failure.detail) for failure in v0916_failures(world)]
                self.assertIn(("PREDECESSOR_REPORT_INVALID", "transitive checks differ from the computed proof"), found)

    def test_a_precursor_difference_follows_the_proof_kind(self) -> None:
        # The exact-output proof compares outputs only; no plan or contract row makes the precursor a gate.
        world = v0916_world()
        route = route_of(world, "exact-output")
        route.update(baseline=output_side("a", "b"), candidate=output_side("a", "c"))
        world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({"output": None, "precursor": spans(1, 0)})
        self.assertEqual([], v0916_failures(world))

    def test_a_proof_that_cannot_run_keeps_its_classification(self) -> None:
        def reject_tp(world):
            route = route_of(world, "tp-prefix-transitive")
            route.update(
                candidate=rejected_side(), result="inconsistent", failureCode="PREDECESSOR_UNAPPROVED_DIFFERENCE", transitive=None
            )
            world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({})
            recount(world)
            return route

        def reject_full_route(world):
            full_id = route_of(world, "tp-prefix-transitive")["transitive"]["fullRouteId"]
            full = next(route for route in world["report"]["routes"] if route["planRouteId"] == full_id)
            full.update(baseline=rejected_side(), result="inconsistent", failureCode="PREDECESSOR_UNAPPROVED_DIFFERENCE")
            world["evidence"][full_id] = validation.V0916RouteEvidence({})
            for route in world["report"]["routes"]:
                if (route["transitive"] or {}).get("fullRouteId") == full_id:
                    route.update(result="inconsistent", failureCode="PREDECESSOR_UNAPPROVED_DIFFERENCE", transitive=None)
                    world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({})
            recount(world)

        def fail_tp(world):
            route = route_of(world, "tp-prefix-transitive")
            route.update(
                candidate={**rejected_side(), "status": "invalid"},
                result="invalid",
                failureCode="PREDECESSOR_PROCESS_FAILED",
                transitive=None,
            )
            world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({})
            recount(world)
            return route

        def fail_full_route(world):
            full_id = route_of(world, "tp-prefix-transitive")["transitive"]["fullRouteId"]
            full = next(route for route in world["report"]["routes"] if route["planRouteId"] == full_id)
            full.update(candidate={**rejected_side(), "status": "invalid"}, result="invalid", failureCode="PREDECESSOR_PROCESS_FAILED")
            world["evidence"][full_id] = validation.V0916RouteEvidence({})
            dependents = [route for route in world["report"]["routes"] if (route["transitive"] or {}).get("fullRouteId") == full_id]
            for route in dependents:
                route.update(result="invalid", failureCode="PREDECESSOR_PROCESS_FAILED", transitive=None)
                world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({})
            recount(world)
            return dependents[0]

        for label, valid in (
            ("typed-tp-rejection", reject_tp),
            ("typed-full-route-rejection", reject_full_route),
            ("failed-tp-side", fail_tp),
            ("failed-full-route", fail_full_route),
        ):
            with self.subTest(case=label):
                world = v0916_world()
                valid(world)
                self.assertEqual([], v0916_failures(world))

        def proof_reported(world):
            reject_tp(world)["transitive"] = {"fullRouteId": "x", "tpLength": 1, **transitive_proof()}

        def proof_evidence(world):
            route = reject_tp(world)
            world["evidence"][route["planRouteId"]] = validation.V0916RouteEvidence({}, computed_transitive(16))

        def rejection_called_invalid(world):
            reject_tp(world).update(result="invalid", failureCode="PREDECESSOR_PROCESS_FAILED")
            recount(world)

        def failure_called_inconsistent(world):
            reject_tp(world)["candidate"]["status"] = "invalid"

        def failed_tp_with_a_product_code(world):
            fail_tp(world)["failureCode"] = "PREDECESSOR_UNAPPROVED_DIFFERENCE"

        def failed_full_route_with_a_product_code(world):
            fail_full_route(world)["failureCode"] = "PREDECESSOR_UNAPPROVED_DIFFERENCE"

        cases = {
            "failed-tp-side-with-a-product-code": (failed_tp_with_a_product_code, "without a shared execution failure code"),
            "failed-full-route-with-a-product-code": (
                failed_full_route_with_a_product_code,
                "without a shared execution failure code",
            ),
            "unrun-proof-reported": (proof_reported, "reports a transitive proof that could not run"),
            "unrun-proof-evidence": (proof_evidence, "transitive evidence for a proof that could not run"),
            "rejection-called-invalid": (rejection_called_invalid, "cannot run is inconsistent"),
            "failure-called-inconsistent": (failure_called_inconsistent, "cannot run is invalid"),
        }
        for label, (mutate, fragment) in cases.items():
            with self.subTest(case=label):
                world = v0916_world()
                self.assertEqual([], v0916_failures(world))
                mutate(world)
                failures = v0916_failures(world)
                self.assertTrue(
                    any(failure.code == "PREDECESSOR_REPORT_INVALID" and fragment in failure.detail for failure in failures),
                    f"{label}: expected PREDECESSOR_REPORT_INVALID '{fragment}', got {failures}",
                )

    def test_result_precedence(self) -> None:
        self.assertEqual("consistent", validation.v0916_result(["consistent", "not-covered"]))
        self.assertEqual("inconsistent", validation.v0916_result(["consistent", "inconsistent"]))
        self.assertEqual("invalid", validation.v0916_result(["inconsistent", "invalid"]))


if __name__ == "__main__":
    unittest.main()
