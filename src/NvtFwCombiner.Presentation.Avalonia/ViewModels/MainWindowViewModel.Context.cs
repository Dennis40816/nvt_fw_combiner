using System.Collections.ObjectModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
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
        OnPropertyChanged(nameof(IsRunInProgress));
        OnPropertyChanged(nameof(CanPreviewStandardMerge));
        OnPropertyChanged(nameof(CanBuildStandardMerge));
        OnPropertyChanged(nameof(CanPreviewMerge));
        OnPropertyChanged(nameof(CanBuildMerge));
        OnPropertyChanged(nameof(MergeReadinessStatus));
        OnPropertyChanged(nameof(MergeBuildActionTip));
        OnPropertyChanged(nameof(CanPreviewReplace));
        OnPropertyChanged(nameof(CanBuildReplace));
        OnPropertyChanged(nameof(HexEditorReadinessStatus));
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
