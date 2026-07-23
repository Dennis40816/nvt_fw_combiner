"""Behavioral tests for the read-only version-branch review handoff collector."""

from __future__ import annotations

import importlib.util
import json
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "collect_review_handoff.py"
SPEC = importlib.util.spec_from_file_location("collect_review_handoff", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class ReviewHandoffCollectorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="review-handoff-")
        self.root = Path(self.temporary.name)
        self.repo = self.root / "repository"
        self.repo.mkdir()
        self.git("init")
        self.git("checkout", "-b", "main")
        self.git("config", "user.name", "Test Author")
        self.git("config", "user.email", "test@example.invalid")
        (self.repo / "baseline.txt").write_text("baseline\n", encoding="utf-8")
        self.git("add", "--", "baseline.txt")
        self.git("commit", "-m", "baseline")
        self.baseline_sha = self.git("rev-parse", "HEAD")
        self.git("tag", "-a", "v0.9.14", "-m", "baseline tag")
        self.git("checkout", "-b", "feature/0.9.15/review-handoff")
        (self.repo / "delivery.txt").write_text("review evidence\n", encoding="utf-8")
        self.git("add", "--", "delivery.txt")
        self.git("commit", "-m", "add review evidence")

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def git(self, *arguments: str) -> str:
        result = subprocess.run(
            ["git", *arguments],
            cwd=self.repo,
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
        return result.stdout.strip()

    def valid_arguments(self) -> dict[str, object]:
        return {
            "repo_root": self.repo,
            "baseline_tag": "v0.9.14",
            "expected_baseline_sha": self.baseline_sha,
            "verification": ["architecture=pass", "verify-all=pass"],
            "ci_state": "not-collected",
            "ci_url": None,
            "impact": ["NT51919/NT51929/NT51932 AB Merge delivery automation"],
            "unchanged_boundary": ["NT51950/NT51951 remain closed candidates"],
            "gates": [
                "firmware-owner=open",
                "golden=open",
                "packaging=not-collected",
                "release-owner=open",
                "codex-review=open",
            ],
        }

    def test_collects_annotated_baseline_lineage_and_complete_inventory(self) -> None:
        report = MODULE.collect_handoff(**self.valid_arguments())

        self.assertEqual("1.0", report["schemaVersion"])
        self.assertEqual("v0.9.14", report["baseline"]["tag"])
        self.assertEqual(self.baseline_sha, report["baseline"]["peeledCommitSha"])
        self.assertEqual("feature/0.9.15/review-handoff", report["head"]["branch"])
        self.assertEqual("clean", report["worktree"]["state"])
        self.assertEqual(
            ["delivery.txt"], [item["path"] for item in report["changes"]["files"]]
        )
        self.assertEqual("not-collected", report["ci"]["state"])
        self.assertEqual("pass", report["verification"]["verify-all"])
        self.assertEqual("open", report["residualGates"]["golden"])
        self.assertEqual(
            ["build", "merge", "publish", "push", "tag"], report["collector"]["doesNot"]
        )

    def test_fails_closed_for_dirty_worktree_and_incomplete_gate_record(self) -> None:
        (self.repo / "dirty.txt").write_text("untracked\n", encoding="utf-8")
        with self.assertRaisesRegex(MODULE.ReviewHandoffError, "worktree is dirty"):
            MODULE.collect_handoff(**self.valid_arguments())
        (self.repo / "dirty.txt").unlink()

        arguments = self.valid_arguments()
        arguments["gates"] = ["golden=open"]
        with self.assertRaisesRegex(MODULE.ReviewHandoffError, "must identify"):
            MODULE.collect_handoff(**arguments)

    def test_rejects_lightweight_or_mismatched_baseline_tag(self) -> None:
        self.git("tag", "v0.9.13", self.baseline_sha)
        arguments = self.valid_arguments()
        arguments["baseline_tag"] = "v0.9.13"
        with self.assertRaisesRegex(MODULE.ReviewHandoffError, "must be annotated"):
            MODULE.collect_handoff(**arguments)

        arguments = self.valid_arguments()
        arguments["expected_baseline_sha"] = "0" * 40
        with self.assertRaisesRegex(MODULE.ReviewHandoffError, "does not peel"):
            MODULE.collect_handoff(**arguments)

    def test_rejects_main_branch_before_collecting_review_evidence(self) -> None:
        self.git("checkout", "main")

        with self.assertRaisesRegex(MODULE.ReviewHandoffError, "non-main branch"):
            MODULE.collect_handoff(**self.valid_arguments())

    def test_writes_only_outside_the_clean_worktree(self) -> None:
        report = MODULE.collect_handoff(**self.valid_arguments())
        external_output = self.root / "handoff.json"

        MODULE.write_handoff(report, external_output, self.repo)

        saved = json.loads(external_output.read_text(encoding="utf-8"))
        self.assertEqual(report, saved)
        with self.assertRaisesRegex(
            MODULE.ReviewHandoffError, "outside the repository"
        ):
            MODULE.write_handoff(report, self.repo / "handoff.json", self.repo)


if __name__ == "__main__":
    unittest.main()
