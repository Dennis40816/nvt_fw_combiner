using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void RefreshCtrlRamRegions()
    {
        CtrlRamRegions.Clear();
        foreach (CtrlRamRegionViewModel region in UiCompositionRunner.GetCtrlRamRegions(
            SelectedIc,
            SelectedNumber,
            GetSelectedCtrlRamBasePath()))
        {
            CtrlRamRegions.Add(region);
        }
    }

    private void RefreshMemoryMapState()
    {
        long? selectedMergeDpInputLength = GetSelectedMergeDpInputLength();
        (
            string rangeLabel,
            IReadOnlyList<MemoryMapRowViewModel> rows,
            IReadOnlyList<MemoryCoverageSegmentViewModel> coverageSegments) = IsGeneralMergeModeSelected
            ? UiCompositionRunner.GetGeneralMergeMemoryDisplay(
                GeneralMergeOutputLength,
                CreateGeneralMergeMappingInputs())
            : UiCompositionRunner.GetStandardMergeMemoryDisplay(
                SelectedIc,
                selectedMergeDpInputLength);
        MergeMemoryRangeLabel = rangeLabel;
        ReplaceRows(MergeMemoryRows, rows);
        ReplaceRows(MergeCoverageSegments, coverageSegments);

        (
            string replaceRangeLabel,
            IReadOnlyList<MemoryMapRowViewModel> replaceRows,
            IReadOnlyList<MemoryCoverageSegmentViewModel> replaceCoverageSegments) = UiCompositionRunner.GetReplaceMemoryDisplay(
            SelectedIc,
            SelectedNumber,
            SelectedReplaceMode,
            GetSelectedReplaceBaseLength(),
            GetSelectedCtrlRamBasePath());
        ReplaceMemoryRangeLabel = replaceRangeLabel;
        ReplaceRows(ReplaceMemoryRows, replaceRows);
        ReplaceRows(ReplaceCoverageSegments, replaceCoverageSegments);
        RefreshReplaceCoverageGroups();

        OnPropertyChanged(nameof(MergeMemoryRangeLabel));
        OnPropertyChanged(nameof(ReplaceMemoryRangeLabel));
        OnPropertyChanged(nameof(ReplaceOutputFileName));
        OnPropertyChanged(nameof(MergeMemorySummary));
        OnPropertyChanged(nameof(ReplaceMemorySummary));
        OnPropertyChanged(nameof(IsReplaceCoverageGrouped));
        OnPropertyChanged(nameof(IsReplaceCoverageFlat));
    }

    private long? GetSelectedMergeDpInputLength()
    {
        return WorkbenchCompositionService.IsDpPerspectiveIc(SelectedIc) &&
            !string.IsNullOrWhiteSpace(_mergeDpSlot.FilePath) &&
            File.Exists(_mergeDpSlot.FilePath)
                ? new FileInfo(_mergeDpSlot.FilePath).Length
                : null;
    }

    private long? GetSelectedReplaceBaseLength()
    {
        return SelectedReplaceMode == DpReplaceMode &&
            WorkbenchCompositionService.HasBuiltInV2DpReplace(SelectedIc) &&
            !string.IsNullOrWhiteSpace(ReplaceBaseSlot.FilePath) &&
            File.Exists(ReplaceBaseSlot.FilePath)
                ? new FileInfo(ReplaceBaseSlot.FilePath).Length
                : null;
    }

    private string? GetSelectedCtrlRamBasePath()
    {
        return SelectedReplaceMode == CtrlRamReplaceMode &&
            !string.IsNullOrWhiteSpace(ReplaceBaseSlot.FilePath) &&
            File.Exists(ReplaceBaseSlot.FilePath)
                ? ReplaceBaseSlot.FilePath
                : null;
    }

    private void RefreshReplaceModeState(bool preserveSlotFiles = false)
    {
        Dictionary<string, string?> preservedSlotFiles = preserveSlotFiles
            ? ReplaceSlots
                .Where(slot => !ReferenceEquals(slot, ReplaceBaseSlot))
                .ToDictionary(slot => slot.SlotId, slot => slot.FilePath, StringComparer.Ordinal)
            : new Dictionary<string, string?>(StringComparer.Ordinal);
        ReplaceSlots.Clear();
        if (IsSelectedReplaceModeSupported &&
            SelectedReplaceMode is DpReplaceMode or CtrlRamReplaceMode)
        {
            ReplaceSlots.Add(ReplaceBaseSlot);
            foreach (FirmwareSlotViewModel slot in UiCompositionRunner.GetReplaceInputSlots(
                SelectedIc,
                SelectedNumber,
                SelectedReplaceMode,
                GetSelectedCtrlRamBasePath()))
            {
                RestorePreservedSlotFile(slot, preservedSlotFiles);
                ReplaceSlots.Add(slot);
            }
        }

        ApplyFirmwareSlotText();
        RefreshReplaceSlotGroups();
        OnPropertyChanged(nameof(SelectedReplaceModeDescription));
        OnPropertyChanged(nameof(SelectedReplaceWorkflowReadiness));
        OnPropertyChanged(nameof(SelectedReplaceModeEvidenceLabel));
        OnPropertyChanged(nameof(SelectedReplaceModeEvidenceTooltip));
        OnPropertyChanged(nameof(IsSelectedReplaceModeGoldenVerified));
        OnPropertyChanged(nameof(IsSelectedReplaceModeEvidenceGated));
        OnPropertyChanged(nameof(IsSelectedReplaceModeUnavailable));
        OnPropertyChanged(nameof(IsCtrlRamReplaceModeSelected));
        OnPropertyChanged(nameof(IsGeneralReplaceModeSelected));
        OnPropertyChanged(nameof(IsStructuredReplaceModeSelected));
        OnPropertyChanged(nameof(IsNonCtrlRamStructuredReplaceModeSelected));
        OnPropertyChanged(nameof(ReplaceOutputFileName));
        RefreshCommandState();
    }

    private void RestorePreservedSlotFile(
        FirmwareSlotViewModel slot,
        Dictionary<string, string?> preservedSlotFiles)
    {
        if (!preservedSlotFiles.TryGetValue(slot.SlotId, out string? filePath) ||
            string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        slot.FilePath = filePath;
        RefreshFirmwareFacts(slot);
    }
}
