# BUG-20260930-memory-single-ic-diff-lane: Single IC shows a "•" focus lane for the cascade-only DIFF CtrlRAM region

Status: fixed (merged into `1.1.x` through #492, merge `efad0f294`)
Severity: P2
Found: 2026-09-30, owner question about the small black dot beside Master CtrlRAM on NT51950,
at feature/1.1.15/memory-layout@9c946e457.
Where: MemoryFocusLaneViewModel.cs (Create)
Observed: NT51950 single IC shows a "•" lane beside CtrlRAM Master. It is the `diff-ctrlram` region
(`DiffDLM.bin`, `0x33200`, 0x1400 bytes, flash-map visibility multi-chip-only). Single IC and the
2 IC cascade image map share one region set, and a region outside the replace slot list is Base group
(MemoryLayoutProjector.cs), which still gets a lane.
Expected: Decision 197: single IC shows no lane and no dot; the slice stays on the flash bar as
context. The label "Slave DIFF CtrlRAM" appears only for 2 IC cascade.
Evidence: single-IC golden bytes are 0x00 at 0x33200; CtrlRamOverviewCompletionTests covers the
single-IC golden (test to be added first).
Owner: Claude Code, feature/1.1.15/memory-layout.
Resolution: MemoryFocusLaneViewModel.Create skips Base-group CtrlRAM ranges when isSingleIc. Tests:
MemoryFocusLaneTopologyTests (single IC yields Master only; cascade keeps the dot lane) and the
950/951 single-IC golden in CtrlRamOverviewCompletionTests; both were RED before the change.
Narrow Memory|CtrlRam|Legend|AbMerge|AbDp UiSmoke run 593/593 (local). Owner visual acceptance and
review pending. Follow-up profile split: BUG-20260930-single-ic-region-set-includes-cascade-only-region.
