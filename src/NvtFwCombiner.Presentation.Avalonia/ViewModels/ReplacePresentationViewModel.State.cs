using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReplacePresentationViewModel
{
    private const string DpReplaceMode = ExperienceIds.DpReplace;
    private const string CtrlRamReplaceMode = ExperienceIds.CtrlRamReplace;
    private const string GeneralReplaceMode = ExperienceIds.GeneralReplace;
    private readonly AuthoringSessionState _dpReplaceSession =
        new(ExperienceIds.DpReplace);
    private readonly AuthoringSessionState _ctrlRamReplaceSession =
        new(ExperienceIds.CtrlRamReplace);
    private readonly AuthoringSessionState _generalReplaceSession =
        new(ExperienceIds.GeneralReplace);
    private int _generalReplaceMappingCounter;
    private string _selectedReplaceMode = DpReplaceMode;

    public IReadOnlyList<string> ReplaceModeChoices { get; } =
    [
        DpReplaceMode,
        CtrlRamReplaceMode,
        GeneralReplaceMode,
    ];

    public PlanningCardText ReplacePreview => Text.ReplacePreview;

    /// <summary>Gets the independent General Replace base firmware slot.</summary>
    public FirmwareSlotViewModel ReplaceBaseSlot { get; } = new(
        CompositionSlotIds.ReplaceBase,
        "Reference firmware",
        "Complete source image cloned before replacement",
        FirmwareSlotKind.Base,
        addressSpaceId: CompositionAddressSpaceIds.ReferenceBase);

    public ObservableCollection<FirmwareSlotViewModel> ReplaceSlots { get; } = [];

    public ObservableCollection<FirmwareSlotGroupViewModel> ReplaceSlotGroups { get; } = [];

    public ObservableCollection<CtrlRamRegionViewModel> CtrlRamRegions { get; } = [];

    public ObservableCollection<MemoryCoverageSegmentViewModel> ReplaceCoverageSegments { get; } = [];

    public ObservableCollection<MemoryCoverageGroupViewModel> ReplaceCoverageGroups { get; } = [];

    public ObservableCollection<MemoryMapRowViewModel> ReplaceMemoryRows { get; } = [];

    public ObservableCollection<GeneralReplaceMappingViewModel> GeneralReplaceMappings { get; } = [];

    public string ReplaceMemoryRangeLabel { get; private set; } = string.Empty;

    public string SelectedReplaceMode
    {
        get => _selectedReplaceMode;
        set => SetSelectedReplaceMode(value);
    }

    public string ReplaceOutputFileName => ResolveAcceptedOutputFileName(
        SelectedReplaceMode switch
        {
            DpReplaceMode => _dpReplaceSession.CurrentSnapshot,
            CtrlRamReplaceMode => _ctrlRamReplaceSession.CurrentSnapshot,
            GeneralReplaceMode => _generalReplaceSession.CurrentSnapshot,
            _ => null,
        },
        $"{SelectedIc.ToLowerInvariant()}-{SelectedReplaceMode}.bin");

    public string CreateCtrlRamReplaceOutputFileName(CtrlRamFirmwareVersionDraftState? edit)
    {
        CtrlRamAuthoringTransitionResult transition =
            _compositionServices.CtrlRamAuthoring.TransitionFirmwareVersionCompilation(
                _ctrlRamReplaceSession,
                SelectedIc,
                SelectedNumber,
                CreateReplaceSlotPaths(),
                edit);
        ActiveSessionSnapshot acceptedSession = transition.Succeeded
            ? transition.Session!
            : throw new InvalidOperationException(
                string.Join("; ", transition.Issues.Select(static issue => issue.Message)));

        return ResolveAcceptedOutputFileName(
            acceptedSession,
            $"{SelectedIc.ToLowerInvariant()}-ctrlram-replace.bin");
    }

    private string ResolveAcceptedOutputFileName(
        ActiveSessionSnapshot? session,
        string fallback)
    {
        return session?.HasCurrentInputInspection == true
            ? _compositionServices.OutputNaming.ResolveAcceptedOutput(session).OutputName.FileName
            : session?.ExactCapability?.CompiledComposition.V2Details
                .OutputNamingRequirement.FileNameTemplate ?? fallback;
    }

    public bool IsCtrlRamReplaceModeSelected => IsSelectedReplaceModeSupported &&
        string.Equals(SelectedReplaceMode, CtrlRamReplaceMode, StringComparison.Ordinal);

    public bool IsGeneralReplaceModeSelected => IsSelectedReplaceModeSupported &&
        string.Equals(SelectedReplaceMode, GeneralReplaceMode, StringComparison.Ordinal);

    public bool IsStructuredReplaceModeSelected => IsSelectedReplaceModeSupported &&
        !string.Equals(SelectedReplaceMode, GeneralReplaceMode, StringComparison.Ordinal);

    public bool IsNonCtrlRamStructuredReplaceModeSelected => IsStructuredReplaceModeSelected &&
        !IsCtrlRamReplaceModeSelected;

    public bool IsReplaceCoverageGrouped => IsCtrlRamReplaceModeSelected && ReplaceCoverageGroups.Count > 0;

    public bool IsReplaceCoverageFlat => !IsReplaceCoverageGrouped;

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

    public bool IsSelectedReplaceModeUnavailable =>
        !SelectedReplaceWorkflowReadiness.IsAvailable;

    public WorkflowInspectionLifecycle Inspection => InspectionLifecycles[SelectedReplaceMode];
    internal WorkflowInspectionSet InspectionLifecycles { get; }

    public bool CanBuildReplace => CanRunReplace() &&
        !IsCtrlRamFirmwareVersionMetadataLoading &&
        (!IsCtrlRamReplaceModeSelected || HasCurrentCtrlRamActionReadiness(build: true)) &&
        (!IsGeneralReplaceModeSelected || _generalReplaceActionReadiness?.Build.IsAvailable == true);

    public CapabilityActionBlocker? PrimaryBuildBlocker => SelectedReplaceMode switch
    {
        CtrlRamReplaceMode => ActiveSessionBuildBlockerResolver.Resolve(
            _ctrlRamReplaceSession.CurrentSnapshot,
            CtrlRamReplaceMode,
            _ctrlRamActionReadiness),
        GeneralReplaceMode => ActiveSessionBuildBlockerResolver.Resolve(
            _generalReplaceSession.CurrentSnapshot,
            GeneralReplaceMode,
            _generalReplaceActionReadiness),
        _ => ActiveSessionBuildBlockerResolver.Resolve(
            _dpReplaceSession.CurrentSnapshot,
            DpReplaceMode),
    };

    public IRelayCommand AddGeneralReplaceMappingCommand { get; }

    public IAsyncRelayCommand PreviewReplaceCommand { get; }

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
        OnPropertyChanged(nameof(Inspection));
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
