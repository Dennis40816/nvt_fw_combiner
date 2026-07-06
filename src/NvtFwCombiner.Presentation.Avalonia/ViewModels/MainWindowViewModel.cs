using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>View model for the production-backed firmware workbench.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private const string DpReplaceMode = "DP";
    private const string CtrlRamReplaceMode = "CtrlRAM";
    private const string GeneralReplaceMode = "General";
    private const string NormalMergeMode = "Normal";
    private const string AbCodeMergeMode = "AB Code";
    private const string GeneralMergeMode = "General";
    private const string MergeDpSlotId = "merge-dp";
    private const string MergeTpSlotId = "merge-tp";
    private const string MergeLdSlotId = "merge-ld";
    private const string ReplaceBaseSlotId = "replace-base";

    private readonly FirmwareSlotViewModel _mergeDpSlot = new(
        MergeDpSlotId,
        "DP BIN",
        "Display payload for Standard Merge",
        kind: FirmwareSlotKind.Dp);
    private readonly FirmwareSlotViewModel _mergeTpSlot = new(
        MergeTpSlotId,
        "TP BIN",
        "Touch payload for Standard Merge",
        kind: FirmwareSlotKind.Tp);
    private readonly FirmwareSlotViewModel _mergeLdSlot = new(
        MergeLdSlotId,
        "LD BIN",
        "Required only when the selected profile uses LD",
        isOptional: true,
        kind: FirmwareSlotKind.Dp);
    private int _generalReplaceMappingCounter;
    private int _generalMergeMappingCounter;
    private string _selectedMergeMode = NormalMergeMode;

    /// <summary>Initializes the main workbench view model.</summary>
    public MainWindowViewModel(
        string shellVersion,
        string appVersion,
        string workspaceTitle,
        string workspaceSummary,
        string previewActionLabel,
        string buildActionLabel,
        string reportModalActionLabel,
        string deviceContextTitle,
        string icLabel,
        string numberLabel,
        string deviceContextStatus,
        PlanningCardViewModel settingsPreview,
        PlanningCardViewModel mergePreview,
        PlanningCardViewModel replacePreview,
        string footerStatus)
    {
        ShellVersion = shellVersion;
        AppVersion = appVersion;
        WorkspaceTitle = workspaceTitle;
        WorkspaceSummary = workspaceSummary;
        PreviewActionLabel = previewActionLabel;
        BuildActionLabel = buildActionLabel;
        ReportModalActionLabel = reportModalActionLabel;
        DeviceContextTitle = deviceContextTitle;
        IcLabel = icLabel;
        NumberLabel = numberLabel;
        DeviceContextRefreshSummary = deviceContextStatus;
        SettingsPreview = settingsPreview;
        MergePreview = mergePreview;
        ReplacePreview = replacePreview;
        FooterStatus = footerStatus;
        ShowHomeCommand = new RelayCommand(() => SetSelectedPage(ShellPage.Home));
        ShowSettingsCommand = new RelayCommand(() => SetSelectedPage(ShellPage.Settings));
        ShowMergeCommand = new RelayCommand(() => SetSelectedPage(ShellPage.Merge));
        ShowReplaceCommand = new RelayCommand(() => SetSelectedPage(ShellPage.Replace));
        GoBackCommand = new RelayCommand(GoBack, () => CanGoBack);
        ShowDpReplaceCommand = new RelayCommand(() => SelectReplaceMode(DpReplaceMode));
        ShowCtrlRamReplaceCommand = new RelayCommand(() => SelectReplaceMode(CtrlRamReplaceMode));
        ShowGeneralReplaceCommand = new RelayCommand(() => SelectReplaceMode(GeneralReplaceMode));
        ShowNormalMergeCommand = new RelayCommand(() => SelectMergeMode(NormalMergeMode));
        AddGeneralReplaceMappingCommand = new RelayCommand(AddGeneralReplaceMapping);
        RemoveGeneralReplaceMappingCommand = new RelayCommand<GeneralReplaceMappingViewModel>(
            RemoveGeneralReplaceMapping);
        AddGeneralMergeMappingCommand = new RelayCommand(AddGeneralMergeMapping);
        RemoveGeneralMergeMappingCommand = new RelayCommand<GeneralMergeMappingViewModel>(
            RemoveGeneralMergeMapping);
        PreviewMergeCommand = new AsyncRelayCommand(
            () => RunMergeAsync(build: false),
            CanRunMerge);
        BuildMergeCommand = new AsyncRelayCommand(
            () => RunMergeAsync(build: true),
            () => CanBuildMerge);
        PreviewReplaceCommand = new AsyncRelayCommand(
            () => RunReplaceAsync(build: false),
            CanRunReplace);
        BuildReplaceCommand = new AsyncRelayCommand(
            () => RunReplaceAsync(build: true),
            () => CanBuildReplace);
        ShowReportCommand = new RelayCommand(ShowReport, () => CanOpenReport);
        CloseReportCommand = new RelayCommand(CloseReport);
        DismissReportToastCommand = new RelayCommand(DismissReportToast);
        OpenReportHistoryEntryCommand = new RelayCommand<ReportHistoryEntryViewModel>(OpenReportHistoryEntry);
        ShowReplaceSelectionCommand = new RelayCommand(ShowReplaceSelection);
        CloseReplaceSelectionCommand = new RelayCommand(CloseReplaceSelection);

        AddGeneralReplaceMapping();
        AddGeneralMergeMapping();
        NavigationTrail.Add(CreateNavigationEntry(ShellPage.Home, isCurrent: true));
        RefreshContextState();
        RefreshSettingsState();
    }

    /// <summary>Gets the shell milestone label.</summary>
    public string ShellVersion { get; }

    /// <summary>Gets the product version.</summary>
    public string AppVersion { get; }

    /// <summary>Gets the workspace title.</summary>
    public string WorkspaceTitle { get; }

    /// <summary>Gets the workspace summary.</summary>
    public string WorkspaceSummary { get; }

    /// <summary>Gets the preview action label.</summary>
    public string PreviewActionLabel { get; }

    /// <summary>Gets the build action label.</summary>
    public string BuildActionLabel { get; }

    /// <summary>Gets the report modal action label.</summary>
    public string ReportModalActionLabel { get; }

    /// <summary>Gets the shared device context heading.</summary>
    public string DeviceContextTitle { get; }

    /// <summary>Gets the IC field label.</summary>
    public string IcLabel { get; }

    /// <summary>Gets the IC count/variant field label.</summary>
    public string NumberLabel { get; }

    /// <summary>Gets the shared device context status text.</summary>
    public string DeviceContextStatus => IsNumberSelectorVisible
        ? $"{SelectedIc} / {SelectedNumber}: {DeviceContextRefreshSummary}"
        : $"{SelectedIc}: {DeviceContextRefreshSummary}";

    /// <summary>Gets selectable IC choices from the current catalog.</summary>
    public IReadOnlyList<string> IcChoices { get; } = UiCompositionRunner.GetSupportedIcIds();

    /// <summary>Gets replace mode choices.</summary>
    public IReadOnlyList<string> ReplaceModeChoices { get; } =
    [
        DpReplaceMode,
        CtrlRamReplaceMode,
        GeneralReplaceMode,
    ];

    /// <summary>Gets merge mode choices reserved in the product taxonomy.</summary>
    public IReadOnlyList<string> MergeModeChoices { get; } =
    [
        NormalMergeMode,
        AbCodeMergeMode,
        GeneralMergeMode,
    ];

    /// <summary>Gets settings card content.</summary>
    public PlanningCardViewModel SettingsPreview { get; }

    /// <summary>Gets merge card content.</summary>
    public PlanningCardViewModel MergePreview { get; }

    /// <summary>Gets replace card content.</summary>
    public PlanningCardViewModel ReplacePreview { get; }

    /// <summary>Gets footer status content.</summary>
    public string FooterStatus { get; }

    /// <summary>Gets merge input slots.</summary>
    public ObservableCollection<FirmwareSlotViewModel> MergeSlots { get; } = [];

    /// <summary>Gets the independent General Replace base firmware slot.</summary>
    public FirmwareSlotViewModel ReplaceBaseSlot { get; } = new(
        ReplaceBaseSlotId,
        "Base flash BIN",
        "Reference firmware image before replacement",
        kind: FirmwareSlotKind.Base);

    /// <summary>Gets replace input slots for the selected replace mode.</summary>
    public ObservableCollection<FirmwareSlotViewModel> ReplaceSlots { get; } = [];

    /// <summary>Gets grouped CtrlRAM replacement slots for dense multi-chip layouts.</summary>
    public ObservableCollection<FirmwareSlotGroupViewModel> ReplaceSlotGroups { get; } = [];

    /// <summary>Gets replace inspector rows for the selected replace mode.</summary>
    public ObservableCollection<string> ActiveReplaceRows { get; } = [];

    /// <summary>Gets merge inspector rows for the selected IC and Number.</summary>
    public ObservableCollection<string> ActiveMergeRows { get; } = [];

    /// <summary>Gets CtrlRAM region rows for the selected IC and Number.</summary>
    public ObservableCollection<CtrlRamRegionViewModel> CtrlRamRegions { get; } = [];

    /// <summary>Gets readable memory-map rows for the selected Merge workflow.</summary>
    public ObservableCollection<MemoryMapRowViewModel> MergeMemoryRows { get; } = [];

    /// <summary>Gets visual final coverage segments for the selected Standard Merge workflow.</summary>
    public ObservableCollection<MemoryCoverageSegmentViewModel> MergeCoverageSegments { get; } = [];

    /// <summary>Gets visual coverage segments for the selected Replace workflow.</summary>
    public ObservableCollection<MemoryCoverageSegmentViewModel> ReplaceCoverageSegments { get; } = [];

    /// <summary>Gets grouped Replace coverage segments for dense CtrlRAM layouts.</summary>
    public ObservableCollection<MemoryCoverageGroupViewModel> ReplaceCoverageGroups { get; } = [];

    /// <summary>Gets readable memory-map rows for the selected Replace workflow.</summary>
    public ObservableCollection<MemoryMapRowViewModel> ReplaceMemoryRows { get; } = [];

    /// <summary>Gets editable General Replace mapping rows.</summary>
    public ObservableCollection<GeneralReplaceMappingViewModel> GeneralReplaceMappings { get; } = [];

    /// <summary>Gets editable General Merge mapping rows.</summary>
    public ObservableCollection<GeneralMergeMappingViewModel> GeneralMergeMappings { get; } = [];

    /// <summary>Gets Merge memory coverage text for the selected IC.</summary>
    public string MergeMemoryRangeLabel => IsGeneralMergeModeSelected
        ? UiCompositionRunner.GetGeneralMergeMemoryRangeLabel(GeneralMergeOutputLength)
        : UiCompositionRunner.GetStandardMergeMemoryRangeLabel(
            SelectedIc,
            GetSelectedMergeDpInputLength());

    /// <summary>Gets the profile-owned default Standard Merge output file name.</summary>
    public string StandardMergeOutputFileName => UiCompositionRunner.GetStandardMergeDefaultOutputFileName(SelectedIc);

    /// <summary>Gets the default General Merge output file name.</summary>
    public string GeneralMergeOutputFileName => UiCompositionRunner.GetGeneralMergeDefaultOutputFileName(SelectedIc);

    /// <summary>Gets the active Merge output file name.</summary>
    public string MergeOutputFileName => IsGeneralMergeModeSelected
        ? GeneralMergeOutputFileName
        : StandardMergeOutputFileName;

    /// <summary>Gets Replace memory coverage text for the selected IC and Number.</summary>
    public string ReplaceMemoryRangeLabel => UiCompositionRunner.GetReplaceMemoryRangeLabel(
        SelectedIc,
        SelectedNumber,
        SelectedReplaceMode,
        GetSelectedReplaceBaseLength(),
        GetSelectedCtrlRamBasePath());

    /// <summary>Gets the default Replace output file name for the active mode.</summary>
    public string ReplaceOutputFileName => UiCompositionRunner.GetReplaceDefaultOutputFileName(
        SelectedIc,
        SelectedReplaceMode);

    /// <summary>Gets short Merge memory-map summary text.</summary>
    public string MergeMemorySummary => SelectedMergeMode switch
    {
        NormalMergeMode when IsStandardMergeSupported => "The bar shows which input file occupies each final flash position.",
        NormalMergeMode => "No merge profile is available for the selected IC.",
        GeneralMergeMode => "The bar starts reserved and marks each explicit source mapping written into the output.",
        _ => "This merge mode is reserved.",
    };

    /// <summary>Gets the latest UI-triggered run summary.</summary>
    public UiRunResultViewModel LastRunResult { get; private set; } = new(
        "No run yet",
        "Drop required BIN files, then run Preview.",
        "No output",
        succeeded: true);

    /// <summary>True when the selected CtrlRAM catalog has visible rows.</summary>
    public bool HasCtrlRamRegions => CtrlRamRegions.Count > 0;

    /// <summary>Gets selected CtrlRAM row summary text.</summary>
    public string CtrlRamRegionSummary => string.Equals(SelectedNumber, "single", StringComparison.OrdinalIgnoreCase)
        ? $"{SelectedIc} single: TP Overview regions that require multi-chip context are hidden."
        : $"{SelectedIc} {SelectedNumber}: TP Overview CtrlRAM regions are loaded from the production flash-map catalog.";

    /// <summary>Gets the standard merge support summary for the selected IC.</summary>
    public string StandardMergeSupportSummary => IsStandardMergeSupported
        ? $"{SelectedIc}: Standard Merge profile found. Required slots: {GetRequiredStandardMergeSlotLabels()}."
        : $"{SelectedIc}: no Standard Merge profile yet.";

    /// <summary>Gets the Replace card status for the home screen.</summary>
    public string HomeReplaceStatus => $"{SelectedIc} / {SelectedNumber}: {CtrlRamRegions.Count} CtrlRAM regions";

    /// <summary>Gets the Merge card status for the home screen.</summary>
    public string HomeMergeStatus => IsStandardMergeSupported
        ? $"{SelectedIc}: standard merge ready"
        : $"{SelectedIc}: no merge profile";

    /// <summary>Gets the selected shell page.</summary>
    public ShellPage SelectedPage { get; private set; } = ShellPage.Home;

    /// <summary>Gets or sets the selected Merge quick-jump mode.</summary>
    public string SelectedMergeMode
    {
        get => _selectedMergeMode;
        set => SelectMergeMode(value);
    }

    /// <summary>True when the clean home view is visible.</summary>
    public bool IsHomeVisible => SelectedPage == ShellPage.Home;

    /// <summary>True when the Settings page is visible.</summary>
    public bool IsSettingsVisible => SelectedPage == ShellPage.Settings;

    /// <summary>True when the Merge page is visible.</summary>
    public bool IsMergeVisible => SelectedPage == ShellPage.Merge;

    /// <summary>True when the Replace page is visible.</summary>
    public bool IsReplaceVisible => SelectedPage == ShellPage.Replace;

    /// <summary>True when DP Replace is selected.</summary>
    public bool IsDpReplaceModeSelected => string.Equals(SelectedReplaceMode, DpReplaceMode, StringComparison.Ordinal);

    /// <summary>True when CtrlRAM Replace is selected.</summary>
    public bool IsCtrlRamReplaceModeSelected => string.Equals(SelectedReplaceMode, CtrlRamReplaceMode, StringComparison.Ordinal);

    /// <summary>True when General Replace is selected.</summary>
    public bool IsGeneralReplaceModeSelected => string.Equals(SelectedReplaceMode, GeneralReplaceMode, StringComparison.Ordinal);

    /// <summary>True when the selected Replace mode uses the fixed slot-card input layout.</summary>
    public bool IsStructuredReplaceModeSelected => !IsGeneralReplaceModeSelected;

    /// <summary>True when the selected Replace mode uses the flat structured slot-card input layout.</summary>
    public bool IsNonCtrlRamStructuredReplaceModeSelected => IsStructuredReplaceModeSelected && !IsCtrlRamReplaceModeSelected;

    /// <summary>True when Replace coverage should use grouped segment details.</summary>
    public bool IsReplaceCoverageGrouped => IsCtrlRamReplaceModeSelected && ReplaceCoverageGroups.Count > 0;

    /// <summary>True when Replace coverage should use the flat segment details list.</summary>
    public bool IsReplaceCoverageFlat => !IsReplaceCoverageGrouped;

    /// <summary>True when Normal Merge is selected.</summary>
    public bool IsNormalMergeModeSelected => string.Equals(SelectedMergeMode, NormalMergeMode, StringComparison.Ordinal);

    /// <summary>True when General Merge is selected.</summary>
    public bool IsGeneralMergeModeSelected => string.Equals(SelectedMergeMode, GeneralMergeMode, StringComparison.Ordinal);

    /// <summary>True when the reserved AB Code Merge option is selected.</summary>
    public bool IsAbCodeMergeModeSelected => string.Equals(SelectedMergeMode, AbCodeMergeMode, StringComparison.Ordinal);

    /// <summary>True when selected IC has a built-in standard merge profile.</summary>
    public bool IsStandardMergeSupported => UiCompositionRunner.IsStandardMergeSupported(SelectedIc);

    /// <summary>Description shown under the selected replace mode.</summary>
    public string SelectedReplaceModeDescription => SelectedReplaceMode switch
    {
        DpReplaceMode => "Replace DP and optional LD payloads without CRC postbuild.",
        CtrlRamReplaceMode => "Replace CtrlRAM payloads, then run combiner.exe postbuild for CRC/header refresh.",
        GeneralReplaceMode => "Author explicit profile-approved ranges; TP ranges require combiner.exe CRC/header refresh.",
        _ => "Select a replace mode.",
    };

    /// <summary>Status shown in the merge inspector.</summary>
    public string MergeReadinessStatus => SelectedMergeMode switch
    {
        NormalMergeMode when IsStandardMergeSupported => $"{SelectedIc}: drop {GetRequiredStandardMergeSlotLabels()} BIN files.",
        NormalMergeMode => $"{SelectedIc}: Standard Merge is not available yet.",
        GeneralMergeMode => GeneralMergeMappings.Any(mapping => mapping.HasFile)
            ? $"{SelectedIc}: General Merge maps {GeneralMergeMappings.Count(mapping => mapping.HasFile)} source BIN file(s) into a blank output."
            : $"{SelectedIc}: add at least one source BIN mapping.",
        _ => "AB Code Merge is reserved for a later workflow.",
    };

    /// <summary>True when Standard Merge preview can run.</summary>
    public bool CanPreviewStandardMerge => CanRunStandardMerge();

    /// <summary>True when Standard Merge build can run.</summary>
    public bool CanBuildStandardMerge => CanRunStandardMerge() && HasCurrentStandardMergePreview();

    /// <summary>True when active Merge preview can run.</summary>
    public bool CanPreviewMerge => CanRunMerge();

    /// <summary>True when active Merge build can run.</summary>
    public bool CanBuildMerge => CanRunMerge() && HasCurrentMergePreview();

    /// <summary>True when Replace preview can run for the active mode.</summary>
    public bool CanPreviewReplace => CanRunReplace();

    /// <summary>True when Replace build can run for the active mode.</summary>
    public bool CanBuildReplace => CanRunReplace() && HasCurrentReplacePreview();

    /// <summary>Command that returns to the clean home view.</summary>
    public IRelayCommand ShowHomeCommand { get; }

    /// <summary>Command that opens Settings.</summary>
    public IRelayCommand ShowSettingsCommand { get; }

    /// <summary>Command that opens Merge.</summary>
    public IRelayCommand ShowMergeCommand { get; }

    /// <summary>Command that opens Replace.</summary>
    public IRelayCommand ShowReplaceCommand { get; }

    /// <summary>Command that opens DP Replace.</summary>
    public IRelayCommand ShowDpReplaceCommand { get; }

    /// <summary>Command that opens CtrlRAM Replace.</summary>
    public IRelayCommand ShowCtrlRamReplaceCommand { get; }

    /// <summary>Command that opens General Replace.</summary>
    public IRelayCommand ShowGeneralReplaceCommand { get; }

    /// <summary>Command that opens Normal Merge.</summary>
    public IRelayCommand ShowNormalMergeCommand { get; }

    /// <summary>Command that adds a General Replace mapping row.</summary>
    public IRelayCommand AddGeneralReplaceMappingCommand { get; }

    /// <summary>Command that removes a General Replace mapping row.</summary>
    public IRelayCommand<GeneralReplaceMappingViewModel> RemoveGeneralReplaceMappingCommand { get; }

    /// <summary>Command that adds a General Merge mapping row.</summary>
    public IRelayCommand AddGeneralMergeMappingCommand { get; }

    /// <summary>Command that removes a General Merge mapping row.</summary>
    public IRelayCommand<GeneralMergeMappingViewModel> RemoveGeneralMergeMappingCommand { get; }

    /// <summary>Command that previews Standard Merge through the application core.</summary>
    public IAsyncRelayCommand PreviewMergeCommand { get; }

    /// <summary>Command that builds Standard Merge output through the application core.</summary>
    public IAsyncRelayCommand BuildMergeCommand { get; }

    /// <summary>Command that previews Replace through the application core or workbench planner.</summary>
    public IAsyncRelayCommand PreviewReplaceCommand { get; }

    /// <summary>Command that builds Replace output through the application/workbench core.</summary>
    public IAsyncRelayCommand BuildReplaceCommand { get; }

    /// <summary>Command that opens the compact Replace input selection overview.</summary>
    public IRelayCommand ShowReplaceSelectionCommand { get; }

    /// <summary>Command that closes the compact Replace input selection overview.</summary>
    public IRelayCommand CloseReplaceSelectionCommand { get; }

    /// <summary>Sets a local file path for a UI input slot.</summary>
    public void SetSlotFile(string slotId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FirmwareSlotViewModel? slot = FindSlot(slotId);
        if (slot is null)
        {
            if (SetGeneralMergeMappingFile(slotId, path))
            {
                return;
            }

            SetGeneralReplaceMappingFile(slotId, path);
            return;
        }

        slot.FilePath = path;
        RefreshFirmwareFacts(slot);
        if (slot.SlotId == ReplaceBaseSlotId && IsCtrlRamReplaceModeSelected)
        {
            RefreshCtrlRamRegions();
            RefreshReplaceModeState(preserveSlotFiles: true);
            RefreshMemoryMapState();
        }
        else if (slot.SlotId is MergeDpSlotId or ReplaceBaseSlotId)
        {
            RefreshMemoryMapState();
        }

        RefreshCommandState();
    }

    private string DeviceContextRefreshSummary { get; }

    private void RefreshNumberChoicesForSelectedIc()
    {
        IReadOnlyList<string> nextChoices = UiCompositionRunner.GetNumberChoices(SelectedIc);
        NumberChoices = nextChoices;
        if (!nextChoices.Contains(SelectedNumber, StringComparer.Ordinal))
        {
            SelectedNumber = nextChoices[0];
        }
    }

    private void RefreshContextState(bool resetRunResult = false)
    {
        RefreshCtrlRamRegions();
        RefreshMemoryMapState();
        RefreshMergeSlotRequirements();
        RefreshMergeModeState();
        RefreshReplaceModeState();
        RefreshSettingsState();
        RefreshCommandState();
        NotifyContextTextChanged();
        if (resetRunResult)
        {
            ResetRunResultForContextChange();
        }
    }

    private void RefreshReplaceSlotGroups()
    {
        ReplaceSlotGroups.Clear();
        if (!IsCtrlRamReplaceModeSelected)
        {
            return;
        }

        foreach (FirmwareSlotGroupViewModel group in ReplaceRegionGroupBuilder.CreateSlotGroups(
            ReplaceSlots.Where(slot => !ReferenceEquals(slot, ReplaceBaseSlot))))
        {
            ReplaceSlotGroups.Add(group);
        }
    }

    private void RefreshReplaceCoverageGroups()
    {
        ReplaceCoverageGroups.Clear();
        if (!IsCtrlRamReplaceModeSelected)
        {
            return;
        }

        foreach (MemoryCoverageGroupViewModel group in ReplaceRegionGroupBuilder.CreateCoverageGroups(
            ReplaceCoverageSegments))
        {
            ReplaceCoverageGroups.Add(group);
        }
    }

    private void AddRows(params string[] rows)
    {
        foreach (string row in rows)
        {
            ActiveReplaceRows.Add(row);
        }
    }

    private static void ReplaceRows<T>(
        ObservableCollection<T> target,
        IEnumerable<T> rows)
    {
        target.Clear();
        foreach (T row in rows)
        {
            target.Add(row);
        }
    }

    private void NotifyContextTextChanged()
    {
        OnPropertyChanged(nameof(IsStandardMergeSupported));
        OnPropertyChanged(nameof(StandardMergeSupportSummary));
        OnPropertyChanged(nameof(StandardMergeOutputFileName));
        OnPropertyChanged(nameof(GeneralMergeOutputFileName));
        OnPropertyChanged(nameof(MergeOutputFileName));
        OnPropertyChanged(nameof(HomeReplaceStatus));
        OnPropertyChanged(nameof(HomeMergeStatus));
        OnPropertyChanged(nameof(MergeReadinessStatus));
        OnPropertyChanged(nameof(ReplaceReadinessStatus));
        OnPropertyChanged(nameof(ReplacePreviewUnavailableReason));
        OnPropertyChanged(nameof(ReplaceBuildUnavailableReason));
        OnPropertyChanged(nameof(ReplaceOutputFileName));
        OnPropertyChanged(nameof(IsDeviceContextVisible));
        OnPropertyChanged(nameof(IsNumberSelectorVisible));
        OnPropertyChanged(nameof(IsNumberSelectorPlaceholderVisible));
        OnPropertyChanged(nameof(DeviceContextStatus));
    }

    private void ResetRunResultForContextChange()
    {
        LastRunResult = new UiRunResultViewModel(
            "Context changed",
            $"{SelectedIc} / {SelectedNumber}: rerun Preview before Build.",
            "No output",
            succeeded: false);
        OnPropertyChanged(nameof(LastRunResult));
        RefreshSettingsState();
    }

    private FirmwareSlotViewModel? FindSlot(string slotId)
    {
        return MergeSlots.Concat(ReplaceSlots)
            .Concat([ReplaceBaseSlot])
            .FirstOrDefault(slot => string.Equals(slot.SlotId, slotId, StringComparison.Ordinal));
    }

    private void SelectReplaceMode(string mode)
    {
        if (ReplaceModeChoices.Contains(mode, StringComparer.Ordinal))
        {
            SelectedReplaceMode = mode;
        }

        SetSelectedPage(ShellPage.Replace);
    }

    private void SelectMergeMode(string mode)
    {
        string nextMode = MergeModeChoices.Contains(mode, StringComparer.Ordinal)
            ? mode
            : NormalMergeMode;
        if (!string.Equals(_selectedMergeMode, nextMode, StringComparison.Ordinal))
        {
            _selectedMergeMode = nextMode;
            OnPropertyChanged(nameof(SelectedMergeMode));
            OnPropertyChanged(nameof(IsNormalMergeModeSelected));
            OnPropertyChanged(nameof(IsGeneralMergeModeSelected));
            OnPropertyChanged(nameof(IsAbCodeMergeModeSelected));
            OnPropertyChanged(nameof(IsNumberSelectorVisible));
            OnPropertyChanged(nameof(IsNumberSelectorPlaceholderVisible));
            OnPropertyChanged(nameof(DeviceContextStatus));
            OnPropertyChanged(nameof(MergeOutputFileName));
            OnPropertyChanged(nameof(MergeReadinessStatus));
            OnPropertyChanged(nameof(MergeMemorySummary));
            ResetRunResultForContextChange();
            RefreshMemoryMapState();
            RefreshCommandState();
        }

        SetSelectedPage(ShellPage.Merge);
    }

    private void SetSelectedPage(ShellPage page)
    {
        NavigateToPage(page);
    }

    private void ApplySelectedPage(ShellPage page)
    {
        if (SelectedPage == page)
        {
            UpdateNavigationState();
            return;
        }

        SelectedPage = page;
        OnPropertyChanged(nameof(SelectedPage));
        OnPropertyChanged(nameof(IsHomeVisible));
        OnPropertyChanged(nameof(IsSettingsVisible));
        OnPropertyChanged(nameof(IsMergeVisible));
        OnPropertyChanged(nameof(IsReplaceVisible));
        OnPropertyChanged(nameof(IsDeviceContextVisible));
        OnPropertyChanged(nameof(IsNumberSelectorVisible));
        OnPropertyChanged(nameof(IsNumberSelectorPlaceholderVisible));
        OnPropertyChanged(nameof(DeviceContextStatus));
        UpdateNavigationState();
    }

    private void RefreshCommandState()
    {
        PreviewMergeCommand.NotifyCanExecuteChanged();
        BuildMergeCommand.NotifyCanExecuteChanged();
        PreviewReplaceCommand.NotifyCanExecuteChanged();
        BuildReplaceCommand.NotifyCanExecuteChanged();
        ShowReportCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanPreviewStandardMerge));
        OnPropertyChanged(nameof(CanBuildStandardMerge));
        OnPropertyChanged(nameof(CanPreviewMerge));
        OnPropertyChanged(nameof(CanBuildMerge));
        OnPropertyChanged(nameof(CanPreviewReplace));
        OnPropertyChanged(nameof(CanBuildReplace));
        OnPropertyChanged(nameof(ReplaceReadinessStatus));
        OnPropertyChanged(nameof(ReplacePreviewUnavailableReason));
        OnPropertyChanged(nameof(ReplaceBuildUnavailableReason));
        RefreshReplaceSelectionState();
    }

    partial void OnSelectedReplaceModeChanged(string value)
    {
        RefreshCtrlRamRegions();
        RefreshReplaceModeState();
        RefreshMemoryMapState();
        ResetRunResultForContextChange();
        NotifyContextTextChanged();
        RefreshCommandState();
    }

    partial void OnSelectedIcChanged(string value)
    {
        RefreshNumberChoicesForSelectedIc();
        GeneralMergeOutputLength = UiCompositionRunner.GetGeneralMergeDefaultOutputLength(value);
        RefreshContextState(resetRunResult: true);
        RefreshAllSelectedSlotFirmwareFacts();
    }

    partial void OnSelectedNumberChanged(string value)
    {
        RefreshContextState(resetRunResult: true);
    }

    partial void OnGeneralMergeOutputLengthChanged(string value)
    {
        RefreshMemoryMapState();
        ResetRunResultForContextChange();
        RefreshCommandState();
    }

    /// <summary>Gets selected replace mode.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedReplaceModeDescription))]
    [NotifyPropertyChangedFor(nameof(ReplaceReadinessStatus))]
    [NotifyPropertyChangedFor(nameof(ReplacePreviewUnavailableReason))]
    [NotifyPropertyChangedFor(nameof(ReplaceBuildUnavailableReason))]
    [NotifyPropertyChangedFor(nameof(IsDpReplaceModeSelected))]
    [NotifyPropertyChangedFor(nameof(IsCtrlRamReplaceModeSelected))]
    [NotifyPropertyChangedFor(nameof(IsGeneralReplaceModeSelected))]
    [NotifyPropertyChangedFor(nameof(IsStructuredReplaceModeSelected))]
    [NotifyPropertyChangedFor(nameof(IsNonCtrlRamStructuredReplaceModeSelected))]
    [NotifyPropertyChangedFor(nameof(IsReplaceCoverageGrouped))]
    [NotifyPropertyChangedFor(nameof(IsReplaceCoverageFlat))]
    public partial string SelectedReplaceMode { get; set; } = DpReplaceMode;

    /// <summary>Gets supported IC count/variant choices for the selected IC.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceContextStatus))]
    public partial IReadOnlyList<string> NumberChoices { get; set; } = UiCompositionRunner.GetNumberChoices("NT51950");

    /// <summary>Gets or sets the selected IC id in the shared context row.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceContextStatus))]
    public partial string SelectedIc { get; set; } = "NT51950";

    /// <summary>Gets or sets the selected IC count/variant in the shared context row.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceContextStatus))]
    public partial string SelectedNumber { get; set; } = "single";

    /// <summary>Gets or sets General Merge output length text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeMemoryRangeLabel))]
    [NotifyPropertyChangedFor(nameof(MergeReadinessStatus))]
    [NotifyPropertyChangedFor(nameof(CanPreviewMerge))]
    [NotifyPropertyChangedFor(nameof(CanBuildMerge))]
    public partial string GeneralMergeOutputLength { get; set; } =
        UiCompositionRunner.GetGeneralMergeDefaultOutputLength("NT51950");

}

/// <summary>Top-level shell page state.</summary>
public enum ShellPage
{
    /// <summary>Clean home view with three entry cards.</summary>
    Home,

    /// <summary>Settings planning page.</summary>
    Settings,

    /// <summary>Merge planning page.</summary>
    Merge,

    /// <summary>Replace planning page.</summary>
    Replace,
}
