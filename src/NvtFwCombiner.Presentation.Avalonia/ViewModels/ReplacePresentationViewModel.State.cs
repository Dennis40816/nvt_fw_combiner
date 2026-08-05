using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReplacePresentationViewModel
{
    private const string DpReplaceMode = WorkbenchReplaceModes.Dp;
    private const string CtrlRamReplaceMode = WorkbenchReplaceModes.CtrlRam;
    private const string GeneralReplaceMode = WorkbenchReplaceModes.General;
    private readonly ReplaceAuthoringSessionSet _authoringSessions = new();
    private int _generalReplaceMappingCounter;
    private string _selectedReplaceMode = DpReplaceMode;

    /// <summary>Gets replace mode choices.</summary>
    public IReadOnlyList<string> ReplaceModeChoices { get; } =
    [
        DpReplaceMode,
        CtrlRamReplaceMode,
        GeneralReplaceMode,
    ];

    /// <summary>Gets localized Replace planning-card content.</summary>
    public PlanningCardText ReplacePreview => Text.ReplacePreview;

    /// <summary>Gets the independent General Replace base firmware slot.</summary>
    public FirmwareSlotViewModel ReplaceBaseSlot { get; } = new(
        WorkbenchSlotIds.ReplaceBase,
        "Reference firmware",
        "Complete source image cloned before replacement",
        FirmwareSlotKind.Base);

    /// <summary>Gets replace input slots for the selected replace mode.</summary>
    public ObservableCollection<FirmwareSlotViewModel> ReplaceSlots { get; } = [];

    /// <summary>Gets grouped CtrlRAM replacement slots for dense multi-chip layouts.</summary>
    public ObservableCollection<FirmwareSlotGroupViewModel> ReplaceSlotGroups { get; } = [];

    /// <summary>Gets CtrlRAM region rows for the selected IC and Number.</summary>
    public ObservableCollection<CtrlRamRegionViewModel> CtrlRamRegions { get; } = [];

    /// <summary>Gets visual coverage segments for the selected Replace workflow.</summary>
    public ObservableCollection<MemoryCoverageSegmentViewModel> ReplaceCoverageSegments { get; } = [];

    /// <summary>Gets grouped Replace coverage segments for dense CtrlRAM layouts.</summary>
    public ObservableCollection<MemoryCoverageGroupViewModel> ReplaceCoverageGroups { get; } = [];

    /// <summary>Gets readable memory-map rows for the selected Replace workflow.</summary>
    public ObservableCollection<MemoryMapRowViewModel> ReplaceMemoryRows { get; } = [];

    /// <summary>Gets editable General Replace mapping rows.</summary>
    public ObservableCollection<GeneralReplaceMappingViewModel> GeneralReplaceMappings { get; } = [];

    /// <summary>Gets Replace memory coverage text for the selected IC and Number.</summary>
    public string ReplaceMemoryRangeLabel { get; private set; } = string.Empty;

    /// <summary>Gets or sets the selected Replace mode.</summary>
    public string SelectedReplaceMode
    {
        get => _selectedReplaceMode;
        set => SetSelectedReplaceMode(value);
    }

    /// <summary>Gets the default Replace output file name for the active mode.</summary>
    public string ReplaceOutputFileName => _stateBindings.CreateOutputFileName(
        ReplaceSlots.Concat([ReplaceBaseSlot]));

    /// <summary>Creates the CtrlRAM Replace output name from the confirmed version choice.</summary>
    public string CreateCtrlRamReplaceOutputFileName(WorkbenchCtrlRamFirmwareVersionEdit? edit)
    {
        return _stateBindings.CreateCtrlRamOutputFileName(
            ReplaceSlots.Concat([ReplaceBaseSlot]),
            edit);
    }

    /// <summary>True when CtrlRAM Replace is selected.</summary>
    public bool IsCtrlRamReplaceModeSelected => IsSelectedReplaceModeSupported &&
        string.Equals(SelectedReplaceMode, CtrlRamReplaceMode, StringComparison.Ordinal);

    /// <summary>True when General Replace is selected.</summary>
    public bool IsGeneralReplaceModeSelected => IsSelectedReplaceModeSupported &&
        string.Equals(SelectedReplaceMode, GeneralReplaceMode, StringComparison.Ordinal);

    /// <summary>True when the selected Replace mode uses the fixed slot-card input layout.</summary>
    public bool IsStructuredReplaceModeSelected => IsSelectedReplaceModeSupported &&
        !string.Equals(SelectedReplaceMode, GeneralReplaceMode, StringComparison.Ordinal);

    /// <summary>True when the selected Replace mode uses the flat structured slot-card input layout.</summary>
    public bool IsNonCtrlRamStructuredReplaceModeSelected => IsStructuredReplaceModeSelected &&
        !IsCtrlRamReplaceModeSelected;

    /// <summary>True when Replace coverage should use grouped segment details.</summary>
    public bool IsReplaceCoverageGrouped => IsCtrlRamReplaceModeSelected && ReplaceCoverageGroups.Count > 0;

    /// <summary>True when Replace coverage should use the flat segment details list.</summary>
    public bool IsReplaceCoverageFlat => !IsReplaceCoverageGrouped;

    /// <summary>Description shown under the selected replace mode.</summary>
    public string SelectedReplaceModeDescription => Text.GetReplaceModeDescription(SelectedReplaceMode);

    /// <summary>Selected Replace workflow availability and golden-evidence state.</summary>
    public CapabilityWorkflowReadiness SelectedReplaceWorkflowReadiness =>
        _compositionServices.Capabilities.GetReplaceWorkflowReadiness(SelectedIc, SelectedReplaceMode);

    /// <summary>Localized evidence badge for the selected Replace workflow.</summary>
    public string SelectedReplaceModeEvidenceLabel =>
        Text.GetWorkflowEvidenceLabel(SelectedReplaceWorkflowReadiness);

    /// <summary>Localized evidence reason and opening condition for the selected Replace workflow.</summary>
    public string SelectedReplaceModeEvidenceTooltip =>
        Text.GetWorkflowEvidenceTooltip(SelectedReplaceWorkflowReadiness);

    /// <summary>True when selected Replace has golden parity evidence.</summary>
    public bool IsSelectedReplaceModeGoldenVerified =>
        SelectedReplaceWorkflowReadiness.HasReviewedEvidence;

    /// <summary>True when selected Replace is available with evidence still open.</summary>
    public bool IsSelectedReplaceModeEvidenceGated =>
        SelectedReplaceWorkflowReadiness.IsEvidencePending;

    /// <summary>True when selected Replace has no approved executable/safety contract.</summary>
    public bool IsSelectedReplaceModeUnavailable =>
        !SelectedReplaceWorkflowReadiness.IsAvailable;

    /// <summary>True when Replace build can run for the active mode.</summary>
    public bool CanBuildReplace => CanRunReplace() &&
        !IsCtrlRamFirmwareVersionMetadataLoading &&
        (!IsCtrlRamReplaceModeSelected || HasCurrentCtrlRamActionReadiness(build: true)) &&
        (!IsGeneralReplaceModeSelected || _generalReplaceActionReadiness?.Build.IsAvailable == true);

    /// <summary>Highest-priority typed pre-run blocker for the active Replace workflow.</summary>
    public CapabilityActionBlocker? PrimaryBuildBlocker => SelectedReplaceMode switch
    {
        CtrlRamReplaceMode => ActiveSessionBuildBlockerResolver.Resolve(
            _authoringSessions.CtrlRamReplace.CurrentSnapshot,
            CtrlRamReplaceMode,
            _ctrlRamActionReadiness),
        GeneralReplaceMode => ActiveSessionBuildBlockerResolver.Resolve(
            _authoringSessions.GeneralReplace.CurrentSnapshot,
            GeneralReplaceMode,
            _generalReplaceActionReadiness),
        _ => ActiveSessionBuildBlockerResolver.Resolve(
            _authoringSessions.DpReplace.CurrentSnapshot,
            DpReplaceMode),
    };

    /// <summary>Command that adds a General Replace mapping row.</summary>
    public IRelayCommand AddGeneralReplaceMappingCommand { get; }

    /// <summary>Command that previews Replace through the application core or workbench planner.</summary>
    public IAsyncRelayCommand PreviewReplaceCommand { get; }

    /// <summary>Command that builds Replace output through the application/workbench core.</summary>
    public IAsyncRelayCommand BuildReplaceCommand { get; }

    /// <summary>Command that keeps the source TP firmware version for the current CtrlRAM build.</summary>
    public IRelayCommand SelectCtrlRamFirmwareVersionPreserveCommand { get; }

    /// <summary>Command that enables TP firmware version editing for the current CtrlRAM build.</summary>
    public IRelayCommand SelectCtrlRamFirmwareVersionEditCommand { get; }

    /// <summary>Command that closes the CtrlRAM firmware-version confirmation modal.</summary>
    public IRelayCommand CloseCtrlRamFirmwareVersionCommand { get; }

    private string SelectedIc => _stateBindings.SelectedIc();

    private string SelectedNumber => _stateBindings.SelectedNumber();

    private ReportPresentationViewModel Reports => _stateBindings.Reports();

    private bool IsSelectedReplaceModeSupported =>
        _compositionServices.Capabilities.IsReplaceWorkflowAvailable(SelectedIc, SelectedReplaceMode);

    private Task RunCompositionAsync(
        bool build,
        CompositionRunWork run,
        Action<string, string> loadErrorReport)
    {
        return _stateBindings.RunCompositionAsync(build, run, loadErrorReport);
    }

    private void SetSelectedReplaceMode(string value)
    {
        if (string.Equals(_selectedReplaceMode, value, StringComparison.Ordinal))
        {
            return;
        }

        _selectedReplaceMode = value;
        OnPropertyChanged(nameof(SelectedReplaceMode));
        NotifyModeChanged();
        _stateBindings.ReplaceModeChanged();
    }

    internal void SelectReplaceMode(string mode)
    {
        if (ReplaceModeChoices.Contains(mode, StringComparer.Ordinal))
        {
            SelectedReplaceMode = mode;
        }
    }

    internal void NotifyContextChanged()
    {
        OnPropertyChanged(nameof(ReplacePreview));
        OnPropertyChanged(nameof(SelectedReplaceModeDescription));
        OnPropertyChanged(nameof(SelectedReplaceWorkflowReadiness));
        OnPropertyChanged(nameof(SelectedReplaceModeEvidenceLabel));
        OnPropertyChanged(nameof(SelectedReplaceModeEvidenceTooltip));
        OnPropertyChanged(nameof(IsSelectedReplaceModeGoldenVerified));
        OnPropertyChanged(nameof(IsSelectedReplaceModeEvidenceGated));
        OnPropertyChanged(nameof(IsSelectedReplaceModeUnavailable));
        OnPropertyChanged(nameof(ReplaceOutputFileName));
        OnPropertyChanged(nameof(ReplaceReadinessStatus));
        OnPropertyChanged(nameof(CanBuildReplace));
        OnPropertyChanged(nameof(ReplaceMemorySummary));
        OnPropertyChanged(nameof(CtrlRamFirmwareVersionCurrentValue));
        OnPropertyChanged(nameof(CtrlRamFirmwareVersionMetadataDetail));
        OnPropertyChanged(nameof(CtrlRamFirmwareVersionValidationDetail));
    }

    internal void NotifyOutputFileNamesChanged()
    {
        OnPropertyChanged(nameof(ReplaceOutputFileName));
    }

    private void NotifyModeChanged()
    {
        OnPropertyChanged(nameof(IsCtrlRamReplaceModeSelected));
        OnPropertyChanged(nameof(IsGeneralReplaceModeSelected));
        OnPropertyChanged(nameof(IsStructuredReplaceModeSelected));
        OnPropertyChanged(nameof(IsNonCtrlRamStructuredReplaceModeSelected));
        OnPropertyChanged(nameof(IsReplaceCoverageGrouped));
        OnPropertyChanged(nameof(IsReplaceCoverageFlat));
        NotifyContextChanged();
    }

    internal void RefreshCommandState()
    {
        NotifyCommandStateChanged();
        _stateBindings.RefreshShellCommandState();
    }

    internal void NotifyCommandStateChanged()
    {
        RefreshDpReplaceInputSelectionReadiness();
        PreviewReplaceCommand.NotifyCanExecuteChanged();
        BuildReplaceCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanBuildReplace));
        OnPropertyChanged(nameof(PrimaryBuildBlocker));
        OnPropertyChanged(nameof(ReplaceReadinessStatus));
        RefreshSelectionState();
    }

    internal Task RefreshSelectedFirmwareInspectionsAsync()
    {
        return _stateBindings.RefreshSelectedFirmwareInspections();
    }

    internal void InvalidateCtrlRamFirmwareVersionContextState()
    {
        InvalidateCtrlRamFirmwareVersionContext();
    }
}
