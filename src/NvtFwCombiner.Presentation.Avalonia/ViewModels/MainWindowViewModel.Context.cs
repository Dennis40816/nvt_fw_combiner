using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void SelectReplaceMode(string mode)
    {
        Replace.SelectReplaceMode(mode);
        NavigateToPage(ShellPage.Replace);
    }

    private void ApplySelectedPage(ShellPage page)
    {
        if (page == ShellPage.Settings)
        {
            _deferredState.EnsureSettings(RefreshSettingsState);
        }
        else if (page is ShellPage.Merge or ShellPage.Replace)
        {
            bool wasWorkflowLoaded = WorkflowSession.IsWorkflowLoaded;
            WorkflowSession.EnsureWorkflowLoaded();
            if (!wasWorkflowLoaded)
            {
                WorkflowSession.RefreshContextState();
            }
        }

        if (SelectedPage == page)
        {
            UpdateNavigationState();
            return;
        }

        SelectedPage = page;
        WorkflowSession.RefreshNumberChoicesForSelectedIc();
        OnPropertyChanged(nameof(SelectedPage));
        OnPropertyChanged(nameof(IsHomeVisible));
        OnPropertyChanged(nameof(IsSettingsVisible));
        OnPropertyChanged(nameof(IsMergeVisible));
        OnPropertyChanged(nameof(IsReplaceVisible));
        OnPropertyChanged(nameof(IsHexEditorVisible));
        OnPropertyChanged(nameof(IsDeviceContextVisible));
        OnPropertyChanged(nameof(IsCompositionActionRailVisible));
        OnPropertyChanged(nameof(IsLatestOutputActionVisible));
        WorkflowSession.NotifyContextTextChanged();
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
        OnPropertyChanged(nameof(Merge.CanBuildMerge));
        OnPropertyChanged(nameof(Merge.MergeReadinessStatus));
    }

}
