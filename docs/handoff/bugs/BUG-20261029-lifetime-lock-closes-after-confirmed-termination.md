# BUG-20261029-lifetime-lock-closes-after-confirmed-termination: the lifetime lock stays open for milliseconds after the tree is confirmed terminated

Status: open; unscheduled (no owner decision covers it)
Severity: P3 (fail-closed timing window; no product failure is known)
Found: 2026-10-29, Claude Code NFC session (Claude Opus 5.5), while diagnosing
`BUG-20261029-sequential-bootstrap-lifetime-lock-dispose-flake` (pull request 660). The first review of pull
request 660 found that owner decision 218, which schedules windows N1-N4 of
`BUG-20260930-managed-start-deadline-residual-windows` for `1.2.6`, does not cover this window.
Where: `src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/ManagedProcessLifetimeLease.cs:174-195`
(`TerminateTreeAndConfirmEmpty`), `:106-112` (the lock file is opened with `FileShare.None`) and `:148-165`
(`GetStatus`); `AnonymousPipeManagedApplicationProcess.cs:224-234` (`Terminate`); the lease acquisitions at
`AnonymousPipeManagedApplicationProcess.cs:91-97`, `AnonymousPipeManagedLauncherProcess.cs:137` and
`StableLauncherHandoff.cs:278-286`.
Observed: `TerminateTreeAndConfirmEmpty` treats the tree as gone when the job object's `ActiveProcesses` count is
zero, and `Terminate` also requires the process exit (`HasExited`). The terminated process can still hold its handle
to the lifetime lock file for a short time after both checks pass. A temporary local probe read the Bootstrap lease
status right after the product had confirmed a failed tree as terminated. On an idle machine 25 of 25 runs found the
lease free. With 20 CPU-burning processes, 3 of 25 runs found it still `Active`, and it became free 10-25 ms later.
On CI (run `40740533693`) the hold lasted at least 450 ms, the time `TempWorkspace.Dispose` spent on retries.
Effect, from reading the code: a start that tries to take the same lease inside this window gets `Busy`. The
adapters turn that into `TerminationUnconfirmed` (`AnonymousPipeManagedApplicationProcess.cs:97`) or
`ImmutableBootstrapStartIssue.Busy` (`StableLauncherHandoff.cs:285-286`). Both fail closed, and no late process can
start. No product run has shown this; only the test cleanup has.
Expected: once the product reports a tree as terminated, the lease of that tree can be taken again at once, or the
window is recorded as accepted with its reason.
Possible directions (not chosen; each needs an owner-visible plan before any product change):
- Confirm termination by also waiting until the lease is free, as the tests now do.
- Retry a `Busy` acquisition for a short, bounded time when the previous tree was just confirmed terminated.
- Accept the window, because every outcome fails closed.
Related: `BUG-20260930-managed-start-deadline-residual-windows` (N1-N4, decision 218, `1.2.6`). This window is
analogous but separate.
Owner: unassigned. No owner decision schedules it. Product code is not changed without a plan the owner can see.
Resolution: open; no production change made. Pull request 660 changes only the test that exposed it.
