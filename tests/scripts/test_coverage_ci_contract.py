"""Static regression checks for coverage-policy CI prerequisites."""

from __future__ import annotations

import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CI_WORKFLOW = ROOT / ".github" / "workflows" / "ci.yml"


class CoverageCiContractTests(unittest.TestCase):
    def test_dotnet_job_fetches_the_fixed_coverage_baseline_revision(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")
        dotnet_job = workflow[workflow.index("  dotnet:") :]

        self.assertIn("fetch-depth: 0", dotnet_job)

    def test_ci_retains_short_lived_real_coverage_evidence(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")

        self.assertIn("name: python-coverage", workflow)
        self.assertIn("path: artifacts/coverage/python/", workflow)
        self.assertIn("name: dotnet-coverage", workflow)
        self.assertIn("path: artifacts/coverage/dotnet/", workflow)
        self.assertEqual(3, workflow.count("retention-days: 3"))


if __name__ == "__main__":
    unittest.main()
