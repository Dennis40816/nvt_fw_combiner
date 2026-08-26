using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MergePresentationViewModel
{
    private const string NormalMergeMode = ExperienceIds.StandardMerge;
    private const string AbCodeMergeMode = ExperienceIds.AbMerge;
    private const string GeneralMergeMode = ExperienceIds.GeneralMerge;
    private readonly Dictionary<string, string> _abMergeAddressSpaceBySlotId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CompiledAuthoringInputBinding> _abMergeBindingsByAddressSpace = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FirmwareSlotViewModel> _abMergeSlotsByAddressSpace = new(StringComparer.Ordinal);
    private readonly AuthoringSessionState _standardMergeSession =
        new(ExperienceIds.StandardMerge);
    private readonly AuthoringSessionState _abMergeSession =
        new(ExperienceIds.AbMerge);
    private readonly AuthoringSessionState _generalMergeSession =
        new(ExperienceIds.GeneralMerge);
    private string? _abMergeTopologyChoicesIcId;
    private readonly MergeStateBindings _stateBindings;
    internal FirmwareSlotViewModel MergeDpSlot { get; } = new(
        CompositionSlotIds.MergeDp,
        "DP BIN",
        "Display payload for Standard Merge",
        FirmwareSlotKind.Dp,
        addressSpaceId: CompositionAddressSpaceIds.DpInput);
    internal FirmwareSlotViewModel MergeTpSlot { get; } = new(
        CompositionSlotIds.MergeTp,
        "TP BIN",
        "Touch payload for Standard Merge",
        FirmwareSlotKind.Tp,
        addressSpaceId: CompositionAddressSpaceIds.TpInput);
    internal FirmwareSlotViewModel MergeLdcSlot { get; } = new(
        CompositionSlotIds.MergeLdc,
        "LDC BIN",
        "Optional LDC payload when the selected profile exposes an LDC region",
        FirmwareSlotKind.Dp,
        isOptional: true,
        addressSpaceId: CompositionAddressSpaceIds.LdcInput);
    internal IEnumerable<FirmwareSlotViewModel> StandardMergeSlots
    {
        get
        {
            yield return MergeDpSlot;
            yield return MergeTpSlot;
            yield return MergeLdcSlot;
        }
    }

    internal bool IsStandardMergeSlot(FirmwareSlotViewModel slot)
    {
        return ReferenceEquals(slot, MergeDpSlot) ||
            ReferenceEquals(slot, MergeTpSlot) ||
            ReferenceEquals(slot, MergeLdcSlot);
    }

    private int _generalMergeMappingCounter;
    private string _selectedMergeMode = NormalMergeMode;
    private bool _isApplyingGeneralMergeInitializer;
    private string? _catalogReconciliationPreviousMode;

    public IReadOnlyList<string> MergeModeChoices => !HasSelectedIc
        ? []
        : Array.AsReadOnly(
        [
            .. WorkflowPageModeCatalog.ForPage(ShellPage.Merge).Where(mode =>
                _stateBindings.IsWorkflowAuthorable(SelectedIc, mode)),
        ]);

    public PlanningCardText MergePreview => Text.MergePreview;

    public ObservableCollection<FirmwareSlotViewModel> MergeSlots { get; } = [];

    public ObservableCollection<CapabilityTopologyChoice> AbMergeTopologyChoices { get; } = [];

    public ObservableCollection<MemoryMapRowViewModel> MergeMemoryRows { get; } = [];

    public ObservableCollection<MemoryCoverageSegmentViewModel> MergeCoverageSegments { get; } = [];

    public ObservableCollection<MemoryCoverageSegmentViewModel> MergeCoverageRows { get; } = [];

    public ObservableCollection<GeneralMergeMappingViewModel> GeneralMergeMappings { get; } = [];

    public string MergeMemoryRangeLabel { get; private set; } = string.Empty;

    public string SelectedMergeMode
    {
        get => _selectedMergeMode;
        set => SelectMergeMode(value);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeMemoryRangeLabel))]
    [NotifyPropertyChangedFor(nameof(MergeReadinessStatus))]
    [NotifyPropertyChangedFor(nameof(CanBuildMerge))]
    public partial string GeneralMergeOutputLength { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeMemoryRangeLabel))]
    [NotifyPropertyChangedFor(nameof(MergeReadinessStatus))]
    [NotifyPropertyChangedFor(nameof(CanBuildMerge))]
    public partial string GeneralMergeOutputFillByte { get; set; } = string.Empty;

    public string StandardMergeOutputFileName => HasSelectedIc
        ? ResolveAcceptedOutputFileName(
            _standardMergeSession.CurrentSnapshot,
            _compositionServices.Capabilities.GetStandardMergeProfileSummaries().FirstOrDefault(profile =>
                StringComparer.Ordinal.Equals(profile.IcId, SelectedIc))?
                .DefaultOutputFileName ?? "nvt-fw-combiner-standard-merge.bin")
        : string.Empty;

    public string GeneralMergeOutputFileName => HasSelectedIc
        ? ResolveAcceptedOutputFileName(
            _generalMergeSession.CurrentSnapshot,
            GeneralMergeAuthoringUseCase.GetDefaultOutputFileName(SelectedIc))
        : string.Empty;

    public string AbMergeOutputFileName => HasSelectedIc
        ? ResolveAcceptedOutputFileName(
            _abMergeSession.CurrentSnapshot,
            _compositionServices.Capabilities
                .GetAbMergeProfileSummaries()
                .FirstOrDefault(profile => StringComparer.Ordinal.Equals(profile.IcId, SelectedIc))?
                .DefaultOutputFileName ?? "nvt-fw-combiner-ab-output.bin")
        : string.Empty;

    public string MergeOutputFileName => SelectedMergeMode switch
    {
        GeneralMergeMode => GeneralMergeOutputFileName,
        AbCodeMergeMode => AbMergeOutputFileName,
        _ => StandardMergeOutputFileName,
    };

    private string ResolveAcceptedOutputFileName(
        ActiveSessionSnapshot? session,
        string fallback)
    {
        return session?.HasCurrentInputInspection == true
            ? _compositionServices.OutputNaming.ResolveAcceptedOutput(session).OutputName.FileName
            : session?.ExactCapability?.CompiledComposition.V2Details
                .OutputNamingRequirement.FileNameTemplate ?? fallback;
    }

    public string MergeMemorySummary => Text.GetMergeMemorySummary(
        SelectedMergeMode,
        IsStandardMergeSupported,
        GeneralMergeMappings.Any(mapping => mapping.HasFile));

    public string StandardMergeSupportSummary => Text.GetStandardMergeSupportSummary(
        SelectedIc,
        IsStandardMergeSupported,
        GetRequiredStandardMergeSlotLabels());

    public bool IsNormalMergeModeSelected => string.Equals(SelectedMergeMode, NormalMergeMode, StringComparison.Ordinal);

    public bool IsGeneralMergeModeSelected => string.Equals(SelectedMergeMode, GeneralMergeMode, StringComparison.Ordinal);

    public bool IsAbCodeMergeModeSelected => string.Equals(SelectedMergeMode, AbCodeMergeMode, StringComparison.Ordinal);

    public bool HasAbMergeTopologyChoices => AbMergeTopologyChoices.Count > 0;

    public bool IsAbMergeSupported =>
        HasSelectedIc && _stateBindings.IsWorkflowAuthorable(SelectedIc, AbCodeMergeMode);

    public bool IsStandardMergeSupported =>
        HasSelectedIc && _stateBindings.IsWorkflowAuthorable(SelectedIc, NormalMergeMode);

    public WorkflowInspectionLifecycle Inspection => InspectionLifecycles[SelectedMergeMode];
    internal WorkflowInspectionSet InspectionLifecycles { get; }

    public string MergeReadinessStatus => !HasSelectedIc
        ? Text.NotAvailableLabel
        : Inspection.IsRunning
        ? Text.FirmwareInspectionLoadingStatus
        : IsAbCodeMergeModeSelected
            ? Text.GetAbMergeReadinessStatus(
                SelectedIc,
                IsAbMergeSupported,
                MergeSlots.Count(static slot => slot.HasFile),
                MergeSlots.Count,
                MergeSlots.Count(static slot => slot.IsInputInspectionBlocking),
                MergeSlots.Count(static slot => slot.IsInputInspectionWarning))
            : Text.GetMergeReadinessStatus(
                SelectedMergeMode,
                SelectedIc,
                GetRequiredStandardMergeSlotLabels(),
                IsStandardMergeSupported,
                GeneralMergeMappings.Count(mapping => mapping.HasFile));

    public bool CanBuildMerge => CanRunMerge();

    public CapabilityActionBlocker? PrimaryBuildBlocker => SelectedMergeMode switch
    {
        GeneralMergeMode => ActiveSessionBuildBlockerResolver.Resolve(
            _generalMergeSession.CurrentSnapshot,
            GeneralMergeMode,
            _generalMergeActionReadiness),
        AbCodeMergeMode => ActiveSessionBuildBlockerResolver.Resolve(
            _abMergeSession.CurrentSnapshot,
            AbCodeMergeMode,
            _abMergeActionReadiness),
        _ => ActiveSessionBuildBlockerResolver.Resolve(
            _standardMergeSession.CurrentSnapshot,
            NormalMergeMode),
    };

    public IRelayCommand AddGeneralMergeMappingCommand { get; }

    public IAsyncRelayCommand PreviewMergeCommand { get; }

    public IAsyncRelayCommand BuildMergeCommand { get; }

    internal IReadOnlyDictionary<string, string> AbMergeAddressSpaceBySlotId => _abMergeAddressSpaceBySlotId;

    internal IEnumerable<FirmwareSlotViewModel> AbMergeSlots => _abMergeSlotsByAddressSpace.Values;

    internal IReadOnlyDictionary<string, FirmwareSlotViewModel> AbMergeSlotsByAddressSpace =>
        _abMergeSlotsByAddressSpace;

    private string SelectedIc => _stateBindings.SelectedIc();

    private bool HasSelectedIc => !string.IsNullOrWhiteSpace(SelectedIc);

    private string SelectedNumber => _stateBindings.SelectedNumber();

    private ReportPresentationViewModel Reports => _stateBindings.Reports();

    private Task RunCompositionAsync(
        bool build,
        CompositionRunWork run,
        Action<string, string> loadErrorReport)
    {
        return _stateBindings.RunCompositionAsync(build, run, loadErrorReport);
    }

    internal void SelectMergeMode(string mode)
    {
        if (!MergeModeChoices.Contains(mode, StringComparer.Ordinal) ||
            string.Equals(_selectedMergeMode, mode, StringComparison.Ordinal))
        {
            return;
        }

        InspectionLifecycles[_selectedMergeMode].Invalidate();
        _selectedMergeMode = mode;
        PublishMergeModeSelectionChanged();
        _stateBindings.NotifySharedContextChanged();
        _stateBindings.ResetRunResult();
        _stateBindings.RefreshNumberChoices();
        if (!HasSelectedIc)
        {
            RefreshCommandState();
            return;
        }

        RefreshMergeSlotRequirements();
        if ((IsNormalMergeModeSelected || IsAbCodeMergeModeSelected) &&
            MergeSlots.Any(static slot => slot.HasFile))
        {
            _ = _stateBindings.RefreshSelectedFirmwareInspections();
        }

        RefreshMergeMemoryMapState();
        RefreshCommandState();
    }

    internal bool StageAuthorableModeForCatalogReconciliation(
        Func<string, bool> isAuthorable)
    {
        ArgumentNullException.ThrowIfNull(isAuthorable);
        string nextMode = ResolveAuthorableModeForCatalogReconciliation(isAuthorable);
        if (string.Equals(_selectedMergeMode, nextMode, StringComparison.Ordinal))
        {
            return false;
        }

        _catalogReconciliationPreviousMode = _selectedMergeMode;
        _selectedMergeMode = nextMode;
        return true;
    }

    internal string ResolveAuthorableModeForCatalogReconciliation(
        Func<string, bool> isAuthorable)
    {
        ArgumentNullException.ThrowIfNull(isAuthorable);
        return !string.IsNullOrWhiteSpace(_selectedMergeMode) &&
            isAuthorable(_selectedMergeMode)
            ? _selectedMergeMode
            : WorkflowPageModeCatalog.ForPage(ShellPage.Merge)
                .FirstOrDefault(isAuthorable) ?? string.Empty;
    }

    internal bool StageModeForWorkflowNavigation(string mode, bool isAuthorable)
    {
        if (!isAuthorable ||
            !WorkflowPageModeCatalog.ForPage(ShellPage.Merge)
                .Contains(mode, StringComparer.Ordinal) ||
            string.Equals(_selectedMergeMode, mode, StringComparison.Ordinal))
        {
            return false;
        }

        _selectedMergeMode = mode;
        return true;
    }

    internal void RestoreStagedWorkflowNavigationMode(string mode)
    {
        _selectedMergeMode = mode;
    }

    internal void CommitStagedWorkflowNavigationMode(string previousMode)
    {
        if (!string.IsNullOrWhiteSpace(previousMode))
        {
            InspectionLifecycles[previousMode].Invalidate();
        }
        PublishCatalogReconciledMergeMode();
    }

    internal void PublishCatalogReconciledMergeMode()
    {
        if (_catalogReconciliationPreviousMode is { } previousMode)
        {
            if (previousMode.Length > 0)
            {
                InspectionLifecycles[previousMode].Invalidate();
            }
            _catalogReconciliationPreviousMode = null;
        }
        PublishMergeModeSelectionChanged();
        _stateBindings.ResetRunResult();
    }

    private void PublishMergeModeSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedMergeMode));
        OnPropertyChanged(nameof(Inspection));
        OnPropertyChanged(nameof(IsNormalMergeModeSelected));
        OnPropertyChanged(nameof(IsGeneralMergeModeSelected));
        OnPropertyChanged(nameof(IsAbCodeMergeModeSelected));
        OnPropertyChanged(nameof(MergeOutputFileName));
        OnPropertyChanged(nameof(MergeReadinessStatus));
        OnPropertyChanged(nameof(MergeMemorySummary));
    }

    internal void RefreshContextState()
    {
        RefreshMergeSlotRequirements();
        RefreshMergeMemoryMapState();
        NotifyContextChanged();
    }

    internal void NotifyContextChanged()
    {
        OnPropertyChanged(nameof(IsStandardMergeSupported));
        OnPropertyChanged(nameof(IsAbMergeSupported));
        OnPropertyChanged(nameof(MergeModeChoices));
        OnPropertyChanged(nameof(StandardMergeSupportSummary));
        OnPropertyChanged(nameof(MergePreview));
        OnPropertyChanged(nameof(StandardMergeOutputFileName));
        OnPropertyChanged(nameof(GeneralMergeOutputFileName));
        OnPropertyChanged(nameof(MergeOutputFileName));
        OnPropertyChanged(nameof(AbMergeOutputFileName));
        OnPropertyChanged(nameof(MergeReadinessStatus));
        OnPropertyChanged(nameof(CanBuildMerge));
    }

    internal void RefreshCommandState()
    {
        NotifyCommandStateChanged();
        _stateBindings.RefreshShellCommandState();
    }

    internal void NotifyCommandStateChanged()
    {
        OnPropertyChanged(nameof(CanBuildMerge));
        OnPropertyChanged(nameof(PrimaryBuildBlocker));
        OnPropertyChanged(nameof(MergeReadinessStatus));
    }

    internal void NotifyOutputFileNamesChanged()
    {
        OnPropertyChanged(nameof(StandardMergeOutputFileName));
        OnPropertyChanged(nameof(GeneralMergeOutputFileName));
        OnPropertyChanged(nameof(MergeOutputFileName));
    }

    internal void ApplyGeneralMergeOutputInitializer(string length, string fillByte)
    {
        _isApplyingGeneralMergeInitializer = true;
        try
        {
            GeneralMergeOutputLength = length;
            GeneralMergeOutputFillByte = fillByte;
        }
        finally
        {
            _isApplyingGeneralMergeInitializer = false;
        }

        GeneralInitializerChanged();
    }

    partial void OnGeneralMergeOutputLengthChanged(string value)
    {
        GeneralInitializerChanged();
    }

    partial void OnGeneralMergeOutputFillByteChanged(string value)
    {
        GeneralInitializerChanged();
    }

    private void GeneralInitializerChanged()
    {
        if (_isApplyingGeneralMergeInitializer ||
            _stateBindings.IsWorkflowLoading() ||
            !_stateBindings.IsWorkflowLoaded())
        {
            return;
        }

        RefreshMergeMemoryMapState();
        _stateBindings.ResetRunResult();
        RefreshCommandState();
    }
}
