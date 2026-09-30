# BUG-20260930-memory-legend-repeated-title: An AB image legend lists two rows with the same title

Status: fixed (merged into `1.1.x` through #492, merge `efad0f294`)
Severity: P3
Found: 2026-09-30, AB Merge inventory, at feature/1.1.15/memory-layout@9c946e457.
Where: MemoryCoverageBarProjection.cs (CoalesceContent), MemoryCoverageSegmentViewModel.cs
Observed: The AB Merge legend shows two rows both titled "DP AB" (0x00000-0x06FFF and 0x40000-0x46FFF).
Expected: Decision 197: ordinals "#1" and "#2" in physical address order, no A/B bank suffix. A title that
occurs once is unchanged; reserved, unmapped, base-kept and CtrlRAM rows are never numbered.
Review follow-up (pull request #492): primary Unmapped and Reserved sections of an overview are excluded by role,
not only by title (OrdinalsSkipPrimaryUnmappedAndReservedSections). Neutral "Context" sections are not in decision
197's exclusion list and stay numberable.
Evidence: the rule is uniform, so the CtrlRAM overview of NT51950/51 single IC (two DP sections) also reads
DP #1 and DP #2; NT51919 has one DP section and stays DP. The overview assertion in
CtrlRamOverviewCompletionTests was updated for this and disclosed to the owner.
Owner: Claude Code, feature/1.1.15/memory-layout.
Resolution: MemoryCoverageBarProjection.CoalesceContent resets ordinals, coalesces runs, then
NumberRepeatedTitles numbers rows that share a title; MemoryCoverageSegmentViewModel.CanNumberRepeatedTitle
and SetDisplayOrdinal keep the exclusions and the accessible text in step. Three new facts in
MemoryCoverageContentGroupingTests were RED before the change. Narrow Memory|CtrlRam|Legend|AbMerge|AbDp
UiSmoke run 593/593 (local). Owner visual acceptance and review pending.
