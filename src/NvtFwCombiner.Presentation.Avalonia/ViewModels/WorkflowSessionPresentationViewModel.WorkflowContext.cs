using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    private WorkflowContextTarget? _workflowContextTarget;
    private string _mergeWorkflowContextIc = string.Empty;
    private string _mergeWorkflowContextNumber = IcNumberSelectionTokens.SingleChip;
    private string _replaceWorkflowContextIc = string.Empty;
    private string _replaceWorkflowContextNumber = IcNumberSelectionTokens.SingleChip;
    private bool _mergeWorkflowContextNeedsRefresh;
    private bool _replaceWorkflowContextNeedsRefresh;
    private bool _isActivatingWorkflowPageContext;

    /// <summary>Gets the cancelable IC context draft shown for Home workflow shortcuts.</summary>
    public WorkflowContextSetupViewModel WorkflowContextSetup { get; }

    [ObservableProperty]
    public partial bool IsWorkflowContextModalOpen { get; set; }

    public string WorkflowContextDetail { get; private set; } = string.Empty;

    public IRelayCommand ConfirmWorkflowContextCommand { get; }

    /// <summary>Command that dismisses the pending Home workflow context selection without changes.</summary>
    public IRelayCommand CancelWorkflowContextCommand { get; }

    internal void BeginWorkflowContext(
        ShellPage page,
        string mode,
        bool showNumber)
    {
        if (_selectorPublication is not { } publication || publication.IcIds.Count == 0)
        {
            return;
        }

        IReadOnlyList<string> icChoices = GetPublishedWorkflowIcChoices(mode);
        if (icChoices.Count == 0)
        {
            return;
        }
        _workflowContextTarget = new WorkflowContextTarget(page, mode, showNumber);
        (string draftIc, string draftNumber) = GetWorkflowPageContext(page);
        WorkflowContextSetup.Configure(publication, draftIc, draftNumber, showNumber, icChoices);
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
        SetWorkflowPageContext(
            target.Page,
            WorkflowContextSetup.SelectedIc,
            target.ShowNumber
                ? WorkflowContextSetup.SelectedNumber
                : GetWorkflowPageContext(target.Page).Number);

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

    private void InvalidateWorkflowContextDraft()
    {
        _workflowContextTarget = null;
        IsWorkflowContextModalOpen = false;
        WorkflowContextSetup.Clear();
    }

    private void ReconcileOpenWorkflowContext(CapabilitySelectorPublication publication)
    {
        if (_workflowContextTarget is not { } target)
        {
            return;
        }

        ReadOnlyCollection<string> choices = GetPublishedWorkflowIcChoices(
            publication,
            target.Mode);
        if (choices.Count == 0)
        {
            InvalidateWorkflowContextDraft();
            return;
        }

        (string draftIc, string draftNumber) = GetWorkflowPageContext(target.Page);
        WorkflowContextSetup.Configure(
            publication,
            draftIc,
            draftNumber,
            target.ShowNumber,
            choices);
    }

    internal WorkflowInspectionOwner? ActiveWorkflowOwner => _stateBindings.SelectedPage() switch
    {
        ShellPage.Merge => WorkflowInspectionOwner.Merge,
        ShellPage.Replace => WorkflowInspectionOwner.Replace,
        ShellPage.Home or ShellPage.HexEditor => null,
        _ => throw new InvalidOperationException("Unknown shell page."),
    };

    internal void InitializeWorkflowPageContexts(string? defaultIc)
    {
        string resolvedIc = ResolveWorkflowContextIc(defaultIc);
        _mergeWorkflowContextIc = resolvedIc;
        _replaceWorkflowContextIc = resolvedIc;
        _mergeWorkflowContextNumber = IcNumberSelectionTokens.SingleChip;
        _replaceWorkflowContextNumber = IcNumberSelectionTokens.SingleChip;
        _mergeWorkflowContextNeedsRefresh = false;
        _replaceWorkflowContextNeedsRefresh = false;
    }

    internal void RememberCurrentWorkflowContext()
    {
        if (ActiveWorkflowOwner is { } owner)
        {
            StoreWorkflowPageContext(owner, SelectedIc, SelectedNumber);
        }
    }

    internal void ActivateWorkflowPageContext(ShellPage page)
    {
        if (page is not (ShellPage.Merge or ShellPage.Replace))
        {
            return;
        }

        (string ic, string number) = GetWorkflowPageContext(page);
        ic = ResolveWorkflowContextIc(ic);
        _isActivatingWorkflowPageContext = true;
        try
        {
            // SelectedPage already names the destination. Publish its IC list
            // before restoring SelectedIc so a TwoWay ComboBox cannot reject
            // the destination value against the previous page's filtered list.
            OnPropertyChanged(nameof(IcChoices));
            SelectedIc = ic;
            SelectedNumber = number;
            RefreshNumberChoicesForSelectedIc();
        }
        finally
        {
            _isActivatingWorkflowPageContext = false;
        }

        StoreWorkflowPageContext(ActiveWorkflowOwner, SelectedIc, SelectedNumber);
        bool needsRefresh = page == ShellPage.Merge
            ? _mergeWorkflowContextNeedsRefresh
            : _replaceWorkflowContextNeedsRefresh;
        if (!needsRefresh)
        {
            return;
        }

        WorkflowInspectionOwner owner = page == ShellPage.Merge
            ? WorkflowInspectionOwner.Merge
            : WorkflowInspectionOwner.Replace;
        InvalidateFirmwareInspection(
            owner,
            clearBaseProjection: owner == WorkflowInspectionOwner.Replace,
            clearSlotProjections: true);
        if (owner == WorkflowInspectionOwner.Replace)
        {
            _replace.InvalidateCtrlRamFirmwareVersionContextState();
        }
        RefreshContextState(
            owner,
            resetRunResult: true,
            preserveReplaceSlotFiles: owner == WorkflowInspectionOwner.Replace);
        RefreshRetainedFirmwareInspections(owner);
        if (page == ShellPage.Merge)
        {
            _mergeWorkflowContextNeedsRefresh = false;
        }
        else
        {
            _replaceWorkflowContextNeedsRefresh = false;
        }
    }

    internal void StoreWorkflowPageContext(
        WorkflowInspectionOwner? owner,
        string ic,
        string number)
    {
        ic = ResolveWorkflowContextIc(ic);
        if (owner is null or WorkflowInspectionOwner.Merge)
        {
            _mergeWorkflowContextIc = ic;
            _mergeWorkflowContextNumber = number;
        }
        if (owner is null or WorkflowInspectionOwner.Replace)
        {
            _replaceWorkflowContextIc = ic;
            _replaceWorkflowContextNumber = number;
        }
    }

    internal string GetWorkflowPageIc(WorkflowInspectionOwner owner)
    {
        return owner switch
        {
            WorkflowInspectionOwner.Merge => _mergeWorkflowContextIc,
            WorkflowInspectionOwner.Replace => _replaceWorkflowContextIc,
            _ => throw new InvalidOperationException("Unknown workflow inspection owner."),
        };
    }

    internal string GetWorkflowPageNumber(WorkflowInspectionOwner owner)
    {
        return owner switch
        {
            WorkflowInspectionOwner.Merge => _mergeWorkflowContextNumber,
            WorkflowInspectionOwner.Replace => _replaceWorkflowContextNumber,
            _ => throw new InvalidOperationException("Unknown workflow inspection owner."),
        };
    }

    private (string Ic, string Number) GetWorkflowPageContext(ShellPage page)
    {
        return page switch
        {
            ShellPage.Merge => (_mergeWorkflowContextIc, _mergeWorkflowContextNumber),
            ShellPage.Replace => (_replaceWorkflowContextIc, _replaceWorkflowContextNumber),
            ShellPage.Home or ShellPage.HexEditor =>
                (SelectedIc, SelectedNumber),
            _ => throw new InvalidOperationException("Unknown shell page."),
        };
    }

    private void SetWorkflowPageContext(ShellPage page, string ic, string number)
    {
        ic = ResolveWorkflowContextIc(ic);
        switch (page)
        {
            case ShellPage.Merge:
                _mergeWorkflowContextNeedsRefresh =
                    !string.Equals(_mergeWorkflowContextIc, ic, StringComparison.Ordinal) ||
                    !string.Equals(_mergeWorkflowContextNumber, number, StringComparison.Ordinal);
                _mergeWorkflowContextIc = ic;
                _mergeWorkflowContextNumber = number;
                break;
            case ShellPage.Replace:
                _replaceWorkflowContextNeedsRefresh =
                    !string.Equals(_replaceWorkflowContextIc, ic, StringComparison.Ordinal) ||
                    !string.Equals(_replaceWorkflowContextNumber, number, StringComparison.Ordinal);
                _replaceWorkflowContextIc = ic;
                _replaceWorkflowContextNumber = number;
                break;
            case ShellPage.Home:
            case ShellPage.HexEditor:
                throw new InvalidOperationException("Workflow context requires Merge or Replace ownership.");
            default:
                throw new InvalidOperationException("Unknown shell page.");
        }
    }

    private string ResolveWorkflowContextIc(string? candidate)
    {
        IReadOnlyList<string> choices = _selectorPublication?.IcIds ?? [];
        return !string.IsNullOrWhiteSpace(candidate) &&
            choices.Contains(candidate, StringComparer.Ordinal)
                ? candidate
                : _selectorPublication?.DefaultIcId ?? string.Empty;
    }

    private sealed record WorkflowContextTarget(ShellPage Page, string Mode, bool ShowNumber);
}
