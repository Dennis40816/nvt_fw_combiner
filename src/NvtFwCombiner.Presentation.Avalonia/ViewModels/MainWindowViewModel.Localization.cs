using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void ApplyTextResources(ShellLanguage language, bool notify = true)
    {
        Text = ShellTextResources.For(language);
        WorkspaceTitle = Text.WorkspaceTitle;
        WorkspaceSummary = Text.WorkspaceSummary;
        DeviceContextRefreshSummary = Text.DeviceContextStatus;
        SettingsPreview = Text.SettingsPreview;
        ReplacePreview = Text.ReplacePreview;
        ApplyFirmwareSlotText();
        RelocalizeFirmwareFacts();
        RefreshDpReplaceInputSelectionReadiness();
        foreach (FirmwareSlotViewModel slot in Merge.AbMergeSlots
                     .Concat(ReplaceSlots).Concat([ReplaceBaseSlot]).Distinct())
        {
            if (WorkflowSession.InspectionSession.TryGetInspection(
                    slot.SlotId, slot.FilePath, out WorkbenchFirmwareInspection projected))
            {
                if (projected.AbMergeInput is not null)
                {
                    FirmwareInspectionProjection.ApplyAbInputInspection(slot, projected, Text);
                }
                else if (projected.InputSlotStatus is { } status)
                {
                    FirmwareInspectionProjection.ApplyInputSlotInspection(slot, status, Text);
                }
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
        OnPropertyChanged(nameof(Merge.MergePreview));
        OnPropertyChanged(nameof(ReplacePreview));
        OnPropertyChanged(nameof(LastRunResult));
        OnPropertyChanged(nameof(Merge.MergeMemorySummary));
        OnPropertyChanged(nameof(ReplaceMemorySummary));
        OnPropertyChanged(nameof(Merge.StandardMergeSupportSummary));
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
        OnPropertyChanged(nameof(Merge.MergeReadinessStatus));
        OnPropertyChanged(nameof(ReplaceReadinessStatus));
        OnPropertyChanged(nameof(CtrlRamFirmwareVersionCurrentValue));
        OnPropertyChanged(nameof(CtrlRamFirmwareVersionMetadataDetail));
        OnPropertyChanged(nameof(CtrlRamFirmwareVersionValidationDetail));
        RefreshNavigationTrail();
        OnPropertyChanged(nameof(NavigationPath));
        OnPropertyChanged(nameof(NavigationClearRoute));
        Reports.ApplyLanguageChanged();
        Merge.ApplyLanguageChanged();
        Replace.ApplyLanguageChanged();
        WorkflowSession.ApplyLanguageChanged();
        _deferredState.RefreshLoaded(
            RefreshSettingsState,
            () => RefreshReplaceModeState(preserveSlotFiles: true),
            WorkflowSession.RefreshCtrlRamDisplayFromInspection,
            Replace.RefreshSelectionState);
    }

    private void RelocalizeFirmwareFacts()
    {
        foreach (FirmwareSlotViewModel slot in Merge.MergeSlots
                     .Concat(ReplaceSlots)
                     .Append(ReplaceBaseSlot)
                     .Distinct())
        {
            if (!FirmwareInspectionRequestFactory.SupportsFacts(slot) ||
                !WorkflowSession.InspectionSession.TryGetInspection(
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
        Merge.MergeDpSlot.ApplyDisplayText(
            "DP BIN",
            ApplySelectedIcDpSlotHint(MergeDpSlotId, Text.MergeDpSlotDescription),
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
        Merge.MergeTpSlot.ApplyDisplayText(
            "TP BIN",
            Text.MergeTpSlotDescription,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
        Merge.MergeLdcSlot.ApplyDisplayText(
            "LDC BIN",
            Text.MergeLdcSlotDescription,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
        foreach (WorkbenchAbMergeInputSlot input in WorkbenchCompositionService.GetAbMergeInputSlots(
                     SelectedIc,
                     Merge.GetSelectedAbMergeTopologyToken()))
        {
            if (Merge.AbMergeSlotsByAddressSpace.TryGetValue(input.AddressSpaceId, out FirmwareSlotViewModel? slot))
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
