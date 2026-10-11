"""Upstream shard-policy cases, including privacy across the real process wrapper of the checker."""
from copy import deepcopy
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

from scripts.ci import baseline_contract, check_test_shard, read_trx, update_test_baseline

ROOT = Path(__file__).resolve().parents[2]
MARKER = "SYNTHETIC_CONFIDENTIAL_SENTINEL_84AC"


def write_trx(directory, outcomes=("Passed", "Passed", "Passed"), *,
              run_outcome="Completed", extra_total=0, cls="Tests.Sample"):
    directory.mkdir(parents=True, exist_ok=True)
    definitions, results = [], []
    for index, outcome in enumerate(outcomes):
        definitions.append(f'<UnitTest id="{index}"><TestMethod className="{cls}, Assembly" '
                           f'name="Method{index}({MARKER})" /></UnitTest>')
        results.append(f'<UnitTestResult testId="{index}" testName="{MARKER}" outcome="{outcome}">'
                       f'<Output><StdOut>{MARKER}</StdOut><ErrorInfo><Message>{MARKER}</Message>'
                       f'<StackTrace>{MARKER}</StackTrace></ErrorInfo></Output></UnitTestResult>')
    payload = (f'<TestRun xmlns="{read_trx.NS[1:-1]}"><TestDefinitions>{"".join(definitions)}'
               f'</TestDefinitions><Results>{"".join(results)}</Results><ResultSummary outcome="{run_outcome}">'
               f'<Counters total="{len(outcomes) + extra_total}" passed="{outcomes.count("Passed")}" '
               f'failed="{outcomes.count("Failed")}" /></ResultSummary></TestRun>')
    path = directory / "fixture.trx"
    path.write_text(payload, encoding="utf-8")
    return path


class CiTestBaselineTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.results = self.root / "results"
        self.baseline = self.root / "baseline.json"
        self.summary = self.root / "shard-summary.json"
        self.document = json.loads((ROOT / "scripts/ci/test-baseline.json").read_text())
        self.document["shards"]["core"].update(minTotal=3, exampleMinTests=0, requiredClasses=[])
        self.save_baseline()
        self.environment = patch.dict(os.environ, {}, clear=False)
        self.environment.start()
        self.addCleanup(self.environment.stop)
        for variable in baseline_contract.UPDATE_MODE_VARIABLES:
            os.environ.pop(variable, None)

    def save_baseline(self):
        baseline_contract.write_json(self.baseline, self.document)

    def check(self, *extra):
        result = subprocess.run(
            [sys.executable, str(ROOT / "scripts/ci/check_test_shard.py"), "--shard", "core",
             "--results-directory", str(self.results), "--baseline-path", str(self.baseline),
             "--summary-path", str(self.summary), "--commit", "a" * 40, *extra],
            capture_output=True, text=True, check=False, env=os.environ.copy(),
        )
        summary = json.loads(self.summary.read_text())
        public_output = self.summary.read_text() + result.stdout + result.stderr
        self.assertNotIn(MARKER, public_output)
        self.assertNotIn("ErrorInfo", public_output)
        self.assertNotIn("StackTrace", public_output)
        return result, summary

    def update(self, *extra):
        return subprocess.run(
            [sys.executable, str(ROOT / "scripts/ci/update_test_baseline.py"), "--shard", "core",
             "--results-directory", str(self.results), "--baseline-path", str(self.baseline),
             "--measured-at-commit", "b" * 40, *extra],
            capture_output=True, text=True, check=False, env=os.environ.copy(),
        )

    def test_pass_and_exact_summary_contract(self):
        write_trx(self.results)
        result, summary = self.check()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("passed", summary["outcome"])
        self.assertEqual((3, 3, 0, 0, 0), tuple(summary[k] for k in
                         ("total", "passed", "failed", "skipped", "notExecuted")))
        self.assertEqual({"schemaVersion", "shard", "commit", "outcome", "total", "passed",
                          "failed", "skipped", "notExecuted", "failedTests", "exampleData", "checks"},
                         set(summary))
        self.assertEqual({"present": False, "executed": 0}, summary["exampleData"])
        self.assertFalse(any(summary["checks"].values()))
        self.assertIn(b"\r\n", self.summary.read_bytes())
        self.assertFalse(self.summary.read_bytes().startswith(b"\xef\xbb\xbf"))

    def test_floor_skip_failure_and_incomplete_rows(self):
        cases = ((('Passed',) * 2, 2, 0, 0, 0, "belowFloor"),
                 (('Passed', 'Passed', 'NotExecuted'), 3, 0, 1, 0, "skippedNotZero"),
                 (('Passed', 'Passed', 'Failed'), 3, 1, 0, 0, None),
                 (('Passed', 'Passed', 'Timeout'), 3, 0, 0, 1, None))
        for outcomes, total, failed, skipped, unfinished, flag in cases:
            with self.subTest(outcomes=outcomes):
                write_trx(self.results, outcomes)
                result, summary = self.check()
                self.assertEqual(1, result.returncode)
                self.assertEqual("Shard validation failed.\n", result.stderr)
                self.assertEqual("failed", summary["outcome"])
                self.assertEqual((total, failed, skipped, unfinished),
                                 tuple(summary[k] for k in ("total", "failed", "skipped", "notExecuted")))
                if flag:
                    self.assertTrue(summary["checks"][flag])
                self.assertEqual(["Tests.Sample.Method2"] if failed else [], summary["failedTests"])

    def test_missing_malformed_dtd_and_inconsistent_trx(self):
        result, summary = self.check()
        self.assertEqual((1, 0), (result.returncode, summary["total"]))
        for payload in (MARKER, f'<!DOCTYPE TestRun [<!ENTITY x SYSTEM "{MARKER}">]><TestRun>&x;</TestRun>'):
            with self.subTest(payload=payload):
                self.results.mkdir(exist_ok=True)
                (self.results / "fixture.trx").write_text(payload, encoding="utf-8")
                result, summary = self.check()
                self.assertEqual((1, 0), (result.returncode, summary["total"]))
                with self.assertRaisesRegex(ValueError, r"^TRX results missing or invalid\.$"):
                    read_trx.read_trx_results(self.results)
        path = write_trx(self.results)
        path.write_text(path.read_text().replace('passed="3"', 'passed="2"'))
        result, summary = self.check()
        self.assertEqual((1, 0), (result.returncode, summary["total"]))

    def test_unfinished_run_unverified_and_process_failure(self):
        for outcome in ("Aborted", "Failed", "", "Passed"):
            with self.subTest(outcome=outcome):
                write_trx(self.results, run_outcome=outcome)
                result, summary = self.check()
                self.assertEqual(1, result.returncode)
                self.assertEqual((3, 3, 0), (summary["total"], summary["passed"], summary["notExecuted"]))
                self.assertFalse(any(summary["checks"].values()))
                self.assertEqual(1, self.update().returncode)
        write_trx(self.results, extra_total=1)
        self.assertEqual(1, self.check()[1]["notExecuted"])
        write_trx(self.results)
        self.assertEqual("failed", self.check("--test-run-failed")[1]["outcome"])
        result, summary = self.check("--unverified")
        self.assertEqual((1, "unverified"), (result.returncode, summary["outcome"]))
        self.assertEqual("unverified, cannot merge\n", result.stderr)

    def test_update_variables_presence_including_empty_and_zero(self):
        write_trx(self.results)
        for variable in baseline_contract.UPDATE_MODE_VARIABLES:
            for value in ("1", "0", ""):
                with self.subTest(variable=variable, value=value), patch.dict(os.environ, {variable: value}):
                    result, summary = self.check()
                    self.assertEqual(1, result.returncode)
                    self.assertTrue(summary["checks"]["updateModeVariableSet"])

    def test_required_class_and_example_floor(self):
        self.document["shards"]["core"].update(requiredClasses=["Tests.Required"])
        self.save_baseline()
        write_trx(self.results)
        self.assertTrue(self.check("--example-data-present")[1]["checks"]["requiredClassMissing"])
        write_trx(self.results, cls="Tests.Required")
        self.assertEqual(0, self.check("--example-data-present")[0].returncode)
        write_trx(self.results, ("Passed", "Passed", "Failed"), cls="Tests.Required")
        self.assertTrue(self.check("--example-data-present")[1]["checks"]["requiredClassMissing"])
        self.document["shards"]["core"].update(exampleMinTests=1, requiredClasses=[])
        self.save_baseline()
        write_trx(self.results)
        self.assertTrue(self.check("--example-data-present")[1]["checks"]["belowFloor"])

    def test_updater_raises_only_and_rejected_run_preserves_bytes(self):
        write_trx(self.results, ("Passed",) * 4)
        before = deepcopy(self.document)
        self.assertEqual(0, self.update().returncode)
        after = json.loads(self.baseline.read_text())
        before["shards"]["core"]["minTotal"] = 4
        before["measuredAtCommit"] = "b" * 40
        self.assertEqual(before, after)
        saved = self.baseline.read_bytes()
        self.assertEqual(0, self.update().returncode)
        self.assertEqual(saved, self.baseline.read_bytes())
        for outcomes in (("Passed",) * 3, ("Passed",) * 3 + ("Failed",),
                         ("Passed",) * 3 + ("NotExecuted",), ("Passed",) * 3 + ("Timeout",)):
            write_trx(self.results, outcomes)
            result = self.update()
            self.assertEqual(1, result.returncode)
            self.assertEqual("Test baseline update failed.\n", result.stderr)
            self.assertNotIn(MARKER, result.stdout + result.stderr)
            self.assertEqual(saved, self.baseline.read_bytes())

    def test_all_trx_files_and_sorted_parameter_free_method_names(self):
        write_trx(self.results, ("Failed",), cls="Tests.Z")
        write_trx(self.results / "second", ("Failed", "Failed"), cls="Tests.A")
        counts = read_trx.read_trx_results(self.results)
        self.assertEqual(3, counts["Total"])
        self.assertEqual(["Tests.A.Method0", "Tests.A.Method1", "Tests.Z.Method0"], counts["FailedTests"])
        self.assertNotIn(MARKER, json.dumps(counts))

    def test_closed_baseline_rejects_upstream_harness_schema_cases(self):
        mutations = (
            lambda b: b.update(privatePayload="fixture"),
            lambda b: b.pop("schemaVersion"),
            lambda b: b.update(measuredAtCommit="bad"),
            lambda b: b["shards"]["core"].update(minTotal=0),
            lambda b: b["shards"]["core"].update(minTotal=1.5),
            lambda b: b["shards"]["core"].update(maxSkipped=1),
            lambda b: b["shards"]["core"].update(requiredClasses=["Tests.Class", "Tests.Class"]),
            lambda b: b["shards"]["core"].update(requiredClasses=["Tests.Class(private)"]),
            lambda b: b["shards"]["core"].update(exampleMinTests=-1),
            lambda b: b["shards"]["core"].pop("requiredClasses"),
        )
        write_trx(self.results)
        for mutate in mutations:
            broken = deepcopy(self.document)
            mutate(broken)
            baseline_contract.write_json(self.baseline, broken)
            self.assertEqual("failed", self.check()[1]["outcome"])


if __name__ == "__main__":
    unittest.main()
