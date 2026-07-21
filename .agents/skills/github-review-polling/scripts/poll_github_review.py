#!/usr/bin/env python3
"""Poll direct GitHub REST resources for an exact-head Codex review response."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import time
from datetime import datetime, timezone
from typing import Any


DEFAULT_BOT_LOGIN = "chatgpt-codex-connector"
MAX_SLEEP_SECONDS = 60
REPOSITORY_PATTERN = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
REQUEST_PATTERN = re.compile(r"(?im)^\s*@codex\s+review\b")


class PollingError(RuntimeError):
    """Raised when GitHub state cannot be read safely."""


class PollArgumentParser(argparse.ArgumentParser):
    """Map command-line errors into the poller's documented JSON contract."""

    def error(self, message: str) -> None:
        raise ValueError(message)


def parse_utc(value: str) -> datetime:
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as error:
        raise ValueError(f"invalid ISO-8601 timestamp: {value}") from error
    if parsed.tzinfo is None:
        raise ValueError("timestamp must include a UTC offset")
    return parsed.astimezone(timezone.utc)


def normalize_login(value: str | None) -> str:
    login = (value or "").strip().lower()
    return login[:-5] if login.endswith("[bot]") else login


def is_bot(item: dict[str, Any], expected_login: str) -> bool:
    user = item.get("user") or {}
    return normalize_login(user.get("login")) == normalize_login(expected_login)


def item_time(item: dict[str, Any], *fields: str) -> datetime | None:
    for field in fields:
        value = item.get(field)
        if value:
            try:
                return parse_utc(str(value))
            except ValueError:
                return None
    return None


def short_body(item: dict[str, Any]) -> str:
    return " ".join(str(item.get("body") or "").split())[:300]


def response_record(
    kind: str, item: dict[str, Any], timestamp: datetime
) -> dict[str, Any]:
    return {
        "kind": kind,
        "timestamp": timestamp.isoformat().replace("+00:00", "Z"),
        "url": item.get("html_url") or item.get("url"),
        "state": item.get("state") or item.get("content"),
        "commit": item.get("commit_id") or item.get("original_commit_id"),
        "body": short_body(item),
    }


def collect_responses(
    snapshot: dict[str, list[dict[str, Any]]],
    requested_after: datetime,
    expected_head: str,
    bot_login: str,
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    responses: list[dict[str, Any]] = []
    pending_reactions: list[dict[str, Any]] = []

    for item in snapshot.get("reviews", []):
        timestamp = item_time(item, "submitted_at", "created_at")
        commit = item.get("commit_id")
        if (
            timestamp is not None
            and timestamp > requested_after
            and is_bot(item, bot_login)
            and commit == expected_head
        ):
            responses.append(response_record("review", item, timestamp))

    for item in snapshot.get("inline_comments", []):
        timestamp = item_time(item, "created_at", "updated_at")
        commit = item.get("commit_id") or item.get("original_commit_id")
        if (
            timestamp is not None
            and timestamp > requested_after
            and is_bot(item, bot_login)
            and commit == expected_head
        ):
            responses.append(response_record("inline_comment", item, timestamp))

    for item in snapshot.get("issue_comments", []):
        timestamp = item_time(item, "created_at", "updated_at")
        if (
            timestamp is not None
            and timestamp > requested_after
            and is_bot(item, bot_login)
        ):
            responses.append(response_record("issue_comment", item, timestamp))

    for item in snapshot.get("reactions", []):
        timestamp = item_time(item, "created_at")
        if (
            timestamp is None
            or timestamp <= requested_after
            or not is_bot(item, bot_login)
        ):
            continue
        record = response_record("reaction", item, timestamp)
        if str(item.get("content") or "").lower() == "eyes":
            pending_reactions.append(record)
        else:
            responses.append(record)

    responses.sort(key=lambda item: (item["timestamp"], item["kind"]))
    pending_reactions.sort(key=lambda item: item["timestamp"])
    return responses, pending_reactions


class GitHubClient:
    def get(self, path: str, *, paginate: bool = False) -> Any:
        command = ["gh", "api"]
        if paginate:
            command.extend(["--paginate", "--slurp"])
        command.append(path)
        try:
            result = subprocess.run(
                command,
                check=True,
                capture_output=True,
                text=True,
                encoding="utf-8",
            )
            payload = json.loads(result.stdout)
        except FileNotFoundError as error:
            raise PollingError("GitHub CLI 'gh' is not available") from error
        except subprocess.CalledProcessError as error:
            detail = (error.stderr or error.stdout or "gh api failed").strip()
            raise PollingError(detail) from error
        except json.JSONDecodeError as error:
            raise PollingError(
                f"GitHub API returned invalid JSON for {path}"
            ) from error

        if paginate:
            if not isinstance(payload, list):
                raise PollingError(
                    f"paginated GitHub response was not a list for {path}"
                )
            if payload and all(isinstance(page, list) for page in payload):
                return [item for page in payload for item in page]
        return payload


def request_time(
    client: GitHubClient,
    repo: str,
    pr_number: int,
    comment_id: int | None,
    requested_after: str | None,
) -> datetime:
    if comment_id is not None:
        comment = client.get(f"repos/{repo}/issues/comments/{comment_id}")
        issue_url = str(comment.get("issue_url") or "")
        if not issue_url.endswith(f"/issues/{pr_number}"):
            raise ValueError(
                "request comment does not belong to the selected pull request"
            )
        if REQUEST_PATTERN.search(str(comment.get("body") or "")) is None:
            raise ValueError(
                "request comment does not contain an @codex review request"
            )
        created = comment.get("created_at")
        if not created:
            raise ValueError("request comment has no creation timestamp")
        return parse_utc(str(created))
    if requested_after:
        return parse_utc(requested_after)
    raise ValueError("provide --request-comment-id or --requested-after")


def read_snapshot(
    client: GitHubClient,
    repo: str,
    pr_number: int,
    request_comment_id: int | None,
) -> tuple[str, dict[str, list[dict[str, Any]]]]:
    pull = client.get(f"repos/{repo}/pulls/{pr_number}")
    head = str(((pull.get("head") or {}).get("sha") or ""))
    if not head:
        raise PollingError("pull request response has no head SHA")

    snapshot = {
        "reviews": client.get(
            f"repos/{repo}/pulls/{pr_number}/reviews?per_page=100", paginate=True
        ),
        "inline_comments": client.get(
            f"repos/{repo}/pulls/{pr_number}/comments?per_page=100", paginate=True
        ),
        "issue_comments": client.get(
            f"repos/{repo}/issues/{pr_number}/comments?per_page=100", paginate=True
        ),
        "reactions": [],
    }
    if request_comment_id is not None:
        snapshot["reactions"] = client.get(
            f"repos/{repo}/issues/comments/{request_comment_id}/reactions?per_page=100",
            paginate=True,
        )
    return head, snapshot


def serialize_payload(payload: dict[str, Any]) -> str:
    return json.dumps(payload, ensure_ascii=True, sort_keys=True)


def emit(payload: dict[str, Any]) -> None:
    print(serialize_payload(payload), flush=True)


def sleep_with_heartbeats(seconds: int, poll_number: int) -> None:
    remaining = seconds
    while remaining > 0:
        tick = min(remaining, MAX_SLEEP_SECONDS)
        time.sleep(tick)
        remaining -= tick
        if remaining > 0:
            emit(
                {
                    "status": "waiting",
                    "poll": poll_number,
                    "secondsRemaining": remaining,
                }
            )


def build_parser() -> argparse.ArgumentParser:
    parser = PollArgumentParser(description=__doc__)
    parser.add_argument("--repo", required=True, help="GitHub owner/name")
    parser.add_argument("--pr", required=True, type=int, help="pull request number")
    parser.add_argument(
        "--expected-head", required=True, help="exact 40-character head SHA"
    )
    parser.add_argument("--request-comment-id", type=int)
    parser.add_argument("--requested-after", help="exact ISO-8601 request time")
    parser.add_argument("--bot-login", default=DEFAULT_BOT_LOGIN)
    parser.add_argument("--interval-seconds", type=int, default=300)
    parser.add_argument("--max-polls", type=int, default=12)
    parser.add_argument("--once", action="store_true")
    return parser


def main(argv: list[str] | None = None) -> int:
    try:
        args = build_parser().parse_args(argv)
    except ValueError as error:
        emit({"status": "invalid_input", "message": str(error)})
        return 5

    identity_count = sum(
        value is not None for value in (args.request_comment_id, args.requested_after)
    )
    if (
        REPOSITORY_PATTERN.fullmatch(args.repo) is None
        or args.pr <= 0
        or len(args.expected_head) != 40
        or any(
            character not in "0123456789abcdefABCDEF"
            for character in args.expected_head
        )
        or args.interval_seconds < 1
        or args.max_polls < 1
        or identity_count != 1
        or (args.request_comment_id is not None and args.request_comment_id <= 0)
    ):
        emit(
            {
                "status": "invalid_input",
                "message": "invalid repository, PR, SHA, request identity, or poll budget",
            }
        )
        return 5

    expected_head = args.expected_head.lower()
    client = GitHubClient()
    try:
        requested_after = request_time(
            client,
            args.repo,
            args.pr,
            args.request_comment_id,
            args.requested_after,
        )
    except ValueError as error:
        emit({"status": "invalid_input", "message": str(error)})
        return 5
    except PollingError as error:
        emit({"status": "api_error", "message": str(error)})
        return 4

    max_polls = 1 if args.once else args.max_polls
    for poll_number in range(1, max_polls + 1):
        try:
            current_head, snapshot = read_snapshot(
                client, args.repo, args.pr, args.request_comment_id
            )
        except PollingError as error:
            emit({"status": "api_error", "poll": poll_number, "message": str(error)})
            return 4

        if current_head.lower() != expected_head:
            emit(
                {
                    "status": "head_changed",
                    "poll": poll_number,
                    "expectedHead": expected_head,
                    "currentHead": current_head,
                }
            )
            return 2

        responses, pending_reactions = collect_responses(
            snapshot, requested_after, expected_head, args.bot_login
        )
        if responses:
            emit(
                {
                    "status": "response",
                    "poll": poll_number,
                    "head": expected_head,
                    "requestedAfter": requested_after.isoformat().replace(
                        "+00:00", "Z"
                    ),
                    "responses": responses,
                    "pendingReactions": pending_reactions,
                }
            )
            return 0

        emit(
            {
                "status": "pending",
                "poll": poll_number,
                "head": expected_head,
                "pendingReactions": pending_reactions,
            }
        )
        if poll_number < max_polls:
            sleep_with_heartbeats(args.interval_seconds, poll_number)

    emit({"status": "timeout", "polls": max_polls, "head": expected_head})
    return 3


if __name__ == "__main__":
    sys.exit(main())
