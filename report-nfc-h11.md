# H11: observe UI event failures

State: local implementation and static audits complete; build/test verification is blocked by the session sandbox. No commit, integration, or publication.

## Admission

- Approved scope: C# health plan H11, owner approved 2026-10-09; task brief is the acceptance contract.
- Branch: `feature/1.3.1/observe-event-failures`; integration base: `origin/1.3.x` (`accbf6920`); target: `1.3.x`.
- Risk: R1 behavior correction with R2 source-consumption/dependency review.
- Production writer: primary Codex session for Presentation; isolated Codex worker for DistributionLauncher. Two isolated test writers own only the new source/assembly guards and adapter/handler failure tests. A separate read-only architect reviews the local patch.
- Owned surfaces: the 23 named handlers, UI observation adapters, the two consuming project files, new UiSmoke test files, this report, and `Vendor/Core/SOURCE.md`.
- Non-goals: firmware bytes/ranges/profiles, close/lifetime/persistence/handoff policy, package changes, CI/governance changes, commits and external writes.
- Owner search: the canonical observation contract is the pinned Core `UiEventRunner`; Presentation diagnostics use `ISystemInformationService.RecordActivity` and Message Center. Launcher diagnostics use its existing outcome surface. The existing host emergency/startup stderr route is reused by both adapters.
- Disposition: `reuse` Core observation and existing host diagnostic routes; no second diagnostics store or mutable global singleton.
- Evidence gates: source pin and consumer rule, compiled async-void scan, adapter and representative handler failure behavior, affected UiSmoke coverage, Architecture tests, zero-warning build, scoped independent review.
- Narrow/final local gates: focused new UiSmoke tests, affected UiSmoke tests, then Architecture tests, all with `--no-restore` and the fixed test-area preamble.
- Residual integration gate: the host commits, executes `nfc-lanes/test-h11.ps1`, and records review/CI on the final committed head.

## Adapter design

The canonical Core runner owns exception observation. The window-owned NFC
adapter records one Important/Error Diagnostics activity through the existing
`ISystemInformationService.RecordActivity` route and notifies Message Center.
An inherited Avalonia attached property passes that adapter to the views and
the static clipboard action. There is no new diagnostics store or mutable
global singleton. Detached controls without a host owner use a transient
emergency-only adapter. The nonthrowing emergency reporter reuses the stderr
route used by Desktop startup diagnostics (`Program` and `DesktopApplication`).

Launcher is a separate assembly. Its own small adapter uses the existing
`OutcomeText` diagnostic surface and the same host stderr emergency route.
Both projects link the same pinned Core source with
`NVT_CORE_SOURCE_CONSUMPTION`; no Core package is referenced.

## Handler inventory

The naming pattern is `Type.Action`. The old logic runs in a Task method,
invoked by the runner on the calling synchronization context. The two
HexEditorPanel task methods are internal test seams under the existing
InternalsVisibleTo; other extracted methods remain private. All
effects before the first await retain their original position and order.
None of these handlers sets `e.Cancel`.

| Handler | Operation | Synchronous effects retained |
| --- | --- | --- |
| `LauncherWindow.Window_Opened` | `LauncherWindow.Opened` | Setup/recovery dispatch and the called preparation's initial busy state remain before its first await. |
| `LauncherWindow.EditLocation_Click` | `LauncherWindow.EditLocation` | Folder picker invocation remains synchronous; selection changes still occur after the picker completes. |
| `LauncherWindow.Primary_Click` | `LauncherWindow.Primary` | Busy/progress state remains before installation/recovery awaits; existing finally clears progress. |
| `ReportCopyAction.CopyButton_OnClick` | `ReportCopyAction.Copy` | Sender/text checks and clipboard lookup retain their order; the existing catch/toast remains. |
| `MainWindow.OptionalPreloadRetryButton_OnClick` | `MainWindow.OptionalPreloadRetry` | Starts retry, focuses the same target, then awaits retry. |
| `MainWindow.OptionalPreloadCancelButton_OnClick` | `MainWindow.OptionalPreloadCancel` | Starts cancellation/drain, focuses the same target, then awaits drain. |
| `MainWindow.BuildMergeButton_OnClick` | `MainWindow.BuildMerge` | Calls the existing build-settings entry on the same context; guards remain in the existing task. |
| `MainWindow.BuildReplaceButton_OnClick` | `MainWindow.BuildReplace` | Calls the existing build-settings entry on the same context; guards remain in the existing task. |
| `MainWindow.LoadReportJsonButton_OnClick` | `MainWindow.LoadReportJson` | Starts the existing session-observed report task synchronously; its guards and cleanup remain. |
| `MainWindow.Settings_UpdateSourceBrowseRequested` | `MainWindow.UpdateSourceBrowse` | Existing session/settings guards and picker-generation capture remain before awaiting the picker. |
| `FirmwareSlotCard.SlotDrop_OnDrop` | `FirmwareSlotCard.SlotDrop` | Clears drag state; validates context/selection; sets `e.Handled`; then awaits the existing slot operation. |
| `FirmwareSlotCard.BrowseButton_OnClick` | `FirmwareSlotCard.Browse` | Guards, picker lease and title remain before the picker await. |
| `GeneralMappingRow.MappingDrop_OnDrop` | `GeneralMappingRow.MappingDrop` | Clears drag state; validates context/selection; sets `e.Handled`; then awaits the existing mapping operation. |
| `GeneralMappingRow.BrowseButton_OnClick` | `GeneralMappingRow.Browse` | Guards, title and selection lease remain before the picker await. |
| `HexEditorPanel.OpenHexEditorSourceButton_OnClick` | `HexEditorPanel.OpenSource` | Captures top-level/context and begins source selection before the picker await. |
| `HexEditorPanel.HexEditorSourceDrop_OnDrop` | `HexEditorPanel.SourceDrop` | Clears drag state; validates context/selection; sets `e.Handled`; then awaits the existing load. |
| `HexEditorSaveModal.ConfirmHexEditorSaveButton_OnClick` | `HexEditorSaveModal.ConfirmSave` | Save/context/provider guards and save-context capture remain before the picker await. |
| `MessageCenterModal.ExportDiagnosticsButton_OnClick` | `MessageCenterModal.ExportDiagnostics` | Provider/context checks and export-context capture remain before the picker await; existing error handling remains. |
| `OutputDeliveryConfirmationModal.ChooseParentButton_OnClick` | `OutputDeliveryConfirmationModal.ChooseParent` | Context/provider guards and preparation-generation capture remain before the picker await. |
| `OutputDeliveryConfirmationModal.ConfirmButton_OnClick` | `OutputDeliveryConfirmationModal.Confirm` | Acquires confirmation admission and disables the button before awaiting; existing finally restores it and releases admission. |
| `ReplaceSelectionModal.BuildReplaceButton_OnClick` | `ReplaceSelectionModal.BuildReplace` | Context/readiness checks remain before calling the existing Replace settings task. |
| `ReportModal.SaveReportButton_OnClick` | `ReportModal.SaveReport` | Top-level check and existing save-operation/busy state begin before the picker await; catch/finally remain. |
| `SettingsModal.Settings_ToolchainBrowseRequested` | `SettingsModal.ToolchainBrowse` | Open/settings/provider guards and operation-generation capture remain before the picker await; existing catch/disposal remain. |

## Tests added

`UiEventSourceConsumptionTests`:

- `VendoredSource_ManifestPin_MatchesRawBytes`
- `VendoredSource_OneByteModified_RejectsManifestPin`
- `VendoredSource_CrlfConversion_RejectsManifestPin`
- `PresentationProject_LinkedSource_IsSafeConsumer`
- `DistributionLauncherProject_LinkedSource_IsSafeConsumer`
- `Project_CopyAndPackage_RejectsConsumer`
- `Project_CopyOnly_AcceptsConsumer`

`UiEventAssemblyTests`:

- `PresentationAssembly_AsyncVoidMethods_ContainsNone`
- `DistributionLauncherAssembly_AsyncVoidMethods_ContainsNone`
- `TestAssembly_AsyncVoidFixture_IsDetected`

`UiEventAdapterTests`:

- `Run_ChildControl_InheritsWindowDiagnosticRoute`
- `RunAsync_ActionThrows_RecordsOperationAndNotifiesOnce`
- `RunAsync_DiagnosticsThrows_ReportsOriginalFailureOnce`
- `RunAsync_NotificationThrows_ReportsOriginalFailureOnce`
- `RunAsync_SuppliedTokenCancels_ReportsNothing`

`UiEventHandlerFailureTests`:

- `OpenSource_PickerThrows_RecordsOperationOnce`
- `SourceDrop_TransferThrows_RecordsOperationOnce`
- `BuildMerge_SettingsCallbackThrows_RecordsOperationOnce`

`UiEventLauncherTests`:

- `WindowOpened_OwnerDisposed_DoesNotEscape`

The source tests read raw bytes, validate the single manifest entry and compile
symbol, and reject changed/CRLF copies in memory. Project tests parse the two
consumers and synthetic documents without building or contacting a network.
The compiled-assembly scanner visits every type and declared public/nonpublic
instance/static method, including compiler-generated types; a test-only async
void fixture proves detection. Failure tests await the NFC adapter's RunAsync
and reuse the existing UiSmoke headless setup. They add no timers, time
providers, custom temporary-directory implementations, or test setup. The
build fixture reuses TempWorkspace for the existing report/preferences
local-state host; it creates no firmware inputs or outputs.

## Handler failure coverage limits

The picker and drop tests inject failures into the two extracted HexEditorPanel
Task methods. The build test injects a failure into the existing shared
OpenBuildSettingsAsync callback stage used by the build handlers; that helper
is now internal under the existing friend-assembly grant. It does not execute
the complete BuildMerge event entry or firmware build. The following handlers
have no individual unexpected Task-body failure injection. Launcher Opened
also has a disposed-owner entry regression that detects an exception before
the runner boundary; its original empty-window Task path is a no-op:

| Handler | Reason |
| --- | --- |
| `LauncherWindow.Window_Opened` | The disposed-owner entry regression is covered; preparation/recovery Task-body failure is not individually injected because no service/lifecycle seam was added. |
| `LauncherWindow.EditLocation_Click` | The native folder picker and subsequent preparation have no existing injected unexpected-failure seam. |
| `LauncherWindow.Primary_Click` | Installation/recovery is coupled to the existing launcher service; its state and cleanup remain unchanged. |
| `ReportCopyAction.CopyButton_OnClick` | Existing clipboard failures are caught and shown through its existing toast; a distinct unexpected failure was not injected. |
| `MainWindow.OptionalPreloadRetryButton_OnClick` | Existing preload/session ownership is out of scope; no new retry failure seam was added. |
| `MainWindow.OptionalPreloadCancelButton_OnClick` | Existing cancellation/drain ownership is out of scope; no new lifetime seam was added. |
| `MainWindow.BuildMergeButton_OnClick` | Its shared build-settings callback stage is tested; the complete event entry is not separately injected. |
| `MainWindow.BuildReplaceButton_OnClick` | Uses the same tested build-settings callback stage; Replace-specific entry is not separately injected. |
| `MainWindow.LoadReportJsonButton_OnClick` | Existing session observer and report error route remain; no new session/lifetime failure seam was added. |
| `MainWindow.Settings_UpdateSourceBrowseRequested` | Representative picker failure is covered in HexEditorPanel; this settings/session-specific path is not individually injected. |
| `FirmwareSlotCard.SlotDrop_OnDrop` | Representative drop failure is covered in HexEditorPanel; the slot-specific load path is not individually injected. |
| `FirmwareSlotCard.BrowseButton_OnClick` | Representative picker failure is covered in HexEditorPanel; the slot-specific picker lease is unchanged. |
| `GeneralMappingRow.MappingDrop_OnDrop` | Representative drop failure is covered in HexEditorPanel; mapping-specific load is not individually injected. |
| `GeneralMappingRow.BrowseButton_OnClick` | Representative picker failure is covered in HexEditorPanel; mapping-specific selection lease is unchanged. |
| `HexEditorSaveModal.ConfirmHexEditorSaveButton_OnClick` | Existing save workflow and picker context remain; no new save-service failure seam was added. |
| `MessageCenterModal.ExportDiagnosticsButton_OnClick` | Existing export failure handling remains; an unexpected exception outside that handling is not separately injected. |
| `OutputDeliveryConfirmationModal.ChooseParentButton_OnClick` | Representative picker failure is covered in HexEditorPanel; delivery preparation generation is unchanged. |
| `OutputDeliveryConfirmationModal.ConfirmButton_OnClick` | Delivery confirmation already owns its admission and result handling; no new coordinator seam was added. |
| `ReplaceSelectionModal.BuildReplaceButton_OnClick` | Shared build-stage observation is tested; Replace modal entry is not separately injected. |
| `ReportModal.SaveReportButton_OnClick` | Existing save catch/finally remains; no additional unexpected-failure seam was introduced. |
| `SettingsModal.Settings_ToolchainBrowseRequested` | Existing browse catch/disposal remains; representative picker failure is covered elsewhere. |

## Implementation and verification

All 23 entries are synchronous wrappers. Existing AXAML/code wiring, handler
names/accessibility, synchronous effects, catches, cancellation sources, busy
state and finally cleanup are retained. No firmware or Application public API
was changed. Both consumers link the single Core file and define the required
symbol; package declarations and lock files are unchanged.

Completed inspections on the local patch:

- Raw Core bytes: SHA-256 `95a382e09250f1cc2f887935fb126dfe358a9eb4771d24f2a6fc5b2eeeed1c05`, 5480 bytes, no CR; the pinned manifest was not modified.
- A UTF-8 source comparison against HEAD found all 23 original handler bodies unchanged inside their Task methods.
- `git grep -nE "async\s+void" -- src`: no output, exit 1 (no matches).
- `git diff --check`: passed.
- Protected firmware, package, lock, CI and size-policy paths: no diff.

Attempted command:

```text
dotnet build tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj --no-restore
```

The command exited 1 before compilation: MSBuild raised MSB1025 and
UnauthorizedAccessException while creating its temporary subdirectory under
the declared test-area temp child. The user-level root declaration was empty
in this console identity; the host already supplied a fixed process-level
NFC_TEST_AREA_ROOT whose temp child exists. TEMP, TMP and TMPDIR were explicitly
set to that child. The sandbox permits writes only inside this worktree, so it
cannot write to the required external test area. No alternate temp root,
permission change, restore or package change was attempted.

An additional `python scripts/verify.py --structure-only` attempt used the
same declared test-area preamble and exited 1 with `VERIFICATION FAILED:
[WinError 5]` (access denied). It produced no individual check results; no
structure or size-policy pass is claimed.

Build warnings/errors from compilation are unknown. The 19 new tests, affected
UiSmoke tests, Architecture tests and host H11 lane have not run. This patch
must not be described as integration-ready until the host completes those
gates in the writable test-area environment.

Required execution order after the environment is available:

```text
dotnet build tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj --no-restore
dotnet test tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj --no-restore --no-build --filter "FullyQualifiedName~UiEvent"
dotnet test tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj --no-restore --no-build --filter "FullyQualifiedName~FirmwareBrowse|FullyQualifiedName~FirmwareDrop|FullyQualifiedName~FirmwareSlot|FullyQualifiedName~GeneralWorkflow|FullyQualifiedName~HexEditor|FullyQualifiedName~OutputConfirmation|FullyQualifiedName~OutputDelivery|FullyQualifiedName~ReplaceSelectionModal|FullyQualifiedName~ReportModal|FullyQualifiedName~ReportRawCopy|FullyQualifiedName~ReportFileLoading|FullyQualifiedName~ReportImportOutcome|FullyQualifiedName~ReportHistory|FullyQualifiedName~SettingsModal|FullyQualifiedName~VersionManagementSettings|FullyQualifiedName~VersionSourceBusy|FullyQualifiedName~ShellPreloadSession|FullyQualifiedName~DistributionLauncher|FullyQualifiedName~DiagnosticsExport|FullyQualifiedName~MessageCenter|FullyQualifiedName~BuildEntryInspection|FullyQualifiedName~BuildCompletedModal|FullyQualifiedName~BuildOutcome"
dotnet test tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj --no-restore
```

Each shell must first load the fixed test-area declaration and set TEMP, TMP
and TMPDIR to its existing temp child. The host also runs nfc-lanes/test-h11.ps1
and records CI/review evidence on its final committed head.

## Files changed

Presentation:

- `src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj`
- `src/NvtFwCombiner.Presentation.Avalonia/UiEventAdapter.cs` (new)
- `src/NvtFwCombiner.Presentation.Avalonia/Behaviors/ReportCopyAction.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/MainWindow.Build.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/MainWindow.Report.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/MainWindow.VersionManagement.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/Views/FirmwareSlotCard.axaml.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/Views/GeneralMappingRow.axaml.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/Views/HexEditorPanel.axaml.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/Views/HexEditorSaveModal.axaml.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/Views/MessageCenterModal.axaml.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/Views/OutputDeliveryConfirmationModal.axaml.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/Views/ReplaceSelectionModal.axaml.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/Views/ReportModal.axaml.cs`
- `src/NvtFwCombiner.Presentation.Avalonia/Views/SettingsModal.axaml.cs`

Launcher:

- `src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj`
- `src/NvtFwCombiner.DistributionLauncher/LauncherWindow.axaml.cs`
- `src/NvtFwCombiner.DistributionLauncher/LauncherUiEventFailureReporter.cs` (new)

New test files in `tests/NvtFwCombiner.UiSmoke.Tests/`:

- `UiEventSourceConsumptionTests.cs`
- `UiEventAssemblyTests.cs`
- `UiEventAdapterTests.cs`
- `UiEventHandlerFailureTests.cs`
- `UiEventLauncherTests.cs`

Documentation: `Vendor/Core/SOURCE.md` and `report-nfc-h11.md` (new).
The supplied `.gitattributes`, Core source and manifest are pre-existing task
inputs and were preserved. No existing test was weakened or deleted.

## Independent review

A separate read-only architect agent (`gpt-6-astra`, high reasoning, fresh
context without the author's conversation) reviewed the local patch and the
affected contracts. Final verdict: `PASS-WITH-HUMAN-GATE`, restricted to static
design review. No P0/P1 findings or remaining design findings were reported.
Two initial P2 findings were corrected and independently re-reviewed:

- The launcher wrappers evaluated a disposable CTS.Token before the runner boundary. All three outer token arguments were removed; the original Task cancellation logic remains. The disposed-owner event-entry regression targets this escape.
- Direct Task-seam tests did not prove inherited adapter routing. The new Window-to-child static Run regression closes that gap and fails promptly if the notification is missing.

The reviewer recorded these frozen source identities:

| File | SHA-256 |
| --- | --- |
| `LauncherWindow.axaml.cs` | `cb28948ef3ee119bc8dd64e0bf420f6f561372946fa71b87eaabb8ed6b837b2d` |
| `UiEventAdapterTests.cs` | `7b9aa79937434b3bf264923448e884dade3802798207d58d77b883e00b3b8d4b` |
| `UiEventLauncherTests.cs` | `53995731b24256df7c0c4a842eb678cf6af3786aae03b79f228f81cdf47249ef` |

Review basis: uncommitted patch on
`accbf692056fbe93bef52b672b1bed12819cedc8`. This is not committed-head review.
Required execution evidence and the host's final head review/CI remain open;
the static verdict does not authorize integration.

## Limitations and deviations

- The reference Core source-consumption README is absent at the supplied path. The task's equivalent source-consumption instructions apply.
- Console tool calls have substantial dispatch latency; commands use the current console and no new terminal windows. No background processes were started.
- Per the task, evidence remains in this report and agent messages; no separate bug, plan, handoff, or status files are created.

- Required build/test execution is blocked by test-area write permissions; structure verification also returned access denied; no successful compilation or test pass is claimed.
- Representative build failure coverage uses its existing shared Task helper, rather than injecting a failure into the complete BuildMerge entry. No test-only production callback was added.
