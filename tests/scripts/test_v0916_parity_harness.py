"""Executable checks for parity production ports."""

import io
import subprocess
import sys
import unittest
from pathlib import Path

from tests.scripts.v0916_parity_test_support import (
    PRODUCTION_AVAILABLE,
    RecordingGithubReader,
    RecordingProtectedApprovalReader,
    ROOT,
)


class V0916ParityHarnessTests(unittest.TestCase):
    def test_exact_workflow_script_entrypoint_imports_from_repository_root(self) -> None:
        result = subprocess.run(
            [
                sys.executable,
                "./scripts/v0916_parity_certification.py",
                "--help",
            ],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("validate-owner-material", result.stdout)
        self.assertNotIn("record-protected-approval", result.stdout)

    def test_package_reader_constructor_and_workflow_content_api_execute(self) -> None:
        reader = RecordingGithubReader(
            {"path": ".github/workflows/release.yml", "sha": "a" * 40},
            {"id": 10, "head_sha": "b" * 40},
            {"id": 20, "digest": "sha256:" + "c" * 64},
            b"archive",
        )
        self.assertEqual(
            "a" * 40,
            reader.get_workflow_content("owner/repo", ".github/workflows/release.yml", "b" * 40)["sha"],
        )
        self.assertEqual(10, reader.get_workflow_run("owner/repo", 10)["id"])
        self.assertEqual(20, reader.get_artifact("owner/repo", 20)["id"])
        self.assertIsInstance(reader.download_artifact("owner/repo", 20), io.BytesIO)

    def test_protected_reader_constructor_and_all_real_api_ports_execute(self) -> None:
        reader = RecordingProtectedApprovalReader(
            {"path": ".github/workflows/release.yml", "sha": "a" * 40},
            {"id": 10},
            {"id": 11, "run_id": 10},
            {"id": 12, "sha": "b" * 40, "environment": "firmware-parity"},
            [{"id": 13, "state": "success"}],
            {20: ({"id": 20}, b"artifact")},
        )
        self.assertEqual("a" * 40, reader.get_workflow_content("owner/repo", "path", "b" * 40)["sha"])
        self.assertEqual(10, reader.get_workflow_run("owner/repo", 10)["id"])
        self.assertEqual(11, reader.get_workflow_job("owner/repo", 11)["id"])
        self.assertEqual(12, reader.get_deployment("owner/repo", 12)["id"])
        self.assertEqual(13, reader.get_deployment_statuses("owner/repo", 12)[0]["id"])
        self.assertEqual(20, reader.get_artifact("owner/repo", 20)["id"])
        self.assertIsInstance(reader.download_artifact("owner/repo", 20), io.BytesIO)

    def test_production_module_is_available_after_design_admission(self) -> None:
        self.assertTrue(PRODUCTION_AVAILABLE)


if __name__ == "__main__":
    unittest.main()
