using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    private CapabilitySelectorPublication? _selectorPublication;

    internal bool HasPublishedWorkflowAuthoringChoices(params string[] workflowIds)
    {
        ArgumentNullException.ThrowIfNull(workflowIds);
        return _selectorPublication is { } publication &&
            publication.IcIds.Any(icId => workflowIds.Any(workflowId =>
                publication.IsWorkflowAuthorable(icId, workflowId)));
    }

    internal IReadOnlyList<string> GetPublishedWorkflowIcChoices(string workflowId)
    {
        return _selectorPublication is { } publication
            ? WorkflowSelectorProjection.WorkflowIcChoices(publication, workflowId)
            : [];
    }

    private ReadOnlyCollection<string> GetPublishedPageIcChoices(ShellPage page)
    {
        return _selectorPublication is { } publication
            ? WorkflowSelectorProjection.PageIcChoices(publication, page)
            : Array.AsReadOnly(Array.Empty<string>());
    }

    private void ApplySelectorPublication(CapabilitySelectorPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        string previousMergeIc = _mergeWorkflowContextIc;
        string previousReplaceIc = _replaceWorkflowContextIc;
        string previousMergeNumber = _mergeWorkflowContextNumber;
        string previousReplaceNumber = _replaceWorkflowContextNumber;
        _selectorPublication = publication;

        if (publication.IcIds.Count == 0)
        {
            _mergeWorkflowContextIc = string.Empty;
            _replaceWorkflowContextIc = string.Empty;
            _mergeWorkflowContextNumber = string.Empty;
            _replaceWorkflowContextNumber = string.Empty;
            _mergeWorkflowContextNeedsRefresh = !string.IsNullOrEmpty(previousMergeIc);
            _replaceWorkflowContextNeedsRefresh = !string.IsNullOrEmpty(previousReplaceIc);
            InvalidateWorkflowContextDraft();
            PublishActiveSelectorState(string.Empty, string.Empty);
            PublishCurrentCatalogChoices();
            return;
        }

        WorkflowPageCatalogReconciliation mergeReconciliation =
            WorkflowNavigationTransaction.ReconcileCatalogPage(
                publication,
                ShellPage.Merge,
                previousMergeIc,
                previousMergeNumber,
                _merge,
                _replace);
        WorkflowPageCatalogReconciliation replaceReconciliation =
            WorkflowNavigationTransaction.ReconcileCatalogPage(
                publication,
                ShellPage.Replace,
                previousReplaceIc,
                previousReplaceNumber,
                _merge,
                _replace);
        _mergeWorkflowContextIc = mergeReconciliation.IcId;
        _replaceWorkflowContextIc = replaceReconciliation.IcId;
        _mergeWorkflowContextNumber = mergeReconciliation.Number;
        _replaceWorkflowContextNumber = replaceReconciliation.Number;
        _mergeWorkflowContextNeedsRefresh = mergeReconciliation.NeedsRefresh;
        _replaceWorkflowContextNeedsRefresh = replaceReconciliation.NeedsRefresh;

        ReconcileOpenWorkflowContext(publication);
        (string activeIc, string activeNumber) = ActiveWorkflowOwner switch
        {
            WorkflowInspectionOwner.Merge =>
                (_mergeWorkflowContextIc, _mergeWorkflowContextNumber),
            WorkflowInspectionOwner.Replace =>
                (_replaceWorkflowContextIc, _replaceWorkflowContextNumber),
            null => (
                publication.DefaultIcId!,
                ResolvePublishedNumber(
                    publication.DefaultIcId!,
                    SelectedNumber,
                    useAbTopology: false)),
            _ => throw new InvalidOperationException("Unknown workflow inspection owner."),
        };
        PublishActiveSelectorState(activeIc, activeNumber);
        if (mergeReconciliation.ModeChanged)
        {
            _merge.PublishCatalogReconciledMergeMode();
        }
        if (replaceReconciliation.ModeChanged)
        {
            _replace.PublishCatalogReconciledReplaceMode();
        }
        PublishCurrentCatalogChoices();
    }

    private string ResolvePublishedNumber(
        string icId,
        string preferredToken,
        bool useAbTopology)
    {
        return WorkflowSelectorProjection.Number(
            _selectorPublication ?? throw new InvalidOperationException(
                "Canonical selector publication is not ready."),
            icId,
            preferredToken,
            useAbTopology);
    }

    private void RefreshGeneralMergeDefaults(string icId)
    {
        _merge.RefreshGeneralMergeDefaults(
            icId,
            IsPublishedWorkflowAuthorable(icId, ExperienceIds.GeneralMerge));
    }

    private void ValidateWorkflowContextRefresh(
        CapabilitySelectorPublication publication,
        ShellPage page)
    {
        string retainedIc = page == ShellPage.Merge
            ? _mergeWorkflowContextIc
            : _replaceWorkflowContextIc;
        string retainedNumber = page == ShellPage.Merge
            ? _mergeWorkflowContextNumber
            : _replaceWorkflowContextNumber;
        WorkflowNavigationTransaction.PrepareContextRefresh(
            publication,
            page,
            retainedIc,
            retainedNumber,
            _merge,
            _replace);
    }
}
