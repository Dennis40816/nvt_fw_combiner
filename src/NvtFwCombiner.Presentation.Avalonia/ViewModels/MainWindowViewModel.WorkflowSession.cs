using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    /// <summary>Focused shared workflow-context and selected-firmware prompt presentation.</summary>
    public WorkflowSessionPresentationViewModel WorkflowSession { get; }

    private string GetWorkflowSelectedIc()
    {
        return WorkflowSession.SelectedIc;
    }

    private string GetWorkflowSelectedNumber()
    {
        return WorkflowSession.SelectedNumber;
    }

    private bool IsWorkflowLoaded()
    {
        return WorkflowSession.IsWorkflowLoaded;
    }

    private bool IsWorkflowLoading()
    {
        return WorkflowSession.IsLoadingWorkflow;
    }

    private void RefreshWorkflowNumberChoices()
    {
        WorkflowSession.RefreshNumberChoicesForSelectedIc();
    }

    internal void PublishCanonicalCatalogState()
    {
        WorkflowSession.PublishCanonicalCatalogState();
        if (!WorkflowSession.IsCanonicalCatalogReady)
        {
            throw new InvalidOperationException("Canonical catalog presentation state was not published.");
        }
        ApplyCatalogBackedTextResources();
        NotifyCatalogWorkflowCommandStateChanged();
    }

    private void NotifyCatalogWorkflowCommandStateChanged()
    {
        PresentationObserver.Invoke(ShowMergeCommand.NotifyCanExecuteChanged);
        PresentationObserver.Invoke(ShowReplaceCommand.NotifyCanExecuteChanged);
        PresentationObserver.Invoke(BeginDpReplaceFromHomeCommand.NotifyCanExecuteChanged);
        PresentationObserver.Invoke(BeginCtrlRamReplaceFromHomeCommand.NotifyCanExecuteChanged);
        PresentationObserver.Invoke(BeginGeneralReplaceFromHomeCommand.NotifyCanExecuteChanged);
        PresentationObserver.Invoke(BeginNormalMergeFromHomeCommand.NotifyCanExecuteChanged);
        PresentationObserver.Invoke(BeginAbMergeFromHomeCommand.NotifyCanExecuteChanged);
        PresentationObserver.Invoke(BeginGeneralMergeFromHomeCommand.NotifyCanExecuteChanged);
    }

    private void ApplyWorkflowContext(WorkflowContextSelection selection)
    {
        WorkflowModeNavigationStage stage =
            WorkflowSession.StageWorkflowModeForNavigation(selection);
        try
        {
            Navigation.NavigateToPage(selection.Page);
        }
        catch
        {
            WorkflowSession.RestoreStagedWorkflowMode(stage);
            throw;
        }

        WorkflowSession.PublishStagedWorkflowMode(stage);
    }

    private void WorkflowSession_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkflowSessionPresentationViewModel.IsWorkflowContextModalOpen) or
            nameof(WorkflowSessionPresentationViewModel.IsFirmwareIcMismatchModalOpen) or
            nameof(WorkflowSessionPresentationViewModel.IsFirmwareNumberMismatchModalOpen))
        {
            OnPropertyChanged(nameof(WorkflowSession));
            NotifyCompositionActionRailVisibilityChanged();
        }
    }
}
