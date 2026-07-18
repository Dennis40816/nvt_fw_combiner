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
        if (IsGeneralMergeModeSelected)
        {
            IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappings = CreateGeneralMergeMappingInputs();
            ReplaceRows(MergeMemoryRows, UiCompositionRunner.GetGeneralMergeMemoryMapRows(
                GeneralMergeOutputLength,
                mappings));
            ReplaceRows(MergeCoverageSegments, UiCompositionRunner.GetGeneralMergeCoverageSegments(
                GeneralMergeOutputLength,
                mappings));
        }
        else
        {
            ReplaceRows(MergeMemoryRows, UiCompositionRunner.GetStandardMergeMemoryMapRows(
                SelectedIc,
                selectedMergeDpInputLength));
            ReplaceRows(MergeCoverageSegments, UiCompositionRunner.GetStandardMergeCoverageSegments(
                SelectedIc,
                selectedMergeDpInputLength));
        }

        ReplaceRows(ReplaceMemoryRows, UiCompositionRunner.GetReplaceMemoryMapRows(
            SelectedIc,
            SelectedNumber,
            SelectedReplaceMode,
            GetSelectedReplaceBaseLength(),
            GetSelectedCtrlRamBasePath()));
        ReplaceRows(ReplaceCoverageSegments, UiCompositionRunner.GetReplaceCoverageSegments(
            SelectedIc,
            SelectedNumber,
            SelectedReplaceMode,
            GetSelectedReplaceBaseLength(),
            GetSelectedCtrlRamBasePath()));
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
            WorkbenchCompositionService.IsDpPerspectiveIc(SelectedIc) &&
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
        switch (IsSelectedReplaceModeSupported ? SelectedReplaceMode : null)
        {
            case DpReplaceMode:
                ReplaceSlots.Add(ReplaceBaseSlot);
                foreach (FirmwareSlotViewModel slot in UiCompositionRunner.GetReplaceInputSlots(
                    SelectedIc,
                    SelectedNumber,
                    SelectedReplaceMode))
                {
                    RestorePreservedSlotFile(slot, preservedSlotFiles);
                    ReplaceSlots.Add(slot);
                }

                break;
            case CtrlRamReplaceMode:
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

                break;
            case GeneralReplaceMode:
                break;
            default:
                break;
        }

        ApplyFirmwareSlotText();
        RefreshReplaceSlotGroups();
        OnPropertyChanged(nameof(SelectedReplaceModeDescription));
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
