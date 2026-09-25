# BUG-20260925-output-confirmation-test-desktop-default: confirmation test depends on the process Desktop directory

Status: fixed
Severity: P2
Found: 2026-09-25, Codex worker (GPT-6), while running WS-IO's affected UiSmoke project, at `feature/1.1.12/io-persistence`@`807c294bc`
Where: `tests/NvtFwCombiner.UiSmoke.Tests/OutputConfirmationLabelTests.cs:112-148`; `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/OutputDeliveryConfirmationViewModel.cs:89-105`
Observed: `SharedInputFileCountsRolesSeparatelyFromBundleCopies` fails at `Assert.True(vm.CanConfirm)` both in the complete UiSmoke run (1652 passed, one failed) and in an isolated rerun. The test enables bundle output but leaves `ParentDirectory` at the process Desktop default, which is not a valid writable destination in this worker environment.
Expected: the test supplies a deterministic existing bundle parent under its `TempWorkspace` before asserting confirmation readiness, per `tests/AGENTS.md` deterministic-path guidance.
Evidence: `ParentDirectory` initializes from `Environment.SpecialFolder.DesktopDirectory`; `CanConfirm` requires `IsBundleDestinationValid` when bundle output is enabled. The F07 diff does not touch this validation path.
Owner: Codex worker, `feature/1.1.12/io-persistence`; gate correction in an output-delivery test file within WS-IO's test write lock.
Resolution: the WS-IO F07 checkpoint sets the bundle parent to the test's existing `TempWorkspace` root. The test failed in the complete and isolated runs before the change, passed alone afterward, and the UiSmoke project passed 1653/1653. The checkpoint commit SHA is recorded in the next WS-IO entry.
