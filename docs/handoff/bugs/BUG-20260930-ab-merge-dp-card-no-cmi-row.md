# BUG-20260930-ab-merge-dp-card-no-cmi-row: the AB Merge DP card does not list the DP CMI

Status: open (owner decision 213; `1.2.5` item R47)
Severity: P3
Found: 2026-09-30, the `1.1.15` Memory Layout work (`feature/1.1.15/memory-layout`), recorded as a finding in
[`docs/handoff/1.1.15/WS-MEMLAYOUT.md`](../1.1.15/WS-MEMLAYOUT.md) (owner visual review corrections, 2026-09-30);
this record was created by Claude Code commander (Claude Opus 5.5) in the post-`1.2.0` document consistency check.
Where: `src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutProjector.Sections.cs` (`ProjectSections` returns no
section locators unless the compiled experience is `CtrlRamReplace`), reached through `MemoryLayoutProjector.cs`
and `UiCompositionRunner.GetMemoryDisplay`.
Observed: on an AB Code Merge route, the DP card's segment facts carry only the Region ID and the Operation; the
DP CMI sub-field (`a-cmi-dp-version`, and the B-bank field) is not listed. CtrlRAM Replace AB already lists
`a-cmi-dp-version` `flash [0x401A,0x401D)` in its DP card.
Expected: decision 192 (`docs/handoff/1.1.12.md`): every DP CMI sub-field belongs to its DP section on every route
and the DP card lists it with its exact range.
Evidence: the source condition under Where; the WS-MEMLAYOUT finding. Listing the CMI needs a declared AB Merge layout
context, a route/profile contract change (R3), which `1.1.15` did not authorize.
Owner: unassigned; `1.2.5` item R47 (decision 213), with firmware-owner review, byte/Golden evidence and an exact
write-range audit. The raw label question P3-6 (`a-cmi-dp-version` versus a readable name, still open in
WS-MEMLAYOUT) is settled with it.
Resolution: pending.
