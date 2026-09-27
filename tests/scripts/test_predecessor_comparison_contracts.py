"""Semantic contract tests for the rolling predecessor comparison.

The tests read only JSON: the predecessor-comparison contract and ledger, the
report schema, the active capability policy and canonical Golden manifest with
their case manifests, the ADR 0057 plan, and the plan's pinned policy and
manifest from Git objects. They never read firmware payloads.

Schema validity, including every conditional relation of the proposed report
and declaration schemas, is tested with the repository's Draft 2020-12 engine
in PredecessorComparisonSchemaContractTests (.NET). This module checks the
facts a schema cannot hold: the ledger against the governed sources it
declares, the contract against the report schema, and the per-side safety
owners the contract names, and that a declaration entry binds output and
precursor differences exactly against the complete computed ranges, of which
the report carries the first 32. The error-list helpers below stand in for the
comparator's single semantic validator until the comparator batch (P-2)
lands; P-2 replaces them with calls to that validator and deletes them.
"""

from __future__ import annotations

import copy
import json
import subprocess
import unittest
from types import SimpleNamespace
from typing import Any

from tests.scripts.v0916_parity_test_support import MODULE, ROOT

CONTRACTS = ROOT / "docs" / "contracts"
PLAN_PATH = CONTRACTS / "v0916-parity-certification-v1.json"
POLICY_PATH = CONTRACTS / "canonical-capability-policy-v1.json"
MANIFEST_PATH = ROOT / "testdata" / "golden" / "canonical" / "manifest.json"
CANDIDATE_EXECUTOR_PATH = CONTRACTS / "v100-candidate-source-executor-v1.json"
UNIVERSE_PUBLICATION = {"supported", "candidate"}
SEED_RENAMES = {
    ("NT51950", "ab-merge", "1-ic", "nt51950-ab-merge-512k"): (
        "NT51950",
        "ab-merge",
        "1-ic",
        "nt51950-ab-merge-maps",
    )
}
DECISION_64_CASES = {"nt51950-ab-hiway-d82t80", "nt51950-ab-osd-d03t02-20260924"}
SUCCESSOR_ROUTE = "route-7-nt51950-8-ab-merge-9-2-plus-ic-23-nt51950-ab-cascade-maps"
REPORTED_RANGE_LIMIT = 32


def load_json(path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def git_json(commit: str, relative: str) -> Any:
    return json.loads(
        subprocess.check_output(["git", "cat-file", "blob", f"{commit}:{relative}"], cwd=ROOT)
    )


def route_key(route: dict[str, Any]) -> tuple[str, str, str, str]:
    return (route["icId"], route["workflowId"], route["icCountVariant"], route["mapVariant"])


def expected_cli(row: dict[str, Any]) -> dict[str, Any]:
    token = MODULE._cli_selection_token(
        SimpleNamespace(
            ic_id=row["icId"],
            workflow_id=row["workflowId"],
            ic_count_variant=row["icCountVariant"],
        )
    )
    option = None
    if token is not None:
        option = "--ic-num" if row["workflowId"] == "ctrlram-replace" else "--ab-topology"
    return {"profile": row["icId"], "selectionOption": option, "selectionToken": token}


def coverage_errors(ledger: dict[str, Any], universe: set[str], evidence: dict[str, Any]) -> list[str]:
    """Partition and completeness rules of the coverage ledger."""

    errors: list[str] = []
    covered = {row["routeId"] for row in ledger["scenarios"]}
    debt = set(ledger["debtSet"]["routeIds"])
    accepted = {row["routeId"] for row in ledger["acceptedGaps"]}
    pending = set(ledger["pendingAcceptedGaps"]["routeIds"])
    if not (debt.isdisjoint(accepted) and debt.isdisjoint(pending) and accepted.isdisjoint(pending)):
        errors.append("debt set, accepted gaps and pending gaps overlap")
    if not covered.isdisjoint(accepted | pending):
        errors.append("a compared route is also listed as a gap")
    unaccounted = universe - covered - debt - accepted - pending
    if unaccounted:
        errors.append(f"universe routes neither compared, owed nor disposed: {sorted(unaccounted)}")
    stray = (accepted | pending) - universe
    if stray:
        errors.append(f"gaps outside the universe: {sorted(stray)}")
    declared = {
        (row["routeId"], row["evidenceCaseId"])
        for row in ledger["scenarios"]
        if row["binding"] == "route-evidence"
    }
    for route_id in sorted(universe):
        case_id = evidence[route_id].get("caseId")
        if case_id and (route_id, case_id) not in declared:
            errors.append(f"route evidence without a scenario: {route_id}")
    return errors


def debt_set_errors(ledger: dict[str, Any], plan: dict[str, Any]) -> list[str]:
    """The debt set is exactly the plan's unbound route ids, in order."""

    expected = sorted(plan["canonicalInputAuthority"]["currentlyMissingRouteIds"])
    actual = ledger["debtSet"]["routeIds"]
    if actual == expected:
        return []
    return [
        f"debt set differs from the plan: missing {sorted(set(expected) - set(actual))}, "
        f"extra {sorted(set(actual) - set(expected))}, sorted {actual == sorted(actual)}"
    ]


def declared_side(side: dict[str, Any]) -> dict[str, Any]:
    """Project one report side onto the side outcome a declaration binds."""

    return {
        "result": "rejected" if side["status"] == "rejected" else "output",
        "output": copy.deepcopy(side["output"]),
        "precursor": copy.deepcopy(side["precursor"]),
        "stage": side["stoppedAt"],
        "issueCodes": sorted({issue["code"] for issue in side["issues"] if issue["severity"] == "error"}),
    }


def merged(spans: list[tuple[int, int]]) -> list[tuple[int, int]]:
    result: list[tuple[int, int]] = []
    for start, end in sorted(spans):
        if result and start <= result[-1][1]:
            result[-1] = (result[-1][0], max(result[-1][1], end))
        else:
            result.append((start, end))
    return result


def range_projection(ranges: list[dict[str, int]] | None) -> dict[str, Any] | None:
    """The report comparison of a complete computed range list: its totals and first 32 ranges."""

    if ranges is None:
        return None
    return {
        "differentByteCount": sum(item["endExclusive"] - item["start"] for item in ranges),
        "rangeCount": len(ranges),
        "rangeListSha256": MODULE.canonical_json_sha256(ranges),
        "ranges": [dict(item) for item in ranges[:REPORTED_RANGE_LIMIT]],
        "rangesTruncated": len(ranges) > REPORTED_RANGE_LIMIT,
    }


def stale_declaration_errors(
    entry: dict[str, Any], scenario: dict[str, Any], computed: dict[str, list[dict[str, int]] | None]
) -> list[str]:
    """A declaration entry reproduces a run exactly: both sides and each scope's complete computed ranges.

    `computed` holds the complete range lists the comparator computed from the bytes; the report
    carries only their projection, which is checked separately.
    """

    errors = [
        f"{side} outcome is not reproduced"
        for side in ("baseline", "candidate")
        if entry["expected"][side] != declared_side(scenario[side])
    ]
    differences = entry["differences"] or {"output": None, "precursor": None}
    for scope, member in (("output", "comparison"), ("precursor", "precursorComparison")):
        complete = computed[scope]
        if scenario[member] != range_projection(complete):
            errors.append(f"{scope} report comparison is not the projection of the computed ranges")
        declared = differences[scope]
        if (declared is None) != (complete is None) or (declared is not None and declared["ranges"] != complete):
            errors.append(f"{scope} difference is not reproduced")
        if declared is None:
            continue
        totals = range_projection(declared["ranges"])
        if any(declared[key] != totals[key] for key in ("differentByteCount", "rangeCount", "rangeListSha256")):
            errors.append(f"{scope} difference is not self-consistent")
        ranges = [(item["start"], item["endExclusive"]) for item in declared["ranges"]]
        spans = [(item["start"], item["endExclusive"]) for item in declared["attribution"]]
        overlapping = sum(end - start for start, end in spans) != sum(end - start for start, end in merged(spans))
        if merged(spans) != merged(ranges) or overlapping:
            errors.append(f"{scope} attribution does not cover exactly its ranges")
    return errors


def report_of_record_blockers(ledger: dict[str, Any]) -> list[str]:
    """Ledger states under which no run may be a report of record."""

    pending = ledger["pendingAcceptedGaps"]["routeIds"]
    return [f"pending gap approvals: {len(pending)}"] if pending else []


class PredecessorComparisonContractTests(unittest.TestCase):
    """Checks the rolling contract and ledger against the governed sources they declare."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.contract = load_json(CONTRACTS / "predecessor-comparison-v1.json")
        cls.ledger = load_json(CONTRACTS / "predecessor-comparison-scenarios-v1.json")
        cls.report_schema = load_json(CONTRACTS / "predecessor-comparison-report-v1.schema.json")
        cls.plan = load_json(PLAN_PATH)
        cls.policy = load_json(POLICY_PATH)
        cls.manifest = load_json(MANIFEST_PATH)
        authority = cls.plan["canonicalInputAuthority"]
        cls.pinned_policy = git_json(authority["repositoryCommit"], cls.plan["policyBinding"]["path"])
        cls.pinned_manifest = git_json(authority["repositoryCommit"], authority["manifestPath"])
        cls.routes_by_key = {route_key(route): route for route in cls.policy["routes"]}
        cls.evidence = {row["routeId"]: row for row in cls.manifest["routeEvidence"]}
        cls.cases = {row["caseId"]: row for row in cls.manifest["cases"]}
        cls.universe = {
            route["routeId"]
            for route in cls.policy["routes"]
            if route["authoring"]["value"] == "available"
            and route["publication"]["value"] in UNIVERSE_PUBLICATION
        }
        cls.plan_selected = [
            route
            for route in cls.pinned_policy["routes"]
            if route["authoring"]["value"] == "available"
            and route["publication"]["value"] == "supported"
            and route["workflowId"] in cls.plan["selection"]["includedWorkflows"]
        ]

    def case_manifest(self, case_id: str) -> dict[str, Any]:
        path = ROOT / "testdata" / "golden" / "canonical" / self.cases[case_id]["manifestPath"]
        return json.loads(path.read_text(encoding="utf-8"))

    def scenario_binding_errors(self, row: dict[str, Any]) -> list[str]:
        """A scenario names a current universe route and the selection the CLI derives."""

        errors: list[str] = []
        route = self.routes_by_key.get(route_key(row))
        if route is None or route["routeId"] != row["routeId"]:
            errors.append(f"{row['scenarioId']}: route key does not name {row['routeId']}")
        if row["routeId"] not in self.universe:
            errors.append(f"{row['scenarioId']}: route outside the universe")
        if row["cli"] != expected_cli(row):
            errors.append(f"{row['scenarioId']}: CLI selection differs from the derived selection")
        if row["scenarioId"] != ":".join([*route_key(row), row["evidenceCaseId"]]):
            errors.append(f"{row['scenarioId']}: identifier differs from its key and case")
        return errors

    def scenario_input_errors(self, row: dict[str, Any]) -> list[str]:
        """A scenario's binding, inputs and CtrlRAM base come from the active Golden cases."""

        errors: list[str] = []
        authority = self.plan["canonicalInputAuthority"]
        bindings = {item["caseId"]: item for item in authority["ctrlRamExecutionBindings"]}
        base_routes = {item["routeId"]: item for item in authority["ctrlRamBaseRoutes"]}
        evidence = self.evidence[row["routeId"]]
        named = evidence.get("caseId")
        if row["binding"] == "route-evidence" and named != row["evidenceCaseId"]:
            errors.append(f"{row['scenarioId']}: route evidence names {named}")
        if row["binding"] == "case" and named is not None:
            errors.append(f"{row['scenarioId']}: a case binding hides route evidence {named}")
        alias = self.case_manifest(row["evidenceCaseId"]).get("alias")
        if (alias["sourceCaseId"] if alias else row["evidenceCaseId"]) != row["inputCaseId"]:
            errors.append(f"{row['scenarioId']}: input case is not the resolved case")
        manifest_inputs = [
            item for item in self.case_manifest(row["inputCaseId"])["artifacts"] if item["role"] == "input"
        ]
        artifacts = {item["artifactId"]: item for item in manifest_inputs}
        if [item["order"] for item in row["inputs"]] != list(range(len(row["inputs"]))):
            errors.append(f"{row['scenarioId']}: input order is not contiguous")
        for item in row["inputs"]:
            artifact = artifacts.get(item["artifactId"])
            if artifact is None or (artifact["size"], artifact["sha256"]) != (item["size"], item["sha256"]):
                errors.append(f"{row['scenarioId']}: input {item['artifactId']} differs from its case")
        pairs = [(item["artifactId"], item["slotId"]) for item in row["inputs"]]
        if row["workflowId"] != "ctrlram-replace":
            if row["ctrlRamBase"] is not None:
                errors.append(f"{row['scenarioId']}: CtrlRAM base on a {row['workflowId']} scenario")
            if pairs != [(item["artifactId"], item["artifactId"]) for item in manifest_inputs]:
                errors.append(f"{row['scenarioId']}: inputs are not the case inputs in order")
            return errors
        binding = bindings[row["inputCaseId"]]
        replacements = [(item["artifactId"], item["slotId"]) for item in binding["replacements"]]
        base = base_routes[row["planRouteId"]]
        if base["kind"] == "tp-input":
            expected_base = {"kind": "tp-input"}
            expected_pairs = [(binding["tpBaseArtifactId"], "replace-base"), *replacements]
        else:
            expected_base = {"kind": "standard-merge", "standardMergeMapVariant": base["standardMergeMapVariant"]}
            recipe = binding["fullBaseRecipe"]
            expected_pairs = [(recipe["dpArtifactId"], "dp-input"), (recipe["tpArtifactId"], "tp-input"), *replacements]
            precursor = (row["icId"], "standard-merge", "selector-free", base["standardMergeMapVariant"])
            if precursor not in self.routes_by_key:
                errors.append(f"{row['scenarioId']}: precursor route {precursor} is not in the policy")
        if row["ctrlRamBase"] != expected_base:
            errors.append(f"{row['scenarioId']}: CtrlRAM base differs from the plan binding")
        if pairs != expected_pairs:
            errors.append(f"{row['scenarioId']}: CtrlRAM slots differ from the plan binding")
        return errors

    def test_modes_have_separate_authority_and_dispositions(self) -> None:
        rolling = self.contract["modes"]["rolling"]
        v0916 = self.contract["modes"]["v0916-1x"]
        self.assertEqual(
            (True, False, "release-declaration"),
            (rolling["declarationRequired"], rolling["amendmentRead"], rolling["differenceDisposition"]),
        )
        self.assertEqual(
            (False, "exact-plan-or-amendment-row"),
            (v0916["declarationRequired"], v0916["differenceDisposition"]),
        )
        self.assertNotEqual(rolling["inputAuthority"]["kind"], v0916["inputAuthority"]["kind"])
        self.assertNotIn("pass", rolling["results"] + v0916["results"] + v0916["routeResults"])
        self.assertEqual(sorted(rolling["outcomes"]), rolling["outcomes"])
        self.assertEqual(sorted(v0916["proofKinds"]), v0916["proofKinds"])
        mode_codes = {"rolling": set(), "v0916-1x": set()}
        for row in self.contract["failureCodes"]:
            for mode in row["modes"]:
                mode_codes[mode].add(row["code"])
        defs = self.report_schema["$defs"]
        self.assertEqual(mode_codes["rolling"], set(defs["rollingFailureCode"]["enum"]))
        self.assertEqual(mode_codes["v0916-1x"], set(defs["v0916FailureCode"]["enum"]))
        self.assertEqual(
            set(rolling["outcomes"]),
            set(defs["scenarioResult"]["properties"]["outcome"]["enum"]),
        )
        self.assertEqual(
            set(v0916["proofKinds"]) | {"not-covered"},
            set(defs["routeResult"]["properties"]["proofKind"]["enum"]),
        )
        self.assertEqual(set(v0916["routeResults"]), set(defs["routeResult"]["properties"]["result"]["enum"]))
        self.assertEqual(set(v0916["results"]), set(defs["v0916Report"]["properties"]["result"]["enum"]))
        codes = [row["code"] for row in self.contract["failureCodes"]]
        self.assertEqual(sorted(set(codes)), codes)

    def test_recorded_identity_is_the_report_executor_identity(self) -> None:
        identity = self.report_schema["$defs"]["executorIdentity"]
        recorded = self.contract["executor"]["recordedIdentity"]
        self.assertEqual(sorted(recorded), recorded)
        self.assertEqual(recorded, sorted(identity["required"]))
        self.assertEqual(recorded, sorted(identity["properties"]))
        self.assertIn("lockFileSetSha256", recorded)
        self.assertEqual(
            sorted(self.contract["executor"]["authorityTrees"]),
            sorted(identity["properties"]["authorityTrees"]["required"]),
        )

    def test_executor_recipe_reuses_the_candidate_source_recipe(self) -> None:
        candidate = load_json(CANDIDATE_EXECUTOR_PATH)
        executor = self.contract["executor"]
        self.assertEqual(candidate["restore"], executor["restore"])
        self.assertEqual(candidate["build"], executor["build"])
        self.assertEqual(candidate["cliAssembly"]["path"], executor["cliAssembly"])
        self.assertEqual(candidate["runtimeClosure"]["root"], executor["runtimeClosureRoot"])
        self.assertEqual(sorted(candidate["source"]["authorityTrees"]), executor["authorityTrees"])
        self.assertTrue(all(item["path"].endswith("/packages.lock.json") for item in candidate["lockFiles"]))
        self.assertEqual("pending-executor-record", executor["compilerHost"]["status"])

    def test_per_side_safety_names_the_shared_owners(self) -> None:
        safety = self.contract["perSideSafety"]
        self.assertEqual("scripts/v0916_parity_certification.py", safety["owner"])
        self.assertIn("validate_report_projection_against_compiled_authority", safety["checks"])
        for name in safety["checks"]:
            with self.subTest(check=name):
                self.assertTrue(callable(getattr(MODULE, name, None)))
        self.assertFalse(safety["readerMayRelaxChecks"])
        self.assertEqual("pending-reader-record", self.contract["interfaces"]["reportReader"]["status"])
        self.assertEqual(
            self.contract["typedRejection"]["processFailureIssueCodes"],
            self.report_schema["$defs"]["processFailureIssueCode"]["enum"],
        )

    def test_a_report_cannot_widen_its_own_allowed_ranges(self) -> None:
        def operation(target_end: int, write_end: int) -> dict[str, Any]:
            return {
                "operationId": "processor-op",
                "sequence": 0,
                "kind": "RunExternalProcessor",
                "status": "succeeded",
                "sourceSpaceId": "source",
                "sourceRange": {"addressSpace": "source", "start": 0, "endExclusive": 8},
                "targetSpaceId": "output-image",
                "targetRange": {"addressSpace": "output-image", "start": 0, "endExclusive": target_end},
                "overlapPolicy": "Reject",
                "processor": {
                    "processorId": "nfc.test",
                    "toolBindingId": "test-tool",
                    "allowedReadRanges": [{"addressSpace": "output-image", "start": 0, "endExclusive": target_end}],
                    "allowedWriteRanges": [{"addressSpace": "output-image", "start": 2, "endExclusive": write_end}],
                },
                "executedCommands": [
                    {
                        "sequence": 0,
                        "executablePackagePath": "external-tools/nfc-test.exe",
                        "workingDirectoryKind": "host-created-staging",
                        "argumentCount": 6,
                        "canonicalArgumentsSha256": "a" * 64,
                    }
                ],
                "reason": "typed authority",
                "provenance": {"kind": "built-in-profile", "sourceId": None, "sourceVersion": None},
            }

        def projection(target_end: int, write_end: int) -> dict[str, Any]:
            return {
                "compilationFingerprint": "1" * 64,
                "compiledOperations": [operation(target_end, write_end)],
                "compiledMutations": [
                    {
                        "operationId": "processor-op",
                        "kind": "RunExternalProcessor",
                        "targetSpaceId": "output-image",
                        "targetRange": {"addressSpace": "output-image", "start": 2, "endExclusive": write_end},
                        "changedByteCount": write_end - 2,
                        "beforeSha256": "1" * 64,
                        "afterSha256": "2" * 64,
                        "reason": "typed mutation",
                    }
                ],
            }

        capacities = {"source": 8, "output-image": 16}
        authority = projection(8, 6)
        authority["compiledMutations"] = []
        MODULE.validate_semantic_report_ranges(projection(8, 6), capacities)
        MODULE.validate_report_projection_against_compiled_authority(projection(8, 6), authority)
        for label, widened in (
            ("processor-write-range", projection(8, 8)),
            ("operation-and-write-range", projection(12, 10)),
        ):
            with self.subTest(widened=label):
                MODULE.validate_semantic_report_ranges(widened, capacities)
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_report_projection_against_compiled_authority(widened, copy.deepcopy(authority))
                self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)

    def test_declared_differences_are_reproduced_exactly(self) -> None:
        self.assertEqual(REPORTED_RANGE_LIMIT, self.contract["comparison"]["reportedRangeLimit"])

        def side(output: str, precursor: str) -> dict[str, Any]:
            return {
                "status": "output",
                "stoppedAt": None,
                "issues": [{"code": "input.address-space.truncated", "severity": "warning", "source": "report"}],
                "output": {"size": 65536, "sha256": output * 64},
                "precursor": {"size": 65536, "sha256": precursor * 64},
            }

        def spans(count: int, offset: int) -> list[dict[str, int]]:
            return [{"start": offset + 4 * index, "endExclusive": offset + 4 * index + 2} for index in range(count)]

        def difference(ranges: list[dict[str, int]] | None, mechanism: str) -> dict[str, Any] | None:
            if ranges is None:
                return None
            totals = range_projection(ranges)
            return {
                "differentByteCount": totals["differentByteCount"],
                "rangeCount": totals["rangeCount"],
                "rangeListSha256": totals["rangeListSha256"],
                "ranges": [dict(item) for item in ranges],
                "attribution": [
                    {
                        **item,
                        "mechanism": mechanism,
                        "cause": "example",
                        "causeVerification": "not-independently-verified",
                        "evidence": [],
                    }
                    for item in ranges
                ],
            }

        runs = {}
        for label, candidate_output, output_ranges, precursor_ranges in (
            ("precursor-only", "a", None, spans(1, 8192)),
            ("output-and-precursor", "d", spans(1, 0), spans(1, 8192)),
            ("33-output-and-precursor-ranges", "d", spans(33, 0), spans(33, 8192)),
        ):
            computed = {"output": output_ranges, "precursor": precursor_ranges}
            scenario = {
                "baseline": side("a", "b"),
                "candidate": side(candidate_output, "c"),
                "comparison": range_projection(output_ranges),
                "precursorComparison": range_projection(precursor_ranges),
            }
            entry = {
                "expected": {
                    "baseline": declared_side(scenario["baseline"]),
                    "candidate": declared_side(scenario["candidate"]),
                },
                "differences": {
                    "output": difference(output_ranges, "precursor-carried"),
                    "precursor": difference(precursor_ranges, "writes-different-bytes"),
                },
            }
            runs[label] = (entry, scenario, computed)
            with self.subTest(run=label):
                self.assertEqual([], stale_declaration_errors(entry, scenario, computed))
        many = runs["33-output-and-precursor-ranges"][1]
        self.assertEqual((33, 32, True), (
            many["comparison"]["rangeCount"],
            len(many["comparison"]["ranges"]),
            many["comparison"]["rangesTruncated"],
        ))

        def rerun(scenario: dict[str, Any], computed: dict[str, Any], scope: str, index: int) -> None:
            computed[scope][index]["endExclusive"] += 1
            member = "comparison" if scope == "output" else "precursorComparison"
            scenario[member] = range_projection(computed[scope])

        drifts = {
            "run-precursor-hash": lambda entry, scenario, computed: scenario["candidate"]["precursor"].update(
                sha256="e" * 64
            ),
            "run-precursor-range": lambda entry, scenario, computed: rerun(scenario, computed, "precursor", 0),
            "declared-precursor-hash": lambda entry, scenario, computed: entry["expected"]["candidate"][
                "precursor"
            ].update(sha256="e" * 64),
            "declared-precursor-range": lambda entry, scenario, computed: entry["differences"]["precursor"]["ranges"][
                0
            ].update(endExclusive=entry["differences"]["precursor"]["ranges"][0]["endExclusive"] + 1),
            "precursor-declared-as-output": lambda entry, scenario, computed: entry["differences"].update(
                output=entry["differences"]["precursor"], precursor=None
            ),
            "attribution-gap": lambda entry, scenario, computed: entry["differences"]["precursor"]["attribution"][
                0
            ].update(endExclusive=entry["differences"]["precursor"]["attribution"][0]["endExclusive"] - 1),
            "report-not-the-projection": lambda entry, scenario, computed: scenario["precursorComparison"].update(
                rangeListSha256="0" * 64
            ),
        }
        beyond_the_report = {
            "run-33rd-output-range": lambda entry, scenario, computed: rerun(scenario, computed, "output", 32),
            "run-33rd-precursor-range": lambda entry, scenario, computed: rerun(scenario, computed, "precursor", 32),
            "declared-33rd-output-range": lambda entry, scenario, computed: entry["differences"]["output"]["ranges"][
                32
            ].update(endExclusive=entry["differences"]["output"]["ranges"][32]["endExclusive"] + 1),
            "declared-first-32-output-ranges-only": lambda entry, scenario, computed: entry["differences"][
                "output"
            ].update(ranges=entry["differences"]["output"]["ranges"][:32]),
            "report-first-32-precursor-ranges": lambda entry, scenario, computed: scenario["precursorComparison"][
                "ranges"
            ][5].update(start=scenario["precursorComparison"]["ranges"][5]["start"] + 1),
        }
        for run_label, (entry, scenario, computed) in runs.items():
            cases = dict(drifts)
            if run_label == "33-output-and-precursor-ranges":
                cases.update(beyond_the_report)
            for label, drift in cases.items():
                with self.subTest(run=run_label, drift=label):
                    drifted = copy.deepcopy((entry, scenario, computed))
                    drift(*drifted)
                    self.assertNotEqual([], stale_declaration_errors(*drifted))

    def test_ledger_has_39_scenarios_over_37_routes(self) -> None:
        scenarios = self.ledger["scenarios"]
        ids = [row["scenarioId"] for row in scenarios]
        self.assertEqual(39, len(scenarios))
        self.assertEqual(sorted(set(ids)), ids)
        self.assertEqual(37, len({row["routeId"] for row in scenarios}))
        origins = sorted(row["origin"] for row in scenarios)
        self.assertEqual(["1.1.12-alignment"] * 37 + ["decision-64"] * 2, origins)

    def test_scenarios_bind_current_published_routes_and_declared_selection(self) -> None:
        for row in self.ledger["scenarios"]:
            with self.subTest(scenario=row["scenarioId"]):
                self.assertEqual([], self.scenario_binding_errors(row))
        ctrlram = next(row for row in self.ledger["scenarios"] if row["cli"]["selectionToken"] == "single")
        for label, field, value in (
            ("selection-token", "selectionToken", "cascade"),
            ("selection-option", "selectionOption", None),
        ):
            with self.subTest(mutation=label):
                mutated = copy.deepcopy(ctrlram)
                mutated["cli"][field] = value
                self.assertNotEqual([], self.scenario_binding_errors(mutated))

    def test_scenario_inputs_come_from_the_active_golden_cases(self) -> None:
        for row in self.ledger["scenarios"]:
            with self.subTest(scenario=row["scenarioId"]):
                self.assertEqual([], self.scenario_input_errors(row))
        first = self.ledger["scenarios"][0]
        ab_case = next(row for row in self.ledger["scenarios"] if row["binding"] == "case")
        full_flash = next(
            row
            for row in self.ledger["scenarios"]
            if (row["ctrlRamBase"] or {}).get("kind") == "standard-merge"
        )
        mutations = {
            "input-sha256": (first, lambda row: row["inputs"][0].update(sha256="0" * 64)),
            "input-size": (first, lambda row: row["inputs"][0].update(size=row["inputs"][0]["size"] + 1)),
            "input-order": (first, lambda row: row["inputs"].reverse()),
            "case-claims-route-evidence": (ab_case, lambda row: row.update(binding="route-evidence")),
            "base-kind": (full_flash, lambda row: row.update(ctrlRamBase={"kind": "tp-input"})),
        }
        for label, (source, mutate) in mutations.items():
            with self.subTest(mutation=label):
                mutated = copy.deepcopy(source)
                mutate(mutated)
                self.assertNotEqual([], self.scenario_input_errors(mutated))

    def test_seed_is_exactly_the_plan_routes_with_canonical_input(self) -> None:
        missing = set(self.plan["canonicalInputAuthority"]["currentlyMissingRouteIds"])
        bound = {route["routeId"]: route for route in self.plan_selected if route["routeId"] not in missing}
        self.assertEqual(64, len(self.plan_selected))
        self.assertEqual(37, len(bound))
        seed = [row for row in self.ledger["scenarios"] if row["origin"] == "1.1.12-alignment"]
        self.assertEqual(sorted(bound), sorted(row["planRouteId"] for row in seed))
        pinned_evidence = {row["routeId"]: row for row in self.pinned_manifest["routeEvidence"]}
        for row in seed:
            with self.subTest(scenario=row["scenarioId"]):
                planned = bound[row["planRouteId"]]
                self.assertEqual(SEED_RENAMES.get(route_key(planned), route_key(planned)), route_key(row))
                self.assertEqual(pinned_evidence[row["planRouteId"]]["caseId"], row["evidenceCaseId"])
        additions = [row for row in self.ledger["scenarios"] if row["origin"] == "decision-64"]
        self.assertEqual(DECISION_64_CASES, {row["evidenceCaseId"] for row in additions})
        for row in additions:
            self.assertIsNone(row["planRouteId"])
            self.assertEqual("case", row["binding"])

    def test_debt_set_is_exactly_the_plan_route_ids(self) -> None:
        self.assertEqual([], debt_set_errors(self.ledger, self.plan))
        shortened = copy.deepcopy(self.ledger)
        shortened["debtSet"]["routeIds"].pop()
        successor = copy.deepcopy(self.ledger)
        successor["debtSet"]["routeIds"][-1] = SUCCESSOR_ROUTE
        for label, ledger in (("shortened", shortened), ("successor-inherits", successor)):
            with self.subTest(mutation=label):
                self.assertNotEqual([], debt_set_errors(ledger, self.plan))

    def test_every_universe_route_is_covered_owed_or_disposed(self) -> None:
        self.assertEqual([], coverage_errors(self.ledger, self.universe, self.evidence))
        covered = {row["routeId"] for row in self.ledger["scenarios"]}
        debt = set(self.ledger["debtSet"]["routeIds"])
        pending = set(self.ledger["pendingAcceptedGaps"]["routeIds"])
        self.assertEqual(74, len(self.universe))
        self.assertEqual(37, len(covered))
        self.assertEqual(26, len(debt & self.universe))
        self.assertEqual([], self.ledger["acceptedGaps"])
        self.assertEqual(11, len(pending))
        self.assertEqual("awaiting-owner-approval", self.ledger["pendingAcceptedGaps"]["status"])

    def test_debt_exemption_is_not_inherited_by_successor_routes(self) -> None:
        debt = set(self.ledger["debtSet"]["routeIds"])
        pending = set(self.ledger["pendingAcceptedGaps"]["routeIds"])
        self.assertNotIn(SUCCESSOR_ROUTE, debt)
        self.assertIn(SUCCESSOR_ROUTE, pending)
        self.assertNotIn("route-7-nt51950-8-ab-merge-9-2-plus-ic-22-nt51950-ab-merge-1024k", self.universe)

    def test_a_dropped_scenario_cannot_become_silently_uncovered(self) -> None:
        by_binding = {row["binding"]: row for row in self.ledger["scenarios"]}
        route_evidence = by_binding["route-evidence"]["scenarioId"]
        for label, gap in (("unlisted", None), ("listed-as-pending", "pendingAcceptedGaps")):
            with self.subTest(dropped="route-evidence", case=label):
                ledger = copy.deepcopy(self.ledger)
                (dropped,) = [row for row in ledger["scenarios"] if row["scenarioId"] == route_evidence]
                ledger["scenarios"].remove(dropped)
                if gap:
                    ledger[gap]["routeIds"].append(dropped["routeId"])
                self.assertNotEqual([], coverage_errors(ledger, self.universe, self.evidence))
        ledger = copy.deepcopy(self.ledger)
        (dropped,) = [
            row
            for row in ledger["scenarios"]
            if row["scenarioId"] == "NT51929:ab-merge:selector-free:nt51929-ab-merge-512k:nt51929-ab-t05-d06"
        ]
        ledger["scenarios"].remove(dropped)
        with self.subTest(dropped="case", case="unlisted"):
            self.assertNotEqual([], coverage_errors(ledger, self.universe, self.evidence))
        # A case scenario may only turn into a pending gap, which blocks every
        # report of record until an approved, declared disposition replaces it.
        ledger["pendingAcceptedGaps"]["routeIds"].append(dropped["routeId"])
        with self.subTest(dropped="case", case="listed-as-pending"):
            self.assertEqual([], coverage_errors(ledger, self.universe, self.evidence))
            self.assertNotEqual([], report_of_record_blockers(ledger))

    def test_pending_gap_approvals_block_every_report_of_record(self) -> None:
        self.assertEqual(["pending gap approvals: 11"], report_of_record_blockers(self.ledger))
        approved = copy.deepcopy(self.ledger)
        approved["pendingAcceptedGaps"]["routeIds"] = []
        self.assertEqual([], report_of_record_blockers(approved))


if __name__ == "__main__":
    unittest.main()
