using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void RefreshCtrlRamRegions()
    {
        CtrlRamRegions.Clear();
        foreach (CtrlRamRegionViewModel region in UiCompositionRunner.GetCtrlRamRegions(
            SelectedIc,
            SelectedNumber))
        {
            CtrlRamRegions.Add(region);
        }
    }

    private void ClearCtrlRamInspectionDisplay()
    {
        CtrlRamRegions.Clear();
        ReplaceMemoryRangeLabel = string.Empty;
        ReplaceMemoryRows.Clear();
        ReplaceCoverageSegments.Clear();
        ReplaceCoverageGroups.Clear();
        OnPropertyChanged(nameof(ReplaceMemoryRangeLabel));
        OnPropertyChanged(nameof(IsReplaceCoverageGrouped));
        OnPropertyChanged(nameof(IsReplaceCoverageFlat));
    }

    private void ApplyCtrlRamInspectionDisplay(WorkbenchCtrlRamInspectionDisplay display)
    {
        ArgumentNullException.ThrowIfNull(display);

        ReplaceRows(CtrlRamRegions, UiCompositionRunner.GetCtrlRamRegions(display.Regions));
        RefreshReplaceModeState(
            preserveSlotFiles: true,
            ctrlRamInputSlots: UiCompositionRunner.GetCtrlRamReplaceInputSlots(display.InputSlots));
        (
            string rangeLabel,
            IReadOnlyList<MemoryMapRowViewModel> rows,
            IReadOnlyList<MemoryCoverageSegmentViewModel> coverageSegments) =
            UiCompositionRunner.GetReplaceMemoryDisplay(
                display.MemoryDisplay,
                GetSelectedReplaceRegionIds());
        ApplyReplaceMemoryDisplay(rangeLabel, rows, coverageSegments);
    }

    private void RefreshMemoryMapState()
    {
        RefreshMergeMemoryMapState();
        RefreshReplaceMemoryMapState();
    }

    private void RefreshMergeMemoryMapState()
    {
        long? selectedMergeDpInputLength = GetSelectedMergeDpInputLength();
        (
            string rangeLabel,
            IReadOnlyList<MemoryMapRowViewModel> rows,
            IReadOnlyList<MemoryCoverageSegmentViewModel> coverageSegments) = SelectedMergeMode switch
            {
                GeneralMergeMode => UiCompositionRunner.GetGeneralMergeMemoryDisplay(
                    GeneralMergeOutputLength,
                    CreateGeneralMergeMappingInputs()),
                AbCodeMergeMode => UiCompositionRunner.GetAbMergeMemoryDisplay(SelectedIc),
                _ => UiCompositionRunner.GetStandardMergeMemoryDisplay(
                    SelectedIc,
                    selectedMergeDpInputLength),
            };
        MergeMemoryRangeLabel = rangeLabel;
        ReplaceRows(MergeMemoryRows, rows);
        ReplaceRows(MergeCoverageSegments, coverageSegments);

        OnPropertyChanged(nameof(MergeMemoryRangeLabel));
        OnPropertyChanged(nameof(MergeMemorySummary));
    }

    private void RefreshReplaceMemoryMapState()
    {
        if (IsCtrlRamReplaceModeSelected && ReplaceBaseSlot.HasFile)
        {
            if (_baseFirmwareInspectionCache is { } cache &&
                cache.MatchesContext(SelectedIc, ReplaceBaseSlot.FilePath))
            {
                ApplyCtrlRamDisplayFromInspection(cache.Inspection);
            }
            else
            {
                ClearCtrlRamInspectionDisplay();
            }

            return;
        }

        (
            string replaceRangeLabel,
            IReadOnlyList<MemoryMapRowViewModel> replaceRows,
            IReadOnlyList<MemoryCoverageSegmentViewModel> replaceCoverageSegments) = UiCompositionRunner.GetReplaceMemoryDisplay(
            SelectedIc,
            SelectedNumber,
            SelectedReplaceMode,
            GetSelectedReplaceBaseLength(),
            GetSelectedReplaceRegionIds());
        ApplyReplaceMemoryDisplay(replaceRangeLabel, replaceRows, replaceCoverageSegments);

        OnPropertyChanged(nameof(ReplaceOutputFileName));
    }

    private void ApplyReplaceMemoryDisplay(
        string rangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> coverageSegments)
    {
        ReplaceMemoryRangeLabel = rangeLabel;
        ReplaceRows(ReplaceMemoryRows, rows);
        ReplaceRows(ReplaceCoverageSegments, coverageSegments);
        RefreshReplaceCoverageGroups();
        OnPropertyChanged(nameof(ReplaceMemoryRangeLabel));
        OnPropertyChanged(nameof(ReplaceMemorySummary));
        OnPropertyChanged(nameof(IsReplaceCoverageGrouped));
        OnPropertyChanged(nameof(IsReplaceCoverageFlat));
    }

    private long? GetSelectedMergeDpInputLength()
    {
        return WorkbenchCompositionService.IsDpPerspectiveIc(SelectedIc) &&
            TryGetInspectedFileLength(_mergeDpSlot, out long length)
                ? length
                : null;
    }

    private long? GetSelectedReplaceBaseLength()
    {
        return SelectedReplaceMode == DpReplaceMode &&
            WorkbenchCompositionService.HasBuiltInV2DpReplace(SelectedIc) &&
            TryGetInspectedFileLength(ReplaceBaseSlot, out long length)
                ? length
                : null;
    }

    private IEnumerable<string> GetSelectedReplaceRegionIds()
    {
        return ReplaceSlots
            .Where(static slot => slot.HasFile && slot.RegionId is not null)
            .Select(static slot => slot.RegionId!);
    }

    private void RefreshReplaceModeState(
        bool preserveSlotFiles = false,
        IReadOnlyList<FirmwareSlotViewModel>? ctrlRamInputSlots = null)
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
            IReadOnlyList<FirmwareSlotViewModel> inputSlots =
                SelectedReplaceMode == CtrlRamReplaceMode && ctrlRamInputSlots is not null
                    ? ctrlRamInputSlots
                    : UiCompositionRunner.GetReplaceInputSlots(
                        SelectedIc,
                        SelectedNumber,
                        SelectedReplaceMode);
            foreach (FirmwareSlotViewModel slot in inputSlots)
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

    private static void RestorePreservedSlotFile(
        FirmwareSlotViewModel slot,
        Dictionary<string, string?> preservedSlotFiles)
    {
        if (!preservedSlotFiles.TryGetValue(slot.SlotId, out string? filePath) ||
            string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        slot.FilePath = filePath;
    }
}
