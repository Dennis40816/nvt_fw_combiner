# BUG-20260929-memory-review-card-capture-timing: New DP-card capture precedes reveal completion

Status: fixed
Severity: P3
Found: 2026-09-29, Codex gpt-6-astra visual inspection of review-after evidence.
Where: ReleaseExampleScreenshots.cs, NT51929 AB CtrlRAM DP card
Observed: The first added card PNG captures the reveal transition; the card is
translucent and the field row is unreadable despite visual-tree assertions.
Expected: Owner-review evidence shows the complete opened card and exact field.
Evidence: The first PNG is retained as
review-after/nt51929-ab-ctrlram-candidate-dp-card-initial-transition.png and is
obsolete. review-card-final.trx passes (1/0); the final dp-card.png was visually
inspected with an opaque card, expanded field row and exact flash range.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Select the overview container's MemoryCoverageBar descendant, use
its existing ReducedMotion option, settle details expansion and assert opacity
and rendered field bounds before saving. The first corrected fixture mistook
the named container for the coverage control; review-followup-green.trx retains
that failure, and the descendant selection fixes it.
