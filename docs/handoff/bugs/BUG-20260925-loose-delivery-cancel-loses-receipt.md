# BUG-20260925-loose-delivery-cancel-loses-receipt: cancellation after primary commit loses the typed receipt

Status: fixed
Severity: P1
Found: 2026-09-25, Codex worker (GPT-6), while revalidating F07, at `feature/1.1.12/io-persistence`@`5882d7a57`
Where: `src/NvtFwCombiner.Application/Composition/CompositionRunService.cs:464-500`
Observed: loose additional delivery can throw `OperationCanceledException` after the primary output commits; the exception escapes before a `CompositionRunResult` carries the committed path, size and hash.
Expected: retain the exact committed primary artifact receipt and report incomplete additional delivery, per `docs/architecture/post-v1.1.8-audit-handoff.md` AUD-03.
Evidence: the loose delivery catch accepts only `ArgumentException`, `IOException` and `UnauthorizedAccessException`; the committed result is built after that catch.
Owner: Codex worker, `feature/1.1.12/io-persistence`
Resolution: the WS-IO F07 checkpoint catches loose-delivery cancellation after primary commit and returns the original typed receipt. `LooseDeliveryCancellationRetainsCommittedPrimaryReceipt` failed with `TaskCanceledException` before the change and passed afterward; the Application project passed 1612/1612. The checkpoint commit SHA is recorded in the next WS-IO entry.
