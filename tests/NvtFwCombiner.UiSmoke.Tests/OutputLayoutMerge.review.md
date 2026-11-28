# Merge output layout local change

## Admission

- Base: `ba383fbf4c6b9035f64ac30e7cff5adee900b14b` on
  `feature/1.3.0/ui-wiremerge`; integration target: `1.3.0`.
- Risk: R1, Presentation only. Implementation owner: primary Codex runtime.
- Scope: Merge Standard, General and AB Code production host, panel draft fixes,
  UiSmoke/Architecture regressions and downward-only size baseline updates.
- Non-goals: Replace/CtrlRAM migration, firmware semantics, commits/publication.
- References: ignored `artifacts/design/README.md`, `map-1-merge*.png`,
  `map-3-states.png`, `map-4-narrow.png`; light/dark, English/Traditional Chinese,
  loaded/expanded/waiting/blocked, 1280/960 px. The latest task includes AB Code.
- Acceptance: one panel model per coherent publication, no stale mode/language
  data, complete existing details, typed issue counts/notices and production-page
  behavior/capture coverage. No byte/range/order/integrity/support change.

## Owner search

`reuse`: `MergePresentationViewModel.PrepareMergeMemoryMapState` selects the
accepted session; `OutputLayoutPresentationViewModel.Publish` owns the atomic
display publication. `UiCompositionRunner.Common` projects `MemoryLayoutProjector`
facts; `MemoryCoverageBarProjection` owns content coalescing and small groups.
`MemoryCoverageSegmentViewModel.DiagnosticSeverity`, `MemoryMapRowViewModel.Action`
and the existing pending projection own diagnostic and waiting/blocked facts.
Input slot warning/error states and General admission issues supply the same
counts used by the page, without counting repeated output slices as new issues.
`IssueCardViewModel.FromBuildAvailability` supplies General non-geometric blockers.
`ShellTextResources` owns complete localized reasons and summaries.

`extend-owner`: carry a panel model in that publication; expose the already
computed pending decision from the existing runner, retaining its old overload.
Carry conflict coordinates directly from `MemoryLayoutConflict` in its existing
segment adapter. Add summary and optional Issues routing capability to panel input.
The panel must not inspect text or reconstruct readiness/overlap/address ranges.

`reject-duplicate`: no second publication subscription or host-side model cache.
There is no current Issues surface for an arbitrary coverage warning; input cards,
Build blocker and historical run reports are separate owners. Hide that action in
Merge rather than opening an unrelated report.

Search evidence: named publication, runner, pending projector, page/template,
slot issue, MessageCenter and report consumers inspected with `rg` and file reads.

## Bug ledger

- Panel draft keyboard pattern conditions need explicit parentheses (IDE0048).
- Panel warning navigation was offered without a host that could handle it.
- Legacy conflict projection omitted numeric geometry and display grouping;
  the new panel would omit those rows without supplemental facts.
- The new pending `out` parameter cannot be captured inside a lambda; use its
  slot identity in a local before the existing lookup. Caught in static review.

## Verification and review boundary

The owner prohibits build, restore, tests and all `dotnet` commands. No red/green,
analyzer, render or test-pass evidence is claimed. Static diff review only;
production page renders must be compared with the approved private references.
Host gates: affected UiSmoke and Architecture tests, analyzers/formatting, size
ratchet, screenshot inspection and independent fixed-source review. Local
Polytail integration verdict: **FAIL (execution and independent evidence pending)**.


## Screen and data changes

- The actual Merge page binds `OutputLayout.Current.Panel` directly. One
  publication constructs one model; its labels, summary, endpoints, rows,
  diagnostics and complete notice travel together. No-IC uses a pending model.
- Summary and full range remain in a collapsed technical context. Existing
  per-row initialization, source, processing and preservation details remain.
- The existing `ContainerQuery` mechanism stacks the panel below inputs when the
  Merge workspace is at most 1000 px wide; wider workspaces keep the 430 px rail.
  Spacing and typography use the existing tokens. Screenshots use 1280/960 by
  1800 px, both themes/languages, six states: 48 production-page frames.
- The original Merge map had no shared-input-hint producer or A/B viewport
  selector. AB's same-TP authoring controls remain in the input area. The new
  panel still supports an explicitly supplied shared-input hint.
- Removed the Merge template, its display aliases and redundant notifications.
  The Merge type's nonblank baseline decreases from 2142 to 2122. The old
  controls and shared templates remain for Replace/CtrlRAM and their tests.

## Test migration and each retired assertion family

No entire old-control test class was deleted. Replace/CtrlRAM routes retain
old-control coverage. All firmware/range/detail assertions continue against
existing typed values; callers of removed aliases now read the publication.

1. `StandardMemoryLayoutControlTests.LoadedStandardCardsStayInsideTheRailAndCloseOnExit`
   becomes `LoadedStandardRowsRetainDetailsAndInputs`; the equivalent AB test
   becomes `LoadedAbRowsRetainBothBanksAndDetails`. Hover/focus auto-popup,
   delayed pointer-exit dismissal, floating-card placement and overlay polling
   assertions are retired because details now expand in the page. Exact facts,
   technical disclosure, containment, keyboard Escape/focus and unchanged inputs
   remain through the real page and shared detail checks.
2. `MemoryCoverageExplorerTests.ProductionMapsExposeSharedSliceExplorer` retains
   its Replace explorer route. Merge's old direct/group/leaf popup assertions
   become real-page disclosure assertions. Small-group members, numbered pins,
   focus and disclosure remain protected by the panel component tests.
3. `MemoryCoverageLegendTests.Nt51950AbLegendCoalescesBanksAndPostbuildImports`
   becomes `Nt51950AbRowsCoalesceBanksAndPostbuildImports`; exact five runs,
   colors, artifact identity and all processing facts remain. The Merge half of
   `StandardLegendKeepsEveryRangeWithoutDefaultCards` replaces passive-legend
   focus refusal and automatic popup assertions with accessible collapsed rows
   and language-stable ranges. Its Replace half retains the old behavior.
4. `MemoryLegendRailHighlightTests.HoveringEveryAbRailSliceLightsItsLegendRow`
   becomes `HoveringEveryAbRailSliceLightsItsNumberedRow`; old `railActive` and
   popup-dismissal assertions become single-row/pin highlight and pointer-exit
   clearing assertions. The CtrlRAM lane test remains unchanged.
5. `MemoryOuterAddressTests.AbMergeBarShowsOuterAddresses` becomes
   `AbMergePanelShowsOuterAddressesAndFullRange`. Old overview-grid names and
   the permanent range box are retired; endpoints and full-range content are
   checked on the new production panel and collapsed context. Replace remains.
6. `AbDummyDpControlTests.DummyDpUsesApprovedRowAndConfirmation` replaces only
   the hover-card mechanism with inline initialization disclosure. Confirmation,
   disabled input, source value and mode/input assertions remain.
7. `CtrlRamMemoryLayoutTests.Nt51928StandardWindowKeepsDpTpAndLdcCoverage`
   uses the new panel. Legacy `ClipToBounds`, fixed rail width and popup renderer
   checks are retired for Merge; typed roles, 34 px bar, inputs and build-history
   assertions remain. New production renders cover 960/1280 containment; loaded
   Standard tests also cover 980/1440. All CtrlRAM-specific methods remain.
8. `MemoryCoveragePublicationTests.MergeTemplateFollowsAtomicPublicationThroughExistingBindings`
   becomes `MergePageFollowsAtomicPanelPublication`. Old template/bar identity
   assertions become actual panel identity and complete-model binding checks.
   Collection/reset tests of the old control remain.
9. `OutputLayoutPublicationTests.ContextPublicationWithoutANewLayoutRaisesTheSummary`
   and `MapRefreshRaisesEveryDisplayPropertyOnce` become current-publication
   identity and one-panel-publication checks. Removed alias `PropertyChanged`
   assertions in mode-selector/catalog reconciliation tests have no remaining
   binding consumer; visible summary/language and publication tests replace them.
10. `XamlControlStyleContractTests.MemoryCoverageSoftLiftUsesSharedThemeMotionAndFocusContracts`
    drops the old Merge binding assertion. `ProductionMemoryCoverageSelectionKeepsExactTrackGeometry`
    keeps both Replace theme cases; its two Merge old-rail cases are replaced by
    the new production geometry and hover tests. Other shared-control tests stay.
11. `MemoryCoverageRegionCardStacksRangeAtFixedRailWidth` drops only the removed
    Merge-template `MaxHeight` assertion. `MemoryCoverageUsesSharedChangedAndKeptVisualStates`
    drops Merge's old classes/legend/top-range-box assertions and retains Replace
    state/style assertions. The one old-host anchor assertion now concerns
    Replace; production Merge renders check both responsive anchors.
12. `OutputAndReportBlocksUseTheSpaciousPanelContract` changes two legacy
    containers to one because Merge has its own panel frame. The spacing-token
    contract drops the old Merge summary stack and fixed-column attribute check;
    measured production bounds now protect its responsive columns.
13. `PresentationBoundaryTests.ShellUsesCleanHomeAndIndependentWorkflowPages`
    requires the new direct panel binding in the production page instead of
    Merge's old bar/template/legend strings. Replace's old host remains required.

The approved behavioral difference is pointer highlight followed by explicit
row disclosure, replacing automatic floating cards. Summary/range facts move
under disclosure; no additional intentional Merge information loss is declared.
The Issues action is hidden because no existing live surface can show that
arbitrary coverage warning; component routing remains tested when enabled.

## Final static review

- Inspected the final local diff for typed ownership, stale publications,
  address provenance, issue routing, mutable state, localization and old-test
  retention. No firmware bytes/ranges/order/integrity/support contract changes.
- `git diff --check`: passed. All three changed XAML documents parsed with
  Python's XML parser. Searches found no old Merge host or removed alias binding.
- A simple nonblank source count gives 2122 for the affected Merge aggregate;
  the size-policy entry only decreases. No repository verifier was executed.
- No build, restore, test, `dotnet`, render, commit, push or new terminal window.
  The 48 captures are test definitions, not generated evidence. Required host
  gates include actual compiler/analyzers, affected tests, independent review
  and comparison with the private references. In particular, unavailable-state
  rendering and full-page responsive geometry have no executed evidence here.

## Changed files

Local source fingerprint (sorted path/SHA-256 pairs, excluding this record):
`d60857a802d357ca9be92add50e960a742a780a2fdb1db2201a4a055508fecee`.

- `scripts/code_size_policy.py`
- `src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml`
- `src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowSharedTemplates.axaml`
- `src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Common.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Localization.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.Memory.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.State.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/OutputLayoutPanelModel.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/OutputLayoutPresentationViewModel.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/Views/OutputLayoutDetails.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/Views/OutputLayoutPanel.axaml`
- `src/NvtFwCombiner.Presentation.Avalonia/Views/OutputLayoutPanelPresenter.cs`
- `tests/NvtFwCombiner.Architecture.Tests/PresentationBoundaryTests.ShellSurface.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/AbDummyDpControlTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/AbMemoryLayoutControlTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/CtrlRamMemoryLayoutTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/CtrlRamSelectorLayoutTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/FirmwareInspectionSlotTests.MemorySourceAb.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/MemoryCoverageExplorerTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/MemoryCoverageLegendTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/MemoryCoveragePublicationTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/MemoryCoverageStatePublicationTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/MemoryLegendRailHighlightTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/MemoryOuterAddressTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/MergeOutputLayoutPageTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/MergeOutputLayoutRenderTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/MergeOutputLayoutTestHost.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/ModeSelectorBindingTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/OutputLayoutPanelInteractionTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/OutputLayoutPublicationTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelDpPerspectiveTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.CanonicalCatalogRefresh.ReconciliationMatrix.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.CanonicalCatalogRefresh.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.FirmwareInspection.PageIsolation.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.FirmwareInspection.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.FirmwareSlots.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.GeneralAdmission.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.Merge.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/SpaciousPanelTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/StandardMemoryLayoutControlTests.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/XamlControlStyleContractTests.MemoryCoverageInteraction.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/XamlControlStyleContractTests.MemoryInfoLayout.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/XamlControlStyleContractTests.SemanticStates.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/XamlControlStyleContractTests.ThemeTokens.cs`
- `tests/NvtFwCombiner.UiSmoke.Tests/OutputLayoutMerge.review.md`
