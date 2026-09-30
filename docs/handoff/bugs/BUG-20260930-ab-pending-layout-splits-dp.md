# BUG-20260930-ab-pending-layout-splits-dp: with only the DP selected, the AB layout splits adjacent DP ranges into numbered rows

Status: open (owner decision 204; fix in 1.2.x)
Severity: P3
Found: 2026-09-30, owner photo of v1.1.15: NT51950 AB Code with only a 1024k DP_AB selected lists `DP AB #1` to
`DP AB #5`, including `0x37000-0x3FFFF` / `0x40000-0x49FFF` and `0x77000-0x7FFFF` / `0x80000-0xFFFFF` as separate
rows. Reproduced headless on the 512k NT51950 BOE AB Golden at `1.1.x`@`9d5783b4e` by removing both TP inputs.
Where: `src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutProjector.ContentSource.cs` (assigns an artifact
identity per admitted slot and resolves each range's content owner through the plan's writers; a range without a
proven owner gets none), reached through `UiCompositionRunner.GetMemoryDisplay` (the DP-only composition still has an
exact capability), `UiCompositionRunner.Common.cs` (`contentArtifactIdentity: segment.ContentSource?.ArtifactIdentity`)
and `MemoryCoverageBarProjection.CoalesceContent` (joins adjacent primary slices only when both carry the same
non-empty artifact identity).
Observed: with all three inputs accepted, every DP range carries the DP_AB artifact identity and the ranges across the
bank boundary join into one row (`DP AB` `0x37000-0x49FFF`). With only the DP selected, the pending layout gives the DP
ranges no artifact identity (the owner resolution in `MemoryLayoutProjector.ContentSource.cs` finds none for them in
the DP-only plan; the exact cause is established at implementation), so nothing joins and decision 197 numbers every
DP row. The pending layout also tags the
TPA range with the DP's identity and shows the internal region id `ab-combiner-work` as content rows inside TPB.
Expected: owner decision 204: before all inputs are accepted, adjacent ranges of one selected file join like the
accepted layout; internal region ids are not shown as content titles; a range is attributed to the file that will
own it.
Evidence: headless probe output (full vs DP-only segment identities) kept in the test area
(`temp/diag-dp-only-keep.txt`); the photo in the owner's report.
Owner: unassigned; the 1.2.x version slot is set in the allocation sync (decision 200).
Resolution: pending.
