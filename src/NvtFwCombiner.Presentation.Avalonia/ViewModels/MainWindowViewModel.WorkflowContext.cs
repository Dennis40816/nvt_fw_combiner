using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private WorkflowContextTarget? _workflowContextTarget;

    /// <summary>Gets the cancelable IC context draft shown for Home workflow shortcuts.</summary>
    public WorkflowContextSetupViewModel WorkflowContextSetup { get; } = new();

    /// <summary>True while the Home workflow context dialog is open.</summary>
    [ObservableProperty]
    public partial bool IsWorkflowContextModalOpen { get; set; }

    /// <summary>Explains what the selected context will configure when confirmed.</summary>
    public string WorkflowContextDetail { get; private set; } = string.Empty;

    /// <summary>Command that confirms the pending Home workflow context selection.</summary>
    public IRelayCommand ConfirmWorkflowContextCommand { get; }

    /// <summary>Command that dismisses the pending Home workflow context selection without changes.</summary>
    public IRelayCommand CancelWorkflowContextCommand { get; }

    private void BeginWorkflowContext(
        ShellPage page,
        string mode,
        bool showNumber,
        IReadOnlyList<string>? icChoices = null)
    {
        _workflowContextTarget = new WorkflowContextTarget(page, mode, showNumber);
        WorkflowContextSetup.Configure(SelectedIc, SelectedNumber, showNumber, icChoices);
        WorkflowContextDetail = page == ShellPage.Replace
            ? Text.WorkflowContextReplaceDetail
            : Text.WorkflowContextMergeDetail;
        OnPropertyChanged(nameof(WorkflowContextDetail));
        IsWorkflowContextModalOpen = true;
    }

    private void ConfirmWorkflowContext()
    {
        if (_workflowContextTarget is not { } target)
        {
            IsWorkflowContextModalOpen = false;
            return;
        }

        SelectedIc = WorkflowContextSetup.SelectedIc;
        if (target.ShowNumber)
        {
            SelectedNumber = WorkflowContextSetup.SelectedNumber;
        }

        IsWorkflowContextModalOpen = false;
        _workflowContextTarget = null;
        if (target.Page == ShellPage.Replace)
        {
            SelectReplaceMode(target.Mode);
        }
        else
        {
            SelectMergeMode(target.Mode);
        }
    }

    private void CancelWorkflowContext()
    {
        _workflowContextTarget = null;
        IsWorkflowContextModalOpen = false;
    }

    private sealed record WorkflowContextTarget(ShellPage Page, string Mode, bool ShowNumber);
}
