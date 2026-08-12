from __future__ import annotations

import importlib.util
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = (
    REPOSITORY_ROOT
    / ".agents"
    / "skills"
    / "assess-refactor-progress"
    / "scripts"
    / "assess_progress.py"
)
SPEC = importlib.util.spec_from_file_location("assess_progress", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class AssessRefactorProgressTests(unittest.TestCase):
    def test_plan_groups_are_complete_and_weighted_once(self) -> None:
        tickets = MODULE.parse_plan(
            REPOSITORY_ROOT / "docs" / "governance" / "0.10.x-ticket-dependency-plan.md"
        )

        self.assertEqual(57, len(tickets))
        self.assertEqual(
            {
                "baseline": 3,
                "headlessCanonicalFoundation": 29,
                "deferredUi": 7,
                "deletion": 3,
                "coreConvergence": 5,
                "integration": 2,
            },
            {
                group: sum(ticket.group == group for ticket in tickets)
                for group in MODULE.GROUP_WEIGHTS
            },
        )
        self.assertEqual(
            8, sum(ticket.group == "preloadLifecycle" for ticket in tickets)
        )
        self.assertEqual(100.0, sum(MODULE.GROUP_WEIGHTS.values()))

    def test_preload_inventory_contributes_to_completion_and_frontier(self) -> None:
        plan_path = (
            REPOSITORY_ROOT / "docs" / "governance" / "0.10.x-ticket-dependency-plan.md"
        )
        tickets = MODULE.parse_plan(plan_path)
        issues = [
            self._issue(
                ticket.number,
                completed=ticket.group != "preloadLifecycle",
                ready=ticket.group == "preloadLifecycle",
            )
            for ticket in tickets
        ]
        snapshot = MODULE.build_snapshot(
            root=REPOSITORY_ROOT,
            plan_path=plan_path,
            repository="owner/repo",
            tickets=tickets,
            issues=issues,
            state_source="github-live",
            queried_at=datetime(2026, 8, 13, tzinfo=timezone.utc),
        )

        self.assertEqual(
            {"completed": 49, "total": 57, "percent": 86.0},
            snapshot["metrics"]["ticketCompletion"],
        )
        self.assertEqual(
            {"completed": 0, "total": 8, "percent": 0.0},
            snapshot["metrics"]["unifiedPreloadLifecycle"],
        )
        self.assertEqual([373], [row["number"] for row in snapshot["frontier"]])

    def test_snapshot_separates_ticket_foundation_and_weighted_metrics(self) -> None:
        plan = """\
| Depth | Wave | Issue | Approved outcome | Blocked by |
| ---: | --- | ---: | --- | --- |
| 0 | Baseline | #1 | baseline | — |
| 1 | Canonical pilot | #2 | foundation | #1 |
| 2 | Deferred UI | #3 | ui | #2 |
| 3 | Runtime deletion | #4 | deletion | #3 |
| 4 | Core convergence | #5 | core | #4 |
| 5 | Integration | #6 | integration | #5 |
"""
        issues = [
            self._issue(1, completed=True),
            self._issue(2, completed=True),
            self._issue(3, ready=True),
            self._issue(4),
            self._issue(5),
            self._issue(6),
        ]
        with tempfile.TemporaryDirectory() as directory:
            plan_path = Path(directory) / "plan.md"
            plan_path.write_text(plan, encoding="utf-8")
            tickets = MODULE.parse_plan(plan_path)
            snapshot = MODULE.build_snapshot(
                root=REPOSITORY_ROOT,
                plan_path=plan_path,
                repository="owner/repo",
                tickets=tickets,
                issues=issues,
                state_source="github-live",
                queried_at=datetime(2026, 7, 29, tzinfo=timezone.utc),
            )

        self.assertEqual(
            {"completed": 2, "total": 6, "percent": 33.3},
            snapshot["metrics"]["ticketCompletion"],
        )
        self.assertEqual(
            {"completed": 1, "total": 1, "percent": 100.0},
            snapshot["metrics"]["headlessCanonicalFoundation"],
        )
        self.assertEqual(55.0, snapshot["metrics"]["weightedTotal"]["percent"])
        self.assertEqual([3], [row["number"] for row in snapshot["frontier"]])
        self.assertEqual("high", snapshot["confidence"])

    def test_missing_planned_issue_lowers_confidence(self) -> None:
        plan = """\
| Depth | Wave | Issue | Approved outcome | Blocked by |
| ---: | --- | ---: | --- | --- |
| 0 | Baseline | #1 | baseline | — |
"""
        with tempfile.TemporaryDirectory() as directory:
            plan_path = Path(directory) / "plan.md"
            plan_path.write_text(plan, encoding="utf-8")
            tickets = MODULE.parse_plan(plan_path)
            snapshot = MODULE.build_snapshot(
                root=REPOSITORY_ROOT,
                plan_path=plan_path,
                repository="owner/repo",
                tickets=tickets,
                issues=[],
                state_source="github-live",
                queried_at=datetime(2026, 7, 29, tzinfo=timezone.utc),
            )

        self.assertEqual("low", snapshot["confidence"])
        self.assertIn("#1", snapshot["dataIssues"][0])

    def test_malformed_row_cannot_silently_reduce_denominator(self) -> None:
        plan = """\
| Depth | Wave | Issue | Approved outcome | Blocked by |
| ---: | --- | ---: | --- | --- |
| 0 | Baseline | #1 | baseline | — | unexpected |
| 1 | Canonical pilot | #2 | foundation | — |
"""

        with self.assertRaisesRegex(ValueError, "Malformed.*line 3"):
            self._parse_temporary_plan(plan)

    def test_invalid_depth_or_issue_fails_plan_integrity(self) -> None:
        malformed_rows = (
            "| depth | Baseline | #1 | baseline | — |",
            "| 0 | Baseline | issue-1 | baseline | — |",
        )
        for row in malformed_rows:
            with self.subTest(row=row):
                plan = f"""\
| Depth | Wave | Issue | Approved outcome | Blocked by |
| ---: | --- | ---: | --- | --- |
{row}
"""
                with self.assertRaisesRegex(ValueError, "Malformed.*line 3"):
                    self._parse_temporary_plan(plan)

    def test_unknown_blocker_fails_plan_integrity(self) -> None:
        plan = """\
| Depth | Wave | Issue | Approved outcome | Blocked by |
| ---: | --- | ---: | --- | --- |
| 0 | Baseline | #1 | baseline | #999 |
"""

        with self.assertRaisesRegex(ValueError, r"#1.*#999"):
            self._parse_temporary_plan(plan)

    def test_blank_line_cannot_silently_truncate_ticket_table(self) -> None:
        plan = """\
| Depth | Wave | Issue | Approved outcome | Blocked by |
| ---: | --- | ---: | --- | --- |
| 0 | Baseline | #1 | first | — |

| 1 | Canonical pilot | #2 | silently omitted | — |
"""

        with self.assertRaisesRegex(ValueError, "Blank line inside.*line 4"):
            self._parse_temporary_plan(plan)

    @staticmethod
    def _parse_temporary_plan(plan: str) -> list[object]:
        with tempfile.TemporaryDirectory() as directory:
            plan_path = Path(directory) / "plan.md"
            plan_path.write_text(plan, encoding="utf-8")
            return MODULE.parse_plan(plan_path)

    @staticmethod
    def _issue(
        number: int,
        *,
        completed: bool = False,
        ready: bool = False,
    ) -> dict[str, object]:
        return {
            "number": number,
            "title": f"Issue {number}",
            "state": "CLOSED" if completed else "OPEN",
            "stateReason": "COMPLETED" if completed else "",
            "labels": [{"name": "ready-for-agent"}] if ready else [],
            "url": f"https://example.test/{number}",
        }


if __name__ == "__main__":
    unittest.main()
