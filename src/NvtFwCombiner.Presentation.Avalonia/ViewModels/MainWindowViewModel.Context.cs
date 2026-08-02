using System.ComponentModel;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string DeviceContextRefreshSummary { get; set; } = string.Empty;

    private void RefreshNumberChoicesForSelectedIc()
    {
        IReadOnlyList<IcNumberChoiceViewModel> nextDisplayChoices = IsAbMergeContextActive
            ?
            [
                .. AbMergeWorkbenchCompositionService.GetTopologyChoices(SelectedIc)
                    .Select(static choice => new IcNumberChoiceViewModel(choice.Token, choice.DisplayLabel)),
            ]
            : UiCompositionRunner.GetNumberSelectionChoices(SelectedIc);
        NumberSelectionChoices = nextDisplayChoices;
        if (nextDisplayChoices.Count == 0)
        {
            OnPropertyChanged(nameof(SelectedNumberChoice));
            return;
        }

        if (!nextDisplayChoices.Any(choice =>
                string.Equals(choice.Token, SelectedNumber, StringComparison.Ordinal)))
        {
            SelectedNumber = nextDisplayChoices.FirstOrDefault(choice =>
                string.Equals(choice.Token, WorkbenchIcNumberTokens.SingleChip, StringComparison.Ordinal))?.Token ??
                nextDisplayChoices[0].Token;
        }

        OnPropertyChanged(nameof(SelectedNumberChoice));
    }

    private void RefreshContextState(
        bool resetRunResult = false,
        bool preserveReplaceSlotFiles = false)
    {
        _deferredState.EnsureWorkflow(
            RefreshNumberChoicesForSelectedIc,
            () => WorkbenchCompositionService.GetGeneralMergeDefaultOutputLength(SelectedIc),
            value => Merge.GeneralMergeOutputLength = value,
            () => WorkbenchCompositionService.GetGeneralMergeDefaultOutputFillByte(SelectedIc),
            value => Merge.GeneralMergeOutputFillByte = value,
            Replace.AddGeneralReplaceMapping,
            Merge.AddGeneralMergeMapping);

        Merge.RefreshMergeSlotRequirements();
        Replace.RefreshContextState(preserveSlotFiles: preserveReplaceSlotFiles);
        ApplyFirmwareSlotText();
        Merge.RefreshMergeMemoryMapState();
        RefreshCommandState();
        NotifyContextTextChanged();
        if (resetRunResult)
        {
            ResetRunResultForContextChange();
        }
    }

    private void NotifyContextTextChanged()
    {
        Merge.NotifyContextChanged();
        OnPropertyChanged(nameof(Merge.IsStandardMergeSupported));
        OnPropertyChanged(nameof(Merge.IsAbMergeSupported));
        OnPropertyChanged(nameof(Merge.MergeModeChoices));
        OnPropertyChanged(nameof(Merge.StandardMergeSupportSummary));
        OnPropertyChanged(nameof(Merge.StandardMergeOutputFileName));
        OnPropertyChanged(nameof(Merge.GeneralMergeOutputFileName));
        OnPropertyChanged(nameof(Merge.MergeOutputFileName));
        OnPropertyChanged(nameof(Merge.AbMergeOutputFileName));
        OnPropertyChanged(nameof(Merge.MergeReadinessStatus));
        Replace.NotifyContextChanged();
        OnPropertyChanged(nameof(SelectedIcFamilySummary));
        OnPropertyChanged(nameof(SelectedIcFamilyLabel));
        OnPropertyChanged(nameof(SelectedIcFamilyTooltip));
        OnPropertyChanged(nameof(HasSelectedIcFamily));
        OnPropertyChanged(nameof(SelectedIcDetailFamily));
        OnPropertyChanged(nameof(SelectedIcDetailReuse));
        OnPropertyChanged(nameof(SelectedIcDetailRuntime));
        OnPropertyChanged(nameof(SelectedIcDetailEvidence));
        OnPropertyChanged(nameof(SelectedIcDetailSupport));
        OnPropertyChanged(nameof(SelectedIcDetailAutomationText));
        OnPropertyChanged(nameof(IsDeviceContextVisible));
        OnPropertyChanged(nameof(IsNumberSelectorVisible));
        OnPropertyChanged(nameof(IsNumberSelectorPlaceholderVisible));
        RunSession.NotifyContextChanged();
    }

    private void SelectReplaceMode(string mode)
    {
        Replace.SelectReplaceMode(mode);
        NavigateToPage(ShellPage.Replace);
    }

    private void ApplySelectedPage(ShellPage page)
    {
        _deferredState.EnsurePage(page, RefreshSettingsState, () => RefreshContextState());

        if (SelectedPage == page)
        {
            UpdateNavigationState();
            return;
        }

        SelectedPage = page;
        RefreshNumberChoicesForSelectedIc();
        OnPropertyChanged(nameof(SelectedPage));
        OnPropertyChanged(nameof(IsHomeVisible));
        OnPropertyChanged(nameof(IsSettingsVisible));
        OnPropertyChanged(nameof(IsMergeVisible));
        OnPropertyChanged(nameof(IsReplaceVisible));
        OnPropertyChanged(nameof(IsHexEditorVisible));
        OnPropertyChanged(nameof(IsDeviceContextVisible));
        OnPropertyChanged(nameof(IsCompositionActionRailVisible));
        OnPropertyChanged(nameof(IsLatestOutputActionVisible));
        OnPropertyChanged(nameof(IsNumberSelectorVisible));
        OnPropertyChanged(nameof(IsNumberSelectorPlaceholderVisible));
        OnPropertyChanged(nameof(DeviceContextStatus));
        OnPropertyChanged(nameof(IcChoices));
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
        if (e.PropertyName is nameof(HexEditorWorkspaceViewModel.IsInsertBytesPromptOpen) or
            nameof(HexEditorWorkspaceViewModel.IsSaveConfirmationOpen))
        {
            NotifyCompositionActionRailVisibilityChanged();
        }

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
        Merge.NotifyCommandStateChanged();
        Replace.NotifyCommandStateChanged();
        RunSession.NotifyCommandStateChanged();
        Merge.PreviewMergeCommand.NotifyCanExecuteChanged();
        Merge.BuildMergeCommand.NotifyCanExecuteChanged();
        Reports.ShowReportCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsDeviceContextVisible));
        OnPropertyChanged(nameof(IsNumberSelectorVisible));
        OnPropertyChanged(nameof(IsNumberSelectorPlaceholderVisible));
        OnPropertyChanged(nameof(DeviceContextStatus));
        OnPropertyChanged(nameof(Merge.CanBuildMerge));
        OnPropertyChanged(nameof(Merge.MergeReadinessStatus));
    }

    private void ReplaceModeChanged()
    {
        WorkflowSession.InvalidateFirmwareNumberMismatch();
        WorkflowSession.InvalidateFirmwareInspection();
        Replace.InvalidateCtrlRamFirmwareVersionContextState();
        RefreshContextState(resetRunResult: true);
        WorkflowSession.RefreshCtrlRamDisplayFromInspection();
    }

    partial void OnSelectedIcChanged(string value)
    {
        WorkflowSession.InvalidateFirmwareNumberMismatch();
        WorkflowSessionPresentationViewModel.AcceptedFirmwareMismatchSelection? acceptedMismatch =
            WorkflowSession.ConsumeAcceptedFirmwareMismatchSelection();
        WorkflowSession.InvalidateFirmwareInspection(clearBaseCache: true, clearFileProjections: true);
        Replace.InvalidateCtrlRamFirmwareVersionContextState();
        if (Merge.IsAbCodeMergeModeSelected && !AbMergeWorkbenchCompositionService.IsAbMergeSupported(value))
        {
            Merge.SelectMergeMode(NormalMergeMode);
            OnPropertyChanged(nameof(Merge.SelectedMergeMode));
            OnPropertyChanged(nameof(Merge.IsNormalMergeModeSelected));
            OnPropertyChanged(nameof(Merge.IsAbCodeMergeModeSelected));
            OnPropertyChanged(nameof(IcChoices));
        }

        WorkflowSession.IsRefreshingFirmwareInspectionContext = true;
        try
        {
            RefreshNumberChoicesForSelectedIc();
            Merge.GeneralMergeOutputLength = WorkbenchCompositionService.GetGeneralMergeDefaultOutputLength(value);
            Merge.GeneralMergeOutputFillByte =
                WorkbenchCompositionService.GetGeneralMergeDefaultOutputFillByte(value);
        }
        finally
        {
            WorkflowSession.IsRefreshingFirmwareInspectionContext = false;
        }

        RefreshContextState(
            resetRunResult: true,
            preserveReplaceSlotFiles: acceptedMismatch is not null);
        string? acceptedMismatchSlotId = null;
        if (acceptedMismatch is { } selection &&
            FindSlot(selection.SlotId) is { } acceptedSlot &&
            string.Equals(acceptedSlot.FilePath, selection.Path, StringComparison.Ordinal))
        {
            acceptedMismatchSlotId = selection.SlotId;
        }
        else if (acceptedMismatch is { } missingSelection)
        {
            Reports.SetShellToast(
                Text.ContextUpdatedToastTitle,
                Text.FormatFirmwareSelectionNotRetainedToast(Path.GetFileName(missingSelection.Path)));
        }

        _ = WorkflowSession.RefreshAllSelectedFirmwareInspectionsAsync(acceptedMismatchSlotId);
        WorkflowSession.RememberReplaceWorkflowContext();
    }

    partial void OnSelectedNumberChanged(string value)
    {
        WorkflowSession.RememberReplaceWorkflowContext();
        WorkflowSession.InvalidateFirmwareNumberMismatch();
        if (WorkflowSession.IsRefreshingFirmwareInspectionContext)
        {
            Replace.InvalidateCtrlRamFirmwareVersionContextState();
            OnPropertyChanged(nameof(SelectedNumberChoice));
            return;
        }

        if (WorkflowSession.IsApplyingFirmwareInspectionContext)
        {
            WorkflowSession.InvalidateFirmwareInspection(clearFileProjections: Merge.IsAbCodeMergeModeSelected && Merge.HasAbMergeTopologyChoices);
            Replace.InvalidateCtrlRamFirmwareVersionContextState();
            OnPropertyChanged(nameof(SelectedNumberChoice));
            RefreshContextState(
                resetRunResult: true,
                preserveReplaceSlotFiles: true);
            RefreshAbMergeInputsAfterTopologyChange();
            return;
        }

        // AB validation is topology-sensitive.  Preserve no projection across a topology
        // switch, then let the shared refresh below inspect the currently selected inputs.
        WorkflowSession.InvalidateFirmwareInspection(clearFileProjections: Merge.IsAbCodeMergeModeSelected && Merge.HasAbMergeTopologyChoices);
        Replace.InvalidateCtrlRamFirmwareVersionContextState();
        OnPropertyChanged(nameof(SelectedNumberChoice));
        RefreshContextState(
            resetRunResult: true,
            preserveReplaceSlotFiles: true);
        RefreshAbMergeInputsAfterTopologyChange();

        WorkflowSession.RefreshCtrlRamDisplayFromInspection();
    }

    private void RefreshAbMergeInputsAfterTopologyChange()
    {
        if (Merge.IsAbCodeMergeModeSelected &&
            Merge.HasAbMergeTopologyChoices &&
            Merge.MergeSlots.Any(slot => slot.HasFile))
        {
            _ = WorkflowSession.RefreshSelectedMergeFirmwareInspectionsAsync();
        }
    }

}
