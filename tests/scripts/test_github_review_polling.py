from __future__ import annotations

import importlib.util
import io
import unittest
from contextlib import redirect_stdout
from datetime import datetime, timezone
from pathlib import Path
from unittest.mock import patch


MODULE_PATH = (
    Path(__file__).resolve().parents[2]
    / ".agents"
    / "skills"
    / "github-review-polling"
    / "scripts"
    / "poll_github_review.py"
)
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

    def test_rejects_review_and_inline_comment_without_commit_identity(self) -> None:
        snapshot = {
            "reviews": [
                {
                    "user": {"login": "chatgpt-codex-connector[bot]"},
                    "submitted_at": "2026-07-21T04:01:00Z",
                    "body": "Unattributed review",
                }
            ],
            "inline_comments": [
                {
                    "user": {"login": "chatgpt-codex-connector[bot]"},
                    "created_at": "2026-07-21T04:02:00Z",
                    "body": "Unattributed inline comment",
                }
            ],
            "issue_comments": [],
            "reactions": [],
        }

        responses, _ = POLL.collect_responses(
            snapshot, self.requested, self.head, "chatgpt-codex-connector"
        )

        self.assertEqual(responses, [])

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

    def test_treats_error_issue_comment_as_response_for_inspection(self) -> None:
        snapshot = {
            "reviews": [],
            "inline_comments": [],
            "issue_comments": [
                {
                    "user": {"login": "chatgpt-codex-connector[bot]"},
                    "created_at": "2026-07-21T04:01:00Z",
                    "html_url": "https://example.invalid/error-response",
                    "body": "Unknown error",
                }
            ],
            "reactions": [],
        }

        responses, _ = POLL.collect_responses(
            snapshot, self.requested, self.head, "chatgpt-codex-connector"
        )

        self.assertEqual(len(responses), 1)
        self.assertEqual(responses[0]["kind"], "issue_comment")
        self.assertEqual(responses[0]["body"], "Unknown error")

    def test_json_output_is_safe_on_windows_cp950_console(self) -> None:
        output = POLL.serialize_payload({"body": "Codex review information ℹ"})

        output.encode("cp950")
        self.assertIn("\\u2139", output)

    def test_main_reports_head_change_with_distinct_exit_code(self) -> None:
        client = _FakeClient("b" * 40)

        exit_code, payload = self._run_main(client)

        self.assertEqual(exit_code, 2)
        self.assertIn('"status": "head_changed"', payload)

    def test_main_accepts_an_exact_head_review(self) -> None:
        client = _FakeClient(
            self.head,
            reviews=[
                {
                    "user": {"login": "chatgpt-codex-connector[bot]"},
                    "submitted_at": "2026-07-21T04:01:00Z",
                    "commit_id": self.head,
                    "body": "No major issues found",
                }
            ],
        )

        exit_code, payload = self._run_main(client)

        self.assertEqual(exit_code, 0)
        self.assertIn('"status": "response"', payload)

    def test_request_comment_must_belong_to_selected_pull_request(self) -> None:
        client = _FakeClient(
            self.head,
            request_comment={
                "issue_url": "https://api.github.test/repos/owner/repository/issues/2",
                "body": "@codex review",
                "created_at": "2026-07-21T04:00:00Z",
            },
        )

        with self.assertRaisesRegex(ValueError, "does not belong"):
            POLL.request_time(client, "owner/repository", 1, 123, None)

    def test_main_times_out_after_an_exact_head_single_poll(self) -> None:
        client = _FakeClient(self.head)

        exit_code, payload = self._run_main(client)

        self.assertEqual(exit_code, 3)
        self.assertIn('"status": "timeout"', payload)

    def test_main_maps_api_failure_to_distinct_exit_code(self) -> None:
        client = _FakeClient(self.head, error=POLL.PollingError("API unavailable"))

        exit_code, payload = self._run_main(client)

        self.assertEqual(exit_code, 4)
        self.assertIn('"status": "api_error"', payload)

    def test_main_rejects_invalid_repository_before_api_access(self) -> None:
        output = io.StringIO()

        with redirect_stdout(output):
            exit_code = POLL.main(
                [
                    "--repo",
                    "invalid",
                    "--pr",
                    "1",
                    "--expected-head",
                    self.head,
                    "--requested-after",
                    "2026-07-21T04:00:00Z",
                    "--once",
                ]
            )

        self.assertEqual(exit_code, 5)
        self.assertIn('"status": "invalid_input"', output.getvalue())

    def test_main_maps_incomplete_or_conflicting_identity_to_invalid_input(
        self,
    ) -> None:
        base_args = [
            "--repo",
            "owner/repository",
            "--pr",
            "1",
            "--expected-head",
            self.head,
            "--once",
        ]
        identity_cases = {
            "missing": [],
            "conflicting": [
                "--request-comment-id",
                "123",
                "--requested-after",
                "2026-07-21T04:00:00Z",
            ],
            "zero-comment-id": ["--request-comment-id", "0"],
        }

        for name, identity_args in identity_cases.items():
            with self.subTest(name=name):
                output = io.StringIO()
                with redirect_stdout(output):
                    exit_code = POLL.main([*base_args, *identity_args])

                self.assertEqual(exit_code, 5)
                self.assertIn('"status": "invalid_input"', output.getvalue())

    def test_main_maps_parser_error_to_invalid_input(self) -> None:
        output = io.StringIO()

        with redirect_stdout(output):
            exit_code = POLL.main(
                [
                    "--repo",
                    "owner/repository",
                    "--pr",
                    "not-an-integer",
                    "--expected-head",
                    self.head,
                    "--requested-after",
                    "2026-07-21T04:00:00Z",
                ]
            )

        self.assertEqual(exit_code, 5)
        self.assertIn('"status": "invalid_input"', output.getvalue())

    def _run_main(self, client: "_FakeClient") -> tuple[int, str]:
        output = io.StringIO()
        with (
            patch.object(POLL, "GitHubClient", return_value=client),
            redirect_stdout(output),
        ):
            exit_code = POLL.main(
                [
                    "--repo",
                    "owner/repository",
                    "--pr",
                    "1",
                    "--expected-head",
                    self.head,
                    "--requested-after",
                    "2026-07-21T04:00:00Z",
                    "--once",
                ]
            )
        return exit_code, output.getvalue()


class _FakeClient:
    def __init__(
        self,
        head: str,
        error: Exception | None = None,
        reviews: list[dict[str, object]] | None = None,
        request_comment: dict[str, object] | None = None,
    ) -> None:
        self.head = head
        self.error = error
        self.reviews = reviews or []
        self.request_comment = request_comment

    def get(self, path: str, *, paginate: bool = False) -> object:
        del paginate
        if self.error is not None:
            raise self.error
        if path.endswith("/pulls/1"):
            return {"head": {"sha": self.head}}
        if "/pulls/1/reviews?" in path:
            return self.reviews
        if path.endswith("/issues/comments/123") and self.request_comment is not None:
            return self.request_comment
        return []


if __name__ == "__main__":
    unittest.main()
