using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReplacePresentationViewModel
{
    private string? _preparedDpReplaceIc;
    private CompiledAuthoringSelectionSnapshot? _preparedDpReplaceSnapshot;
    private string? _preparedCtrlRamIc;
    private string? _preparedCtrlRamNumber;
    private CtrlRamInspectionDisplay? _preparedCtrlRamDisplay;

    internal void ValidateContextRefresh(string icId, string number, string mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        switch (mode)
        {
            case DpReplaceMode:
                _preparedDpReplaceIc = null;
                _preparedDpReplaceSnapshot = null;
                FirmwareSlotViewModel[] retainedDpSelections =
                [
                    .. CurrentReplaceInputSlots().DistinctBy(ReplaceInputId),
                ];
                CompiledAuthoringSelectionSnapshot dpSnapshot =
                    ResolveDpReplaceAuthoringSnapshotCore(icId, retainedDpSelections);
                _preparedDpReplaceIc = icId;
                _preparedDpReplaceSnapshot = dpSnapshot;
                break;
            case CtrlRamReplaceMode:
                _preparedCtrlRamIc = null;
                _preparedCtrlRamNumber = null;
                _preparedCtrlRamDisplay = null;
                CtrlRamInspectionDisplay display = _compositionServices.CtrlRamAuthoring
                    .GetDiscoveryDisplay(icId, number);
                _preparedCtrlRamIc = icId;
                _preparedCtrlRamNumber = number;
                _preparedCtrlRamDisplay = display;
                break;
            case GeneralReplaceMode:
                ValidateGeneralReplaceContextRefresh(icId);
                break;
            default:
                throw new InvalidOperationException("Unknown Replace workflow mode.");
        }
    }

    public bool HasObservedMemoryChanges =>
        ReplaceCoverageSegments.Any(static segment => segment.IsChanged);

    public bool ShowsGenericCoverageStateLegend => !IsCtrlRamReplaceModeSelected;

    internal void RefreshContextState(bool preserveSlotFiles = false)
    {
        RefreshReplaceModeState(preserveSlotFiles: preserveSlotFiles);
        RefreshReplaceMemoryMapState();
        NotifyContextChanged();
    }

    internal void ClearUnavailableContextState()
    {
        ClearCtrlRamInspectionDisplay();
        ReplaceSlots.Clear();
        ReplaceSlotGroups.Clear();
        _generalReplaceAuthoringStates = [];
        _generalReplaceDraft = null;
        _generalReplaceAdmission = null;
        _generalReplaceActionReadiness = null;
        _generalReplaceDiagnosticPreviewReport = null;
        foreach (GeneralReplaceMappingViewModel mapping in GeneralReplaceMappings)
        {
            mapping.ApplyAuthoringIssue(null);
            mapping.SetFileSelectionAvailability(
                canSelect: false,
                Text.FirmwareSlotPendingFactDetail);
        }
        InspectionLifecycles[GeneralReplaceMode].Invalidate();
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
        OnPropertyChanged(nameof(HasObservedMemoryChanges));
        NotifyCoverageGroupingChanged();
    }

    /// <summary>Returns dynamic CtrlRAM inputs to discovery state after their Base identity is cleared.</summary>
    internal void ClearCtrlRamBaseSelectionState()
    {
        ClearCtrlRamInspectionDisplay();
        RefreshReplaceModeState();
    }

    internal void ApplyCtrlRamInspectionDisplay(CtrlRamInspectionDisplay display)
    {
        ArgumentNullException.ThrowIfNull(display);

        ReplaceRows(CtrlRamRegions, UiCompositionRunner.GetCtrlRamRegions(display.Regions));
        RefreshReplaceModeState(
            preserveSlotFiles: true,
            ctrlRamInputSlots: UiCompositionRunner.GetCtrlRamReplaceInputSlots(display.InputSlots));
        ApplyCtrlRamMemoryDisplay(display);
    }

    private void ApplyCtrlRamMemoryDisplay(CtrlRamInspectionDisplay display)
    {
        ActiveSessionSnapshot? acceptedSession =
            _ctrlRamReplaceSession.CurrentSnapshot;
        (
            string rangeLabel,
            IReadOnlyList<MemoryMapRowViewModel> rows,
            IReadOnlyList<MemoryCoverageSegmentViewModel> coverageSegments) =
            acceptedSession?.ExactCapability is null
                ? UiCompositionRunner.GetPendingMemoryDisplay(
                    Text,
                    ReplaceSlots,
                    GetPendingReplaceMemoryPrerequisite())
                : UiCompositionRunner.GetMemoryDisplay(
                    _compositionServices,
                    acceptedSession,
                    Text,
                    ctrlRamRegions: display.Regions);
        ApplyReplaceMemoryDisplay(rangeLabel, rows, coverageSegments);
    }

    private void RelocalizeReplaceMemoryMapState()
    {
        if (IsCtrlRamReplaceModeSelected &&
            ReplaceBaseSlot.CurrentInspectionProjection is { } inspection)
        {
            ApplyCtrlRamMemoryDisplay(FirmwareInspectionProjection.ResolveCtrlRamDisplay(
                _firmwareInspection,
                inspection,
                SelectedIc,
                SelectedNumber));
            return;
        }

        RefreshReplaceMemoryMapState(refreshAuthoring: false);
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
        OnPropertyChanged(nameof(HasObservedMemoryChanges));
        NotifyCoverageGroupingChanged();
    }

    private void NotifyCoverageGroupingChanged()
    {
        OnPropertyChanged(nameof(IsReplaceCoverageGrouped));
        OnPropertyChanged(nameof(IsReplaceCoverageFlat));
        OnPropertyChanged(nameof(ReplaceSelectedCoverageItems));
        OnPropertyChanged(nameof(ReplaceBaseCoverageItems));
        OnPropertyChanged(nameof(ReplaceBaseCoverageGroup));
        OnPropertyChanged(nameof(HasReplaceBaseCoverage));
        OnPropertyChanged(nameof(ReplaceSelectedCoverageSummary));
        OnPropertyChanged(nameof(ReplaceBaseCoverageSummary));
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
                ReplaceSlots,
                GetPendingReplaceMemoryPrerequisite())
            : UiCompositionRunner.GetMemoryDisplay(_compositionServices, acceptedSession, Text);
    }

    private MemoryPendingPrerequisite GetPendingReplaceMemoryPrerequisite()
    {
        return SelectedReplaceMode switch
        {
            DpReplaceMode => MemoryPendingPrerequisite.DpBin,
            CtrlRamReplaceMode => MemoryPendingPrerequisite.CtrlRamReplacement,
            _ => MemoryPendingPrerequisite.BaseBin,
        };
    }

    private void RefreshReplaceModeState(
        bool preserveSlotFiles = false,
        IReadOnlyList<FirmwareSlotViewModel>? ctrlRamInputSlots = null)
    {
        if (IsCtrlRamReplaceModeSelected && ctrlRamInputSlots is null)
        {
            CtrlRamInspectionDisplay display = ResolveCtrlRamDiscoveryDisplay(
                SelectedIc,
                SelectedNumber);
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
        CompiledAuthoringSelectionSnapshot? dpProjection = null;
        bool usesPreparedDpProjection = SelectedReplaceMode == DpReplaceMode &&
            string.Equals(_preparedDpReplaceIc, SelectedIc, StringComparison.Ordinal) &&
            _preparedDpReplaceSnapshot is not null;
        if (IsSelectedReplaceModeSupported &&
            SelectedReplaceMode is DpReplaceMode or CtrlRamReplaceMode)
        {
            ReplaceSlots.Add(ReplaceBaseSlot);
            IReadOnlyList<FirmwareSlotViewModel> inputSlots =
                SelectedReplaceMode == CtrlRamReplaceMode && ctrlRamInputSlots is not null
                    ? ctrlRamInputSlots
                    : SelectedReplaceMode == DpReplaceMode
                        ? UiCompositionRunner.GetDpReplaceInputSlots(
                            dpProjection = ResolveDpReplaceAuthoringSnapshot([]))
                        : ctrlRamInputSlots ?? throw new InvalidOperationException(
                            "CtrlRAM mode requires one coherent discovery publication.");
            foreach (FirmwareSlotViewModel slot in inputSlots)
            {
                RestorePreservedSlotFile(slot, preservedSlotFiles);
                ReplaceSlots.Add(slot);
            }

            if (dpProjection is not null)
            {
                _ = _dpReplaceSession.Activate(dpProjection);
                _catalogRefreshDpProjection = usesPreparedDpProjection
                    ? dpProjection
                    : null;
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
        OnPropertyChanged(nameof(ShowsGenericCoverageStateLegend));
        OnPropertyChanged(nameof(IsGeneralReplaceModeSelected));
        OnPropertyChanged(nameof(IsStructuredReplaceModeSelected));
        OnPropertyChanged(nameof(IsNonCtrlRamStructuredReplaceModeSelected));
        OnPropertyChanged(nameof(ReplaceOutputFileName));
        RefreshCommandState(dpProjection);
    }

    private CtrlRamInspectionDisplay ResolveCtrlRamDiscoveryDisplay(
        string icId,
        string number)
    {
        if (string.Equals(_preparedCtrlRamIc, icId, StringComparison.Ordinal) &&
            string.Equals(_preparedCtrlRamNumber, number, StringComparison.Ordinal) &&
            _preparedCtrlRamDisplay is not null)
        {
            CtrlRamInspectionDisplay prepared = _preparedCtrlRamDisplay;
            _preparedCtrlRamIc = null;
            _preparedCtrlRamNumber = null;
            _preparedCtrlRamDisplay = null;
            return prepared;
        }

        return _compositionServices.CtrlRamAuthoring.GetDiscoveryDisplay(icId, number);
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
