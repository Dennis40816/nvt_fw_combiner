"""Behavioral tests for the encrypted debug archive workflow."""

from __future__ import annotations

import re
import unittest
from pathlib import Path

import yaml


ROOT = Path(__file__).resolve().parents[2]
DEBUG_WORKFLOW = ROOT / ".github" / "workflows" / "debug-archive.yml"
PINNED_ACTION = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40}$")


class DebugArchiveWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = DEBUG_WORKFLOW.read_text(encoding="utf-8")
        cls.workflow = yaml.safe_load(cls.text)
        cls.pack_steps = {
            step["name"]: step for step in cls.workflow["jobs"]["pack"]["steps"]
        }

    def test_triggers_are_the_main_ci_completion_and_a_manual_rehearsal(self) -> None:
        events = self.workflow.get("on", self.workflow.get(True))
        self.assertEqual(
            {"workflow_run": {"workflows": ["ci"], "types": ["completed"], "branches": ["main"]},
             "workflow_dispatch": None},
            events,
        )
        self.assertNotIn("pull_request", self.text.replace("pull-requests", ""))
        self.assertEqual({"contents": "read"}, self.workflow["permissions"])
        gate = self.workflow["jobs"]["gate"]
        for clause in ("github.ref == 'refs/heads/main'",
                       "github.event.workflow_run.conclusion == 'success'",
                       "github.event.workflow_run.event == 'push'",
                       "github.event.workflow_run.head_branch == 'main'",
                       "github.event.workflow_run.head_sha == github.sha",
                       "github.sha == github.workflow_sha"):
            self.assertIn(clause, gate["if"])

    def test_the_packing_job_is_gated_by_the_environment_and_has_read_only_rights(self) -> None:
        self.assertEqual({"gate", "pack"}, set(self.workflow["jobs"]))
        pack = self.workflow["jobs"]["pack"]
        self.assertEqual("debug-archive", pack["environment"])
        self.assertEqual({"contents": "read"}, pack["permissions"])
        self.assertEqual(["gate"], pack["needs"])
        self.assertEqual("${{ needs.gate.outputs.eligible == 'true' }}", pack["if"])
        self.assertNotIn("environment", self.workflow["jobs"]["gate"])
        self.assertNotIn("contents: write", self.text)
        self.assertEqual(1, self.text.count("environment:"))

    def test_every_action_is_pinned_and_checkouts_drop_credentials(self) -> None:
        for job in self.workflow["jobs"].values():
            for step in job["steps"]:
                if "uses" in step:
                    self.assertRegex(step["uses"], PINNED_ACTION)
                if str(step.get("uses", "")).startswith("actions/checkout@"):
                    self.assertIs(False, step["with"]["persist-credentials"])
                    self.assertEqual("${{ github.workflow_sha }}", step["with"]["ref"])

    def test_secrets_reach_only_the_packing_step_through_its_environment(self) -> None:
        for job in self.workflow["jobs"].values():
            for step in job["steps"]:
                self.assertNotIn("secrets.", step.get("run", ""))
        pack = self.pack_steps["Pack and self-check the encrypted archive"]
        self.assertEqual(
            {"NFC_DEBUG_ARCHIVE_PASSWORD", "TESTDATA_DEPLOY_KEY", "PRIVATE_ASSETS_DEPLOY_KEY"},
            {key for key, value in pack["env"].items() if "secrets." in str(value)},
        )
        self.assertEqual(3, self.text.count("secrets."))
        for other in ("ci.yml", "release.yml", "release-rehearsal.yml"):
            workflow = (ROOT / ".github" / "workflows" / other).read_text(encoding="utf-8")
            self.assertNotIn("NFC_DEBUG_ARCHIVE_PASSWORD", workflow)
            self.assertNotIn("PRIVATE_ASSETS_DEPLOY_KEY", workflow)

    def test_the_archive_is_encrypted_checked_and_only_counts_reach_the_log(self) -> None:
        run = self.pack_steps["Pack and self-check the encrypted archive"]["run"]
        for required in ("-t7z", "-mx=9", "-mhe=on", "-bso0", "-bse0", "-bsp0",
                         "'t'", "'x'", "'l'", "golden_pin.py verify",
                         "The debug archive header is not encrypted.",
                         "if ($failed) { exit 1 }"):
            self.assertIn(required, run)
        self.assertGreaterEqual(run.count("$password.Length -lt 24"), 1)
        # Deliberate failures use the safe prefix; any other error text is withheld.
        self.assertEqual(0, len(re.findall(r"throw '(?!debug-archive: )", run)))
        self.assertIn("details are withheld", run)
        for forbidden in ("Write-Output", "Write-Verbose", "Format-List", "Out-Host", "Tee-Object"):
            self.assertNotIn(forbidden, run)
        self.assertNotIn("Write-Host \"$", run.replace('Write-Host "::error::$text"', ""))

    def test_the_artifact_is_the_7z_alone_with_a_release_or_rehearsal_name(self) -> None:
        pack = self.pack_steps["Pack and self-check the encrypted archive"]["run"]
        self.assertIn('"debug-archive-rehearsal-$($env:GITHUB_RUN_ID)"', pack)
        self.assertIn('"debug-archive-v$($env:NFC_VERSION)-$($env:NFC_SOURCE_SHA)"', pack)
        upload = self.pack_steps["Upload encrypted debug archive"]
        self.assertEqual("${{ steps.pack.outputs.artifact-name }}", upload["with"]["name"])
        self.assertEqual("${{ steps.pack.outputs.archive-path }}", upload["with"]["path"])
        self.assertEqual("error", upload["with"]["if-no-files-found"])
        self.assertEqual(3, upload["with"]["retention-days"])
        self.assertTrue(upload["uses"].startswith("actions/upload-artifact@"))
        self.assertEqual("pack", self.pack_steps["Pack and self-check the encrypted archive"]["id"])

    def test_a_rehearsal_runs_at_any_version_and_a_release_needs_1_2_2(self) -> None:
        gate = self.workflow["jobs"]["gate"]["steps"][-1]["run"]
        self.assertLess(gate.index("workflow_dispatch"), gate.index("[version]'1.2.2'"))
        self.assertIn("rehearsal=true", gate)
        self.assertIn("collect-release-request", gate)
        self.assertIn("--automatic", gate)

    def test_a_missing_pin_file_is_reported_without_a_path_or_name(self) -> None:
        run = self.pack_steps["Pack and self-check the encrypted archive"]["run"]
        self.assertLess(
            run.index("test-baseline.json -PathType Leaf"), run.index("ConvertFrom-Json).pin")
        )
        self.assertIn("debug-archive: The test data pin file is missing in this checkout.", run)


if __name__ == "__main__":
    unittest.main()
