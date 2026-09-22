using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReplacePresentationViewModel
{
    [ObservableProperty]
    public partial bool HasMemoryLayoutDisplayError { get; private set; }

    private string? _preparedCtrlRamIc;
    private string? _preparedCtrlRamNumber;
    private CtrlRamInspectionDisplay? _preparedCtrlRamDisplay;
    private MemoryLayoutSnapshot? _ctrlRamMemoryLayout;
    private string? _viewedCtrlRamBankId;

    public bool HasCtrlRamBankView => IsCtrlRamReplaceModeSelected && IsAbCtrlRamReference &&
        _ctrlRamMemoryLayout?.Banks.Count == 2;

    public bool IsViewingCtrlRamBankB
    {
        get => _viewedCtrlRamBankId == "b-bank";
        set
        {
            string id = value ? "b-bank" : "a-bank";
            if (!HasCtrlRamBankView || id == _viewedCtrlRamBankId)
            {
                return;
            }
            _viewedCtrlRamBankId = id;
            RefreshCtrlRamBankOverview();
            PublishReplaceMemoryContext();
        }
    }

    public string CtrlRamBankViewSubtitle => Text.FormatCtrlRamBankView(
        IsViewingCtrlRamBankB,
        IsCtrlRamBothBanksSelected || (IsViewingCtrlRamBankB ? IsCtrlRamBankBSelected : IsCtrlRamBankASelected));

    private void RefreshCtrlRamBankOverview()
    {
        if (_ctrlRamMemoryLayout is not { Banks.Count: 2 } layout)
        {
            return;
        }
        MemoryLayoutBankLocator bank = layout.Banks.Single(item => item.BankId == _viewedCtrlRamBankId);
        ReplaceRows(CtrlRamOverview, UiCompositionRunner.GetMemoryOverview(layout, Text, bank));
    }

    internal void ValidateContextRefresh(string icId, string number, string mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        switch (mode)
        {
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
        PrepareReplaceMemoryMapState();
        PublishFullContext();
    }

    internal void PrepareAcceptedModeContextState(bool preserveSlotFiles = false)
    {
        RefreshReplaceModeState(preserveSlotFiles: preserveSlotFiles);
        PrepareReplaceMemoryMapState();
    }

    internal void ClearUnavailableContextState()
    {
        ClearCtrlRamInspectionDisplay();
        ReplaceSlots.Clear();
        ReplaceRegionGroupBuilder.UpdateSlotGroups(ReplaceSlotGroups, [], Text);
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
        PublishFullContext();
    }

    private void RefreshReplaceSlotGroups()
    {
        ReplaceRegionGroupBuilder.UpdateSlotGroups(
            ReplaceSlotGroups,
            IsCtrlRamReplaceModeSelected
                ? ReplaceSlots.Where(slot => !ReferenceEquals(slot, ReplaceBaseSlot))
                : [],
            Text);
    }

    private void RefreshReplaceCoverageGroups()
    {
        ReplaceCoverageGroups.Clear();
        CtrlRamFocusLanes.Clear();
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
        ReplaceRows(CtrlRamFocusLanes, MemoryFocusLaneViewModel.Create(
            ReplaceCoverageGroups.SelectMany(static group => group.Items), Text,
            isSingleIc: _ctrlRamReplaceSession.CurrentSnapshot?.ExactCapability?.CompiledComposition
                .V2Details.Provenance.ResolvedMap.TopologySelection?.ChipCount == 1));
    }

    internal void ClearCtrlRamInspectionDisplay()
    {
        PrepareClearCtrlRamInspectionDisplay();
        PublishReplaceMemoryContext();
    }

    private void PrepareClearCtrlRamInspectionDisplay()
    {
        _ctrlRamMemoryLayout = null;
        if (!IsAbCtrlRamReference)
        {
            _viewedCtrlRamBankId = null;
        }
        HasMemoryLayoutDisplayError = false;
        CtrlRamRegions.Clear();
        ReplaceMemoryRangeLabel = string.Empty;
        ReplaceMemoryRows.Clear();
        ReplaceCoverageSegments.Clear();
        ReplaceCoverageGroups.Clear();
        CtrlRamFocusLanes.Clear();
        CtrlRamOverview.Clear();
    }

    /// <summary>Returns dynamic CtrlRAM inputs to discovery state after their Base identity is cleared.</summary>
    internal void ClearCtrlRamBaseSelectionState()
    {
        CurrentCtrlRamDraft = null;
        NotifyCtrlRamBankState();
        PrepareClearCtrlRamInspectionDisplay();
        RefreshReplaceModeState();
        PublishAcceptedModeContext();
    }

    internal void ApplyCtrlRamInspectionDisplay(CtrlRamInspectionDisplay display)
    {
        PrepareCtrlRamInspectionDisplay(display);
        PublishAcceptedModeContext();
    }

    private void PrepareCtrlRamInspectionDisplay(CtrlRamInspectionDisplay display)
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
        _ctrlRamMemoryLayout = null;
        ActiveSessionSnapshot? acceptedSession =
            _ctrlRamReplaceSession.CurrentSnapshot;
        IReadOnlyList<MemoryCoverageSegmentViewModel> overview = [];
        (string rangeLabel, IReadOnlyList<MemoryMapRowViewModel> rows,
            IReadOnlyList<MemoryCoverageSegmentViewModel> coverageSegments) result;
        if (acceptedSession?.ExactCapability is null)
        {
            result = UiCompositionRunner.GetPendingMemoryDisplay(
                Text, ReplaceSlots, GetPendingReplaceMemoryPrerequisite());
        }
        else
        {
            try
            {
                result = UiCompositionRunner.GetMemoryDisplay(
                    _compositionServices,
                    acceptedSession,
                    Text,
                    out overview,
                    out MemoryLayoutSnapshot layout,
                    ctrlRamRegions: acceptedSession.DraftState is AbCtrlRamDraftState ? null : display.Regions);
                _ctrlRamMemoryLayout = layout;
                if (layout.Banks.Count == 2 && !layout.Banks.Any(bank => bank.BankId == _viewedCtrlRamBankId))
                {
                    _viewedCtrlRamBankId = IsCtrlRamBankBSelected ? "b-bank" : "a-bank";
                }
            }
            catch (MemoryLayoutDisplayProjectionException)
            {
                // Drop only derived preview state. Input health and Build admission
                // still publish from their accepted Application results.
                ApplyReplaceMemoryDisplay(string.Empty, [], []);
                HasMemoryLayoutDisplayError = true;
                return;
            }
        }
        ApplyReplaceMemoryDisplay(result.rangeLabel, result.rows, result.coverageSegments, overview);
        RefreshCtrlRamBankOverview();
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

        PrepareReplaceMemoryMapState(refreshAuthoring: false);
    }

    internal void RefreshReplaceMemoryMapState(bool refreshAuthoring = true)
    {
        PrepareReplaceMemoryMapState(refreshAuthoring);
        PublishReplaceMemoryContext();
    }

    private void PrepareReplaceMemoryMapState(bool refreshAuthoring = true)
    {
        if (IsCtrlRamReplaceModeSelected && ReplaceBaseSlot.HasFile)
        {
            if (_stateBindings.GetBaseInspection() is { } inspection)
            {
                PrepareCtrlRamInspectionDisplay(FirmwareInspectionProjection.ResolveCtrlRamDisplay(
                    _compositionServices.FirmwareInspection,
                    inspection,
                    SelectedIc,
                    SelectedNumber));
            }
            else
            {
                PrepareClearCtrlRamInspectionDisplay();
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
    }

    private void ApplyReplaceMemoryDisplay(
        string rangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> coverageSegments,
        IReadOnlyList<MemoryCoverageSegmentViewModel>? overview = null)
    {
        HasMemoryLayoutDisplayError = false;
        ReplaceMemoryRangeLabel = rangeLabel;
        ReplaceRows(ReplaceMemoryRows, rows);
        ReplaceRows(ReplaceCoverageSegments, coverageSegments);
        ReplaceRows(CtrlRamOverview, overview ?? []);
        RefreshReplaceCoverageGroups();
    }

    private void PublishReplaceMemoryContext()
    {
        OnPropertyChanged(nameof(ReplaceMemoryRangeLabel));
        OnPropertyChanged(nameof(ReplaceMemorySummary));
        OnPropertyChanged(nameof(HasObservedMemoryChanges));
        NotifyCoverageGroupingChanged();
        OnPropertyChanged(nameof(ReplaceOutputFileName));
    }

    private void NotifyCoverageGroupingChanged()
    {
        OnPropertyChanged(nameof(HasCtrlRamBankView));
        OnPropertyChanged(nameof(IsViewingCtrlRamBankB));
        OnPropertyChanged(nameof(CtrlRamBankViewSubtitle));
        OnPropertyChanged(nameof(CtrlRamStartAddress));
        OnPropertyChanged(nameof(HasCtrlRamFocusLayout));
        OnPropertyChanged(nameof(CtrlRamCapacityLabel));
        OnPropertyChanged(nameof(CtrlRamPositions));
        OnPropertyChanged(nameof(CtrlRamEndAddress));
        OnPropertyChanged(nameof(CtrlRamSharedInputHint));
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
            _ctrlRamMemoryLayout = null;
            HasMemoryLayoutDisplayError = false;
            CtrlRamRegions.Clear();
        }

        Dictionary<string, string?> preservedSlotFiles = preserveSlotFiles
            ? ReplaceSlots
                .Where(slot => !ReferenceEquals(slot, ReplaceBaseSlot))
                .ToDictionary(slot => slot.SlotId, slot => slot.FilePath, StringComparer.Ordinal)
            : new Dictionary<string, string?>(StringComparer.Ordinal);
        ReplaceSlots.Clear();
        if (IsSelectedReplaceModeSupported &&
            SelectedReplaceMode == CtrlRamReplaceMode)
        {
            ReplaceSlots.Add(ReplaceBaseSlot);
            IReadOnlyList<FirmwareSlotViewModel> inputSlots = ctrlRamInputSlots ??
                throw new InvalidOperationException("CtrlRAM mode requires one coherent discovery publication.");
            foreach (FirmwareSlotViewModel slot in inputSlots)
            {
                RestorePreservedSlotFile(slot, preservedSlotFiles);
                ReplaceSlots.Add(slot);
            }

        }

        ApplyFirmwareSlotText();
        RefreshReplaceSlotGroups();
        RefreshCommandState();
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
