# BUG-20260929-managed-start-cancellation-unbounded: caller cancellation bypassed cleanup bound

Status: fixed
Severity: P2
Found: 2026-09-29, independent Codex `gpt-6-astra` R2 review of
`feature/1.1.15/flaky-fixes` (pre-push head, since rewritten)
Where: `src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/ManagedStartDeadline.cs`
Observed: after native creation began, the caller-cancellation branch awaited
an unfinished worker without a limit. An in-flight process creation or
admission write could outlive the ready deadline indefinitely.
Expected: use the existing bounded cleanup interval and fail closed as
`TerminationUnconfirmed` when worker completion cannot be observed, per ADR 0056
and `docs/contracts/launcher-bootstrap-v1.md`.
Evidence: fixed-head review of the pre-push head; both branches now allow two
`ManagedProcessTermination.DefaultWaitTimeout` intervals for root and tree cleanup. The controlled
`CallerCancellationCannotWaitForeverForUnfinishedCreation` test holds a worker
past caller cancellation and observes the existing fail-closed outcome.
Owner: Codex `gpt-6-sol`, `feature/1.1.15/flaky-fixes`.
Resolution: fixed in `e20119d71` (corrected by `95e2c1747`), the decision 194 production-change checkpoint by applying
the same bounded cleanup interval to caller cancellation; the later review
correction expanded both branches to the two-step cleanup maximum. The controlled test
passed locally; independent review of the corrected head remains required.
