using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class WorkflowSessionPresentationViewModel
{
    private WorkflowContextTarget? _workflowContextTarget;
    private string _replaceWorkflowContextIc;
    private string _replaceWorkflowContextNumber = WorkbenchIcNumberTokens.SingleChip;

    /// <summary>Gets the cancelable IC context draft shown for Home workflow shortcuts.</summary>
    public WorkflowContextSetupViewModel WorkflowContextSetup { get; }

    /// <summary>True while the Home workflow context dialog is open.</summary>
    [ObservableProperty]
    public partial bool IsWorkflowContextModalOpen { get; set; }

    /// <summary>Explains what the selected context will configure when confirmed.</summary>
    public string WorkflowContextDetail { get; private set; } = string.Empty;

    /// <summary>Command that confirms the pending Home workflow context selection.</summary>
    public IRelayCommand ConfirmWorkflowContextCommand { get; }

    /// <summary>Command that dismisses the pending Home workflow context selection without changes.</summary>
    public IRelayCommand CancelWorkflowContextCommand { get; }

    internal void BeginWorkflowContext(
        ShellPage page,
        string mode,
        bool showNumber,
        IReadOnlyList<string>? icChoices = null)
    {
        icChoices ??= string.Equals(mode, WorkbenchMergeModes.AbCode, StringComparison.Ordinal)
            ? [.. _compositionServices.Capabilities.GetAbMergeProfileSummaries()
                .Select(static profile => profile.IcId)]
            : null;
        _workflowContextTarget = new WorkflowContextTarget(page, mode, showNumber);
        string draftIc = page == ShellPage.Replace ? _replaceWorkflowContextIc : SelectedIc;
        string draftNumber = page == ShellPage.Replace ? _replaceWorkflowContextNumber : SelectedNumber;
        WorkflowContextSetup.Configure(draftIc, draftNumber, showNumber, icChoices);
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

        IsWorkflowContextModalOpen = false;
        _workflowContextTarget = null;
        if (target.Page == ShellPage.Replace)
        {
            _replaceWorkflowContextIc = WorkflowContextSetup.SelectedIc;
            _replaceWorkflowContextNumber = WorkflowContextSetup.SelectedNumber;
        }

        SelectedIc = WorkflowContextSetup.SelectedIc;
        if (target.ShowNumber)
        {
            SelectedNumber = WorkflowContextSetup.SelectedNumber;
        }

        _applyWorkflowContext(new WorkflowContextSelection(
            target.Page,
            target.Mode,
            target.ShowNumber,
            WorkflowContextSetup.SelectedIc,
            WorkflowContextSetup.SelectedNumber));
    }

    private void CancelWorkflowContext()
    {
        _workflowContextTarget = null;
        IsWorkflowContextModalOpen = false;
    }

    internal void RememberReplaceWorkflowContext()
    {
        if (_stateBindings.SelectedPage() != ShellPage.Replace)
        {
            return;
        }

        _replaceWorkflowContextIc = SelectedIc;
        _replaceWorkflowContextNumber = SelectedNumber;
    }

    private sealed record WorkflowContextTarget(ShellPage Page, string Mode, bool ShowNumber);
}
