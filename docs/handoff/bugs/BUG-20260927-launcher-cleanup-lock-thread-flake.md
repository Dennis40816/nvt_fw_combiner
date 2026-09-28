# BUG-20260927-launcher-cleanup-lock-thread-flake: the launcher cleanup test's lock helper thread can fail with access denied under load

Status: fixed locally (2026-09-28); independent review and integration verification pending
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

Resolved locally (2026-09-28): the previous retry only guarded `CreateFileW`. The helper first observed the CMD
target marker with `exists()` and read it once with `read_text()`. Marker visibility does not prove that CMD has
finished writing and closed it; access denial at that unguarded read escaped into `lock_errors`. Injecting
`PermissionError(13)` on that first read reproduces the uncovered setup failure. The historical log does not
identify its exact I/O operation, so this is a demonstrated acquisition gap, not proof of every historical cause.

`read_cleanup_lock_target` now retries missing, empty, or access-denied marker reads within the existing absolute
10-second setup deadline. Persistent denial raises a setup-specific `TimeoutError` retaining the original error;
unrelated I/O errors still propagate. The existing native handle acquisition retries and every product assertion
in `test_distribution_launcher_cleanup_preserves_registered_snapshot_only` remain unchanged, including snapshot
preservation/removal, registered worktree state, permanent-lock evidence, and unrelated-file preservation.

Red evidence: `python -m unittest discover -s tests/scripts -p test_release_package_policy.py
-k test_cleanup_lock_target_retries_transient_access_denied -v` failed once with `PermissionError(13)` against
the extracted original helper. Green evidence: the four `test_cleanup_lock_target` tests passed, covering
transient denial, missing/empty publication, bounded persistent denial, and unrelated I/O propagation.
No production behavior, firmware bytes, ranges, integrity, ordering, or support declarations changed.

Full-module evidence: all 83 tests passed twice (561.807 s and 463.649 s), with no skips, including the unchanged
cleanup product test. Both runs used `unittest.defaultTestLoader.loadTestsFromModule(test_release_package_policy)`
and the normal `TextTestRunner`; only the module's `DOTNET` command was directed through an external test-area
wrapper that appended an offline NuGet config (cleared package sources), the existing package cache, and
`-p:NuGetAudit=false`. This allowed the existing RID locked-restore test to execute without downloading anything.
The second run overlapped the first using their independent temporary fixtures.

`python scripts/verify.py --structure-only`, `python scripts/polytail_check.py`, and `git diff --check` passed.
Local self-check found no removed product assertions or production changes. It is not independent review;
the commander retains fixed-head review and `verify.py --all` at integration. The separate Bootstrap host
deadline flake remains open as recorded in `BUG-20260928-launcher-admission-deadline-test-flake.md`.
