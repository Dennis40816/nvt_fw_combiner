---
name: github-review-polling
description: Monitor an exact GitHub pull-request head for asynchronous Codex review responses. Use when a review request is pending, when five-minute polling is requested, or when aggregated gh pr view data may lag direct GitHub REST state.
---

# GitHub Review Polling

Use direct GitHub REST resources to monitor one review request without confusing
an old response, a stale head, or an `eyes` acknowledgement with a completed
review. This skill is read-only: it never comments, resolves threads, reruns CI,
merges, or changes a branch.

## Lock The Request Identity

Record all of these before polling:

```text
repository owner/name
pull request number
exact expected head SHA
@codex review request comment id or its exact UTC creation time
poll interval and maximum polls
```

Read the current head from `GET /repos/{owner}/{repo}/pulls/{pr}`. Stop with
`head_changed` if it differs from the expected SHA. A review of an earlier head
is evidence for that earlier head only.

Do not use `gh pr view` aggregation as the response source. Query these direct
resources on every poll:

- `GET /repos/{owner}/{repo}/pulls/{pr}/reviews`
- `GET /repos/{owner}/{repo}/pulls/{pr}/comments`
- `GET /repos/{owner}/{repo}/issues/{pr}/comments`
- request-comment reactions when a request comment id is available

Normalize the reviewer login so both `chatgpt-codex-connector` and
`chatgpt-codex-connector[bot]` identify the same bot. Require response timestamps
to be strictly later than the request. Treat `eyes` as pending; a later bot
review, inline comment, issue comment, or non-`eyes` reaction is a response that
still requires thread-aware inspection.

## Run The Poller

From the repository root:

```powershell
python .agents/skills/github-review-polling/scripts/poll_github_review.py `
  --repo Dennis40816/nvt_fw_combiner `
  --pr 154 `
  --expected-head 0123456789abcdef0123456789abcdef01234567 `
  --request-comment-id 123456789 `
  --interval-seconds 300 `
  --max-polls 12
```

Use `--requested-after 2026-07-21T04:00:00Z` only when the request comment id is
unavailable. Use `--once` for a single direct-state check. The poller sleeps in
at most 60-second ticks and emits JSON heartbeats, so a five-minute interval does
not become an opaque five-minute wait.

Exit codes are part of the contract:

| Code | Status | Meaning |
| ---: | --- | --- |
| 0 | `response` | A new bot response was found for the locked request window. |
| 2 | `head_changed` | The PR no longer points to the expected SHA. |
| 3 | `timeout` | The configured polling budget ended without a response. |
| 4 | `api_error` | Authentication, GitHub CLI, REST, or JSON access failed. |
| 5 | `invalid_input` | The request identity was incomplete or inconsistent. |

## Inspect Before Acting

When the poller returns `response`, inspect all returned URLs and current review
threads. Report the exact head, response type, timestamp, and whether actionable
threads remain. A bot response does not authorize merge, thread resolution, CI
reruns, or review-request repetition.

If GitHub review remains pending, keep it as a merge gate while continuing safe
independent work under `$supervised-branch-development`. Request another review
only after a new locally reviewed SHA or after actionable feedback was fixed.

## Validate The Skill

Run both deterministic tests and the skill validator:

```powershell
python tests/scripts/test_github_review_polling.py
python scripts/validate_repository.py
```
