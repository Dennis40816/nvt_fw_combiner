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

        string mergeIc = ResolveWorkflowContextIc(previousMergeIc, ShellPage.Merge);
        string replaceIc = ResolveWorkflowContextIc(previousReplaceIc, ShellPage.Replace);
        _mergeWorkflowContextIc = mergeIc;
        _replaceWorkflowContextIc = replaceIc;

        bool mergeModeReconciled = _merge.StageAuthorableModeForCatalogReconciliation(
            workflowId => !string.IsNullOrWhiteSpace(mergeIc) &&
                publication.IsWorkflowAuthorable(mergeIc, workflowId));
        bool replaceModeReconciled = _replace.StageAuthorableModeForCatalogReconciliation(
            workflowId => !string.IsNullOrWhiteSpace(replaceIc) &&
                publication.IsWorkflowAuthorable(replaceIc, workflowId));

        _mergeWorkflowContextNumber = ResolvePublishedNumber(
            mergeIc,
            previousMergeNumber,
            useAbTopology: _merge.IsAbCodeMergeModeSelected);
        _replaceWorkflowContextNumber = ResolvePublishedNumber(
            replaceIc,
            previousReplaceNumber,
            useAbTopology: false);
        _mergeWorkflowContextNeedsRefresh =
            !string.Equals(previousMergeIc, _mergeWorkflowContextIc, StringComparison.Ordinal) ||
            !string.Equals(previousMergeNumber, _mergeWorkflowContextNumber, StringComparison.Ordinal) ||
            mergeModeReconciled;
        _replaceWorkflowContextNeedsRefresh =
            !string.Equals(previousReplaceIc, _replaceWorkflowContextIc, StringComparison.Ordinal) ||
            !string.Equals(previousReplaceNumber, _replaceWorkflowContextNumber, StringComparison.Ordinal) ||
            replaceModeReconciled;

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
        if (mergeModeReconciled)
        {
            _merge.PublishCatalogReconciledMergeMode();
        }
        if (replaceModeReconciled)
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
        if (!IsPublishedWorkflowAuthorable(icId, ExperienceIds.GeneralMerge) ||
            string.Equals(_generalMergeDefaultsIc, icId, StringComparison.Ordinal))
        {
            return;
        }

        bool canReplaceCurrentValues = _generalMergeDefaultsIc is null ||
            (string.Equals(
                _merge.GeneralMergeOutputLength,
                _generalMergeDefaultLength,
                StringComparison.Ordinal) &&
            string.Equals(
                _merge.GeneralMergeOutputFillByte,
                _generalMergeDefaultFillByte,
                StringComparison.Ordinal));
        if (!canReplaceCurrentValues)
        {
            return;
        }

        (string length, string fillByte) = GetGeneralMergeDefaults(icId);
        _generalMergeDefaultsIc = icId;
        _generalMergeDefaultLength = length;
        _generalMergeDefaultFillByte = fillByte;
        _merge.ApplyGeneralMergeOutputInitializer(length, fillByte);
    }

    private (string Length, string FillByte) GetGeneralMergeDefaults(string icId)
    {
        if (string.Equals(_preparedGeneralMergeDefaultsIc, icId, StringComparison.Ordinal))
        {
            (string Length, string FillByte) prepared = (
                _preparedGeneralMergeDefaultLength,
                _preparedGeneralMergeDefaultFillByte);
            _preparedGeneralMergeDefaultsIc = null;
            _preparedGeneralMergeDefaultLength = string.Empty;
            _preparedGeneralMergeDefaultFillByte = string.Empty;
            return prepared;
        }

        return ResolveGeneralMergeDefaults(icId);
    }

    private (string Length, string FillByte) ResolveGeneralMergeDefaults(string icId)
    {
        string length = _compositionServices.GeneralAuthoring.GetDefaultOutputLength(icId);
        string fillByte = _compositionServices.GeneralAuthoring.GetDefaultOutputFillByte(icId);
        return (length, fillByte);
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
        _preparedGeneralMergeDefaultsIc = null;
        PreparedGeneralMergeDefaults? prepared =
            WorkflowNavigationTransaction.PrepareContextRefresh(
                publication,
                page,
                retainedIc,
                retainedNumber,
                _merge,
                _replace,
                _generalMergeDefaultsIc,
                _generalMergeDefaultLength,
                _generalMergeDefaultFillByte,
                ResolveGeneralMergeDefaults);
        if (prepared is not null)
        {
            _preparedGeneralMergeDefaultsIc = prepared.IcId;
            _preparedGeneralMergeDefaultLength = prepared.Length;
            _preparedGeneralMergeDefaultFillByte = prepared.FillByte;
        }
    }
}
