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

        OnPropertyChanged(nameof(HasCtrlRamRegions));
        OnPropertyChanged(nameof(CtrlRamRegionSummary));
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
        OnPropertyChanged(nameof(ReplacePreviewUnavailableReason));
        OnPropertyChanged(nameof(ReplaceBuildUnavailableReason));
        OnPropertyChanged(nameof(IsReplaceCoverageGrouped));
        OnPropertyChanged(nameof(IsReplaceCoverageFlat));
    }

    private long? GetSelectedMergeDpInputLength()
    {
        return SelectedIc is "NT51950" or "NT51951" &&
            !string.IsNullOrWhiteSpace(_mergeDpSlot.FilePath) &&
            File.Exists(_mergeDpSlot.FilePath)
                ? new FileInfo(_mergeDpSlot.FilePath).Length
                : null;
    }

    private long? GetSelectedReplaceBaseLength()
    {
        return SelectedReplaceMode == DpReplaceMode &&
            SelectedIc is "NT51950" or "NT51951" &&
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
        ActiveReplaceRows.Clear();
        switch (SelectedReplaceMode)
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

                AddRows(
                    $"{SelectedIc} / {SelectedNumber}: DP Replace input policy is active.",
                    SelectedIc is "NT51950" or "NT51951"
                        ? "DP replacement follows the selected base BIN length: 0x40000, 0x80000, or 0x100000; the original TP range is restored from base."
                        : "Build stays gated until this IC has approved DP Replace source mapping evidence.",
                    SelectedIc == "NT51928"
                        ? "NT51928 exposes an explicit LDC slot; other ICs hide LDC in DP Replace."
                        : "Only DP and TP restore regions are shown for this IC.");
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

                AddRows(
                    $"{SelectedIc} / {SelectedNumber}: {Math.Max(ReplaceSlots.Count - 1, 0)} replaceable CtrlRAM regions.",
                    "Each CtrlRAM region slot may receive its own replacement BIN; empty slots stay from base.",
                    "Preview and Build run the staged Combiner postbuild command sequence.",
                    "Private golden outputs are still required before support parity is claimed.");
                break;
            case GeneralReplaceMode:
                AddRows(
                    $"{SelectedIc} / {SelectedNumber}: General Replace input policy is active.",
                    "Base firmware stays separate; mapping rows define replacement ranges.",
                    "The compiler must approve each explicit range before build.",
                    "Any TP-range mapping must compile with an approved Combiner CRC/header refresh.");
                break;
            default:
                AddRows("Select a replace mode.");
                break;
        }

        RefreshReplaceSlotGroups();
        OnPropertyChanged(nameof(SelectedReplaceModeDescription));
        OnPropertyChanged(nameof(IsDpReplaceModeSelected));
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
