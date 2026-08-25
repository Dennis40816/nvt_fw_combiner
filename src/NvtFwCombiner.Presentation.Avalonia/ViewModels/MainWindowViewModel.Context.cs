using System.ComponentModel;
using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private void SelectReplaceMode(string mode)
    {
        Navigation.NavigateToPage(ShellPage.Replace);
        Replace.SelectReplaceMode(mode);
    }

    private void ApplySelectedPage(ShellPage page)
    {
        bool pageChanged = SelectedPage != page;
        if (pageChanged)
        {
            WorkflowSession.RememberCurrentWorkflowContext();
            SelectedPage = page;
            WorkflowSession.ActivateWorkflowPageContext(page);
        }

        if (page is ShellPage.Merge or ShellPage.Replace)
        {
            // Destination publication and page-owned selection must be active
            // before any profile-dependent lazy-load query observes the IC.
            bool wasWorkflowLoaded = WorkflowSession.IsWorkflowLoaded;
            WorkflowSession.EnsureWorkflowLoaded();
            if (!wasWorkflowLoaded)
            {
                WorkflowSession.RefreshContextState();
            }
        }

        if (pageChanged)
        {
            OnPropertyChanged(nameof(SelectedPage));
            OnPropertyChanged(nameof(IsHomeVisible));
            OnPropertyChanged(nameof(IsMergeVisible));
            OnPropertyChanged(nameof(IsReplaceVisible));
            OnPropertyChanged(nameof(IsHexEditorVisible));
            OnPropertyChanged(nameof(IsDeviceContextVisible));
            OnPropertyChanged(nameof(IsCompositionActionRailVisible));
            OnPropertyChanged(nameof(IsLatestOutputActionVisible));
            WorkflowSession.NotifyContextTextChanged();
        }

        Navigation.UpdateState();
        NotifyHexEditorCommandStateChanged();
        if (pageChanged)
        {
            RecordDebugActivity(SystemActivityCodes.UserNavigated,
                SystemActivityCategory.Navigation, page.ToString());
        }
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

        NotifyHexEditorCommandStateChanged();
    }

    private void NotifyHexEditorCommandStateChanged()
    {
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
        OnPropertyChanged(nameof(HasMergeBuildBlocker));
        OnPropertyChanged(nameof(MergeBuildBlockerText));
        OnPropertyChanged(nameof(HasReplaceBuildBlocker));
        OnPropertyChanged(nameof(ReplaceBuildBlockerText));
    }

}
