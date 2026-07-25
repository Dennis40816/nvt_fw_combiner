"""Static regression checks for coverage-policy CI prerequisites."""

from __future__ import annotations

import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CI_WORKFLOW = ROOT / ".github" / "workflows" / "ci.yml"
VERIFIER = ROOT / "scripts" / "verify.py"


class CoverageCiContractTests(unittest.TestCase):
    def test_dotnet_job_fetches_the_fixed_coverage_baseline_revision(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")
        dotnet_job = workflow[workflow.index("  dotnet:") :]

        self.assertIn("fetch-depth: 0", dotnet_job)

    def test_structure_job_does_not_restore_or_own_evaluated_project_policy(
        self,
    ) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")
        structure_job = workflow[
            workflow.index("  structure:") : workflow.index("  python-worker:")
        ]

        self.assertNotIn("install-dotnet", structure_job)
        self.assertNotIn("dotnet restore", structure_job)
        self.assertIn("python scripts/verify.py --structure-only", structure_job)

    def test_dotnet_lane_restores_before_its_evaluated_source_check(self) -> None:
        verifier = VERIFIER.read_text(encoding="utf-8")
        dotnet_lane = verifier[
            verifier.index("def verify_dotnet(") : verifier.index(
                "def verify_windows_process_orchestration("
            )
        ]

        self.assertEqual(1, dotnet_lane.count('"--evaluated-source-ownership-only"'))
        self.assertLess(
            dotnet_lane.index('[dotnet, "restore", str(SOLUTION)]'),
            dotnet_lane.index('"--evaluated-source-ownership-only"'),
        )

    def test_ci_retains_short_lived_real_coverage_evidence(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")

        self.assertIn("name: python-coverage", workflow)
        self.assertIn("path: artifacts/coverage/python/", workflow)
        self.assertIn("name: dotnet-coverage", workflow)
        self.assertIn("path: artifacts/coverage/dotnet/", workflow)
        self.assertEqual(3, workflow.count("retention-days: 3"))


if __name__ == "__main__":
    unittest.main()
