from __future__ import annotations

import importlib.util
import unittest
from datetime import datetime, timezone
from pathlib import Path


MODULE_PATH = Path(__file__).with_name("poll_github_review.py")
SPEC = importlib.util.spec_from_file_location("poll_github_review", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
POLL = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(POLL)


class PollGithubReviewTests(unittest.TestCase):
    def setUp(self) -> None:
        self.requested = datetime(2026, 7, 21, 4, 0, tzinfo=timezone.utc)
        self.head = "a" * 40

    def test_normalizes_github_bot_suffix(self) -> None:
        self.assertEqual(
            POLL.normalize_login("chatgpt-codex-connector[bot]"),
            "chatgpt-codex-connector",
        )

    def test_eyes_is_pending_but_plus_one_is_response(self) -> None:
        snapshot = {
            "reviews": [],
            "inline_comments": [],
            "issue_comments": [],
            "reactions": [
                {
                    "user": {"login": "chatgpt-codex-connector[bot]"},
                    "created_at": "2026-07-21T04:01:00Z",
                    "content": "eyes",
                },
                {
                    "user": {"login": "chatgpt-codex-connector[bot]"},
                    "created_at": "2026-07-21T04:02:00Z",
                    "content": "+1",
                },
            ],
        }

        responses, pending = POLL.collect_responses(
            snapshot, self.requested, self.head, "chatgpt-codex-connector"
        )

        self.assertEqual([item["state"] for item in responses], ["+1"])
        self.assertEqual([item["state"] for item in pending], ["eyes"])

    def test_ignores_stale_review_and_accepts_current_head_inline_comment(self) -> None:
        snapshot = {
            "reviews": [
                {
                    "user": {"login": "chatgpt-codex-connector[bot]"},
                    "submitted_at": "2026-07-21T04:01:00Z",
                    "commit_id": "b" * 40,
                }
            ],
            "inline_comments": [
                {
                    "user": {"login": "chatgpt-codex-connector"},
                    "created_at": "2026-07-21T04:02:00Z",
                    "commit_id": self.head,
                    "html_url": "https://example.invalid/current",
                    "body": "Actionable current-head finding",
                }
            ],
            "issue_comments": [],
            "reactions": [],
        }

        responses, _ = POLL.collect_responses(
            snapshot, self.requested, self.head, "chatgpt-codex-connector"
        )

        self.assertEqual(len(responses), 1)
        self.assertEqual(responses[0]["kind"], "inline_comment")
        self.assertEqual(responses[0]["commit"], self.head)

    def test_requires_response_strictly_after_request(self) -> None:
        snapshot = {
            "reviews": [],
            "inline_comments": [],
            "issue_comments": [
                {
                    "user": {"login": "chatgpt-codex-connector[bot]"},
                    "created_at": "2026-07-21T04:00:00Z",
                    "body": "Codex Review: Same timestamp is not a new response",
                }
            ],
            "reactions": [],
        }

        responses, _ = POLL.collect_responses(
            snapshot, self.requested, self.head, "chatgpt-codex-connector"
        )

        self.assertEqual(responses, [])

    def test_ignores_unrelated_bot_issue_comment(self) -> None:
        snapshot = {
            "reviews": [],
            "inline_comments": [],
            "issue_comments": [
                {
                    "user": {"login": "chatgpt-codex-connector[bot]"},
                    "created_at": "2026-07-21T04:01:00Z",
                    "body": "I answered a question unrelated to the requested review.",
                }
            ],
            "reactions": [],
        }

        responses, _ = POLL.collect_responses(
            snapshot, self.requested, self.head, "chatgpt-codex-connector"
        )

        self.assertEqual(responses, [])

    def test_json_output_is_safe_on_windows_cp950_console(self) -> None:
        output = POLL.serialize_payload({"body": "Codex review information ℹ"})

        output.encode("cp950")
        self.assertIn("\\u2139", output)


if __name__ == "__main__":
    unittest.main()
