# BUG-20260930-replace-coverage-state-reassigned-after-publish: Replace reassigns slice interaction states after publishing them

Status: fixed (local, uncommitted in 1.1.15)
Severity: P3
Found: 2026-09-30, while fixing BUG-20260930-merge-legend-highlight-stale-state, at
feature/1.1.15/memory-layout@9c946e457 plus uncommitted changes.
Where: ReplacePresentationViewModel.Memory.cs (ApplyReplaceMemoryDisplay, RefreshReplaceCoverageGroups)
Observed: ApplyReplaceMemoryDisplay publishes ReplaceCoverageSegments and only then calls
RefreshReplaceCoverageGroups, whose logical items replace every CtrlRAM slice's Interaction. With a bank
view, CreateLogicalItems runs a second time over the bank's slices and replaces their states again, so
coverage-group items and focus lanes can hold different states for one slice.
Expected: every observer (legend row, rail, coverage row, lane) shares the state a slice keeps after publication.
Evidence: code order only. Not reproduced on screen: in CtrlRAM mode the ReplaceCoverageSegments bar is
hidden while lanes exist, and outside CtrlRAM mode no logical items are built. The bank-view path was not
exercised.
Owner: Claude Code, feature/1.1.15/memory-layout (owner scheduled it for 1.1.15 on 2026-09-30).
Resolution: ApplyReplaceMemoryDisplay builds the coverage groups before publishing the slices, and the
logical-item constructor keeps a run head's existing state, so a second construction (bank view) is
idempotent. MemoryCoverageStatePublicationTests: the Replace Golden launch changed 15 of 19 published
states before the fix and none after; the idempotence case was RED before.
