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

        (string previousIc, string previousNumber) = GetWorkflowPageContext(target.Page);
        bool previousNeedsRefresh = target.Page == ShellPage.Merge
            ? _mergeWorkflowContextNeedsRefresh
            : _replaceWorkflowContextNeedsRefresh;
        string selectedIc = WorkflowContextSetup.SelectedIc;
        string selectedNumber = target.ShowNumber
            ? WorkflowContextSetup.SelectedNumber
            : previousNumber;
        selectedNumber = target.ShowNumber
            ? ResolvePublishedNumber(
                ResolveWorkflowContextIc(selectedIc, target.Page),
                selectedNumber,
                useAbTopology: StringComparer.Ordinal.Equals(target.Mode, ExperienceIds.AbMerge))
            : selectedNumber;
        SetWorkflowPageContext(
            target.Page,
            selectedIc,
            selectedNumber);
        (selectedIc, selectedNumber) = GetWorkflowPageContext(target.Page);
        var selection = new WorkflowContextSelection(
            target.Page,
            target.Mode,
            target.ShowNumber,
            selectedIc,
            selectedNumber);
        try
        {
            _applyWorkflowContext(selection);
        }
        catch
        {
            SetWorkflowPageContext(target.Page, previousIc, previousNumber);
            if (target.Page == ShellPage.Merge)
            {
                _mergeWorkflowContextNeedsRefresh = previousNeedsRefresh;
            }
            else
            {
                _replaceWorkflowContextNeedsRefresh = previousNeedsRefresh;
            }
            throw;
        }

        _workflowContextTarget = null;
        IsWorkflowContextModalOpen = false;
        WorkflowNavigationTransaction.RecordConfirmedSelection(
            _recordActivity,
            selection,
            previousIc,
            previousNumber);
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

        System.Collections.ObjectModel.ReadOnlyCollection<string> choices =
            WorkflowSelectorProjection.WorkflowIcChoices(publication, target.Mode);
        if (choices.Count == 0)
        {
            InvalidateWorkflowContextDraft();
            return;
        }

        (string committedIc, string committedNumber) = GetWorkflowPageContext(target.Page);
        bool retainsModalIc = choices.Contains(
            WorkflowContextSetup.SelectedIc,
            StringComparer.Ordinal);
        string draftIc = retainsModalIc
            ? WorkflowContextSetup.SelectedIc
            : committedIc;
        string draftNumber = retainsModalIc
            ? WorkflowContextSetup.SelectedNumber
            : committedNumber;
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
        _mergeWorkflowContextIc = ResolveWorkflowContextIc(defaultIc, ShellPage.Merge);
        _replaceWorkflowContextIc = ResolveWorkflowContextIc(defaultIc, ShellPage.Replace);
        _mergeWorkflowContextNumber = ResolvePublishedNumber(
            _mergeWorkflowContextIc,
            IcNumberSelectionTokens.SingleChip,
            useAbTopology: false);
        _replaceWorkflowContextNumber = ResolvePublishedNumber(
            _replaceWorkflowContextIc,
            IcNumberSelectionTokens.SingleChip,
            useAbTopology: false);
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

    internal PageActivationRollback CapturePageActivationRollback()
    {
        return new PageActivationRollback(SelectedIc, SelectedNumber, IsWorkflowLoaded);
    }

    internal void ValidatePageActivation(ShellPage page)
    {
        if (page is not (ShellPage.Merge or ShellPage.Replace) ||
            _selectorPublication is not { } publication)
        {
            return;
        }

        ValidateWorkflowContextRefresh(publication, page);
    }

    internal void RestorePageActivation(
        ShellPage page,
        PageActivationRollback rollback,
        bool restoreContext)
    {
        if (restoreContext)
        {
            RestoreNavigationContext(page, rollback.SelectedIc, rollback.SelectedNumber);
        }

        IsWorkflowLoaded = rollback.WorkflowLoaded;
    }

    internal void ActivateWorkflowPageContext(ShellPage page)
    {
        if (page is not (ShellPage.Merge or ShellPage.Replace))
        {
            return;
        }

        (string ic, string number) = GetWorkflowPageContext(page);
        ic = ResolveWorkflowContextIc(ic, page);
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
        if (page == ShellPage.Merge && IsWorkflowLoaded)
        {
            RefreshGeneralMergeDefaults(SelectedIc);
        }
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

    internal void RestoreNavigationContext(
        ShellPage page,
        string previousIc,
        string previousNumber)
    {
        PublishActiveSelectorState(previousIc, previousNumber);
        if (page is ShellPage.Merge or ShellPage.Replace)
        {
            StoreWorkflowPageContext(
                page == ShellPage.Merge
                    ? WorkflowInspectionOwner.Merge
                    : WorkflowInspectionOwner.Replace,
                previousIc,
                previousNumber);
        }
    }

    internal WorkflowModeNavigationStage StageWorkflowModeForNavigation(
        ShellPage page,
        string mode)
    {
        bool previousNeedsRefresh = page == ShellPage.Merge
            ? _mergeWorkflowContextNeedsRefresh
            : _replaceWorkflowContextNeedsRefresh;
        var stage = WorkflowModeNavigationStage.Create(
            page,
            mode,
            GetWorkflowPageContext(page).Ic,
            previousNeedsRefresh,
            _merge,
            _replace,
            IsPublishedWorkflowAuthorable);
        SetWorkflowContextNeedsRefresh(page, previousNeedsRefresh || stage.Changed);
        return stage;
    }

    internal void RestoreStagedWorkflowMode(WorkflowModeNavigationStage stage)
    {
        stage.Restore(_merge, _replace);
        SetWorkflowContextNeedsRefresh(stage.Page, stage.PreviousNeedsRefresh);
    }

    internal void PublishStagedWorkflowMode(WorkflowModeNavigationStage stage)
    {
        stage.Publish(_merge, _replace);
    }

    private void SetWorkflowContextNeedsRefresh(ShellPage page, bool value)
    {
        if (page == ShellPage.Merge)
        {
            _mergeWorkflowContextNeedsRefresh = value;
            return;
        }
        _replaceWorkflowContextNeedsRefresh = value;
    }

    internal void StoreWorkflowPageContext(
        WorkflowInspectionOwner? owner,
        string ic,
        string number)
    {
        if (owner is null or WorkflowInspectionOwner.Merge)
        {
            string mergeIc = ResolveWorkflowContextIc(ic, ShellPage.Merge);
            _mergeWorkflowContextIc = mergeIc;
            _mergeWorkflowContextNumber = ResolvePublishedNumber(
                mergeIc,
                number,
                useAbTopology: _merge.IsAbCodeMergeModeSelected);
        }
        if (owner is null or WorkflowInspectionOwner.Replace)
        {
            string replaceIc = ResolveWorkflowContextIc(ic, ShellPage.Replace);
            _replaceWorkflowContextIc = replaceIc;
            _replaceWorkflowContextNumber = ResolvePublishedNumber(
                replaceIc,
                number,
                useAbTopology: false);
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
        ic = ResolveWorkflowContextIc(ic, page);
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

    private string ResolveWorkflowContextIc(string? candidate, ShellPage page)
    {
        CapabilitySelectorPublication? publication = _selectorPublication;
        return publication is null
            ? string.Empty
            : WorkflowSelectorProjection.ContextIc(publication, candidate, page);
    }

}
