using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
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

    private void WorkflowReplaceModeChanged()
    {
        WorkflowSession.ReplaceModeChanged();
    }

    private string CreateWorkflowFlashCodeOutputFileName(IEnumerable<FirmwareSlotViewModel> candidateSlots)
    {
        return WorkflowSession.CreateFlashCodeOutputFileName(candidateSlots);
    }

    private string CreateWorkflowCtrlRamOutputFileName(
        IEnumerable<FirmwareSlotViewModel> candidateSlots,
        WorkbenchCtrlRamFirmwareVersionEdit? edit)
    {
        return WorkflowSession.CreateCtrlRamReplaceOutputFileName(candidateSlots, edit);
    }

    private void ApplyWorkflowContext(WorkflowSessionPresentationViewModel.WorkflowContextSelection selection)
    {
        if (selection.Page == ShellPage.Replace)
        {
            SelectReplaceMode(selection.Mode);
        }
        else
        {
            Merge.SelectMergeMode(selection.Mode);
            NavigateToPage(ShellPage.Merge);
        }
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
