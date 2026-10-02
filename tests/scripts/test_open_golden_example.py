"""Canonical examples keep their launch routes and refuse evidence-only aliases."""

from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "open_golden_example.py"
SPEC = importlib.util.spec_from_file_location("open_golden_example", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
opener = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(opener)


class OpenGoldenExampleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.catalog = opener.cases()

    def run_cli(self, *arguments: str, script: Path = SCRIPT) -> subprocess.CompletedProcess:
        return subprocess.run(
            [sys.executable, str(script), *arguments],
            cwd=ROOT, capture_output=True, text=True, check=False,
        )

    def assert_alias_refused(self, result: subprocess.CompletedProcess, alias: dict) -> None:
        source_id = alias["alias"]["sourceCaseId"]
        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertEqual(result.stdout, "")
        self.assertEqual(
            result.stderr.strip(),
            f"error: {alias['caseId']}: alias of {source_id}; "
            f"open source case '{source_id}' instead",
        )
        self.assertNotIn("Traceback", result.stderr)

    def test_direct_case_builds_standard_merge_launch_arguments(self) -> None:
        case = self.catalog["nt51926-gen-flash"]
        self.assertTrue(case["directGolden"])
        self.assertEqual(case["testDisposition"]["kind"], "direct-full-output")
        paths = {item["artifactId"]: str((opener.GOLDEN / item["path"]).resolve())
                 for item in case["artifacts"]}
        self.assertEqual(opener.launch_args(case), [
            "--workflow", "standard-merge", "--ic", "NT51926", "--ic-num", "single",
            "--dp", paths["dp-input"], "--tp", paths["tp-input"],
        ])

    def test_input_only_case_builds_declared_ctrlram_launch_arguments(self) -> None:
        case = self.catalog["nt51927-2chip-self-20260705"]
        self.assertEqual(case["testDisposition"]["kind"], "input-only-evidence")
        paths = {item["artifactId"]: str((opener.GOLDEN / item["path"]).resolve())
                 for item in case["artifacts"]}
        expected = [
            "--workflow", "ctrlram-replace", "--ic", "NT51927", "--ic-num", "2",
            "--base", paths["reference-base"],
        ]
        for slot in (
            "replace-ctrlram-nf", "replace-ctrlram-normal-master", "replace-ctrlram-mp-master",
            "replace-ctrlram-vn", "replace-ctrlram-normal-slave-r", "replace-ctrlram-mp-slave-r",
        ):
            expected += ["--ctrlram", f"{slot}={paths[slot]}"]
        self.assertEqual(opener.launch_args(case), expected)

    def test_metadata_only_case_keeps_its_missing_input_error(self) -> None:
        case_id = "nt51929-certified-metadata-inputs-20260904"
        self.assertEqual(self.catalog[case_id]["testDisposition"]["kind"], "input-only-evidence")
        result = self.run_cli(case_id, "--dry-run")
        self.assertEqual(result.returncode, 1)
        self.assertEqual(result.stdout, "")
        self.assertEqual(result.stderr.strip(), f"error: {case_id}: expected one dp-input artifact")

    def test_committed_aliases_are_refused_before_artifact_access(self) -> None:
        aliases = [case for case in self.catalog.values() if "alias" in case]
        self.assertTrue(aliases)
        for case in aliases:
            with self.subTest(case_id=case["caseId"]):
                self.assertNotIn("artifacts", case)
                self.assert_alias_refused(self.run_cli(case["caseId"], "--dry-run"), case)
                with mock.patch.object(opener, "artifact", side_effect=AssertionError("artifact accessed")):
                    with self.assertRaises(opener.AliasCaseError):
                        opener.launch_args(case)

    def test_alias_with_missing_source_is_refused_without_resolving_it(self) -> None:
        alias = {
            **self.catalog["nt51951-ab-boe-d82t80-workflow-alias"],
            "caseId": "synthetic-alias",
            "alias": {"sourceCaseId": "missing-source-case"},
        }
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            script = root / "scripts" / SCRIPT.name
            script.parent.mkdir()
            script.write_text(SCRIPT.read_text(encoding="utf-8"), encoding="utf-8", newline="\n")
            manifest = root / "testdata" / "golden" / "canonical" / "synthetic" / "provenance" / "case.json"
            manifest.parent.mkdir(parents=True)
            manifest.write_text(json.dumps(alias), encoding="utf-8", newline="\n")
            self.assert_alias_refused(self.run_cli(alias["caseId"], "--dry-run", script=script), alias)

    def test_listing_keeps_aliases_and_their_declared_disposition(self) -> None:
        result = self.run_cli("--list")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stderr, "")
        for case in self.catalog.values():
            if "alias" in case:
                self.assertIn(
                    f"{case['caseId']}\t{case['ic']}\t{case['workflow']}\t"
                    f"{case['topology']}\t{case['testDisposition']['kind']}",
                    result.stdout.splitlines(),
                )


if __name__ == "__main__":
    unittest.main()
