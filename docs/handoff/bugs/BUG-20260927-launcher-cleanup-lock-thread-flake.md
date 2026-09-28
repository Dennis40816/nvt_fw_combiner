# BUG-20260927-launcher-cleanup-lock-thread-flake: the launcher cleanup test's lock helper thread can fail with access denied under load

Status: reopened (2026-09-28): the #470 fix does not cover heavy load
Severity: P3
Found: 2026-09-27, `python scripts/verify.py --all` at batch 3 head `06e09e0ad` (lane `test_release_package_policy`)
Where: `tests/scripts/test_release_package_policy.py`,
`ReleasePackagePolicyTests.test_distribution_launcher_cleanup_preserves_registered_snapshot_only`
Observed: the helper thread that holds a lock on the cleanup target recorded `PermissionError(13, access denied)`, so
`assertEqual([], lock_errors)` failed (1 failed, 83 passed). Another worker's .NET UI tests ran on the same machine at
the time. The same test passed 3/3 in isolated reruns at the same head, and batch 3 does not touch the test or
`scripts/release_package_policy.py`; the lane passed at the earlier batch head `4a81541a2`.
Expected: the lock helper tolerates or retries a transient access-denied result while acquiring its lock, or the test
reports it as a setup failure distinct from the product assertion.
Evidence: the batch 3 verify log at `06e09e0ad` and the reruns.
Owner: 1.1.13 follow-up (test-only, R1).
Resolution:
Reopened (2026-09-28): `python scripts/verify.py --all` on the 1.1.13 integration branch C ran while another agent built the repository. The lock helper again recorded `PermissionError(13)` (`test_release_package_policy.py:2674`). The same test passed 3/3 alone and the module 79/79 afterwards. Owner: 1.1.14 (test-only, R1).
