# Post-v1.1.0 navigation and CtrlRAM first-open handoff

Status: handoff-only scope, acceptance, diagnosis, and evidence. Version
allocation is owned by the NFC roadmap's
[current sequence](../architecture/nfc_roadmap.md#current-release-sequence--2026-09-14):
first-entry UI in `1.2.4`; conditional remaining cold/warm performance
reassessment in `1.2.8`. Completed
[`v1.1.5` performance work](../architecture/nfc_roadmap.md#115-startup-first-open-and-local-verification-performance)
and its measured residuals retain their original evidence. Reassessment is
not a commitment to optimize an unproven bottleneck or repeat shipped work.

Post-release audit intake, 2026-09-19: the
[AUD reconciliation](v1.1.x-custom-options-layout-handoff.md#aud-to-existing-allocation-and-acceptance-index)
adds precise proposed failure sequences to existing scope, not a new page
redesign. AUD-01 (`1.2.1`, persistence health subitem in `1.2.4`) covers
Close → failed handoff → further save → second Close, contained READY
cancellation and terminal run/inspection/Config/Report work before disposal.
AUD-04 (`1.2.2`/`1.2.3`) covers old picker results after context change or
Cancel/Reopen, including a legitimate preparation successor; AUD-05 (`1.2.3`)
covers slow older Hex Load losing at the document owner, not merely in UI.
These external proposals still need scoped implementation admission and
current-head validation. They do not reopen completed `1.1.5` performance work,
approve a new global lifetime store, or settle first-entry UX decisions below.

This handoff records two owner-observed usability/performance problems. It is
not part of `v1.1.1`, and grants no implementation or firmware authority. It
does not authorize a UI, Application, preload, profile, or support change;
the first-entry design decisions below still require owner approval, while
performance implementation/claims require a measured target and separate
owner approval. The latest allocation does not settle those decisions.

## 1. Shared first-entry IC selection

### Observation and intended direction

Directly clicking a workflow in the navigation can enter through a different
experience from Home. On a user's first entry, the product should guide them
to select an IC through the same semantic flow used by Home rather than leave
the destination page to invent its own prompt or selection state.

The intended direction, pending design approval, is:

- when the destination workflow has no current, accepted, compatible IC
  context, navigation presents the shared IC-selection/admission experience;
- once that workflow has a current accepted IC context, later navigation may
  skip the chooser and enter the page directly; and
- a stale, removed, unsupported, or destination-incompatible context must not
  silently satisfy the skip rule.

“Already selected” is not yet defined as process-wide, persisted, or
workflow-session state. No implementation may guess that lifetime or infer IC
identity from a filename, cached label, or Presentation-only state.

### Required reuse inventory

Before proposing code, identify the existing Home entry command, shell
navigation seam, `AuthoringSessionState` transition, canonical capability
selection/admission result, and accepted-session publication already used by
the six workflows. Extend that owner when its contract is insufficient. Do not
add a second IC catalog, remembered-selection store, workflow-specific
selection service, or UI-only admission rule.

### Decisions to close

1. Whether a remembered selection is scoped to one workflow session, the
   current application session, or an explicitly persisted preference.
2. Whether an accepted IC may be reused across workflows and how destination
   capability/IC Count compatibility is revalidated.
3. Which events invalidate the shortcut, including catalog reload, IC or IC
   Count change, route removal, and session reset.
4. Back, Cancel, explicit “Change IC”, deep-link/direct-navigation, keyboard,
   focus, screen-reader, and localization behavior.
5. Whether selection and destination navigation form one atomic transition or
   a resumable two-step flow when loading or validation fails.

### Future acceptance outline

- First direct entry with no valid context prompts through the shared flow.
- A current compatible accepted context enters directly without another
  prompt.
- Invalid or stale context prompts again and cannot publish a partial page
  state.
- Home and direct navigation exercise the same Application-owned transition
  and produce equivalent typed outcomes.
- Switching IC remains visible and reversible; skipping the chooser never
  removes the user's way to correct an accidental selection.

## 2. CtrlRAM Replace cold first-open performance

### Observation

The owner observes that opening CtrlRAM Replace for the first time is
materially slow. There is currently no controlled baseline or stage timing, so
this handoff makes no root-cause or improvement claim.

### Investigation contract

Use the repository bug/performance diagnosis loop before changing production
code:

1. Reproduce on a named build/package and controlled Windows machine. Record
   fresh-process first navigation separately from the second/warm navigation.
2. Hold the selected IC, IC Count/topology, catalog/profile identity, window
   size, theme, and preload state constant; record median and individual runs.
3. Capture elapsed work at existing ownership seams: navigation/admission,
   accepted-session/capability resolution, ViewModel construction, XAML/control
   materialization, slot/card population, Memory Layout/selector projection,
   and deferred preload/dispatcher work.
4. Determine whether work is duplicated, synchronously awaited, performed
   before visible content is necessary, or already available from the unified
   preload and immutable session publications.
5. Add a red-capable regression at the narrowest stable seam before the fix.
   Prefer deterministic work-count or lifecycle evidence; make elapsed time a
   gate only after a reproducible baseline exists.

The implementation must reuse the existing navigation, accepted-session,
capability, projection, and unified-preload owners. It may defer safe
presentation materialization or reuse immutable results, but cannot hide
loading, bypass catalog/profile validation, duplicate firmware facts in UI,
preselect support, or weaken Preview/Build readiness.

### Evidence and completion criteria to define after diagnosis

- exact cold and warm baselines plus the stage that owns the dominant delay;
- an owner-approved target derived from those measurements, not an invented
  universal threshold;
- equivalent IC/workflow/slot/readiness state before and after optimization;
- no additional semantic owner or unbounded cache;
- focused lifecycle/UI evidence and the risk-proportionate canonical verifier
  gate; and
- a fresh packaged-Windows observation if the fix depends on XAML, trimming,
  dispatcher scheduling, or package startup behavior.

## Handoff boundary

The two items may share discovery of shell navigation and preload behavior but
are not one implementation ticket by default. The first is an interaction and
state-lifetime decision; the second is a measured performance diagnosis. Split
their implementation and review unless evidence shows that one existing owner
and one bounded change safely resolves both.

## 3. Exit and navigation confirmation consistency — 2026-09-06

Owner direction for `v1.1.4`: selected files must prompt before leaving the
application, and confirmation surfaces must use the same visual language as
the existing suggested-IC and page-navigation dialogs. The owner subsequently
confirmed that Report JSON also counts. **Locally implemented and verified;
not committed, integrated or published.**

- `MainWindow.axaml.cs::OnClosing` now requests confirmation before cancelling
  startup/run work, disabling the window or completing persistence queues.
  Confirm resumes the existing close/flush lifecycle once; Cancel leaves the
  app and persistence pipeline usable. Empty-window close remains direct.
- `WorkflowSessionPresentationViewModel.Slots.cs::HasSelectedInputs` already
  owns Merge/Replace selected-file detection, including AB, General mappings,
  reference BIN and hidden-mode selections. Paths are assigned before
  inspection, so failed/pending inspection is still selected input.
- `ShellNavigationViewModel::RequestNavigation` checks the source page;
  confirmation still navigates successfully before clearing it. Exit checks
  both Merge and Replace, not merely the visible page, and does not navigate
  or clear input selections before closing.
- `LoadedHexEditorWorkspace` avoids constructing the utility during a close
  check. Its `HasSelectedFile` is set before the first load await, covering
  pending, failed, clean and dirty selections. Hex page navigation now asks
  with explicit retained-document wording; confirmation retains the document
  and edits, and does not save or discard them.
- Report `HasSelectedReportFile` is set on explicit JSON/file-load entry,
  before asynchronous reading. Report-only selection therefore asks even while
  loading or after failure. Automatic history restore, generated diagnostics
  and preferences alone do not count as a new user file selection. Report is
  global and retained across page navigation, so it does not add a redundant
  prompt on every page change.

Keep Cancel initially focused, Escape equivalent to Cancel, cyclic Tab order,
localized action labels, warning-bordered `modalSurface`, 22 px padding and
right-aligned buttons with 8 px gaps. Use a red destructive action where
applicable. The exit overlay must remain above Report/Settings/other modals,
and repeated Close does not stack prompts. Exit replaces a pending page-change
request with a fresh Cancel-focused confirmation; cancelling never executes
the superseded navigation. Settings/Report remain present after Cancel.
Explicit updater restart authorization and failed-handoff retry behavior remain
unchanged; failed handoff resets the ordinary-exit approval flag.

### Local evidence

Source: branch `1.1.4`, uncommitted patch over
`e5202e2707d272076d24216222188d478314a07d`; admission checkpoint
`fffc4bacd67d9b3f357e4919467de72256946311`.

- `exit-confirm-red.trx`: two intended failures reproduced the missing
  Report-only close prompt. `exit-navigation-focus-red.trx`: one intended
  failure reproduced unsafe focus when Exit superseded page navigation.
- `exit-confirm-acceptance.trx`: **258 passed, 0 failed, 0 skipped, 76 s**.
  `dotnet test tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj
  --no-restore` selected `ExitConfirmationTests`, `ShellNavigationSystemTests`,
  `RunAndHexEditorTests`, `VersionManagementSettingsTests`,
  `ReportHistoryControlTests`, `ReportRawCopyTests`,
  `NavigationClearModalAccessibilityTests`, `FirmwareDropProcessSmokeTests`,
  `FirmwareBrowseProcessSmokeTests` and `BuildEntryInspectionTests`.
  Twelve new exit cases cover category/state detection, real Close, safe
  keyboard/default focus, repeated close, pending navigation replacement,
  pending Hex/Report reads, Settings preservation, empty/automatic-history
  startup, localized Light/Dark red action, centered 520 px surface and actual
  persistence after cancelling and then confirming exit. Test teardown now
  explicitly confirms closing only its own test windows.
- Intermediate failures are retained: test input/API corrections are not
  product regressions; a real red-action regression exposed a conflicting
  static `primary` class and was fixed by using mutually exclusive bound
  `primary`/`danger` classes. The old Hex navigation test now explicitly
  confirms the newly approved page-leave prompt before asserting page-scoped
  shortcuts; it still verifies the original shortcut invariant.
- Desktop `dotnet build .../NvtFwCombiner.Desktop.csproj --no-restore
  --verbosity quiet`: 0 warnings/errors, 4.07 s. Every test/build used the
  user-level fixed `NFC_TEST_AREA_ROOT` and its existing `temp` for all three
  temp variables. TRX evidence is under
  `D:/NvtFwCombiner-TestArea/evidence/v114-report-history`.
- [Actual native confirmation](references/v1.1.4-exit-confirmation-actual.jpg):
  Windows, 1920 × 1032 full frame, Light/English, the retained synthetic
  failed-run Report open underneath. Same navigation-modal geometry and
  suggested-IC visual tokens; Cancel default, red Exit. Native Alt+F4/Escape
  preserved the report; the preceding development build also completed an
  actual confirmed close. The visible `1.1.3 desktop` label is the unchanged
  development version label, not a claim this change shipped in `v1.1.3`.
- Scoped capability-reuse admission validator: PASS; record
  `UI-114-EXIT-CONFIRM-09` with sole shared-path owners 02 and 08.
  Primary-agent scoped R1 Polytail: **PASS**; no open P0/P1/P2 in this slice.
  Reviewed the close-before-side-effects guard, single callback, retained
  navigation transaction, selection projections, focus/modality, updater
  exception and executable tests. `git diff --check`: PASS.

Residual boundary: no full-suite, integration, package-release or fresh Golden
claim. Firmware bytes/profiles are untouched; accumulated branch code-size and
frozen integration/release gates remain open. Next queued item is the CtrlRAM
selector visual contract, whose screenshot-based reference still needs approval.

## 4. v1.1.5 CtrlRAM group refresh — 2026-09-13

This dated update supplements the earlier diagnosis status; it does not approve
the deferred first-entry chooser design. The owner approved bounded performance
work and explicitly required that future information additions must not be lost
by selective synchronization.

Local R1 base: `1211d14697a78885cfa74a3455916a87c0199867`, branch `1.1.5`.
Existing presentation owners: `ReplaceRegionGroupBuilder`,
`FirmwareSlotGroupViewModel`, and `ReplacePresentationViewModel.Memory`.
No Application/profile/firmware, startup, CI, or release authority is changed.

### Implemented local mechanism

- Reconcile the existing group collection by the Application-provided
  `ReplaceRegionGroup`; retain matching group containers and expansion state,
  move/insert/remove changed groups in the existing ordering.
- Continue creating complete fresh slot projections through the existing
  producer. Replace each retained group's slot collection with those exact
  objects. There is no field-copy allowlist, declaration-equivalence shortcut,
  slot-data cache, or skipped inspection/readiness refresh.
- Disconnect old slot event subscriptions before attaching current slots;
  removed groups also disconnect. Publish an all-properties notification when
  replacing a group's complete slot publication or language resources, so
  future derived group bindings are not omitted from a notification list.
- New slot information must still be provided/bound through its original
  producer and card. This optimization adds no second place where that new
  information must be copied or compared. It does not automatically invent UI
  for a newly introduced product field.

### Local evidence and remaining boundary

`FirmwareSlotGroupRefreshTests` covers retained group identity, exact fresh-slot
object publication, old-event disconnection/current-event delivery, group
addition/removal/order, whole-publication/language notifications, and actual
compiled UI bindings retaining group controls while rebinding every card.
The initial two tests failed on the old group's replacement behavior before
production edits. A broader CtrlRAM/Mode/selector/guidance/navigation-clear
selection passed 88 tests before the final language-notification addition;
the final affected group/selector/guidance selection passed 33 tests, zero
failures/skips (`groups-final-2.trx`). Primary-agent scoped local R1 Polytail:
PASS; no open correctness/ownership/evidence finding in this unit. This is not
a verdict on the full integration candidate.

Evidence directory:
`D:/NvtFwCombiner-TestArea/evidence/v115-navigation-20260912-232305`.
The exploratory same-harness CtrlRAM return measured about 152 ms versus a
prior 190 ms sample, with four group objects retained rather than zero and
layout allocation about 10.53 MB rather than 13.44 MB in earlier observations.
This is not a statistical speedup claim or packaged-Windows latency guarantee;
the timing run preceded the final no-op language-resource guard. Slot objects
are deliberately still fresh, and first-workflow initialization/template
preload remain unmodified.

Local work does not certify frozen-candidate records, protected CI, packaging,
all certified Golden outputs, or release. Those integration/release gates and
native packaged performance confirmation remain separate.

## 5. v1.1.5 first-workflow activation — 2026-09-13

Owner-approved bounded local R1 continuation on `1.1.5`, base `d78f249d`.
The pre-edit task discussion admitted the existing
`WorkflowSessionPresentationViewModel` page-refresh owner, initially its
`WorkflowContext.cs` partial, `FirstWorkflowActivationTests.cs`, and this
handoff. A reproduced catalog-reload case then admitted the same owner's
`SelectorPublication.cs` partial before editing that path. No new initializer,
cache, background preload, public contract, firmware rule, or visual design
was introduced.

### Mechanism and acceptance

- Catalog publication marks both existing page projections as needing refresh;
  it no longer treats catalog readiness as completed page initialization.
  First activation follows the existing owner-specific full-refresh path.
  Unvisited page slots/groups stay deferred until that page is entered.
- Home confirmation and catalog reconciliation preserve pending refresh work
  even when IC/Mode/Number values are unchanged. Successful page refresh clears
  its flag; existing transactional rollback restores the previous state.
- Required inspection invalidation, complete slot publication, readiness,
  mode selection and page isolation remain on their original paths. Shared
  bootstrap and initial General mapping rows remain unchanged; this does not
  claim that every off-page allocation is removed.

### Evidence and scoped review

Evidence remains in
`D:/NvtFwCombiner-TestArea/evidence/v115-navigation-20260912-232305`:

- `activation-red-2.trx`: all four initial cases reproduced unwanted off-page
  slot initialization. `activation-green.trx`: those four passed.
- `activation-catalog-red.trx`: four added pre-entry catalog-reload cases
  reproduced cleared pending flags. One additional test assertion used `1`
  rather than the existing AB `single` topology token; the test was corrected,
  without changing the product token contract.
- Final production/test source: `activation-regression.trx` passed 213 tests;
  `activation-final.trx` passed 58 (overlapping selections, not 271 unique
  cases). Zero failures/skips. Coverage includes all ten new cases, navigation
  cancellation/rollback, catalog reconciliation, Mode controls, CtrlRAM,
  General/Merge workflows and cross-page inspection isolation. Real compiled
  controls bind confirmed NT51927 three-chip CtrlRAM and NT51950 AB context
  and each complete current slot object.
- Commands: Release `dotnet test` on the UI smoke project with the named
  affected class/method filters, and scoped `dotnet format whitespace
  --verify-no-changes` (exit 0; workspace-load warning). Each process loaded
  the fixed test-area root and set TEMP/TMP/TMPDIR. Detailed logs accompany
  each TRX. `git diff --check` passed.
- Primary-agent scoped local R1 Polytail: **PASS**. Reviewed pending-flag
  lifetime, cancellation/rollback, refresh ownership, whole projections and
  actual-control evidence. No outstanding finding in this unit; this is not
  full-candidate integration or release approval.

The retained diagnostic harness ran once in each navigation order, with no
template intervention (`activation-timing-merge-first` and
`activation-timing-replace-first`, JSON/TRX/log). Merge-first command measured
74.59 ms versus the preceding group-fix sample's 108.16 ms; complete transition
373.86 ms versus 399.15 ms. First Replace after Merge now pays its own deferred
work (40.80 ms command). Reverse order measured Replace 58.01 ms and subsequent
Merge 63.45 ms. These are exploratory headless samples, not a statistical or
packaged-Windows speed guarantee. Work is deferred, not eliminated: both-page
total time is not proven lower, and layout remains dominant. The temporary
probe was removed after measurements; its existing evidence copy remains.

Local implementation is complete. Native packaged timing, frozen integration
records, full required verification/Golden execution and release are separate
remaining boundaries. Do not expand into the deferred chooser redesign or
claim the Home/first-open performance targets are newly certified.

Subsequent local package verification on the committed `dba19a30` source is
recorded in [tests/README.md](../../tests/README.md#local-package-refresh--2026-09-13-unpublished).
The package/worker checks and real Windows Home/Merge/Replace startup checks
passed. Home remains above 700 ms, and direct-process startup does not certify
in-process navigation latency. Formal release and clean-machine gates remain.
