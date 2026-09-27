"""Evidence tests for the v0.9.16 parity 1.x amendment.

The amendment's rows are firmware-owner approvals of exact values. These tests
check every bound identity against independent payload-free sources already in
the repository: the ADR 0057 plan and its pinned policy and manifest, the
canonical case manifests, the capability profile regions the attributions
cite, the recorded 1.1.12 comparison evidence, and the board record of
decision 12. Each row check is an error-list helper, and a mutation of each
bound identity must make it fail. The tests never read firmware payloads, and
they do not reproduce bytes: the milestone run of the v0.9.16 1.x mode does.
"""

from __future__ import annotations

import copy
import hashlib
import json
import re
import subprocess
import unittest
from typing import Any

from scripts import predecessor_validation as validation
from tests.scripts.v0916_parity_test_support import ROOT

CONTRACTS = ROOT / "docs" / "contracts"
AMENDMENT_PATH = CONTRACTS / "v0916-parity-1x-amendment-v1.json"
PLAN_PATH = CONTRACTS / "v0916-parity-certification-v1.json"
COMPARISON_PATH = "docs/handoff/1.1.12/parity/v0916-local-comparison.json"
EXPLANATIONS_PATH = "docs/handoff/1.1.12/parity/explanations.json"
BOARD_PATH = ROOT / "docs" / "handoff" / "1.1.12.md"
FAMILY_PATH = "profiles/built-in/nt51950-ctrlram-replace-candidate/families/nt51950-ctrlram-replace.json"
TP_WORK_ROUTE = "route-7-nt51950-15-ctrlram-replace-4-2-ic-36-nt51950-ctrlram-fw1x-cascade-tp-work"
FULL_FLASH_ROUTE = "route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash"
DIFF_RECORD_START = 0x33200
OTHER_SHA256 = "0" * 64


def load_json(path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def git_json(commit: str, relative: str) -> Any:
    return json.loads(
        subprocess.check_output(["git", "cat-file", "blob", f"{commit}:{relative}"], cwd=ROOT)
    )


def hex_offset(value: str) -> int:
    return int(value, 16)


class V0916Parity1xAmendmentTests(unittest.TestCase):
    """Checks the amendment rows against the evidence they transcribe."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.amendment = load_json(AMENDMENT_PATH)
        cls.plan = load_json(PLAN_PATH)
        authority = cls.plan["canonicalInputAuthority"]
        pinned_policy = git_json(authority["repositoryCommit"], cls.plan["policyBinding"]["path"])
        cls.plan_routes = {
            route["routeId"]: route
            for route in pinned_policy["routes"]
            if route["authoring"]["value"] == "available"
            and route["publication"]["value"] == "supported"
            and route["workflowId"] in cls.plan["selection"]["includedWorkflows"]
        }
        cls.pinned_evidence = {
            row["routeId"]: row
            for row in git_json(authority["repositoryCommit"], authority["manifestPath"])["routeEvidence"]
        }
        comparison = load_json(ROOT / COMPARISON_PATH)
        cls.recorded = {row["routeId"]: row for row in comparison["routes"]}
        cls.explanations = load_json(ROOT / EXPLANATIONS_PATH)
        family = load_json(ROOT / FAMILY_PATH)
        cls.regions = {
            region["regionId"]: region
            for region_set in family["regionSets"]
            if region_set["regionSetId"] == "nt51950-ctrlram-fw200-tp-work"
            for region in region_set["regions"]
        }
        (cls.correction,) = cls.amendment["approvedSemanticCorrections"]
        (cls.not_applicable,) = cls.amendment["baselineNotApplicable"]

    def case_manifest(self, relative: str) -> dict[str, Any]:
        return load_json(ROOT / relative)

    def correction_errors(self, row: dict[str, Any]) -> list[str]:
        """A correction row transcribes the recorded comparison and the plan's twin row exactly."""

        errors: list[str] = []
        recorded = self.recorded[row["routeId"]]
        comparison = recorded["comparison"]
        if recorded["result"] != "different" or comparison["rangesTruncated"]:
            errors.append("the recorded comparison is not a complete difference")
        if row["baselineOutput"] != recorded["baseline"]["output"]:
            errors.append("baseline output differs from the recorded v0.9.16 output")
        if row["candidateOutput"] != recorded["candidate"]["output"]:
            errors.append("candidate output differs from the recorded candidate output")
        if row["differentByteCount"] != comparison["differentByteCount"]:
            errors.append("byte count differs from the recorded comparison")
        if row["differentRanges"] != comparison["ranges"]:
            errors.append("ranges differ from the recorded comparison")
        ranges = [(item["start"], item["endExclusive"]) for item in row["differentRanges"]]
        if any(start >= end for start, end in ranges) or any(
            left[1] > right[0] for left, right in zip(ranges, ranges[1:])
        ):
            errors.append("ranges are not ordered, disjoint and half-open")
        if row["differentByteCount"] != sum(end - start for start, end in ranges):
            errors.append("byte count differs from the range lengths")
        if [(item["start"], item["endExclusive"]) for item in row["attribution"]] != ranges:
            errors.append("attribution does not cover exactly the differing ranges")
        (twin,) = self.plan["approvedSemanticCorrections"]
        if (row["differentRanges"], row["differentByteCount"]) != (twin["differentRanges"], twin["differentByteCount"]):
            errors.append("ranges differ from the plan's NT51951 row")
        if self.plan_routes[row["routeId"]]["capabilityFingerprint"] != row["capabilityFingerprint"]:
            errors.append("fingerprint differs from the plan route")
        if row["routeId"] in self.plan["canonicalInputAuthority"]["currentlyMissingRouteIds"]:
            errors.append("the route has no canonical input in the plan")
        if row["routeId"] in {item["routeId"] for item in self.plan["transitiveRoutes"]}:
            errors.append("the route is a transitive plan route")
        return errors

    def attribution_errors(self, row: dict[str, Any]) -> list[str]:
        """Each attribution cites evidence that exists and names exactly its range."""

        errors: list[str] = []
        provenance = row["candidateProvenance"]
        source_case = self.case_manifest(provenance["inputCaseManifest"]["path"])
        classified = {
            (hex_offset(item["start"]), hex_offset(item["endExclusive"])): item["classification"]
            for item in source_case["allowedByteDifferenceContract"]["allowedDifferenceRanges"]
        }
        alias = self.case_manifest(provenance["evidenceCaseManifest"]["path"])["alias"]
        tail = next(scope for scope in alias["factScope"] if "reference-preserved Diff NF tail" in scope)
        tail_start, tail_end = (hex_offset(value) for value in re.findall(r"0x[0-9A-Fa-f]+", tail.split("tail")[1])[:2])
        for item in row["attribution"]:
            span = (item["start"], item["endExclusive"])
            for reference in item["evidence"]:
                path, _, anchor = reference.partition("#")
                if not (ROOT / path).is_file():
                    errors.append(f"{span}: evidence {path} does not exist")
                elif path.startswith("profiles/") and anchor and anchor not in self.regions:
                    errors.append(f"{span}: profile region {anchor} does not exist")
                elif path.endswith("case.json") and anchor and anchor.split(".")[0] not in self.case_manifest(path):
                    errors.append(f"{span}: case member {anchor} does not exist")
            if item["mechanism"] == "derived-field":
                classification = classified.get(span)
                if classification is None or not item["cause"].startswith(classification + ","):
                    errors.append(f"{span}: not a classified CRC word of the source case")
                if item["causeVerification"] != "not-independently-verified":
                    errors.append(f"{span}: a CRC cause is claimed as verified")
                regions = [
                    region
                    for region in self.regions.values()
                    if region["kind"] == "header"
                    and region["range"]["start"] <= span[0]
                    and span[1] <= region["range"]["start"] + region["range"]["length"]
                ]
                if not regions:
                    errors.append(f"{span}: no header region of the profile contains it")
            elif item["mechanism"] == "stopped-write-preserved-bytes":
                if span != (DIFF_RECORD_START + tail_start, DIFF_RECORD_START + tail_end):
                    errors.append(f"{span}: not the alias case's preserved Diff NF tail")
                diff = self.regions["diff-ctrlram"]["range"]
                if not (diff["start"] <= span[0] and span[1] == diff["start"] + diff["length"]):
                    errors.append(f"{span}: not the tail of the profile's Diff CtrlRAM region")
            else:
                errors.append(f"{span}: unexpected mechanism {item['mechanism']}")
        return errors

    def not_applicable_errors(self, row: dict[str, Any]) -> list[str]:
        """A not-applicable row transcribes the recorded two-sided run and the plan binding."""

        errors: list[str] = []
        recorded = self.recorded[row["routeId"]]
        baseline, candidate = recorded["baseline"], recorded["candidate"]
        precursor = row["expectedBaseline"]["precursorOutput"]
        if precursor != baseline["basePrecursorOutput"] or precursor != candidate["basePrecursorOutput"]:
            errors.append("precursor differs from the recorded precursors")
        if row["expectedCandidate"]["precursorOutput"] != precursor:
            errors.append("the two sides bind different precursors")
        case = self.case_manifest(
            f"testdata/golden/canonical/NT51951/ctrlram-replace/fw2.0.0/cascade-2/{row['binding']['inputCaseId']}/provenance/case.json"
        )
        if case["baseRecipe"]["resultSha256"] != precursor["sha256"]:
            errors.append("precursor differs from the input case's recorded base")
        last = baseline["invocations"][-1]
        if (baseline["status"], baseline["failedStage"], last["action"], last["exitCode"]) != (
            "error",
            "workflow",
            row["expectedBaseline"]["rejectingStage"],
            1,
        ):
            errors.append("the recorded baseline did not stop at the bound stage")
        explanation = self.explanations[row["routeId"]]["explanation"]
        if not all(f"`{code}`" in explanation for code in row["expectedBaseline"]["issueCodes"]):
            errors.append("the recorded explanation does not name the bound issue codes")
        if row["expectedCandidate"]["output"] != candidate["output"]:
            errors.append("candidate output differs from the recorded candidate output")
        (twin,) = self.plan["approvedSemanticCorrections"]
        if row["expectedCandidate"]["output"] != twin["candidateOutput"]:
            errors.append("candidate output is not the NT51951 route's registered-Combiner output")
        base = {item["routeId"]: item for item in self.plan["canonicalInputAuthority"]["ctrlRamBaseRoutes"]}[row["routeId"]]
        if (base["kind"], base["standardMergeMapVariant"]) != ("standard-merge", row["binding"]["precursorMapVariant"]):
            errors.append("precursor map variant differs from the plan binding")
        if self.pinned_evidence[row["routeId"]]["caseId"] != row["binding"]["evidenceCaseId"]:
            errors.append("evidence case differs from the plan's route evidence")
        if self.plan_routes[row["routeId"]]["capabilityFingerprint"] != row["capabilityFingerprint"]:
            errors.append("fingerprint differs from the plan route")
        return errors

    def test_amendment_binds_the_plan_without_its_candidate_authority(self) -> None:
        self.assertEqual([], validation.amendment_binding_failures(self.amendment, self.plan))
        resynchronized = copy.deepcopy(self.plan)
        resynchronized["candidateAuthority"]["resynchronizedByTest"] = True
        self.assertEqual([], validation.amendment_binding_failures(self.amendment, resynchronized))
        changed = copy.deepcopy(self.plan)
        changed["canonicalInputAuthority"]["currentlyMissingRouteIds"].pop()
        altered = copy.deepcopy(self.amendment)
        altered["plan"]["withoutCandidateAuthorityJcsSha256"] = OTHER_SHA256
        for label, amendment, plan in (("plan-changed", self.amendment, changed), ("digest-altered", altered, self.plan)):
            with self.subTest(mutation=label):
                self.assertNotEqual([], validation.amendment_binding_failures(amendment, plan))

    def test_recorded_evidence_is_bound_by_size_and_sha256(self) -> None:
        paths = [row["path"] for row in self.amendment["recordedEvidence"]]
        self.assertEqual(sorted([COMPARISON_PATH, EXPLANATIONS_PATH]), paths)
        for row in self.amendment["recordedEvidence"]:
            with self.subTest(path=row["path"]):
                payload = (ROOT / row["path"]).read_bytes()
                self.assertEqual((row["size"], row["sha256"]), (len(payload), hashlib.sha256(payload).hexdigest()))

    def test_tp_work_correction_transcribes_the_recorded_comparison(self) -> None:
        row = self.correction
        self.assertEqual(TP_WORK_ROUTE, row["routeId"])
        self.assertEqual([], self.correction_errors(row))
        self.assertEqual((225280, 2816), (row["candidateOutput"]["size"], row["differentByteCount"]))
        provenance = row["candidateProvenance"]
        self.assertEqual(self.pinned_evidence[row["routeId"]]["caseId"], provenance["evidenceCaseId"])
        self.assertEqual(
            provenance["inputCaseId"],
            self.case_manifest(provenance["evidenceCaseManifest"]["path"])["alias"]["sourceCaseId"],
        )
        for key in ("evidenceCaseManifest", "inputCaseManifest"):
            with self.subTest(manifest=key):
                payload = (ROOT / provenance[key]["path"]).read_bytes()
                self.assertEqual(
                    (provenance[key]["size"], provenance[key]["sha256"]),
                    (len(payload), hashlib.sha256(payload).hexdigest()),
                )
        mutations = {
            "widened-range": lambda item: item["differentRanges"][-1].update(endExclusive=214532),
            "candidate-output-sha256": lambda item: item["candidateOutput"].update(sha256=OTHER_SHA256),
            "baseline-output-sha256": lambda item: item["baselineOutput"].update(sha256=OTHER_SHA256),
            "byte-count": lambda item: item.update(differentByteCount=2812),
            "fingerprint": lambda item: item.update(capabilityFingerprint=OTHER_SHA256),
        }
        for label, mutate in mutations.items():
            with self.subTest(mutation=label):
                mutated = copy.deepcopy(row)
                mutate(mutated)
                self.assertNotEqual([], self.correction_errors(mutated))

    def test_tp_work_attribution_cites_existing_evidence_and_claims_no_crc_cause(self) -> None:
        row = self.correction
        self.assertEqual([], self.attribution_errors(row))
        mechanisms = [item["mechanism"] for item in row["attribution"]]
        self.assertEqual(["derived-field"] * 4 + ["stopped-write-preserved-bytes"], mechanisms)
        mutations = {
            "crc-cause-claimed": lambda item: item["attribution"][0].update(causeVerification="supported-by-cited-evidence"),
            "unclassified-word": lambda item: item["attribution"][1].update(start=41260, endExclusive=41264),
            "tail-widened": lambda item: item["attribution"][4].update(start=211724),
            "missing-region": lambda item: item["attribution"][0]["evidence"].append(f"{FAMILY_PATH}#no-such-region"),
        }
        for label, mutate in mutations.items():
            with self.subTest(mutation=label):
                mutated = copy.deepcopy(row)
                mutate(mutated)
                self.assertNotEqual([], self.attribution_errors(mutated))

    def test_full_flash_binding_transcribes_the_recorded_rejection(self) -> None:
        row = self.not_applicable
        self.assertEqual(FULL_FLASH_ROUTE, row["routeId"])
        self.assertEqual([], self.not_applicable_errors(row))
        mutations = {
            "precursor-sha256": lambda item: item["expectedBaseline"]["precursorOutput"].update(sha256=OTHER_SHA256),
            "candidate-output-sha256": lambda item: item["expectedCandidate"]["output"].update(sha256=OTHER_SHA256),
            "issue-code": lambda item: item["expectedBaseline"].update(issueCodes=["profile.v2.compile.other"]),
            "rejecting-stage": lambda item: item["expectedBaseline"].update(rejectingStage="build"),
            "precursor-map": lambda item: item["binding"].update(precursorMapVariant="nt51950-standard-merge-1024k"),
        }
        for label, mutate in mutations.items():
            with self.subTest(mutation=label):
                mutated = copy.deepcopy(row)
                mutate(mutated)
                self.assertNotEqual([], self.not_applicable_errors(mutated))

    def test_rows_cite_only_their_own_decisions_and_scope(self) -> None:
        self.assertEqual(
            (
                "1.1.12 board decision 12",
                "exactly-these-values",
                "owner-approved-diff-nf-preservation",
                "exact-output-with-approved-semantic-correction",
            ),
            (
                self.correction["boardDecision"],
                self.correction["scope"],
                self.correction["kind"],
                self.correction["requiredProofKind"],
            ),
        )
        self.assertEqual(
            ("1.1.12 board decision 62", "this-canonical-binding-only", "2.0.0-terminal-rebinding"),
            (self.not_applicable["boardDecision"], self.not_applicable["scope"], self.not_applicable["revisit"]),
        )
        self.assertEqual(
            {
                "status": "pending-executor-record",
                "boardDecisions": ["1.1.12 board decision 63", "1.1.12 board decision 79"],
                "contract": None,
            },
            self.amendment["baselineExecutor"],
        )
        board = " ".join(BOARD_PATH.read_text(encoding="utf-8").split())
        decision_12 = board.split("12. **Parity, flash capacity and preload**", 1)[1].split("13. **", 1)[0]
        self.assertIn("the NT51951 and NT51950 2-IC cascade Diff NF preservation differences are approved", decision_12)

    def test_candidate_values_are_historical_observations(self) -> None:
        evidence = {row["path"] for row in self.amendment["recordedEvidence"]}
        observations = (self.correction["observation"], self.not_applicable["expectedCandidate"]["observation"])
        for observation in observations:
            with self.subTest(recorded=observation["recordedIn"]):
                self.assertEqual("historical-non-certifying-observation", observation["status"])
                self.assertIn(observation["recordedIn"], evidence)
                for commit in observation["candidateSourceCommits"]:
                    subprocess.run(["git", "merge-base", "--is-ancestor", commit, "HEAD"], cwd=ROOT, check=True)
        self.assertTrue(self.correction["observation"]["sameRunConfirmationRequired"])
        self.assertEqual("required-at-the-1.1.13-milestone-run", self.not_applicable["expectedCandidate"]["ownerConfirmation"])


if __name__ == "__main__":
    unittest.main()
