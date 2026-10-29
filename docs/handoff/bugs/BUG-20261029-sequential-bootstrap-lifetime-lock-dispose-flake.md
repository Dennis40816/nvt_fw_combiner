# BUG-20261029-sequential-bootstrap-lifetime-lock-dispose-flake: test cleanup found the bootstrap lifetime lock still open once on CI

Status: open; cause found and test fix proposed in pull request 660; close after its non-draft CI passes
Severity: P3 (test flake; no product failure is known)
Found: 2026-10-29, Claude Code NFC session (Claude Opus 5.5), from pull request 619 run `40740533693`
(job `123006345544`) at `b28d80f8` (release notes, predecessor comparison declarations and the 1.2.2 board;
no version-management code in that pull request's own diff). The test failed on attempt 1 and passed on the
retry, so the decision 193 gate marked it flaky and `dotnet / build-test` failed with "flaky test has no bug record".
Where: `NvtFwCombiner.Infrastructure.Tests.VersionManagement.AnonymousPipeManagedApplicationProcessTests.SequentialBootstrapFailurePreservesPriorAcceptedDescendant`,
`tests/NvtFwCombiner.Infrastructure.Tests/VersionManagement/AnonymousPipeManagedApplicationProcessTests.Bootstrap.Descendants.cs`
(at `b28d80f8` the test starts at line 262, and the stack trace names line 321, the last statement in the test's
`finally` block. The test's `TempWorkspace` scope, `using var workspace` at line 264, ends at line 323).
Observed: on attempt 1 the test body finished, then `TempWorkspace.Dispose()`
(`tests/NvtFwCombiner.TestSupport/TempWorkspace.cs`, line 61) failed to delete the workspace with
`System.IO.IOException: The process cannot access the file 'bootstrap-state.json.bootstrap-lifetime.v1.lock'
because it is being used by another process.`
Expected: when the test ends, no process holds the bootstrap lifetime lock
(`ManagedProcessLifetimeLease.BootstrapSuffix`), so the workspace can be deleted.
Cause, 2026-10-29: the failed tree's process still held its handle to the lifetime lock for a short time after the
product confirmed the tree as terminated. `WaitForAdmissionAsync` returned `HealthUnavailable`, and the test's
`IsRunning` checks for the failed root and child passed, a few milliseconds before Windows closed that handle. The
test then ended, and `TempWorkspace.Dispose` deleted the workspace while the lock was still open. The CI run used one
test thread (`xUnit.MaxParallelThreads=1`), so no concurrent test was involved. The test took 684 ms, and about
450 ms of that were the Dispose retries, so on CI the hold lasted at least 450 ms.
Evidence: the attempt-1 files of run `40740533693` (`dotnet-test-core-evidence-attempt-1`) show the stack above. A
temporary local probe read the Bootstrap lease status right after the failed tree's assertions. On an idle machine,
25 of 25 runs found the lease free. With 20 CPU-burning processes, 3 of 25 runs found it still `Active`, and it
became free 10-25 ms later.
Fix, pull request 660: before the workspace is deleted, the test waits up to 2 seconds for the lease to be free
(`WaitForBootstrapLifetimeExitAsync`), as the class's other Bootstrap tests already do. A longer hold now fails as an
assertion on the lease status; if the workspace deletion then fails too, both failures are reported together, so
the cleanup `IOException` does not hide the assertion. Test-only change. The window between "termination
confirmed" and the handle close is a product window analogous to those in
`BUG-20260930-managed-start-deadline-residual-windows`. It is not one of that record's N1-N4 windows, so owner
decision 218 does not schedule it; this record does not change product code.
Limit: the original test also passed locally under load, because the local hold (10-25 ms) is shorter than the
Dispose retries. The probe shows the window; non-draft CI on pull request 660 shows the result on a runner.
Related: `BUG-20261009-blocked-validation-expiry-rollback-start-flake` and
`BUG-20261001-stable-launcher-handoff-start-flake` (open flakes of other tests in the same class).
Release impact: board decision 193 makes the release source CI reject any observed flaky test, even with this
record, so a release commit needs a run in which this test passes on its first attempt.
Owner: NFC session (pull request 660).
