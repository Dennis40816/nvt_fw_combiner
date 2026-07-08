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
        ShellLanguage language = ShellLanguage.English)
    {
        ShellVersion = shellVersion;
        AppVersion = appVersion;
        ApplyTextResources(language, notify: false);
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
        ShowReportHistoryCommand = new RelayCommand(ShowReportHistory, () => CanOpenReportHistory);
        CloseReportHistoryCommand = new RelayCommand(CloseReportHistory);
        ClearReportHistoryCommand = new RelayCommand(ClearReportHistory, () => CanClearReportHistory);
        OpenReportHistoryEntryCommand = new RelayCommand<ReportHistoryEntryViewModel>(OpenReportHistoryEntry);
        RemoveReportHistoryEntryCommand = new RelayCommand<ReportHistoryEntryViewModel>(RemoveReportHistoryEntry);
        ShowReplaceSelectionCommand = new RelayCommand(ShowReplaceSelection);
        CloseReplaceSelectionCommand = new RelayCommand(CloseReplaceSelection);

        AddGeneralReplaceMapping();
        AddGeneralMergeMapping();
        NavigationTrail.Add(CreateNavigationEntry(ShellPage.Home, isCurrent: true));
        RefreshContextState();
        RefreshSettingsState();
    }

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
        OnPropertyChanged(nameof(StandardMergeOutputFileName));
        OnPropertyChanged(nameof(GeneralMergeOutputFileName));
        OnPropertyChanged(nameof(MergeOutputFileName));
        OnPropertyChanged(nameof(ReplaceOutputFileName));
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

    private string DeviceContextRefreshSummary { get; set; } = string.Empty;

    private static PlanningCardViewModel CreatePlanningCard(PlanningCardText text)
    {
        return new PlanningCardViewModel(
            text.Title,
            text.Subtitle,
            text.Rows,
            text.Status);
    }

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
        OnPropertyChanged(nameof(MergeReadinessStatus));
        OnPropertyChanged(nameof(MergeBuildActionTip));
        OnPropertyChanged(nameof(ReplaceReadinessStatus));
        OnPropertyChanged(nameof(ReplacePreviewUnavailableReason));
        OnPropertyChanged(nameof(ReplaceBuildUnavailableReason));
        OnPropertyChanged(nameof(ReplaceBuildActionTip));
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
            $"{SelectedIc} / {SelectedNumber}: run Build to validate the latest context.",
            "No output",
            succeeded: false);
        OnPropertyChanged(nameof(LastRunResult));
        OnPropertyChanged(nameof(MergeBuildActionTip));
        OnPropertyChanged(nameof(ReplaceBuildActionTip));
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
        OnPropertyChanged(nameof(MergeReadinessStatus));
        OnPropertyChanged(nameof(MergeBuildActionTip));
        OnPropertyChanged(nameof(CanPreviewReplace));
        OnPropertyChanged(nameof(CanBuildReplace));
        OnPropertyChanged(nameof(ReplaceReadinessStatus));
        OnPropertyChanged(nameof(ReplacePreviewUnavailableReason));
        OnPropertyChanged(nameof(ReplaceBuildUnavailableReason));
        OnPropertyChanged(nameof(ReplaceBuildActionTip));
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
