# Post-v1.1.0 navigation and CtrlRAM first-open handoff

Status: handoff-only scope, acceptance, diagnosis, and evidence. Version
allocation is owned by the NFC roadmap: [first-entry UI in `v1.1.7`](../architecture/nfc_roadmap.md#deferred-ui-completion-from-114--2026-09-10),
and [cold/warm performance in `v1.1.5`](../architecture/nfc_roadmap.md#115-startup-first-open-and-local-verification-performance).

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
