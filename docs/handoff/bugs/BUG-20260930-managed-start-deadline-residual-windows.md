# BUG-20260930-managed-start-deadline-residual-windows: four microsecond-scale, fail-closed windows in the managed start deadline

Status: open
Severity: P3
Found: 2026-09-30, independent Claude Opus 5.5 R3 delta review of
`feature/1.1.15/flaky-fixes`@`95e2c1747`
Where: `src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/ManagedStartDeadline.cs:23-62`, `src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/BootstrapStartupProtocol.cs:238-272`,
`src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedApplicationProcess.cs:94,229-230`, `src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/AnonymousPipeManagedLauncherProcess.cs:140,206-208,299-300`
Observed: none reproduced; each window is found by reading the code.
- N1: `BootstrapAdmissionSignal.ReportAdmittedAsync` (`BootstrapStartupProtocol.cs:238`) does not catch cancellation. If
  the deadline fires after its entry check and before the flush completes, the reported state stays at the in-flight
  value `-1` (set at `:248`), so no later report can succeed. The candidate and the
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
Evidence: the code lines under Where; the independent review of `95e2c1747` is recorded in
`docs/handoff/1.1.15/WS-FLAKES.md`. Every case fails closed and none can start a late process.
Owner: unassigned. Candidate: 1.2.x hardening of the managed start, after the owner decides whether N1-N4 are
accepted or need a fix. That owner question is open on the [1.2.x board](../1.2.x.md) (2026-09-30); the
commander recommends a fix in `1.2.6` with C01-2 and R05-02.
Resolution: open; no production change made for these four windows.
