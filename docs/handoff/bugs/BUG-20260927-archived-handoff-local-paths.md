# BUG-20260927-archived-handoff-local-paths: the archived custom-options handoff history keeps local absolute evidence paths

Status: fixed (merged into `1.1.x` by #470, merge `93da007af`, 2026-09-28)
Severity: P3
Found: 2026-09-27, Codex (gpt-6-astra) fixed-head review of batch 3 (pull request #468) at `ee593b7ee`
Where: `docs/ui/v1.1.x-custom-options-layout-history.md` (lines 67, 178, 209, 457, 476, 509, 574 at `ee593b7ee`)
Observed: seven evidence references use the absolute test-area prefix of the owner's machine. The text predates batch 3
(it was in `docs/ui/v1.1.x-custom-options-layout-handoff.md` on the trunk); TODO 55 moved it unchanged.
Expected: public documents name evidence relative to the test area (for example `<test-area>/evidence/...`), keeping the
historical results and evidence names.
Evidence: the batch 3 E-head review.
Owner: 1.1.13 batch 4 (documentation, R0).
Resolution:
