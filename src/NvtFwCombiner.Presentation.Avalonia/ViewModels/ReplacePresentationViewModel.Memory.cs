using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReplacePresentationViewModel
{
    private string? _preparedCtrlRamIc;
    private string? _preparedCtrlRamNumber;
    private CtrlRamInspectionDisplay? _preparedCtrlRamDisplay;
    // UI thread only. Typed geometry, pending state and projection failure share one preparation lifetime.
    private ReplaceMemoryDisplayContext _memoryDisplayContext = new();
    private string? _viewedCtrlRamBankId;

    public bool HasCtrlRamBankView => IsCtrlRamReplaceModeSelected && IsAbCtrlRamReference &&
        _memoryDisplayContext.Layout?.CanViewIndividualBanks == true;

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

    private void RefreshCtrlRamBankOverview()
    {
        if (_memoryDisplayContext.Layout is not { CanViewIndividualBanks: true } layout)
        {
            return;
        }
        MemoryLayoutBankLocator? bank = layout.Banks.FirstOrDefault(item => item.BankId == _viewedCtrlRamBankId);
        if (bank is null) { return; }
        CtrlRamOverview.ReplaceAll(UiCompositionRunner.GetMemoryOverview(layout, Text, bank));
        RefreshReplaceCoverageGroups(ReplaceCoverageSegments);
        _memoryDisplayContext = _memoryDisplayContext with { Viewport = bank };
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

    private void RefreshReplaceCoverageGroups(IReadOnlyList<MemoryCoverageSegmentViewModel> segments)
    {
        ReplaceCoverageGroups.Clear();
        CtrlRamFocusLanes.Clear();
        if (!IsCtrlRamReplaceModeSelected ||
            segments.Any(static segment => segment.RegionId is null))
        {
            return;
        }

        foreach (MemoryCoverageGroupViewModel group in ReplaceRegionGroupBuilder.CreateCoverageGroups(
            segments,
            Text))
        {
            ReplaceCoverageGroups.Add(group);
        }
        MemoryLayoutBankLocator? bank = HasCtrlRamBankView
            ? _memoryDisplayContext.Layout!.Banks.SingleOrDefault(item => item.BankId == _viewedCtrlRamBankId)
            : null;
        IEnumerable<MemoryCoverageLogicalItemViewModel> items = bank is null
            ? ReplaceCoverageGroups.SelectMany(static group => group.Items)
            : ReplaceRegionGroupBuilder.CreateLogicalItems(segments.Where(segment =>
                segment.AddressSpaceId == bank.AddressSpaceId && segment.RangeStart >= bank.Range.Start &&
                segment.RangeEndExclusive <= bank.Range.EndExclusive), Text);
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap? localMap = _memoryDisplayContext.Layout?.AfterSegments
            .Select(static segment => segment.BankRegion?.LocalMap).Where(static map => map is not null)
            .DistinctBy(static map => map, ReferenceEqualityComparer.Instance).SingleOrDefault();
        ReplaceRows(CtrlRamFocusLanes, MemoryFocusLaneViewModel.Create(
            items, Text,
            isSingleIc: (localMap ?? _ctrlRamReplaceSession.CurrentSnapshot?.ExactCapability?.CompiledComposition
                .V2Details.Provenance.ResolvedMap)?.TopologySelection?.ChipCount == 1));
    }

    internal void ClearCtrlRamInspectionDisplay()
    {
        PrepareClearCtrlRamInspectionDisplay();
        PublishReplaceMemoryContext();
    }

    private void PrepareClearCtrlRamInspectionDisplay()
    {
        _memoryDisplayContext = new();
        _viewedCtrlRamBankId = null;
        CtrlRamRegions.Clear();
        ReplaceMemoryRangeLabel = string.Empty;
        ReplaceMemoryRows.Clear();
        ReplaceCoverageSegments.Clear();
        ReplaceCoverageGroups.Clear();
        CtrlRamFocusLanes.Clear();
        CtrlRamOverview.Clear();
        (string range, IReadOnlyList<MemoryMapRowViewModel> rows, IReadOnlyList<MemoryCoverageSegmentViewModel> segments) =
            UiCompositionRunner.GetPendingMemoryDisplay(Text, GetReplaceMemorySlots(), GetPendingReplaceMemoryPrerequisite(),
                out MemoryLayoutPendingDisplay pending);
        _memoryDisplayContext = new(Pending: pending);
        ApplyReplaceMemoryDisplay(range, rows, segments);
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
        _memoryDisplayContext = new();
        ActiveSessionSnapshot? acceptedSession =
            _ctrlRamReplaceSession.CurrentSnapshot;
        IReadOnlyList<MemoryCoverageSegmentViewModel> overview = [];
        (string rangeLabel, IReadOnlyList<MemoryMapRowViewModel> rows,
            IReadOnlyList<MemoryCoverageSegmentViewModel> coverageSegments) result;
        if (acceptedSession?.ExactCapability is null)
        {
            result = UiCompositionRunner.GetPendingMemoryDisplay(
                Text, ReplaceSlots, GetPendingReplaceMemoryPrerequisite(), out MemoryLayoutPendingDisplay pending, acceptedSession);
            _memoryDisplayContext = new(Pending: pending);
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
                    ctrlRamRegions: display.Regions);
                _memoryDisplayContext = new(Layout: layout);
                if (!layout.CanViewIndividualBanks)
                {
                    _viewedCtrlRamBankId = null;
                }
                else if (!layout.Banks.Any(bank => bank.BankId == _viewedCtrlRamBankId))
                {
                    _viewedCtrlRamBankId = IsCtrlRamBankBSelected ? "b-bank" : "a-bank";
                }
            }
            catch (MemoryLayoutDisplayProjectionException)
            {
                // Drop only derived preview state. Input health and Build admission
                // still publish from their accepted Application results.
                ApplyReplaceMemoryDisplay(string.Empty, [], []);
                _memoryDisplayContext = new(IsUnavailable: true);
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
        _memoryDisplayContext = new();
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
        ReplaceMemoryRangeLabel = rangeLabel;
        ReplaceRows(ReplaceMemoryRows, rows);
        // Coverage groups assign each slice's interaction state; publish only after, so every bar binds the final state.
        RefreshReplaceCoverageGroups(coverageSegments);
        ReplaceCoverageSegments.ReplaceAll(coverageSegments);
        CtrlRamOverview.ReplaceAll(overview ?? []);
    }

    private void PublishReplaceMemoryContext()
    {
        OutputLayoutBankDisplay? bank = HasCtrlRamBankView ? new(IsViewingCtrlRamBankB,
            Text.FormatCtrlRamBankView(false, IsCtrlRamBothBanksSelected || IsCtrlRamBankASelected),
            Text.FormatCtrlRamBankView(true, IsCtrlRamBothBanksSelected || IsCtrlRamBankBSelected),
            new RelayCommand<bool>(viewBankB => { IsViewingCtrlRamBankB = viewBankB; })) : null;
        OutputLayout.Publish(ReplaceOutputLayoutProjection.Create(
            _memoryDisplayContext, Text, ReplaceMemoryRangeLabel, ReplaceMemoryRows,
            ReplaceCoverageSegments, CtrlRamOverview, CtrlRamFocusLanes,
            HasCtrlRamFocusLayout, bank, GetReplaceMemorySlots(),
            IsGeneralReplaceModeSelected ? _generalReplaceAdmission : null,
            BuildAvailability, Text.GetReplaceMemorySummary(SelectedReplaceMode)));
        OnPropertyChanged(nameof(ReplaceOutputFileName));
    }

    private (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetSelectedReplaceMemoryDisplay()
    {
        ActiveSessionSnapshot? acceptedSession = !HasSelectedIc ? null : SelectedReplaceMode switch
        {
            CtrlRamReplaceMode => _ctrlRamReplaceSession.CurrentSnapshot,
            GeneralReplaceMode => _generalReplaceSession.CurrentSnapshot,
            _ => null,
        };
        if (acceptedSession?.ExactCapability is null)
        {
            (string RangeLabel, IReadOnlyList<MemoryMapRowViewModel> Rows, IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) pendingDisplay =
                UiCompositionRunner.GetPendingMemoryDisplay(
                Text, GetReplaceMemorySlots(), GetPendingReplaceMemoryPrerequisite(),
                out MemoryLayoutPendingDisplay pending, acceptedSession);
            _memoryDisplayContext = new(Pending: pending);
            return pendingDisplay;
        }
        try
        {
            (string RangeLabel, IReadOnlyList<MemoryMapRowViewModel> Rows, IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) display =
                UiCompositionRunner.GetMemoryDisplay(_compositionServices, acceptedSession, Text,
                out _, out MemoryLayoutSnapshot layout,
                IsGeneralReplaceModeSelected ? _generalReplaceAdmission : null);
            _memoryDisplayContext = new(Layout: layout);
            return display;
        }
        catch (MemoryLayoutDisplayProjectionException)
        {
            // Display failure has no authority over the accepted Build admission.
            _memoryDisplayContext = new(IsUnavailable: true);
            return (string.Empty, [], []);
        }
    }

    private IEnumerable<FirmwareSlotViewModel> GetReplaceMemorySlots()
    {
        return !HasSelectedIc ? [] : IsGeneralReplaceModeSelected ? [ReplaceBaseSlot] : ReplaceSlots;
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
            _memoryDisplayContext = new();
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
