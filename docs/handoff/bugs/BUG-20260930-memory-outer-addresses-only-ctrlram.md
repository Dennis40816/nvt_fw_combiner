# BUG-20260930-memory-outer-addresses-only-ctrlram: Outer start/end addresses appear only on the CtrlRAM flash overview

Status: fixed (local, uncommitted in 1.1.15)
Severity: P3
Found: 2026-09-30, owner question "why do not all memory layouts show the start and end hex address",
at feature/1.1.15/memory-layout@9c946e457 plus uncommitted changes.
Where: MainWindowWorkflowTemplates.axaml (lines 378-386), MainWindowSharedTemplates.axaml (line 507)
Observed: only the CtrlRAM flash overview binds StartAddress/EndAddress (CtrlRamStartAddress,
CtrlRamEndAddress). The Replace flash bar and the Merge bar bind neither; Merge shows the range in a text
box above the bar (MergeMemoryRangeLabel, for example "0x00000-0x7FFFF (len 0x80000)").
Expected: owner decision 2026-09-30: every Memory Layout bar shows its outer start and end address in
the same place as the CtrlRAM overview, from the layout the presentation already has (no range derived
in the control); Merge keeps its range box above the bar.
Evidence: template bindings listed above; MemoryCoverageBar hides the address row when both are empty.
Owner: Claude Code, feature/1.1.15/memory-layout (if the owner puts it in 1.1.15).
Resolution: MemoryCoverageBarProjection.OuterAddresses supplies the outer start/end for the CtrlRAM
overview, the Replace flash bar and the Merge bar (new ReplaceStartAddress/EndAddress and
MergeStartAddress/EndAddress bindings); Merge keeps its range box. MemoryOuterAddressTests covers the
NT51929 AB bar and the NT51950 single Replace flash bar.
