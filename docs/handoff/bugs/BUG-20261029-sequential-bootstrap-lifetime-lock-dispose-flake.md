# BUG-20261029-sequential-bootstrap-lifetime-lock-dispose-flake: test cleanup found the bootstrap lifetime lock still open once on CI

Status: open; cause not proven
Severity: P3 (test flake; no product failure is known)
Found: 2026-10-29, Claude Code NFC session (Claude Opus 5.5), from pull request 619 run `40740533693`
(job `123006345544`) at `b28d80f8` (release notes, predecessor comparison declarations and the 1.2.2 board;
no version-management code in that pull request's own diff). The test failed on attempt 1 and passed on the
retry, so the decision 193 gate marked it flaky and `dotnet / build-test` failed with "flaky test has no bug record".
Where: `NvtFwCombiner.Infrastructure.Tests.VersionManagement.AnonymousPipeManagedApplicationProcessTests.SequentialBootstrapFailurePreservesPriorAcceptedDescendant`,
`tests/NvtFwCombiner.Infrastructure.Tests/VersionManagement/AnonymousPipeManagedApplicationProcessTests.Bootstrap.Descendants.cs`
(the test starts at line 262; the failure is at line 321, the end of the test's `TempWorkspace` scope).
Observed: on attempt 1 the test body finished, then `TempWorkspace.Dispose()`
(`tests/NvtFwCombiner.TestSupport/TempWorkspace.cs`, line 61) failed to delete the workspace with
`System.IO.IOException: The process cannot access the file 'bootstrap-state.json.bootstrap-lifetime.v1.lock'
because it is being used by another process.`
Expected: when the test ends, no process holds the bootstrap lifetime lock
(`ManagedProcessLifetimeLease.BootstrapSuffix`), so the workspace can be deleted.
Possible cause, not confirmed: a bootstrap or descendant process of the test was still releasing the lifetime
lease when the workspace was deleted. `BUG-20260930-managed-start-deadline-residual-windows` describes the lifetime
lease window in N3 for the same class. This record does not claim a match.
The attempt-1 result files were not downloaded.
Next step: when the test fails again, keep the attempt-1 result file and check which process held the lock and
whether the test waits for every started process to exit before the workspace is disposed. Change no test or product
code before that.
Owner: NFC session until the investigation names a fix owner.
