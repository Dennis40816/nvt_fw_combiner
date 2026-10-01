# BUG-20261001-ctrlram-cancel-reopens-stale-successor: Cancel during the CtrlRAM Build Settings readiness wait reopens the confirmation, and its Build writes the cancelled firmware version

Status: fixed on `feature/1.2.1/ctrlram-cancel-reopen` (commit `1427142d2`, R56); closes when that branch merges
into `1.2.x`
Severity: P2 confirmed (R12-02 F02). The released CtrlRAM Replace workflows (Standard and AB) reach it. The output's
Backup (or AB B-bank) FirmwareVersion/FirmwareSubVersion bytes differ from what the confirmation shows, without a
warning. It needs Cancel to land inside the readiness wait and a second Confirm.
Found: 2026-10-01, `1.2.1` UI interaction review R12-02 finding F02 (Claude Fable 5.1, code reading; Codex
`gpt-6-astra` review and confirmation); reproduced by the headless checks the owner authorized in decision 241
(Claude Opus 5.5 sub-agent, test-only, on `6f2e2cfa2`, whose `src/` equals the review's fixed source `d5770e52e`).
Where: `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Execution.cs`: the
CtrlRAM Build Settings Confirm awaits action readiness (`:126`) and then reacquires a new preparation generation
(`:132`), so a Cancel during that wait does not invalidate the pending proposal; the naming/hashing wait is
guarded (`:49`, `:55`). The reopened exact-session successor keeps the cancelled edit and session (`:66-77`) and
does not rerun the version transition when a session already exists (`:199-200`), while the visible selection in
`Views/OutputDeliveryConfirmationModal.axaml:157-176` and `CtrlRamFirmwareVersion.cs:238-243` changes only the
screen. The AB draft (`CtrlRamFirmwareVersion.cs:154-170`) uses the same path.
Observed (tests `evidence/1.2.1/R12-02-H/R12H2CtrlRamCancelTests.cs`, test-area relative, asserting the current
behavior). The commander re-ran both test files (SHA-256 `69aa0aac...`, `1104e56d...`) on a clean checkout of
`6f2e2cfa2` with .NET SDK 10.0.303: `dotnet test tests/NvtFwCombiner.UiSmoke.Tests --no-build --filter
"FullyQualifiedName~R12H"`, 9 of 9 passed in each of 3 runs; logs and TRX files in `evidence/1.2.1/R12-02-H/runs/`
(`source.txt` records the source identity):
- H2(a) `R12H2aCurrentBehaviorCancelDuringReadinessReopensSuccessor`: the dialog closes on Cancel and reopens when
  readiness completes, still showing the cancelled Edit 2A/0C. Control
  `R12H2aCurrentBehaviorCancelDuringNamingDoesNotReopen`: a Cancel during naming does not reopen.
- H2(b) `R12H2bCurrentBehaviorReopenedSuccessorExecutesCancelledEditNotVisibleSelection`: after the reopen the
  user selects Keep (source version 01/00) or edits the fields to 33/44 and confirms; the executed request carries
  the cancelled 2A/0C draft and the written Backup version is 2A/0C in both cases.
- H2(c) `R12H2cCurrentBehaviorAbDraftCancelDuringReadinessReopensAndExecutesCancelledDraft`: the AB path reopens
  the same way; with the B-bank editor set back to Keep, the output B-bank version is the cancelled 34/12 (source
  05/00).
Expected: a Cancel while the proposal is pending invalidates it and nothing reopens (`docs/handoff/1.2.1/R39.md:66`,
the accepted R39 B13 acceptance); a Build always writes the version the confirmation shows. Disabling Cancel is not
an equivalent fix.
Not measured: the real length of the readiness window; the tests hold it open with a gate and confirm through the
view model and the modal's confirm method without mounting `OutputDeliveryConfirmationModal`; the bundle path was
not run.
Fix: `1.2.1` R56 (decision 242), split out of R39 B13 (`1.2.7`). It changes the flow that decides output bytes:
R3 firmware-semantic gate (firmware-owner review, byte evidence, write-range audit), with these tests turned into
regression tests that assert the expected behavior.
Resolution (R56, decision 242): the CtrlRAM Confirm begins its output preparation before the readiness wait, and
only a still-current preparation may open the confirmation; when a Cancel invalidates it while it still owns the shared
session, the previous draft is restored through the existing transition (the publication is invalidated if that
fails), and a newer session is never replaced. The same restore removes the cancelled version a Cancel during the
naming wait used to leave in the draft. Implementation Codex `gpt-6.1-sol`, integration and review Claude Code
commander. Evidence (`evidence/1.2.1/R56-runs/`, test-area relative): the 11 regression tests (Standard and AB, loose
and bundle, Keep and Edit after the Cancel, naming control) passed 11/11 in each of 3 runs and failed 11/11 on the
unfixed `6f2e2cfa2`; the CtrlRAM, Replace, OutputDelivery and shared-confirmation scope passed 808/808; the
Presentation project built with warnings as errors. Write ranges, mappings, CRC/Header processing, order, length and
naming are unchanged; only which version reaches the existing write plan changes.
Owner: Claude Code commander (R56).
