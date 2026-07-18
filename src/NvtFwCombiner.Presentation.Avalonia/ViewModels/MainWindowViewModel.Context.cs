using System.Collections.ObjectModel;
using System.ComponentModel;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string DeviceContextRefreshSummary { get; set; } = string.Empty;

    private void RefreshNumberChoicesForSelectedIc()
    {
        IReadOnlyList<IcNumberChoiceViewModel> nextDisplayChoices = UiCompositionRunner.GetNumberSelectionChoices(SelectedIc);
        NumberSelectionChoices = nextDisplayChoices;
        if (!nextDisplayChoices.Any(choice =>
                string.Equals(choice.Token, SelectedNumber, StringComparison.Ordinal)))
        {
            SelectedNumber = nextDisplayChoices.FirstOrDefault(choice =>
                string.Equals(choice.Token, WorkbenchIcNumberTokens.SingleChip, StringComparison.Ordinal))?.Token ??
                nextDisplayChoices[0].Token;
        }

        OnPropertyChanged(nameof(SelectedNumberChoice));
    }

    private void RefreshContextState(bool resetRunResult = false)
    {
        RefreshCtrlRamRegions();
        RefreshMemoryMapState();
        RefreshMergeSlotRequirements();
        RefreshReplaceModeState();
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
        OnPropertyChanged(nameof(ReplaceBuildActionTip));
        OnPropertyChanged(nameof(ReplaceOutputFileName));
        OnPropertyChanged(nameof(SelectedReplaceWorkflowReadiness));
        OnPropertyChanged(nameof(SelectedReplaceModeEvidenceLabel));
        OnPropertyChanged(nameof(SelectedReplaceModeEvidenceTooltip));
        OnPropertyChanged(nameof(IsSelectedReplaceModeGoldenVerified));
        OnPropertyChanged(nameof(IsSelectedReplaceModeEvidenceGated));
        OnPropertyChanged(nameof(IsSelectedReplaceModeUnavailable));
        OnPropertyChanged(nameof(SelectedIcFamilySummary));
        OnPropertyChanged(nameof(SelectedIcFamilyLabel));
        OnPropertyChanged(nameof(SelectedIcFamilyTooltip));
        OnPropertyChanged(nameof(HasSelectedIcFamily));
        OnPropertyChanged(nameof(IsDeviceContextVisible));
        OnPropertyChanged(nameof(IsNumberSelectorVisible));
        OnPropertyChanged(nameof(IsNumberSelectorPlaceholderVisible));
        NotifyActiveRunContextChanged();
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
        OnPropertyChanged(nameof(IsHexEditorVisible));
        OnPropertyChanged(nameof(IsDeviceContextVisible));
        OnPropertyChanged(nameof(IsNumberSelectorVisible));
        OnPropertyChanged(nameof(IsNumberSelectorPlaceholderVisible));
        OnPropertyChanged(nameof(DeviceContextStatus));
        UpdateNavigationState();
    }

    private bool CanRequestHexEditorSave()
    {
        return IsHexEditorVisible &&
               !HexEditorWorkspace.IsTextEntryFocused &&
               !HexEditorWorkspace.IsInlineEditActive &&
               HexEditorWorkspace.CanSave;
    }

    private void RequestHexEditorSave()
    {
        if (CanRequestHexEditorSave())
        {
            HexEditorWorkspace.RequestSaveCommand.Execute(null);
        }
    }

    private bool CanRequestHexEditorUndo()
    {
        return IsHexEditorVisible &&
               !HexEditorWorkspace.IsTextEntryFocused &&
               !HexEditorWorkspace.IsInlineEditActive &&
               HexEditorWorkspace.UndoCommand.CanExecute(null);
    }

    private void RequestHexEditorUndo()
    {
        if (CanRequestHexEditorUndo())
        {
            HexEditorWorkspace.UndoCommand.Execute(null);
        }
    }

    private bool CanRequestHexEditorRedo()
    {
        return IsHexEditorVisible &&
               !HexEditorWorkspace.IsTextEntryFocused &&
               !HexEditorWorkspace.IsInlineEditActive &&
               HexEditorWorkspace.RedoCommand.CanExecute(null);
    }

    private void RequestHexEditorRedo()
    {
        if (CanRequestHexEditorRedo())
        {
            HexEditorWorkspace.RedoCommand.Execute(null);
        }
    }

    private void HexEditorWorkspace_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(HexEditorWorkspaceViewModel.CanSave) or
            nameof(HexEditorWorkspaceViewModel.ChangeCount) or
            nameof(HexEditorWorkspaceViewModel.IsInlineEditActive) or
            nameof(HexEditorWorkspaceViewModel.IsTextEntryFocused)))
        {
            return;
        }

        RequestHexEditorSaveCommand.NotifyCanExecuteChanged();
        RequestHexEditorUndoCommand.NotifyCanExecuteChanged();
        RequestHexEditorRedoCommand.NotifyCanExecuteChanged();
    }

    private void RefreshCommandState()
    {
        PreviewMergeCommand.NotifyCanExecuteChanged();
        BuildMergeCommand.NotifyCanExecuteChanged();
        PreviewReplaceCommand.NotifyCanExecuteChanged();
        BuildReplaceCommand.NotifyCanExecuteChanged();
        ShowReportCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsRunInProgress));
        OnPropertyChanged(nameof(IsDeviceContextVisible));
        OnPropertyChanged(nameof(IsNumberSelectorVisible));
        OnPropertyChanged(nameof(IsNumberSelectorPlaceholderVisible));
        OnPropertyChanged(nameof(DeviceContextStatus));
        OnPropertyChanged(nameof(HasTypedRunProgress));
        OnPropertyChanged(nameof(RunProgressStatusLabel));
        OnPropertyChanged(nameof(RunProgressDisplayLabel));
        OnPropertyChanged(nameof(ShouldAnimateRunProgress));
        OnPropertyChanged(nameof(CanBuildMerge));
        OnPropertyChanged(nameof(MergeReadinessStatus));
        OnPropertyChanged(nameof(MergeBuildActionTip));
        OnPropertyChanged(nameof(CanBuildReplace));
        OnPropertyChanged(nameof(ReplaceReadinessStatus));
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
        GeneralMergeOutputLength = WorkbenchCompositionService.GetGeneralMergeDefaultOutputLength(value);
        RefreshContextState(resetRunResult: true);
        RefreshAllSelectedSlotFirmwareFacts();
    }

    partial void OnSelectedNumberChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedNumberChoice));
        RefreshContextState(resetRunResult: true);
    }

    partial void OnGeneralMergeOutputLengthChanged(string value)
    {
        RefreshMemoryMapState();
        ResetRunResultForContextChange();
        RefreshCommandState();
    }
}
