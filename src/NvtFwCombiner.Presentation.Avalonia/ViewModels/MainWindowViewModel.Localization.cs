using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void ApplyTextResources(ShellLanguage language, bool notify = true)
    {
        Text = ShellTextResources.For(language);
        WorkspaceTitle = Text.WorkspaceTitle;
        WorkspaceSummary = Text.WorkspaceSummary;
        PreviewActionLabel = Text.PreviewActionLabel;
        BuildActionLabel = Text.BuildActionLabel;
        ReportModalActionLabel = Text.ReportModalActionLabel;
        DeviceContextTitle = Text.DeviceContextTitle;
        IcLabel = Text.IcLabel;
        NumberLabel = Text.NumberLabel;
        DeviceContextRefreshSummary = Text.DeviceContextStatus;
        SettingsPreview = Text.SettingsPreview;
        MergePreview = Text.MergePreview;
        ReplacePreview = Text.ReplacePreview;
        FooterStatus = Text.FooterStatus;
        ApplyFirmwareSlotText();
        ApplyInitialRunResultText();
        HexEditorWorkspace.ApplyTextResources(Text);

        if (!notify)
        {
            return;
        }

        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(WorkspaceTitle));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(PreviewActionLabel));
        OnPropertyChanged(nameof(BuildActionLabel));
        OnPropertyChanged(nameof(ReportModalActionLabel));
        OnPropertyChanged(nameof(DeviceContextTitle));
        OnPropertyChanged(nameof(IcLabel));
        OnPropertyChanged(nameof(NumberLabel));
        OnPropertyChanged(nameof(DeviceContextStatus));
        OnPropertyChanged(nameof(SettingsPreview));
        OnPropertyChanged(nameof(MergePreview));
        OnPropertyChanged(nameof(ReplacePreview));
        OnPropertyChanged(nameof(FooterStatus));
        OnPropertyChanged(nameof(LastRunResult));
        OnPropertyChanged(nameof(MergeMemorySummary));
        OnPropertyChanged(nameof(ReplaceMemorySummary));
        OnPropertyChanged(nameof(StandardMergeSupportSummary));
        OnPropertyChanged(nameof(SelectedReplaceModeDescription));
        OnPropertyChanged(nameof(MergeReadinessStatus));
        OnPropertyChanged(nameof(ReplaceReadinessStatus));
        OnPropertyChanged(nameof(MergeBuildActionTip));
        OnPropertyChanged(nameof(ReplaceBuildActionTip));
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
        RelocalizeLoadedReport();
        RefreshSettingsState();
        RefreshReplaceModeState(preserveSlotFiles: true);
        RefreshReplaceSelectionState();
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
            Text.MergeDpSlotDescription,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
        _mergeTpSlot.ApplyDisplayText(
            "TP BIN",
            Text.MergeTpSlotDescription,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
        _mergeLdSlot.ApplyDisplayText(
            "LD BIN",
            Text.MergeLdSlotDescription,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
        ReplaceBaseSlot.ApplyDisplayText(
            Text.BaseFlashBinTitle,
            Text.BaseFlashBinDescription,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);

        foreach (FirmwareSlotViewModel slot in ReplaceSlots.Where(slot => !ReferenceEquals(slot, ReplaceBaseSlot)))
        {
            ApplyReplaceSlotText(slot);
        }
    }

    private void ApplyReplaceSlotText(FirmwareSlotViewModel slot)
    {
        if (string.Equals(slot.SlotId, WorkbenchSlotIds.ReplaceDp, StringComparison.Ordinal))
        {
            slot.ApplyDisplayText(
                Text.DpReplacementBinTitle,
                Text.DpReplacementBinDescription,
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
}
