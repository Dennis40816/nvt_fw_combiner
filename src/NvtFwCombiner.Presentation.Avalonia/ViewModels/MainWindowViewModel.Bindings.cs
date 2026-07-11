using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private static string DefaultIcId => UiCompositionRunner.GetDefaultIcId();

    /// <summary>Gets the shell milestone label.</summary>
    public string ShellVersion { get; }

    /// <summary>Gets the product version.</summary>
    public string AppVersion { get; }

    /// <summary>Gets the active localized text bundle.</summary>
    public ShellTextResources Text { get; private set; } = ShellTextResources.For(ShellLanguage.English);

    /// <summary>Gets the standalone raw-BIN utility workspace exposed from Home Util Tools.</summary>
    public HexEditorWorkspaceViewModel HexEditorWorkspace { get; }

    /// <summary>Gets the workspace title.</summary>
    public string WorkspaceTitle { get; private set; } = string.Empty;

    /// <summary>Gets the workspace summary.</summary>
    public string WorkspaceSummary { get; private set; } = string.Empty;

    /// <summary>Gets the preview action label.</summary>
    public string PreviewActionLabel { get; private set; } = string.Empty;

    /// <summary>Gets the build action label.</summary>
    public string BuildActionLabel { get; private set; } = string.Empty;

    /// <summary>Gets the report modal action label.</summary>
    public string ReportModalActionLabel { get; private set; } = string.Empty;

    /// <summary>Gets the shared device context heading.</summary>
    public string DeviceContextTitle { get; private set; } = string.Empty;

    /// <summary>Gets the IC field label.</summary>
    public string IcLabel { get; private set; } = string.Empty;

    /// <summary>Gets the IC count/variant field label.</summary>
    public string NumberLabel { get; private set; } = string.Empty;

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
    public PlanningCardViewModel SettingsPreview { get; private set; } = CreatePlanningCard(ShellTextResources.For(ShellLanguage.English).SettingsPreview);

    /// <summary>Gets merge card content.</summary>
    public PlanningCardViewModel MergePreview { get; private set; } = CreatePlanningCard(ShellTextResources.For(ShellLanguage.English).MergePreview);

    /// <summary>Gets replace card content.</summary>
    public PlanningCardViewModel ReplacePreview { get; private set; } = CreatePlanningCard(ShellTextResources.For(ShellLanguage.English).ReplacePreview);

    /// <summary>Gets footer status content.</summary>
    public string FooterStatus { get; private set; } = string.Empty;

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
    public string StandardMergeOutputFileName => CreateFlashCodeOutputFileName();

    /// <summary>Gets the default General Merge output file name.</summary>
    public string GeneralMergeOutputFileName => CreateFlashCodeOutputFileName();

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
    public string ReplaceOutputFileName => CreateFlashCodeOutputFileName();

    /// <summary>Gets short Merge memory-map summary text.</summary>
    public string MergeMemorySummary => Text.GetMergeMemorySummary(
        SelectedMergeMode,
        IsStandardMergeSupported,
        GeneralMergeMappings.Any(mapping => mapping.HasFile));

    /// <summary>Gets the latest UI-triggered run summary.</summary>
    public UiRunResultViewModel LastRunResult { get; private set; } = new(
        "No run yet",
        "Drop required BIN files, then run Build.",
        "No output",
        succeeded: true);

    /// <summary>True when the selected CtrlRAM catalog has visible rows.</summary>
    public bool HasCtrlRamRegions => CtrlRamRegions.Count > 0;

    /// <summary>Gets selected CtrlRAM row summary text.</summary>
    public string CtrlRamRegionSummary => Text.GetCtrlRamRegionSummary(SelectedIc, SelectedNumber);

    /// <summary>Gets the standard merge support summary for the selected IC.</summary>
    public string StandardMergeSupportSummary => IsStandardMergeSupported
        ? Text.GetStandardMergeSupportSummary(
            SelectedIc,
            supported: true,
            GetRequiredStandardMergeSlotLabels())
        : Text.GetStandardMergeSupportSummary(
            SelectedIc,
            supported: false,
            GetRequiredStandardMergeSlotLabels());

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

    /// <summary>True when the independent raw-BIN Hex Editor utility page is visible.</summary>
    public bool IsHexEditorVisible => SelectedPage == ShellPage.HexEditor;

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
    public string SelectedReplaceModeDescription => Text.GetReplaceModeDescription(SelectedReplaceMode);

    /// <summary>Status shown in the merge inspector.</summary>
    public string MergeReadinessStatus => Text.GetMergeReadinessStatus(
        SelectedMergeMode,
        SelectedIc,
        GetRequiredStandardMergeSlotLabels(),
        IsStandardMergeSupported,
        GeneralMergeMappings.Count(mapping => mapping.HasFile));

    /// <summary>One-line Build action hint for Merge.</summary>
    public string MergeBuildActionTip => CreateBuildActionTip(MergeReadinessStatus, CanRunMerge());

    /// <summary>One-line Build action hint for Replace.</summary>
    public string ReplaceBuildActionTip => CreateBuildActionTip(ReplaceReadinessStatus, CanRunReplace());

    /// <summary>True when Standard Merge preview can run.</summary>
    public bool CanPreviewStandardMerge => !IsRunInProgress && CanRunStandardMerge();

    /// <summary>True when Standard Merge build can run.</summary>
    public bool CanBuildStandardMerge => !IsRunInProgress && CanRunStandardMerge();

    /// <summary>True when active Merge preview can run.</summary>
    public bool CanPreviewMerge => CanRunMerge();

    /// <summary>True when active Merge build can run.</summary>
    public bool CanBuildMerge => CanRunMerge();

    /// <summary>True when Replace preview can run for the active mode.</summary>
    public bool CanPreviewReplace => CanRunReplace();

    /// <summary>True when Replace build can run for the active mode.</summary>
    public bool CanBuildReplace => CanRunReplace();

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

    /// <summary>Home entry command that collects Replace context before opening DP Replace.</summary>
    public IRelayCommand BeginDpReplaceFromHomeCommand { get; }

    /// <summary>Command that opens CtrlRAM Replace.</summary>
    public IRelayCommand ShowCtrlRamReplaceCommand { get; }

    /// <summary>Home entry command that collects Replace context before opening CtrlRAM Replace.</summary>
    public IRelayCommand BeginCtrlRamReplaceFromHomeCommand { get; }

    /// <summary>Command that opens General Replace.</summary>
    public IRelayCommand ShowGeneralReplaceCommand { get; }

    /// <summary>Home entry command that collects Replace context before opening General Replace.</summary>
    public IRelayCommand BeginGeneralReplaceFromHomeCommand { get; }

    /// <summary>Command that opens the independent Hex Editor workspace.</summary>
    public IRelayCommand ShowHexEditorCommand { get; }

    /// <summary>Window-level save shortcut scoped to the active raw-BIN Hex Editor page.</summary>
    public IRelayCommand RequestHexEditorSaveCommand { get; }

    /// <summary>Command that opens Normal Merge.</summary>
    public IRelayCommand ShowNormalMergeCommand { get; }

    /// <summary>Home entry command that collects Merge context before opening Standard Merge.</summary>
    public IRelayCommand BeginNormalMergeFromHomeCommand { get; }

    /// <summary>Command that opens General Merge.</summary>
    public IRelayCommand ShowGeneralMergeCommand { get; }

    /// <summary>Home entry command that collects Merge context before opening General Merge.</summary>
    public IRelayCommand BeginGeneralMergeFromHomeCommand { get; }

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
    public partial IReadOnlyList<string> NumberChoices { get; set; } = UiCompositionRunner.GetNumberChoices(DefaultIcId);

    /// <summary>Gets grouped display choices for the IC-count control.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<IcNumberChoiceViewModel> NumberSelectionChoices { get; set; } =
        UiCompositionRunner.GetNumberSelectionChoices(DefaultIcId);

    /// <summary>Gets or sets the selected displayed IC-count choice while retaining its planner token.</summary>
    public IcNumberChoiceViewModel? SelectedNumberChoice
    {
        get => NumberSelectionChoices.FirstOrDefault(choice =>
            string.Equals(choice.Token, SelectedNumber, StringComparison.Ordinal));
        set
        {
            if (value is not null && !string.Equals(SelectedNumber, value.Token, StringComparison.Ordinal))
            {
                SelectedNumber = value.Token;
            }
        }
    }

    /// <summary>Gets or sets the selected IC id in the shared context row.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceContextStatus))]
    public partial string SelectedIc { get; set; } = DefaultIcId;

    /// <summary>Gets or sets the selected IC count/variant in the shared context row.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceContextStatus))]
    public partial string SelectedNumber { get; set; } = WorkbenchIcNumberTokens.SingleChip;

    /// <summary>Gets or sets General Merge output length text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeMemoryRangeLabel))]
    [NotifyPropertyChangedFor(nameof(MergeReadinessStatus))]
    [NotifyPropertyChangedFor(nameof(CanPreviewMerge))]
    [NotifyPropertyChangedFor(nameof(CanBuildMerge))]
    public partial string GeneralMergeOutputLength { get; set; } =
        UiCompositionRunner.GetGeneralMergeDefaultOutputLength(DefaultIcId);
}
