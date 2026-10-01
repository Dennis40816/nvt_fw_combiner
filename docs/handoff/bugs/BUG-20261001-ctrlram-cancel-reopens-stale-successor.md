# BUG-20261001-ctrlram-cancel-reopens-stale-successor: Cancel during the CtrlRAM Build Settings readiness wait reopens the confirmation, and its Build writes the cancelled firmware version

Status: fixed (R56, #507, merge `de58ebaf3`; released in `v1.2.1`)
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
only a still-current preparation may open the confirmation. The independent review of the first fix (Codex
`gpt-6-astra`, reject, P1 admission evidence, two P2) found that restoring the cancelled version only when the old
continuation resumed left two interleavings: a Cancel during the naming wait followed by a new Build Settings let the
late restore invalidate the new dialog's lease (its Confirm was refused), and on AB a bank switch after the Cancel
could copy the cancelled B version into the new bank's session. The fix now restores at the moment the preparation is
invalidated (`OutputDeliveryConfirmationViewModel.BeginPreparation` runs the previous preparation's invalidation
callback on Cancel, a new preparation or Open; the CtrlRAM dialog's close and context invalidation trigger it),
synchronously, while the cancelled preparation still owns the shared session, through the existing
`TransitionFirmwareVersionCompilation` (the publication is invalidated if that fails); a newer session is never
replaced, an opened confirmation releases the callback, and the old continuation no longer restores or reopens.
Implementation Codex `gpt-6.1-sol`, integration and review Claude Code commander. Evidence
(`evidence/1.2.1/R56-runs-v2/`, test-area relative): 15 regression tests passed 15/15 in each of 3 runs (8 compare the
written version bytes, 2 check that nothing is written, 1 checks the naming-cancel restore, 4 cover the two
interleavings; the AB A-bank check compares FWConfig metadata, not all A-bank bytes); the 4 interleaving tests fail on
the first fix `be5f79c9e` and the first 11 fail on the unfixed `6f2e2cfa2` (`evidence/1.2.1/R56-runs/red.log`); the
related scope passed 812/812; the Presentation project built with warnings as errors. Write ranges, mappings,
CRC/Header processing, order, length and naming are unchanged; only which version reaches the existing write plan
changes.
Owner: Claude Code commander (R56).
