# BUG-20260930-ab-pending-layout-splits-dp: with only the DP selected, the AB layout splits adjacent DP ranges into numbered rows

Status: closed (not triggered on the current contract; owner decision 306, 2026-10-04)
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
Owner: unassigned; `1.2.9` item R44 (decision 212).
Resolution: closed as not triggered (owner decision 306: keep the contract). For NT51950 and NT51951 with a saved Event Buffer Format configuration, the pending card with only the DP selected is the non-geometric `AB_FORMAT_PRIMARY_INVALID` card, so the split does not occur there; NT51919, NT51929 and NT51932 and the case without a saved configuration were not checked, and the known issue of `CHANGELOG.md` for 1.2.0 is not reconciled. The record reopens if an exact-capability pending case that splits adjacent ranges is reproduced.

R44 investigation (2026-10-03, base `8479dee8ee95dbcee9f38ecf4793e70858c9ff41`), scoped to NT51950/NT51951 with a saved
Event Buffer Format configuration (the NT51919/29/32 families have no AB format policy, and without a saved configuration
the admission returns `AB_FORMAT_CONFIGURATION_INVALID` first; neither case was checked here, and the [1.2.0] known issue
in `CHANGELOG.md` is not reconciled): the current NT51950/NT51951
authoring path requires both TP primary FWConfig values before selecting an exact format/map. A fresh NT51950
DP-only selection, at either Single or Cascade, now produces `AB_FORMAT_PRIMARY_INVALID` and a non-geometric
pending card; it does not reach `MemoryLayoutProjector.Project`. This was checked with synthetic inputs using
both existing UI test hosts. `MergeWorkflowTests.AbMemoryCapacityComesFromDetectedFormat` explicitly requires
no address geometry after DP-only selection, and `AbMergeAuthoringExperience.ResolveCapturedFormat` /
`AbMergeFormatAdmission.Assess` own that prerequisite. The historical slices in `temp/diag-dp-only-keep.txt`
therefore do not reproduce on this base. No production correction or execution-range change was made.
Resolution (2026-10-04, owner decision 306): the commander and the owner reconciled decision 204 with the current format prerequisite by keeping the contract, and this record is closed as not triggered; the scope limits of the investigation above stay as they are.
