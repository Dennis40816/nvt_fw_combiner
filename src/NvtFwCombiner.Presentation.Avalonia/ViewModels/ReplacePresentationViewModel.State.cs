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
    private string _selectedReplaceMode = CtrlRamReplaceMode;
    private string? _catalogReconciliationPreviousMode;

    public IReadOnlyList<string> ReplaceModeChoices =>
        string.IsNullOrWhiteSpace(SelectedIc)
            ? []
            : Array.AsReadOnly(
            [
                .. WorkflowPageModeCatalog.ForPage(ShellPage.Replace).Where(mode =>
                    _stateBindings.IsWorkflowAuthorable(SelectedIc, mode)),
            ]);

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

    public IReadOnlyList<MemoryCoverageLogicalItemViewModel> ReplaceSelectedCoverageItems =>
    [
        .. ReplaceCoverageGroups
            .Where(static group => !group.IsBaseFirmwareGroup)
            .SelectMany(static group => group.Items),
    ];

    public IReadOnlyList<MemoryCoverageLogicalItemViewModel> ReplaceBaseCoverageItems =>
    [
        .. ReplaceCoverageGroups
            .Where(static group => group.IsBaseFirmwareGroup)
            .SelectMany(static group => group.Items),
    ];

    public MemoryCoverageGroupViewModel? ReplaceBaseCoverageGroup =>
        ReplaceCoverageGroups.FirstOrDefault(static group => group.IsBaseFirmwareGroup);

    public bool HasReplaceBaseCoverage => ReplaceBaseCoverageItems.Count > 0;

    public string ReplaceSelectedCoverageSummary =>
        Text.FormatSelectedCtrlRamCoverageSummary(ReplaceSelectedCoverageItems.Count);

    public string ReplaceBaseCoverageSummary => Text.FormatBaseFirmwareCoverageSummary();

    public ObservableCollection<MemoryMapRowViewModel> ReplaceMemoryRows { get; } = [];

    public ObservableCollection<GeneralReplaceMappingViewModel> GeneralReplaceMappings { get; } = [];

    public string ReplaceMemoryRangeLabel { get; private set; } = string.Empty;

    public string SelectedReplaceMode
    {
        get => _selectedReplaceMode;
        set => SetSelectedReplaceMode(value);
    }

    public string ReplaceOutputFileName => HasSelectedIc
        ? ResolveAcceptedOutputFileName(
            SelectedReplaceMode switch
            {
                DpReplaceMode => _dpReplaceSession.CurrentSnapshot,
                CtrlRamReplaceMode => _ctrlRamReplaceSession.CurrentSnapshot,
                GeneralReplaceMode => _generalReplaceSession.CurrentSnapshot,
                _ => null,
            },
            $"{SelectedIc.ToLowerInvariant()}-{SelectedReplaceMode}.bin")
        : string.Empty;

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

    /// <summary>
    /// Selected Replace workflow availability and golden-evidence state, or
    /// null when no IC is selected.
    /// </summary>
    public CapabilityWorkflowReadiness? SelectedReplaceWorkflowReadiness =>
        HasSelectedIc
            ? _compositionServices.Capabilities.GetReplaceWorkflowReadiness(SelectedIc, SelectedReplaceMode)
            : null;

    /// <summary>Localized evidence badge for the selected Replace workflow.</summary>
    public string SelectedReplaceModeEvidenceLabel =>
        SelectedReplaceWorkflowReadiness is { } readiness
            ? Text.GetWorkflowEvidenceLabel(readiness)
            : string.Empty;

    /// <summary>Localized evidence reason and opening condition for the selected Replace workflow.</summary>
    public string SelectedReplaceModeEvidenceTooltip =>
        SelectedReplaceWorkflowReadiness is { } readiness
            ? Text.GetWorkflowEvidenceTooltip(readiness)
            : string.Empty;

    /// <summary>True when selected Replace has golden parity evidence.</summary>
    public bool IsSelectedReplaceModeGoldenVerified =>
        SelectedReplaceWorkflowReadiness?.HasReviewedEvidence == true;

    /// <summary>True when selected Replace is available with evidence still open.</summary>
    public bool IsSelectedReplaceModeEvidenceGated =>
        SelectedReplaceWorkflowReadiness?.IsEvidencePending == true;

    public bool IsSelectedReplaceModeUnavailable =>
        SelectedReplaceWorkflowReadiness is { IsAvailable: false };

    public WorkflowInspectionLifecycle Inspection => InspectionLifecycles[SelectedReplaceMode];
    internal WorkflowInspectionSet InspectionLifecycles { get; }

    public bool CanBuildReplace => CanRunReplace() &&
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

    private bool HasSelectedIc => !string.IsNullOrWhiteSpace(SelectedIc);

    private string SelectedNumber => _stateBindings.SelectedNumber();

    private ReportPresentationViewModel Reports => _stateBindings.Reports();

    private bool IsSelectedReplaceModeSupported =>
        HasSelectedIc &&
        _stateBindings.IsWorkflowAuthorable(SelectedIc, SelectedReplaceMode);

    private Task RunCompositionAsync(
        bool build,
        CompositionRunWork run,
        Action<string, string> loadErrorReport)
    {
        return _stateBindings.RunCompositionAsync(build, run, loadErrorReport);
    }

    private void SetSelectedReplaceMode(string value)
    {
        if (!ReplaceModeChoices.Contains(value, StringComparer.Ordinal))
        {
            return;
        }

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

    internal bool StageAuthorableModeForCatalogReconciliation(
        Func<string, bool> isAuthorable)
    {
        ArgumentNullException.ThrowIfNull(isAuthorable);
        string nextMode = ResolveAuthorableModeForCatalogReconciliation(isAuthorable);
        if (string.Equals(_selectedReplaceMode, nextMode, StringComparison.Ordinal))
        {
            return false;
        }

        _catalogReconciliationPreviousMode = _selectedReplaceMode;
        _selectedReplaceMode = nextMode;
        return true;
    }

    internal string ResolveAuthorableModeForCatalogReconciliation(
        Func<string, bool> isAuthorable)
    {
        ArgumentNullException.ThrowIfNull(isAuthorable);
        return !string.IsNullOrWhiteSpace(_selectedReplaceMode) &&
            isAuthorable(_selectedReplaceMode)
            ? _selectedReplaceMode
            : WorkflowPageModeCatalog.ForPage(ShellPage.Replace)
                .FirstOrDefault(isAuthorable) ?? string.Empty;
    }

    internal bool StageModeForWorkflowNavigation(string mode, bool isAuthorable)
    {
        if (!isAuthorable ||
            !WorkflowPageModeCatalog.ForPage(ShellPage.Replace)
                .Contains(mode, StringComparer.Ordinal) ||
            string.Equals(_selectedReplaceMode, mode, StringComparison.Ordinal))
        {
            return false;
        }

        _selectedReplaceMode = mode;
        return true;
    }

    internal void RestoreStagedWorkflowNavigationMode(string mode)
    {
        _selectedReplaceMode = mode;
    }

    internal void CommitStagedWorkflowNavigationMode(string previousMode)
    {
        if (!string.IsNullOrWhiteSpace(previousMode))
        {
            InspectionLifecycles[previousMode].Invalidate();
        }
        PublishCatalogReconciledReplaceMode();
    }

    internal void PublishCatalogReconciledReplaceMode()
    {
        if (_catalogReconciliationPreviousMode is { } previousMode)
        {
            if (previousMode.Length > 0)
            {
                InspectionLifecycles[previousMode].Invalidate();
            }
            _catalogReconciliationPreviousMode = null;
        }
        OnPropertyChanged(nameof(SelectedReplaceMode));
        OnPropertyChanged(nameof(Inspection));
        NotifyModeChanged();
        _stateBindings.ResetRunResult();
    }

    internal void NotifyContextChanged()
    {
        OnPropertyChanged(nameof(ReplaceModeChoices));
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
        NotifyCommandAvailabilityChanged();
    }

    internal void NotifyCommandAvailabilityChanged()
    {
        PreviewReplaceCommand.NotifyCanExecuteChanged();
        BuildReplaceCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanBuildReplace));
        OnPropertyChanged(nameof(PrimaryBuildBlocker));
        OnPropertyChanged(nameof(ReplaceReadinessStatus));
        RefreshSelectionState();
    }

    internal void InvalidateCtrlRamFirmwareVersionContextState()
    {
        InvalidateCtrlRamFirmwareVersionContext();
    }
}
