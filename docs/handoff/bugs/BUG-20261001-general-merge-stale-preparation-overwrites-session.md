# BUG-20261001-general-merge-stale-preparation-overwrites-session: a superseded General Merge preparation can overwrite the accepted session, and Build then writes the stale mapping

Status: open; root cause identified by code reading; the cross-mode check and the fix owner are pending
Severity: P1 candidate, latent. A reachable case writes output bytes at a mapping the screen no longer shows,
without a warning. The released UI hides the General Merge entry and the CLI prepares once, so no released path
is known to reach it; it blocks reopening the Customized Merge entry in `1.2.4` (decision 229). The final severity
follows the check of the other authoring modes below.
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
Cause (code reading at `6f2e2cfa2`; `src/` is identical to `d5770e52e`):
- Every mapping edit builds a new draft and starts a preparation without awaiting it
  (`src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.General.cs:155`). All
  preparations pass the same `_generalMergeSession` to the Application (`:216-217`); the Presentation checks
  that its draft is still current only after the Application call returns (`:224`, `:229`).
- `GeneralAuthoringExperience.PrepareMergeSessionAsync`
  (`src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs`) awaits the file capture with
  `ConfigureAwait(false)` (`:76`) and then mutates the shared session (`Activate` `:107`, `SetDraft` `:115`,
  accepted files, readiness `:132`) with no cancellation check and no expected-snapshot guard after the await.
- So the `0x5` preparation, already superseded in the Presentation, can still activate and draft the shared
  session after the `0x4` preparation did, on a thread-pool thread. The session then holds the `0x5` draft while
  the Presentation published `0x4`. Build takes `context.AcceptedSession`, the session's current snapshot
  (`MergePresentationViewModel.Execution.cs:153`, `:166`), so it writes the `0x5` mapping, and its currency check
  compares the session with itself. The window is small, so the test fails only under runner load.
- The General Replace preparation in the same file has the same shape (`:198`, `:226`, `:235`); General Replace is
  retired (decision 230) and its entry is hidden.
Reachability: the released UI hides the General Merge and General Replace entries
(`src/NvtFwCombiner.Presentation.Avalonia/WorkflowModeDisplayConverters.cs:16`). The CLI
(`src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs:132`) prepares once per run.
Pending: (1) a read-only check of every other authoring preparation (Standard Merge, AB Merge, CtrlRAM and
Standard Replace) for a superseded preparation that can mutate a shared session after a newer one, with
reachability in the released UI; (2) a deterministic reproduction; (3) the fix owner and version. A fix changes
Application behavior that decides output bytes: R3 firmware-semantic gate (firmware-owner review, byte evidence,
write-range audit).
Owner: Claude Code commander until the fix owner is assigned.
