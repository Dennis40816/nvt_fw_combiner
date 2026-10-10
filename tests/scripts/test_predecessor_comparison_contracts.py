"""Semantic contract tests for the rolling predecessor comparison.

The tests read canonical contracts and JSON: the predecessor-comparison contract and ledger, the
report schema, the active capability policy and canonical Golden manifest with
their case manifests, the ADR 0057 plan, and the plan's pinned policy and
manifest from Git objects. They never read firmware payloads.

Schema validity, including every conditional relation of the in-effect report
and declaration schemas, is tested with the repository's Draft 2020-12 engine
in PredecessorComparisonSchemaContractTests (.NET). This module checks the
facts a schema cannot hold: the ledger against the governed sources it
declares, the contract against the report schema, and the per-side safety
owners the contract names. The ledger rules are those of the comparator's
single semantic validator, `scripts/predecessor_validation.py`, which these
tests call on the committed documents; that module's own tests cover every
rule, including declaration reproduction.
"""

from __future__ import annotations

import copy
import hashlib
import json
import subprocess
import unittest
from typing import Any

from scripts import predecessor_validation as validation
from scripts import predecessor_report_reader as reader
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


def load_json(path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def git_json(commit: str, relative: str) -> Any:
    return json.loads(
        subprocess.check_output(["git", "cat-file", "blob", f"{commit}:{relative}"], cwd=ROOT)
    )


def route_key(route: dict[str, Any]) -> tuple[str, str, str, str]:
    return (route["icId"], route["workflowId"], route["icCountVariant"], route["mapVariant"])


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
        cls.case_manifests = {
            case_id: load_json(ROOT / "testdata" / "golden" / "canonical" / row["manifestPath"])
            for case_id, row in cls.cases.items()
        }
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

    def scenario_input_failures(self, row: dict[str, Any]) -> list[validation.Failure]:
        return validation.scenario_input_failures(
            row,
            evidence=self.evidence,
            case_manifests=self.case_manifests,
            plan=self.plan,
            routes_by_key=self.routes_by_key,
        )

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
        self.assertEqual("in-effect", executor["compilerHost"]["status"])

    def test_per_side_safety_names_the_shared_owners(self) -> None:
        safety = self.contract["perSideSafety"]
        owners = {"scripts/v0916_parity_certification.py": MODULE,
                  "scripts/predecessor_validation.py": validation}
        self.assertEqual(list(owners), safety["owner"])
        self.assertIn("validate_report_projection_against_compiled_authority", safety["checks"])
        for name in safety["checks"]:
            with self.subTest(check=name):
                owner = validation if name in ("_executed_command_failures", "_processor_write_audit_failures") else MODULE
                self.assertTrue(callable(getattr(owner, name, None)))
        self.assertIn("_executed_command_failures", safety["checks"])
        self.assertIn("_processor_write_audit_failures", safety["checks"])
        self.assertFalse(safety["readerMayRelaxChecks"])
        rules = safety["writtenReportRules"]
        self.assertEqual(sorted(validation.WORK_ADDRESS_SPACES), rules["workAddressSpaces"])
        self.assertEqual(sorted(validation.V0916_WORK_ADDRESS_SPACES), rules["v0916ExecutorWorkAddressSpaces"])
        self.assertEqual(validation.OUTPUT_ADDRESS_SPACE, rules["processorWriteAudit"]["addressSpace"])
        self.assertIs(False, rules["processorWriteAudit"]["contentPreviewsRead"])
        schema = load_json(CONTRACTS / "predecessor-comparison-v1.schema.json")
        for member in ("owner", "checks"):
            self.assertEqual(safety[member], schema["properties"]["perSideSafety"]["properties"][member]["const"])
        self.assertEqual(rules, schema["properties"]["perSideSafety"]["properties"]["writtenReportRules"]["const"])
        self.assertEqual(self.contract["typedRejection"]["skippedOperations"],
                         schema["properties"]["typedRejection"]["properties"]["skippedOperations"]["const"])
        for name in ("declarationSchema", "reportSchema", "reportReader"):
            self.assertEqual("in-effect", self.contract["interfaces"][name]["status"])
        self.assertEqual(sorted(reader.READER_VERSIONS.values()), self.contract["interfaces"]["reportReader"]["readerVersions"])
        self.assertEqual(sorted(reader.READER_VERSIONS.values()), self.report_schema["$defs"]["capturedReport"]["properties"]["readerVersion"]["enum"])
        self.assertEqual(
            self.contract["typedRejection"]["processFailureIssueCodes"],
            self.report_schema["$defs"]["processFailureIssueCode"]["enum"],
        )
        self.assertEqual(sorted(validation.PROCESS_FAILURE_ISSUE_CODES), self.contract["typedRejection"]["processFailureIssueCodes"])
        declaration_schema = load_json(CONTRACTS / "predecessor-comparison-declaration-v1.schema.json")
        self.assertEqual(self.contract["typedRejection"]["processFailureIssueCodes"], declaration_schema["$defs"]["processFailureIssueCode"]["enum"])

    def test_decision_278_contract_does_not_reclassify_saved_invalid_results(self) -> None:
        contract = (CONTRACTS / "predecessor-comparison-v1.md").read_text(encoding="utf-8")
        decision = contract.split("**Owner decision 278 (2026-10-03):", 1)[1].split("\n\n", 1)[0]
        for member in ("ab-combiner-work", "[262144, 524288)", "v0.9.16", "273"):
            self.assertIn(member, decision)
        self.assertIn('report_version="v0916"', decision)
        self.assertIn("Later writes", decision)

        report = load_json(ROOT / "tests/scripts/fixtures/predecessor-comparison/v0916-consistent.json")
        route = next(row for row in report["routes"] if row["result"] == "consistent")
        route.update(result="invalid", failureCode="PREDECESSOR_REPORT_INVALID")
        report["result"] = "invalid"
        report["summary"] = validation.v0916_summary([row["result"] for row in report["routes"]])
        report["failures"] = [{"code": "PREDECESSOR_REPORT_INVALID", "subject": route["planRouteId"],
                               "detail": "saved audit refusal"}]
        report["deterministicSha256"] = validation.deterministic_report_sha256(report)
        saved = copy.deepcopy(report)
        projected = reader.render_owner_list(report)
        self.assertIn("result: invalid.", projected)
        self.assertIn("PREDECESSOR_REPORT_INVALID", projected)
        self.assertEqual(saved, report)

        reclassified = copy.deepcopy(saved)
        reclassified["result"] = "consistent"
        reclassified["deterministicSha256"] = validation.deterministic_report_sha256(reclassified)
        with self.assertRaises(reader.ReportReaderError):
            reader.render_owner_list(reclassified)

    def test_all_execution_interfaces_are_in_effect(self) -> None:
        self.assertEqual("in-effect", self.contract["executor"]["compilerHost"]["status"])
        amendment = load_json(CONTRACTS / "v0916-parity-1x-amendment-v1.json")
        self.assertEqual("in-effect", amendment["baselineExecutor"]["status"])
        pending = next(row for row in self.contract["failureCodes"] if row["code"] == "PREDECESSOR_CONTRACT_PENDING")
        self.assertIn("compiler-host pinning", pending["meaning"])
        self.assertIn("v0.9.16 baseline executor", pending["meaning"])

    def test_additive_terminal_entry_points_are_available(self) -> None:
        for name in ("admit_case_inputs", "runtime_closure_inventory", "resolve_case", "cli_arguments", "input_option",
                     "normalize_raw_operation", "normalize_raw_mutation"):
            with self.subTest(entry_point=name):
                self.assertTrue(callable(getattr(MODULE, name, None)))

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
                self.assertEqual([], validation.scenario_binding_failures(row, routes_by_key=self.routes_by_key, universe=self.universe))
        ctrlram = next(row for row in self.ledger["scenarios"] if row["cli"]["selectionToken"] == "single")
        for label, field, value in (
            ("selection-token", "selectionToken", "cascade"),
            ("selection-option", "selectionOption", None),
        ):
            with self.subTest(mutation=label):
                mutated = copy.deepcopy(ctrlram)
                mutated["cli"][field] = value
                self.assertNotEqual([], validation.scenario_binding_failures(mutated, routes_by_key=self.routes_by_key, universe=self.universe))

    def test_scenario_inputs_come_from_the_active_golden_cases(self) -> None:
        for row in self.ledger["scenarios"]:
            with self.subTest(scenario=row["scenarioId"]):
                self.assertEqual([], self.scenario_input_failures(row))
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
                self.assertNotEqual([], self.scenario_input_failures(mutated))

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
        self.assertEqual([], validation.debt_set_failures(self.ledger, self.plan))
        shortened = copy.deepcopy(self.ledger)
        shortened["debtSet"]["routeIds"].pop()
        successor = copy.deepcopy(self.ledger)
        successor["debtSet"]["routeIds"][-1] = SUCCESSOR_ROUTE
        for label, ledger in (("shortened", shortened), ("successor-inherits", successor)):
            with self.subTest(mutation=label):
                self.assertNotEqual([], validation.debt_set_failures(ledger, self.plan))

    def test_every_universe_route_is_covered_owed_or_disposed(self) -> None:
        self.assertEqual([], validation.coverage_failures(self.ledger, universe=self.universe, evidence=self.evidence))
        covered = {row["routeId"] for row in self.ledger["scenarios"]}
        debt = set(self.ledger["debtSet"]["routeIds"])
        pending = set(self.ledger["pendingAcceptedGaps"]["routeIds"])
        accepted = {row["routeId"] for row in self.ledger["acceptedGaps"]}
        self.assertEqual(74, len(self.universe))
        self.assertEqual(37, len(covered))
        self.assertEqual(26, len(debt & self.universe))
        self.assertEqual(11, len(accepted))
        self.assertEqual(set(), pending)
        self.assertEqual({"1.2.2"}, {row["approvedInVersion"] for row in self.ledger["acceptedGaps"]})
        self.assertEqual({"RP-1.2.2-01"}, {row["declarationEntryId"] for row in self.ledger["acceptedGaps"]})
        self.assertEqual("awaiting-owner-approval", self.ledger["pendingAcceptedGaps"]["status"])

    def test_declaration_binds_this_ledger_and_its_accepted_gaps(self) -> None:
        declaration = load_json(CONTRACTS / "predecessor-comparison-declarations" / "1.2.2.json")
        ledger_bytes = (CONTRACTS / "predecessor-comparison-scenarios-v1.json").read_bytes()
        self.assertEqual(hashlib.sha256(ledger_bytes).hexdigest(), declaration["ledgerSha256"])
        (entry,) = [row for row in declaration["entries"] if row["kind"] == "accepted-gap"]
        gaps = self.ledger["acceptedGaps"]
        self.assertEqual([], entry["scenarioIds"])
        self.assertEqual("RP-1.2.2-01", entry["id"])
        self.assertEqual(sorted(entry["routeIds"]), sorted(row["routeId"] for row in gaps))
        self.assertEqual(len(entry["routeIds"]), len(set(entry["routeIds"])))
        self.assertEqual([entry["approval"]], [dict(approval) for approval in {
            tuple(sorted(row["approval"].items())) for row in gaps}])
        notes = validation.render_release_notes((ROOT / "CHANGELOG.md").read_text(encoding="utf-8"), "1.2.2")
        self.assertRegex(notes, r"(?<![0-9A-Za-z.-])RP-1\.2\.2-01(?![0-9])")

    def test_debt_exemption_is_not_inherited_by_successor_routes(self) -> None:
        debt = set(self.ledger["debtSet"]["routeIds"])
        accepted = {row["routeId"] for row in self.ledger["acceptedGaps"]}
        self.assertNotIn(SUCCESSOR_ROUTE, debt)
        self.assertIn(SUCCESSOR_ROUTE, accepted)
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
                self.assertNotEqual([], validation.coverage_failures(ledger, universe=self.universe, evidence=self.evidence))
        ledger = copy.deepcopy(self.ledger)
        (dropped,) = [
            row
            for row in ledger["scenarios"]
            if row["scenarioId"] == "NT51929:ab-merge:selector-free:nt51929-ab-merge-512k:nt51929-ab-t05-d06"
        ]
        ledger["scenarios"].remove(dropped)
        with self.subTest(dropped="case", case="unlisted"):
            self.assertNotEqual([], validation.coverage_failures(ledger, universe=self.universe, evidence=self.evidence))
        # A case scenario may only turn into a pending gap, which blocks every
        # report of record until an approved, declared disposition replaces it.
        ledger["pendingAcceptedGaps"]["routeIds"].append(dropped["routeId"])
        with self.subTest(dropped="case", case="listed-as-pending"):
            self.assertEqual([], validation.coverage_failures(ledger, universe=self.universe, evidence=self.evidence))
            self.assertNotEqual([], validation.report_of_record_blockers(ledger))

    def test_pending_gap_approvals_block_every_report_of_record(self) -> None:
        self.assertEqual([], validation.report_of_record_blockers(self.ledger))
        unapproved = copy.deepcopy(self.ledger)
        unapproved["pendingAcceptedGaps"]["routeIds"] = [row["routeId"] for row in unapproved["acceptedGaps"]]
        unapproved["acceptedGaps"] = []
        self.assertEqual(11, len(validation.report_of_record_blockers(unapproved)))


if __name__ == "__main__":
    unittest.main()
