# BUG-20260929-managed-start-cancellation-outcome: cancellation hid accepted READY

Status: fixed
Severity: P1
Found: 2026-09-29, independent Codex `gpt-6-astra` R2 review of
`feature/1.1.15/flaky-fixes`@`8f7e8d0ab`
Where: `src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/ManagedStartDeadline.cs`
Observed: if caller cancellation won the outer wait after native creation had
begun, the helper awaited and discarded a worker's `Ready` result, then threw
cancellation. A child could already have passed exact READY and released its
kill-on-close custody while its caller never received that result.
Expected: the adapter delivers an already accepted terminal result so its
Application caller can commit the corresponding state, per ADR 0056 and
`docs/contracts/launcher-bootstrap-v1.md`.
Evidence: fixed-head review of `8f7e8d0ab`; cancellation can occur between the
adapter's final token check and accepted READY release. The controlled
`CallerCancellationDoesNotDiscardAlreadyAcceptedReady` test exercises the
terminal-result race; `CallerCancellationPreservesWorkerCancellationAfterCreation`
retains the cancellation path.
Owner: Codex `gpt-6-sol`, `feature/1.1.15/flaky-fixes`.
Resolution: fixed in the decision 194 production-change checkpoint by returning
the worker's terminal result after bounded supervision. Both controlled tests
passed locally; independent review of the corrected head remains required.
