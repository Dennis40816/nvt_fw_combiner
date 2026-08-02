using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Focused shared workflow-context and selected-firmware prompt presentation.</summary>
    public WorkflowSessionPresentationViewModel WorkflowSession { get; }

    private void ApplyWorkflowContext(WorkflowSessionPresentationViewModel.WorkflowContextSelection selection)
    {
        SelectedIc = selection.IcId;
        if (selection.ShowNumber)
        {
            SelectedNumber = selection.Number;
        }

        if (selection.Page == ShellPage.Replace)
        {
            SelectReplaceMode(selection.Mode);
        }
        else
        {
            SelectMergeMode(selection.Mode);
            NavigateToPage(ShellPage.Merge);
        }
    }

    private void ApplyDetectedFirmwareNumber(string numberToken)
    {
        _isApplyingFirmwareInspectionContext = true;
        try
        {
            SelectedNumber = numberToken;
        }
        finally
        {
            _isApplyingFirmwareInspectionContext = false;
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
