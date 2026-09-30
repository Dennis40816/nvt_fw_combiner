# BUG-20260930-managed-start-deadline-residual-windows: four microsecond-scale, fail-closed windows in the managed start deadline

Status: open
Severity: P3
Found: 2026-09-30, independent Claude Opus 5.5 R3 delta review of
`feature/1.1.15/flaky-fixes`@`95e2c1747`
Where: `ManagedStartDeadline.cs`, `BootstrapStartupProtocol.cs`,
`AnonymousPipeManagedApplicationProcess.cs`, `AnonymousPipeManagedLauncherProcess.cs`
Observed: none reproduced; each window is found by reading the code.
- N1: `BootstrapStartupProtocol.ReportAdmittedAsync` does not catch cancellation. If the deadline fires after
  its entry check and before the flush completes, the reported state stays unset. The candidate and the
  last-known-good fallback share one process object, so the fallback's admission report returns false and the
  result is `StartFailed`, not a rollback.
- N2: both adapters check the deadline before they check whether the lifetime lease was acquired. If the deadline
  expires exactly while the lease is busy (an old tree still alive), the result is `ReadyTimeout` instead of
  `TerminationUnconfirmed`, and the coordinator writes a rollback phase.
- N3: caller cancellation before process creation rethrows without waiting for the worker to release the lifetime
  lease, so an immediate retry in the same process can see `TerminationUnconfirmed`. Creation is refused, so no
  late process appears.
- N4: after creation, cleanup is two five-second waits plus overhead against a ten-second bound, so a cleanup that
  finishes near the bound can report a false `TerminationUnconfirmed`.
Expected: each path settles on the outcome ADR 0056 and `docs/contracts/launcher-bootstrap-v1.md` name for its
actual state, or the window is recorded as accepted with its reason.
Evidence: reviewer citations are in the review record for `95e2c1747`; every case fails closed and none can start
a late process.
Owner: unassigned. Candidate: 1.2.x hardening of the managed start, after the owner decides whether N1-N4 are
accepted or need a fix.
Resolution: open; no production change made for these four windows.
