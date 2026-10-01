# BUG-20261001-general-merge-stale-preparation-overwrites-session: a superseded General Merge preparation can overwrite the accepted session, and Build then writes the stale mapping

Status: test flake fixed on `feature/1.2.1/ctrlram-cancel-reopen` (commit `0930eb181`); the Application freshness gap
(hypothesis B) stays open for the `1.2.4` General Merge reopening
Severity: P3 as a test flake. If either hypothesis below can occur on the desktop path, it is a latent P2: General Merge
would write a mapping the screen no longer shows, without a warning. The released UI hides the General Merge entry
and each CLI run uses its own session, so no released path is known to reach it; it blocks reopening the Customized
Merge entry in `1.2.4` (decision 229).
Found: 2026-10-01, Claude Code commander (Claude Opus 5.5), from push CI run `36817388940` on `1.2.x` at
`6f2e2cfa2` (the merge of #504, which changed only G0 scripts and records). Job `dotnet / test (ui)`: the test
failed on attempt 1 and passed on the in-job retry, so the decision 193 gate marked it flaky and
`dotnet / build-test` failed with "flaky test has no bug record".
Where: `NvtFwCombiner.UiSmoke.Tests.MergeWorkflowTests.GeneralMergePreviewAndBuildUseExplicitMappingRows`,
`tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.Merge.cs` lines 537-621 (assertion at line 594).
Observed: the attempt-1 TRX (artifact `dotnet-test-ui-evidence-attempt-1`, downloaded with the owner's approval;
`evidence/1.2.1/flake-36817388940/`, test-area relative) shows `Assert.Equal() Failure: Collections differ` at
line 594: expected `0x11 0x12 0x13` at output offset 4 (target start `0x4`), actual `0xA5` at offset 4 and
`0x11 0x12` at offsets 5-6, the output of target start `0x5`. 1946 of 1947 UiSmoke tests passed; attempt 2
passed. The test sets the mapping's target start to `0x5`, then back to `0x4`, awaits the inspection and then
Previews and Builds; the Build used the superseded `0x5` mapping.
Expected: Build writes exactly the accepted mapping the screen shows; a superseded preparation never changes the
accepted session.
Cause, first reading (commander, refuted): a superseded preparation finishing after the newer one. A read-only
cross-mode check (Codex `gpt-6.1-sol`, `evidence/1.2.1/stale-prep-crossmode.md`, at `d5770e52e`) showed that
preparations of one `WorkflowInspectionLifecycle` run one after another: a new one cancels and awaits its
predecessor (`WorkflowInspectionLifecycle.cs:134`, `:157`), and the cancellation is observed through the progress
callback after each file capture (`GeneralAuthoringExperience.cs:336`, `WorkflowInspectionLifecycle.cs:214`); each
session transition is locked (`AuthoringSessionState.cs:13`, `:313`, `:407`).
Remaining hypotheses:
- A, test timing: the test runs without the dedicated UI-thread test context, so the view model's continuation
  after the Application call can run on a thread-pool thread. While it applies the `0x5` preparation it sets
  `_isApplyingGeneralMergePreparation` (`MergePresentationViewModel.General.cs:233`), and
  `GeneralMergeMappingPropertyChanged` ignores every mapping edit while that flag is set (`:306`); an edit back to
  `0x4` landing in that moment starts no new preparation, so the session keeps `0x5` while the row shows `0x4`.
  Window publication (`MainWindow.Lifetime.cs:321`) does not move the work to the UI thread, so whether the
  desktop path can interleave the same way is not yet established.
- B, Application freshness gap: `GeneralAuthoringExperience.PrepareMergeSessionAsync` binds no request identity or
  expected snapshot; after the last progress report it mutates the shared session (`Activate` `:107`, `SetDraft`
  `:115`, readiness `:132`) without another check, and Build trusts any coherent current session
  (`MergePresentationViewModel.Execution.cs:153`, `:166`; `AcceptedSessionCompositionExecution.cs:466`, `:512`).
  The same shape exists in General Replace (`GeneralAuthoringExperience.cs:226`, `:235`) and in the AB
  `PrepareSessionAsync` used only by the CLI (`AbMergeAuthoringExperience.Format.cs:224`).
The same check found no such gap on the visible paths: Standard Merge, AB Merge inspection and settings re-apply,
CtrlRAM Standard/AB inspection, bank switching and firmware-version edits.
Reachability: the released UI hides the General Merge and General Replace entries
(`src/NvtFwCombiner.Presentation.Avalonia/WorkflowModeDisplayConverters.cs:16`). The CLI
(`src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs:132`) prepares once per run.
Reproduction (deterministic tests, test-area `evidence/1.2.1/gm-repro-runs/`, 3 runs each on `6f2e2cfa2`):
- A confirmed as the CI failure's mechanism: without a UI thread the edit back to `0x4` is swallowed while the `0x5`
  preparation is applied and Build writes `0x5`; on the dedicated UI-thread test context the edit is queued and the
  result is `0x4`; the desktop dispatcher keeps the apply and the edit on the UI thread (3/3 UI tests in each run).
  The desktop path awaits the lifecycle and the Application call in the UI context
  (`WorkflowInspectionLifecycle.cs:157`, `:171`; `MergePresentationViewModel.General.cs:216`), so A is a test-harness
  race, not a desktop defect.
- B confirmed at the Application API: with one shared session, a preparation held after its last progress check
  replaces a newer accepted snapshot (target `0x5` over `0x4`) even when cancelled after that check (2/2 Application
  tests in each run). No current caller reaches it: the desktop lifecycle serializes preparations and each CLI run
  uses its own session.
Fix: the test runs on `UiThreadTestContext` (test-only; `MergeWorkflowTests` 40/40 in 3 runs). B is fixed in the
Application `AuthoringSessionState` owner (prepare a private candidate, adopt it once under the transition lock after
checking a lease renewed when a newer request is queued) before the Customized Merge entry reopens in `1.2.4`; that
change is an R3 firmware-semantic gate.
Owner: Claude Code commander (test fix); the `1.2.4` General input owner (hypothesis B).
