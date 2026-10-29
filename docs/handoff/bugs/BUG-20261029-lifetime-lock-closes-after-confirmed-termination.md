# BUG-20261029-lifetime-lock-closes-after-confirmed-termination: the lifetime lock stays open for milliseconds after the tree is confirmed terminated

Status: open; unscheduled (no owner decision covers it)
Severity: P3 (timing window that starts no late process; no product failure is known)
Found: 2026-10-29, Claude Code NFC session (Claude Opus 5.5), while diagnosing
`BUG-20261029-sequential-bootstrap-lifetime-lock-dispose-flake` (pull request 660). The first review of pull
request 660 found that owner decision 218, which schedules windows N1-N4 of
`BUG-20260930-managed-start-deadline-residual-windows` for `1.2.6`, does not cover this window. Source inspected:
`1.2.2` at `accc0cbbac8bd3c7f7224c6d6fc8a85166a61196`; all line numbers below refer to it.
Where: `src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/ManagedProcessLifetimeLease.cs:174-195`
(`TerminateTreeAndConfirmEmpty`), `:106-112` (the lock file is opened with `FileShare.None`) and `:148-165`
(`GetStatus`); `AnonymousPipeManagedApplicationProcess.cs:224-234` (`Terminate`); the lease acquisitions at
`AnonymousPipeManagedApplicationProcess.cs:91-97`, `AnonymousPipeManagedLauncherProcess.cs:137` and
`StableLauncherHandoff.cs:278-286`.
Observed: `TerminateTreeAndConfirmEmpty` treats the tree as gone when the job object's `ActiveProcesses` count is
zero, and `Terminate` also requires the process exit (`HasExited`). The terminated process can still hold its handle
to the lifetime lock file for a short time after both checks pass.
Evidence:
- Code inspection at `accc0cbb`: the two checks above, and no check of the lock file before `Terminate` reports
  the tree as terminated.
- Local probe, reported by the author and not kept in the repository: a temporary test read the Bootstrap lease
  status right after the product had confirmed a failed tree as terminated. On an idle machine 25 of 25 runs found
  the lease free. With 20 CPU-burning processes, 3 of 25 runs found it still `Active`, and it became free 10-25 ms
  later.
- CI: in run `40740533693` (attempt-1 files of `dotnet-test-core-evidence-attempt-1`), `TempWorkspace.Dispose`
  retried for about 450 ms and still found the lock file open, so the hold lasted at least that long there.
Effect, from reading the code: a start that tries to take the same lease inside this window gets `Busy`. Unless
deadline expiry or caller cancellation wins, the adapters turn that into `TerminationUnconfirmed`
(`AnonymousPipeManagedApplicationProcess.cs:97`, `AnonymousPipeManagedLauncherProcess.cs:143`) or
`ImmutableBootstrapStartIssue.Busy` (`StableLauncherHandoff.cs:285-286`), and no process is created. Both process
adapters check the deadline right after the acquisition (`AnonymousPipeManagedApplicationProcess.cs:94`,
`AnonymousPipeManagedLauncherProcess.cs:140`). If the deadline expires there, the result is `ReadyTimeout`
(`:189-193`, `:245-249`), and for a launcher candidate that starts a rollback (`LauncherBootstrapCoordinator.cs:264-266`).
That case is window N2 of the residual-windows record; caller cancellation propagates as cancellation. In every
case no late process can start. No product run has shown this window; only the test cleanup has.
Expected: once the product reports a tree as terminated, the lease of that tree can be taken again at once, or the
window is recorded as accepted with its reason.
Possible directions (not chosen; each needs an owner-visible plan before any product change):
- Confirm termination by also waiting until the lease is free, as the tests now do.
- Retry a `Busy` acquisition for a short, bounded time when the previous tree was just confirmed terminated.
- Accept the window, because no outcome starts a late process.
Related: `BUG-20260930-managed-start-deadline-residual-windows` (N1-N4, decision 218, `1.2.6`). This window is
analogous but separate; it can lead into N2 when the deadline expires during the acquisition.
Owner: unassigned. No owner decision schedules it. Product code is not changed without a plan the owner can see.
Resolution: open; no production change made. Pull request 660 changes only the test that exposed it.
