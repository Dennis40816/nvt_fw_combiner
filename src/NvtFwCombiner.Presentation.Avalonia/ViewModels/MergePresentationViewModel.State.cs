using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MergePresentationViewModel
{
    private const string NormalMergeMode = ExperienceIds.StandardMerge;
    private const string AbCodeMergeMode = ExperienceIds.AbMerge;
    private const string GeneralMergeMode = ExperienceIds.GeneralMerge;
    private static readonly IReadOnlyList<string> s_standardMergeModeChoices =
        Array.AsReadOnly([NormalMergeMode, GeneralMergeMode]);
    private static readonly IReadOnlyList<string> s_abMergeModeChoices =
        Array.AsReadOnly([NormalMergeMode, AbCodeMergeMode, GeneralMergeMode]);
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

    /// <summary>Gets executable Merge modes for the selected IC.</summary>
    public IReadOnlyList<string> MergeModeChoices => IsAbMergeSupported
        ? s_abMergeModeChoices
        : s_standardMergeModeChoices;

    /// <summary>Gets localized Merge planning-card content.</summary>
    public PlanningCardText MergePreview => Text.MergePreview;

    /// <summary>Gets merge input slots.</summary>
    public ObservableCollection<FirmwareSlotViewModel> MergeSlots { get; } = [];

    /// <summary>Gets profile-owned symbolic AB topologies when the selected AB IC requires an operator choice.</summary>
    public ObservableCollection<CapabilityTopologyChoice> AbMergeTopologyChoices { get; } = [];

    /// <summary>Gets readable memory-map rows for the selected Merge workflow.</summary>
    public ObservableCollection<MemoryMapRowViewModel> MergeMemoryRows { get; } = [];

    /// <summary>Gets visual final coverage segments for the selected Merge workflow.</summary>
    public ObservableCollection<MemoryCoverageSegmentViewModel> MergeCoverageSegments { get; } = [];

    /// <summary>Gets editable General Merge mapping rows.</summary>
    public ObservableCollection<GeneralMergeMappingViewModel> GeneralMergeMappings { get; } = [];

    /// <summary>Gets Merge memory coverage text for the selected IC.</summary>
    public string MergeMemoryRangeLabel { get; private set; } = string.Empty;

    /// <summary>Gets or sets the selected Merge mode.</summary>
    public string SelectedMergeMode
    {
        get => _selectedMergeMode;
        set => SelectMergeMode(value);
    }

    /// <summary>Gets or sets General Merge output length text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeMemoryRangeLabel))]
    [NotifyPropertyChangedFor(nameof(MergeReadinessStatus))]
    [NotifyPropertyChangedFor(nameof(CanBuildMerge))]
    public partial string GeneralMergeOutputLength { get; set; } = string.Empty;

    /// <summary>Gets or sets General Merge blank-output fill-byte text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeMemoryRangeLabel))]
    [NotifyPropertyChangedFor(nameof(MergeReadinessStatus))]
    [NotifyPropertyChangedFor(nameof(CanBuildMerge))]
    public partial string GeneralMergeOutputFillByte { get; set; } = string.Empty;

    /// <summary>Gets the profile-owned default Standard Merge output file name.</summary>
    public string StandardMergeOutputFileName => ResolveAcceptedOutputFileName(
        _standardMergeSession.CurrentSnapshot,
        "nvt-fw-combiner-standard-merge.bin");

    /// <summary>Gets the default General Merge output file name.</summary>
    public string GeneralMergeOutputFileName => ResolveAcceptedOutputFileName(
        _generalMergeSession.CurrentSnapshot,
        GeneralMergeAuthoringUseCase.GetDefaultOutputFileName(SelectedIc));

    /// <summary>Gets the compiled AB profile output file name.</summary>
    public string AbMergeOutputFileName => ResolveAcceptedOutputFileName(
        _abMergeSession.CurrentSnapshot,
        _compositionServices.Capabilities
            .GetAbMergeProfileSummaries()
            .FirstOrDefault(profile => StringComparer.Ordinal.Equals(profile.IcId, SelectedIc))?
            .DefaultOutputFileName ?? "nvt-fw-combiner-ab-output.bin");

    /// <summary>Gets the active Merge output file name.</summary>
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

    /// <summary>Gets short Merge memory-map summary text.</summary>
    public string MergeMemorySummary => Text.GetMergeMemorySummary(
        SelectedMergeMode,
        IsStandardMergeSupported,
        GeneralMergeMappings.Any(mapping => mapping.HasFile));

    /// <summary>Gets the standard merge support summary for the selected IC.</summary>
    public string StandardMergeSupportSummary => Text.GetStandardMergeSupportSummary(
        SelectedIc,
        IsStandardMergeSupported,
        GetRequiredStandardMergeSlotLabels());

    /// <summary>True when Normal Merge is selected.</summary>
    public bool IsNormalMergeModeSelected => string.Equals(SelectedMergeMode, NormalMergeMode, StringComparison.Ordinal);

    /// <summary>True when General Merge is selected.</summary>
    public bool IsGeneralMergeModeSelected => string.Equals(SelectedMergeMode, GeneralMergeMode, StringComparison.Ordinal);

    /// <summary>True when AB Code Merge is selected.</summary>
    public bool IsAbCodeMergeModeSelected => string.Equals(SelectedMergeMode, AbCodeMergeMode, StringComparison.Ordinal);

    /// <summary>True when the selected AB profile exposes an operator topology selector.</summary>
    public bool HasAbMergeTopologyChoices => AbMergeTopologyChoices.Count > 0;

    /// <summary>True when the selected IC has an admitted AB profile.</summary>
    public bool IsAbMergeSupported =>
        _compositionServices.AbMergeAuthoring.IsAvailable(SelectedIc);

    /// <summary>True when selected IC has a built-in standard merge profile.</summary>
    public bool IsStandardMergeSupported =>
        _compositionServices.StandardMergeAuthoring.IsSupported(SelectedIc);

    /// <summary>Status shown in the Merge inspector.</summary>
    public string MergeReadinessStatus => _stateBindings.IsFirmwareInspectionLoading()
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

    /// <summary>True when active Merge build can run.</summary>
    public bool CanBuildMerge => CanRunMerge();

    /// <summary>Highest-priority typed pre-run blocker for the active Merge workflow.</summary>
    public CapabilityActionBlocker? PrimaryBuildBlocker => SelectedMergeMode switch
    {
        GeneralMergeMode => ActiveSessionBuildBlockerResolver.Resolve(
            _generalMergeSession.CurrentSnapshot,
            GeneralMergeMode,
            _generalMergeActionReadiness),
        AbCodeMergeMode => ActiveSessionBuildBlockerResolver.Resolve(
            _abMergeSession.CurrentSnapshot,
            AbCodeMergeMode),
        _ => ActiveSessionBuildBlockerResolver.Resolve(
            _standardMergeSession.CurrentSnapshot,
            NormalMergeMode),
    };

    /// <summary>Command that adds a General Merge mapping row.</summary>
    public IRelayCommand AddGeneralMergeMappingCommand { get; }

    /// <summary>Command that previews the active Merge through the application core.</summary>
    public IAsyncRelayCommand PreviewMergeCommand { get; }

    /// <summary>Command that builds the active Merge through the application core.</summary>
    public IAsyncRelayCommand BuildMergeCommand { get; }

    internal IReadOnlyDictionary<string, string> AbMergeAddressSpaceBySlotId => _abMergeAddressSpaceBySlotId;

    internal IEnumerable<FirmwareSlotViewModel> AbMergeSlots => _abMergeSlotsByAddressSpace.Values;

    internal IReadOnlyDictionary<string, FirmwareSlotViewModel> AbMergeSlotsByAddressSpace =>
        _abMergeSlotsByAddressSpace;

    private string SelectedIc => _stateBindings.SelectedIc();

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
        string nextMode = MergeModeChoices.Contains(mode, StringComparer.Ordinal)
            ? mode
            : NormalMergeMode;
        if (string.Equals(_selectedMergeMode, nextMode, StringComparison.Ordinal))
        {
            return;
        }

        _selectedMergeMode = nextMode;
        OnPropertyChanged(nameof(SelectedMergeMode));
        OnPropertyChanged(nameof(IsNormalMergeModeSelected));
        OnPropertyChanged(nameof(IsGeneralMergeModeSelected));
        OnPropertyChanged(nameof(IsAbCodeMergeModeSelected));
        OnPropertyChanged(nameof(MergeOutputFileName));
        OnPropertyChanged(nameof(MergeReadinessStatus));
        OnPropertyChanged(nameof(MergeMemorySummary));
        _stateBindings.NotifySharedContextChanged();
        _stateBindings.ResetRunResult();
        _stateBindings.RefreshNumberChoices();
        RefreshMergeSlotRequirements();
        if (IsAbCodeMergeModeSelected && MergeSlots.Any(slot => slot.HasFile))
        {
            _ = _stateBindings.RefreshSelectedFirmwareInspections();
        }

        RefreshMergeMemoryMapState();
        RefreshCommandState();
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
        if (_stateBindings.IsWorkflowLoading() || !_stateBindings.IsWorkflowLoaded())
        {
            return;
        }

        RefreshMergeMemoryMapState();
        _stateBindings.ResetRunResult();
        RefreshCommandState();
    }
}
