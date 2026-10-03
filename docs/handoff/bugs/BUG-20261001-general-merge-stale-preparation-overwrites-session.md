# BUG-20261001-general-merge-stale-preparation-overwrites-session: a superseded General Merge preparation can overwrite the accepted session, and Build then writes the stale mapping

Status: the Application freshness gap (hypothesis B) is fixed on `feature/1.2.4/general-preparation-freshness`, which
merges with `1.2.4` as an R3 change and is a prerequisite of the `1.2.4` General Merge reopening;
the test flake itself is fixed (#507, merge `de58ebaf3`, test-only; released in `v1.2.1`)
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
Fix of B as implemented (2026-10-02, Codex `gpt-6.1-sol`; built, run and integrated by the commander; firmware-semantic
review by Claude Opus 5.5):
- `AuthoringSessionState` issues a request lease when a General Merge preparation enters the Application call (not
  when it is queued), before any progress report or file read. A newer preparation, or an invalidation of the
  session's canonical publication, replaces or revokes it.
- The preparation computes its result on a call-local candidate session that shares the transition lock and has a
  separate publication identity; the shared session is not touched until adoption.
- Adoption happens once, inside the transition lock: the lease must still be the current one, the caller's token must
  not be cancelled, and the accepted snapshot must be the one the preparation started from. Otherwise the session is
  left unchanged and the call returns the existing stale-inspection outcome (`authoring.session.inspection-stale`)
  or throws the cancellation. No issue code was added.
- On the desktop the lifecycle already serializes preparations (it cancels and awaits the predecessor), so a
  superseded preparation reaches the adoption check through its cancelled token; the lease covers callers that do
  not serialize.
- General Replace and the AB `PrepareSessionAsync` keep the old shape: General Replace is retiring (decision 230,
  R54) and the AB method is used only by the CLI, which prepares once per run on a fresh session.
- Unchanged for the newest preparation: accepted session, draft, slots, revision numbers, readiness and progress
  reports. Changed only for a cancelled or failed preparation: a token already cancelled on entry now throws even
  with no file rows; a capture failure together with cancellation or supersession reports the cancellation or the
  stale outcome instead of the capture issue; a late failure keeps the previous accepted snapshot instead of a
  half-applied one.
- Evidence: `GeneralPreparationFreshnessTests` (19 cases, gated with completion sources, no timing): nine of the
  first ten fail on the base production code and pass with the fix. At `edbc7b5ee`: Application.Tests 1673,
  Bootstrap.Tests 2158, the UiSmoke Merge tests 201 and Architecture.Tests 277 passed. The independent review
  accepted that head with no open P0 or P1. No Build bytes or Golden cases were executed for this change; the
  R3 pull request that merges it in `1.2.4` carries the firmware-owner evidence.
P2 local integration evidence (2026-10-03, Codex; base `68d2be017b9e9357966ad03feb8055d6b291f4e9`,
uncommitted patch on `feature/1.2.4/r02-p2-freshness`):
- Reused the three production files from `f795e57b14e722c9e39072dee51ca48b69df869d` without changes. The existing
  P1 inspector signature is used by the freshness test double; it still requests CaptureBytes.
- Retained all 19 original freshness cases; added late mapping-compilation failure/next-request recovery and
  candidate publication isolation/one-time adoption cases. The affected Application class filters pass 79/79,
  including these 21 freshness cases and existing session/selected-file owners.
- Added `GeneralPreparationExecutionFreshnessTests` through the real Bootstrap host, planner/executor and file
  writer. The independent complete-output oracle is a 16-byte image filled with `A5`, with source bytes `11 12 13`
  copied from source `[0,3)` to `output-image` `[4,7)`. All other bytes retain `A5`; no processor is declared.
  Source SHA-256 is `56e75135f0ade48aa22e0f12a1b8dbdacf1eacadc82f5af7c1b46757dc4dd697`; output SHA-256 is
  `fe37a2c508d14879606c1b2df9097a50dfeb1f7df9259952dc44b2932d28817d`.
- Before production integration, all ten General member cases passed this complete Preview/Build oracle.
  All four gated completion/cancellation cases failed. Both newest-first cases actually committed the old
  `[5,8)` mapping, producing SHA-256 `2248e4dbb2531604cc92730a1756cac16bc41826d2bbccbf492efd832d5be775`;
  the old-first cases admitted revoked work or ignored cancellation. No sleep is used in the tests.
- After integration, the new class passes 14/14. Together with `AcceptedSessionFileIdentityTests` and
  `GeneralOutputConfirmationTests`, Bootstrap filters pass 35/35 (the existing 21 also passed before the change).
  Both touched test projects build Release with no restore, 0 warnings and 0 errors; all filtered runs have 0 skips.
- This is fresh local execution evidence, not integration/release certification. G-fixed complete actual-candidate
  Golden comparisons, controlled UI rapid-edit/cancel/retry/Build acceptance, independent exact-source review,
  integration verifier/CI and firmware-owner approval of the final push remain commander gates. No Golden bytes,
  profile, ranges, operation order, CRC/header, padding/truncation, output naming or public visibility were changed.
Owner: Claude Code commander (test fix); the `1.2.4` General input owner (hypothesis B).
