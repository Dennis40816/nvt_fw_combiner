# BUG-20261128-ctrlram-slave-slot-title: NT51925 cascade P and S0 CtrlRAM inputs share one slot title

Status: open
Severity: P3
Found: 2026-11-28, Claude Opus 5.5 R3 reviewer, while reviewing the NT51925 FW 1.x cascade S0 CtrlRAM change, at feature/1.3.0/nt51925-s0-open@6b47b63a9
Where: src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutProjector.CtrlRam.cs:89 and src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ShellTextResources.ReplaceGrouping.cs:24
Observed: the CtrlRAM Replace input slot title is "Normal CtrlRAM" for both the P source `normal` and the S0 source `normal-slave`. The title stem comes from `DynamicCtrlRamReplacementIds.FormatRegionBaseDisplayLabel`, which drops the side. The Cascade postbuild branch resolves both regions to `ReplaceRegionGroup.Common`, so no group suffix is added. Only the slot description shows the two different ranges.
Expected: each CtrlRAM input slot has a title that tells the P and S0 sources apart, like the run report label "Normal CtrlRAM (Slave)" from `DynamicCtrlRamReplacementIds.TryFormatDisplayLabel`.
Evidence: static review of the code paths above. No byte, range or admission behavior depends on the title.
Owner: unassigned
Resolution:
