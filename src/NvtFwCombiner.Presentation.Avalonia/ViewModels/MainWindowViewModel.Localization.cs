using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly AsyncRelayCommand _relocalizeLoadedReportCommand;
    private CancellationTokenSource? _reportRelocalizationIterationCancellation;
    private long _reportRelocalizationRequestVersion;

    internal bool IsReportRelocalizationRunning => _relocalizeLoadedReportCommand.IsRunning;

    internal Task? ReportRelocalizationTask => _relocalizeLoadedReportCommand.ExecutionTask;

    private void ApplyTextResources(ShellLanguage language, bool notify = true)
    {
        Text = ShellTextResources.For(language);
        WorkspaceTitle = Text.WorkspaceTitle;
        WorkspaceSummary = Text.WorkspaceSummary;
        DeviceContextRefreshSummary = Text.DeviceContextStatus;
        SettingsPreview = Text.SettingsPreview;
        MergePreview = Text.MergePreview;
        ReplacePreview = Text.ReplacePreview;
        ApplyFirmwareSlotText();
        RelocalizeFirmwareFacts();
        RefreshDpReplaceInputSelectionReadiness();
        foreach (FirmwareSlotViewModel slot in _abMergeSlotsByAddressSpace.Values)
        {
            if (_firmwareInspectionSession.TryGetInspection(
                    slot.SlotId,
                    slot.FilePath,
                    out WorkbenchFirmwareInspection projected) &&
                projected.AbMergeInput is not null)
            {
                FirmwareInspectionProjection.ApplyAbInputInspection(slot, projected, Text);
            }
        }

        ApplyInitialRunResultText();
        LoadedHexEditorWorkspace?.ApplyTextResources(Text);
        CompositionProgress.ApplyLanguage(language);

        if (!notify)
        {
            return;
        }

        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(WorkspaceTitle));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(RunProgressAccessibleLabel));
        OnPropertyChanged(nameof(DeviceContextStatus));
        OnPropertyChanged(nameof(SettingsPreview));
        OnPropertyChanged(nameof(MergePreview));
        OnPropertyChanged(nameof(ReplacePreview));
        OnPropertyChanged(nameof(LastRunResult));
        OnPropertyChanged(nameof(MergeMemorySummary));
        OnPropertyChanged(nameof(ReplaceMemorySummary));
        OnPropertyChanged(nameof(StandardMergeSupportSummary));
        OnPropertyChanged(nameof(SelectedReplaceModeDescription));
        OnPropertyChanged(nameof(SelectedReplaceModeEvidenceLabel));
        OnPropertyChanged(nameof(SelectedReplaceModeEvidenceTooltip));
        OnPropertyChanged(nameof(SelectedIcFamilyLabel));
        OnPropertyChanged(nameof(SelectedIcFamilyTooltip));
        OnPropertyChanged(nameof(SelectedIcDetailFamily));
        OnPropertyChanged(nameof(SelectedIcDetailReuse));
        OnPropertyChanged(nameof(SelectedIcDetailRuntime));
        OnPropertyChanged(nameof(SelectedIcDetailEvidence));
        OnPropertyChanged(nameof(SelectedIcDetailSupport));
        OnPropertyChanged(nameof(SelectedIcDetailAutomationText));
        OnPropertyChanged(nameof(MergeReadinessStatus));
        OnPropertyChanged(nameof(ReplaceReadinessStatus));
        OnPropertyChanged(nameof(CtrlRamFirmwareVersionCurrentValue));
        OnPropertyChanged(nameof(CtrlRamFirmwareVersionMetadataDetail));
        OnPropertyChanged(nameof(CtrlRamFirmwareVersionValidationDetail));
        OnPropertyChanged(nameof(ReportActionLabel));
        OnPropertyChanged(nameof(ReportActionStatus));
        RefreshNavigationTrail();
        OnPropertyChanged(nameof(ReportHistorySummary));
        OnPropertyChanged(nameof(ReportHistoryStorageSummary));
        OnPropertyChanged(nameof(ReportHistoryStorageWarning));
        OnPropertyChanged(nameof(NavigationPath));
        OnPropertyChanged(nameof(NavigationClearRoute));
        RequestReportRelocalization();
        _deferredState.RefreshLoaded(
            RefreshSettingsState,
            () => RefreshReplaceModeState(preserveSlotFiles: true),
            RefreshCtrlRamDisplayFromInspection,
            RefreshReplaceSelectionState);
    }

    private void RelocalizeFirmwareFacts()
    {
        foreach (FirmwareSlotViewModel slot in MergeSlots
                     .Concat(ReplaceSlots)
                     .Append(ReplaceBaseSlot)
                     .Distinct())
        {
            if (!FirmwareInspectionRequestFactory.SupportsFacts(slot) ||
                !_firmwareInspectionSession.TryGetInspection(
                    slot.SlotId,
                    slot.FilePath,
                    out WorkbenchFirmwareInspection inspection) ||
                inspection.AbMergeInput is not null)
            {
                continue;
            }

            slot.RelocalizeFirmwareFacts(slot.SlotKind == FirmwareSlotKind.Dp
                ? UiCompositionRunner.GetDpFirmwareSlotFacts(inspection, Text)
                : UiCompositionRunner.GetFirmwareSlotFacts(
                    inspection,
                    includeBaseFacts: slot.SlotKind == FirmwareSlotKind.Base,
                    text: Text));
        }
    }

    private void RequestReportRelocalization()
    {
        _ = Interlocked.Increment(ref _reportRelocalizationRequestVersion);
        Volatile.Read(ref _reportRelocalizationIterationCancellation)?.Cancel();
        if (!_relocalizeLoadedReportCommand.IsRunning)
        {
            _relocalizeLoadedReportCommand.Execute(null);
        }
    }

    private void CancelReportRelocalization()
    {
        Volatile.Read(ref _reportRelocalizationIterationCancellation)?.Cancel();
    }

    private void ApplyInitialRunResultText()
    {
        if (!string.Equals(LastRunResult.Title, "No run yet", StringComparison.Ordinal) &&
            !string.Equals(LastRunResult.Title, "尚未執行", StringComparison.Ordinal))
        {
            return;
        }

        LastRunResult = new UiRunResultViewModel(
            Text.InitialRunTitle,
            Text.InitialRunDetail,
            Text.NoOutputLabel,
            succeeded: true);
    }

    private void ApplyFirmwareSlotText()
    {
        _mergeDpSlot.ApplyDisplayText(
            "DP BIN",
            ApplySelectedIcDpSlotHint(MergeDpSlotId, Text.MergeDpSlotDescription),
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
        _mergeTpSlot.ApplyDisplayText(
            "TP BIN",
            Text.MergeTpSlotDescription,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
        _mergeLdcSlot.ApplyDisplayText(
            "LDC BIN",
            Text.MergeLdcSlotDescription,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
        foreach (WorkbenchAbMergeInputSlot input in WorkbenchCompositionService.GetAbMergeInputSlots(
                     SelectedIc,
                     GetSelectedAbMergeTopologyToken()))
        {
            if (_abMergeSlotsByAddressSpace.TryGetValue(input.AddressSpaceId, out FirmwareSlotViewModel? slot))
            {
                slot.ApplyDisplayText(
                    ShellTextResources.GetAbSlotTitle(input.Role),
                    Text.GetAbSlotDescription(input),
                    Text.RequiredLabel,
                    Text.OptionalLabel,
                    Text.NoBinSelectedLabel);
            }
        }

        ReplaceBaseSlot.ApplyDisplayText(
            Text.GetReplaceBaseTitle(SelectedReplaceMode),
            Text.GetReplaceBaseDescription(
                SelectedReplaceMode,
                _deferredState.IsWorkflowLoaded
                    ? WorkbenchCompositionService.GetDpReplaceReferenceCapacityLabel(SelectedIc)
                    : null),
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);

        foreach (FirmwareSlotViewModel slot in ReplaceSlots.Where(slot => !ReferenceEquals(slot, ReplaceBaseSlot)))
        {
            ApplyReplaceSlotText(slot);
        }

        foreach (FirmwareSlotViewModel slot in ReplaceSlots.Where(static slot => slot.UsesSharedSlotPresentation))
        {
            slot.ApplyExperienceText(Text);
        }
    }

    private void ApplyReplaceSlotText(FirmwareSlotViewModel slot)
    {
        if (string.Equals(slot.SlotId, WorkbenchSlotIds.ReplaceDp, StringComparison.Ordinal))
        {
            slot.ApplyDisplayText(
                Text.DpReplacementBinTitle,
                ApplySelectedIcDpSlotHint(slot.SlotId, slot.Description),
                Text.RequiredLabel,
                Text.OptionalLabel,
                Text.NoBinSelectedLabel);
            return;
        }

        slot.ApplyDisplayText(
            slot.Title,
            slot.Description,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
    }

    private string ApplySelectedIcDpSlotHint(string slotId, string description)
    {
        string? hint = WorkbenchCompositionService.GetFirmwareSlotHint(SelectedIc, slotId) ==
            WorkbenchFirmwareSlotHint.InitialCodeAndLdc
            ? Text.InitialCodeAndLdcSlotHint
            : null;
        return !string.IsNullOrWhiteSpace(hint) && !description.Contains(hint, StringComparison.Ordinal)
            ? $"{description} {hint}"
            : description;
    }
}
