# Replace output layout local change

## Admission and dispatch

- Base: `05410065a13d8febc580bfaf7fa953803c4f2583`; branch
  `feature/1.3.0/ui-wirerepl`; integration target `1.3.0`, whose inspected
  remote-tracking tip is `2c4559fd5795b46bc408e00c85527e4ea108436d`.
- Risk: R1, Presentation display and interaction only. No firmware bytes,
  ranges, ordering, integrity, profile, support or Build-admission change.
- Authority: local edits only, limited to Presentation, Desktop, UiSmoke,
  Architecture and downward-only size baselines. No commit, publication,
  build, restore, test, verifier or `dotnet` execution is authorized.
- Reference: ignored `artifacts/design/README.md`, `gap-report.md`,
  `ctrlparts-handoff.md`, `map-2-ctrlram*.png`, `map-3-states.png` and
  `map-4-narrow.png`; Light/Dark, English/Traditional Chinese, loaded,
  expanded, A/B, waiting/blocked, 1280/960 px. The current owner task replaces
  the older reference's Merge-only release scope. The existing Merge panel
  establishes production spacing and typography.
- Acceptance: one prepared immutable panel publication, complete physical
  rows, typed bank/lane links, inline zoom and shared hint, geometry-only pin
  collisions, local small-group scale, full typed reasons/counts, no stale
  mode/IC/bank/input/language state, and production-page test/capture definitions.
- Primary writer: Replace preparation/publication, production host, new page
  and render tests, size ratchet and integration review.
- Delegated writer `panel_gaps`: `gpt-6-sol`, high reasoning, worker; owns
  panel model/text/presenter/helpers/styles and panel component regressions.
- Delegated writer `legacy_test_migration`: `gpt-6-sol`, high reasoning,
  worker; owns existing Replace UiSmoke/Architecture migrations and the shared
  production Replace test host. Writers preserve other surfaces and do not
  execute tests. Bounded independent surfaces justify concurrent work.
- Narrow host gate: affected panel/parts and Replace UiSmoke plus Architecture
  tests, analyzers/formatting, size ratchet, and production screenshot comparison.
  Required integration/CI and independent fixed-source review remain external.

## Semantic owner search

`reuse`: `ReplacePresentationViewModel.Memory` owns selection of the accepted
General/CtrlRAM session, pending prerequisite, bank availability/viewport and
the existing shared-input grouping. `UiCompositionRunner.GetMemoryDisplay`
and `GetPendingMemoryDisplay` carry `MemoryLayoutProjector` facts. The existing
coverage group builder and `MemoryFocusLaneViewModel` own physical display
runs and endpoint placement. `MemoryCoverageSegmentViewModel` carries typed
severity, disposition, observed change, source, processing and preservation
details. The slot state and General admission own input diagnostic counts;
`IssueCardViewModel.FromBuildAvailability` owns the page's complete Build
reason. Controls do not classify or parse descriptions.

`extend-owner`: `OutputLayoutPresentationViewModel` accepts one already
prepared Replace publication. The panel model owns immutable bank and typed
row/lane display inputs, geometry-only marker grouping and a neutral context
group for existing primary rows with no workflow disposition. Replace's
existing preparation retains typed pending/layout facts until publication.

`reject-duplicate`: no Application edits, second readiness/overlap/range
calculation, text matching, per-binding display cache, or independently bound
bank/zoom/count/notice state. No arbitrary Issues action is shown without an
existing typed navigation destination. Old control files remain for the next
task; shared segment, projection, lane and interaction types remain consumers.

Search evidence: inspected the named preparation/publishing/runner paths,
production workflow template and MainWindow host, Merge publication and review,
bank/zoom controls and adapter, Application section locators (read-only), slot
and Build-card consumers, plus the old-map inventory and test handoff.

## Verification boundary

Owner explicitly prohibits execution. Tests and capture matrices are authored
for the host; no compilation, analyzer, red/green, screenshot, Golden or
test-pass evidence is claimed. Static review results will be appended after
the local change is frozen. Integration acceptance remains pending.
