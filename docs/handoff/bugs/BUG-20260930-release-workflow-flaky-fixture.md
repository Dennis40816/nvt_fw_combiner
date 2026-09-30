# BUG-20260930-release-workflow-flaky-fixture: exact workflow fixture lacks annotations

Status: fixed
Severity: P2
Found: 2026-09-30, Codex gpt-6-astra, PR 489 CI diagnosis at
`feature/1.1.15/ci-rerun@449714ac504da99bfef9af70055dfd360aea7df7`.
Where: `tests/scripts/test_release_package_policy.py`,
`release_admission_fixture` and `FAKE_ADMISSION_GH`.
Observed: the selected run declares attempt 2, but fake transport provides only
attempt-2 jobs and no check URLs, check metadata or annotations endpoints.
Candidate, pre-tag and release-create positive tests consequently exit 1.
Expected: positive fixtures supply complete zero-flaky observations for both
attempts under the accepted source-CI release contract; no policy relaxation.
Evidence: targeted reproduction at the starting head: 5 failed, 84 deselected;
all five method failures first reach the missing attempt-1 jobs inventory.
Reported assertion lines: 3221, 3273, 3306, 3327 and 3393.
Owner: Codex gpt-6-astra, sole writer, `feature/1.1.15/ci-rerun`.
Resolution: fixed in the commit containing the PR 489 verified checkpoint.
The fixture now provides paginated jobs for
both attempts, distinct job/check identities, zero counts and empty annotation
pages. Original duplicate-check, candidate, pre-tag and release-create tests
pass; full script suite: 1443 passed, 0 failed, 4 skipped. See
[checkpoint](../1.1.15/WS-CIRERUN.md).
