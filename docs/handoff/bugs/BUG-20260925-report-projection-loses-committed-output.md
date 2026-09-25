# BUG-20260925-report-projection-loses-committed-output: report projection failure hides committed output

Status: fixing
Severity: P1
Found: 2026-09-25, Codex worker (GPT-6), while revalidating F07, at `feature/1.1.12/io-persistence`@`5882d7a57`
Where: `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/CompositionRunPresentationViewModel.cs:190-220,338-360`
Observed: after a successful build returns, cancellation or report projection failure enters the generic catch without publishing the already committed output result. The error display can say `No output`.
Expected: preserve and display the committed path, size and hash without rerunning processors, per `docs/architecture/post-v1.1.8-audit-handoff.md` AUD-03.
Evidence: `ProjectAndApplyRunResultAsync` runs after `run(...)`, while the catch returns `null` for cancellation or publishes a `No output` failure for projection exceptions.
Owner: Codex worker, `feature/1.1.12/io-persistence`
Resolution:
