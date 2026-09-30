# BUG-20260930-merge-legend-highlight-stale-state: Merge legend rows of unmerged slices never light on hover

Status: fixed (local, uncommitted in 1.1.15)
Severity: P2
Found: 2026-09-30, owner report "not every region lights the legend on hover", reproduced with a
headless hover probe at feature/1.1.15/memory-layout@9c946e457 plus uncommitted changes.
Where: MergePresentationViewModel.Memory.cs (memory display refresh), MemoryCoverageGroupViewModel.cs
(MemoryCoverageLogicalItemViewModel constructor)
Observed: On AB Merge, hovering a rail slice or tiny marker opened its card but its legend row stayed
unlit for every slice that was not a coalesced run (NT51929 Golden: all four slices; NT51950 Golden:
DP AB #1, TPA and DP AB #3). Applies to every Merge mode that uses MergeCoverageSegments.
Expected: Decision 196: the hovered slice's legend row lights (background only) and follows the open card.
Evidence: the slice's state became rail-active and raised PropertyChanged, but the row watched another
state object. The refresh published MergeCoverageSegments (the bar rebuilds and binds each row to the
slice's Interaction) before CreateLogicalItems replaced every slice's Interaction.
Owner: Claude Code, feature/1.1.15/memory-layout.
Resolution: create the logical items before publishing the slices. MemoryLegendRailHighlightTests
(NT51929 and NT51950 AB Golden, every rail slice lights exactly its own row) failed before and passes
after. Related open path: BUG-20260930-replace-coverage-state-reassigned-after-publish.
