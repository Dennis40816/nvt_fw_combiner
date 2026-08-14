using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReplacePresentationViewModel
{
    internal void RefreshContextState(bool preserveSlotFiles = false)
    {
        RefreshReplaceModeState(preserveSlotFiles: preserveSlotFiles);
        RefreshReplaceMemoryMapState();
        NotifyContextChanged();
    }

    private void RefreshReplaceSlotGroups()
    {
        ReplaceSlotGroups.Clear();
        if (!IsCtrlRamReplaceModeSelected)
        {
            return;
        }

        foreach (FirmwareSlotGroupViewModel group in ReplaceRegionGroupBuilder.CreateSlotGroups(
            ReplaceSlots.Where(slot => !ReferenceEquals(slot, ReplaceBaseSlot)),
            Text))
        {
            ReplaceSlotGroups.Add(group);
        }
    }

    private void RefreshReplaceCoverageGroups()
    {
        ReplaceCoverageGroups.Clear();
        if (!IsCtrlRamReplaceModeSelected ||
            ReplaceCoverageSegments.Any(static segment => segment.RegionId is null))
        {
            return;
        }

        foreach (MemoryCoverageGroupViewModel group in ReplaceRegionGroupBuilder.CreateCoverageGroups(
            ReplaceCoverageSegments,
            Text))
        {
            ReplaceCoverageGroups.Add(group);
        }
    }

    internal void ClearCtrlRamInspectionDisplay()
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

    internal void ApplyCtrlRamInspectionDisplay(CtrlRamInspectionDisplay display)
    {
        ArgumentNullException.ThrowIfNull(display);

        ReplaceRows(CtrlRamRegions, UiCompositionRunner.GetCtrlRamRegions(display.Regions));
        RefreshReplaceModeState(
            preserveSlotFiles: true,
            ctrlRamInputSlots: UiCompositionRunner.GetCtrlRamReplaceInputSlots(display.InputSlots));
        ActiveSessionSnapshot? acceptedSession =
            _ctrlRamReplaceSession.CurrentSnapshot;
        (
            string rangeLabel,
            IReadOnlyList<MemoryMapRowViewModel> rows,
            IReadOnlyList<MemoryCoverageSegmentViewModel> coverageSegments) =
            acceptedSession?.ExactCapability is null
                ? UiCompositionRunner.GetPendingMemoryDisplay(
                    Text,
                    "Select and inspect the required inputs to resolve the compiled memory layout.")
                : UiCompositionRunner.GetMemoryDisplay(
                    _compositionServices,
                    acceptedSession,
                    Text,
                    ctrlRamRegions: display.Regions);
        ApplyReplaceMemoryDisplay(rangeLabel, rows, coverageSegments);
    }

    internal void RefreshReplaceMemoryMapState(bool refreshAuthoring = true)
    {
        if (IsCtrlRamReplaceModeSelected && ReplaceBaseSlot.HasFile)
        {
            if (_stateBindings.GetBaseInspection() is { } inspection)
            {
                ApplyCtrlRamInspectionDisplay(FirmwareInspectionProjection.ResolveCtrlRamDisplay(
                    _compositionServices.FirmwareInspection,
                    inspection,
                    SelectedIc,
                    SelectedNumber));
            }
            else
            {
                ClearCtrlRamInspectionDisplay();
            }

            return;
        }

        if (IsGeneralReplaceModeSelected && refreshAuthoring)
        {
            RefreshGeneralReplaceAuthoringState();
        }

        (
            string replaceRangeLabel,
            IReadOnlyList<MemoryMapRowViewModel> replaceRows,
            IReadOnlyList<MemoryCoverageSegmentViewModel> replaceCoverageSegments) =
            GetSelectedReplaceMemoryDisplay();
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

    private (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetSelectedReplaceMemoryDisplay()
    {
        ActiveSessionSnapshot? acceptedSession = SelectedReplaceMode switch
        {
            DpReplaceMode => _dpReplaceSession.CurrentSnapshot,
            CtrlRamReplaceMode => _ctrlRamReplaceSession.CurrentSnapshot,
            GeneralReplaceMode => _generalReplaceSession.CurrentSnapshot,
            _ => null,
        };
        return acceptedSession?.ExactCapability is null
            ? UiCompositionRunner.GetPendingMemoryDisplay(
                Text,
                "Select and inspect the required inputs to resolve the compiled memory layout.")
            : UiCompositionRunner.GetMemoryDisplay(_compositionServices, acceptedSession, Text);
    }

    private void RefreshReplaceModeState(
        bool preserveSlotFiles = false,
        IReadOnlyList<FirmwareSlotViewModel>? ctrlRamInputSlots = null)
    {
        if (IsCtrlRamReplaceModeSelected && ctrlRamInputSlots is null)
        {
            CtrlRamInspectionDisplay display =
                _compositionServices.CtrlRamAuthoring.GetDiscoveryDisplay(
                    SelectedIc,
                    SelectedNumber,
                    basePath: null);
            ReplaceRows(CtrlRamRegions, UiCompositionRunner.GetCtrlRamRegions(display.Regions));
            ctrlRamInputSlots = UiCompositionRunner.GetCtrlRamReplaceInputSlots(display.InputSlots);
        }
        else if (!IsCtrlRamReplaceModeSelected)
        {
            CtrlRamRegions.Clear();
        }

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
                    : SelectedReplaceMode == DpReplaceMode
                        ? UiCompositionRunner.GetDpReplaceInputSlots(
                            ResolveDpReplaceAuthoringSnapshot([]))
                        : ctrlRamInputSlots ?? throw new InvalidOperationException(
                            "CtrlRAM mode requires one coherent discovery publication.");
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

    private static void ReplaceRows<T>(
        System.Collections.ObjectModel.ObservableCollection<T> target,
        IEnumerable<T> rows)
    {
        target.Clear();
        foreach (T row in rows)
        {
            target.Add(row);
        }
    }
}
