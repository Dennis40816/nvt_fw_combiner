using System.ComponentModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MergePresentationViewModel
{
    private IReadOnlyList<AuthoringMappingState> _generalMergeAuthoringStates = [];
    private GeneralMergeDraftState? _generalMergeDraft;
    private GeneralAuthoringAdmissionResult? _generalMergeAdmission;
    private CapabilityActionReadinessSnapshot? _generalMergeActionReadiness;

    internal void AddGeneralMergeMapping()
    {
        _generalMergeMappingCounter++;
        var mapping = new GeneralMergeMappingViewModel(
            $"general-merge-map-{_generalMergeMappingCounter}",
            GeneralMergeMappings.Count + 1,
            Text);
        mapping.PropertyChanged += GeneralMergeMappingPropertyChanged;
        GeneralMergeMappings.Add(mapping);
        RefreshMergeMemoryMapState();
        RefreshCommandState();
    }

    private IReadOnlyList<AuthoringMappingState> CreateGeneralMergeAuthoringStates()
    {
        return
        [
            .. GeneralMergeMappings
                .Where(mapping => mapping.HasFile)
                .Select(mapping => WorkbenchCompositionService.CreateGeneralMergeAuthoringState(
                    mapping.MappingId,
                    mapping.FilePath!,
                    mapping.SourceStartAddress,
                    mapping.TargetStartAddress,
                    mapping.Length,
                    acceptedFileStamp: mapping.AcceptedFileStamp)),
        ];
    }

    private void RefreshGeneralMergeAuthoringState()
    {
        _generalMergeAuthoringStates = CreateGeneralMergeAuthoringStates();
        _generalMergeAdmission = null;
        _generalMergeActionReadiness = null;
        string[] selectedMappingIds =
        [
            .. GeneralMergeMappings
                .Where(static mapping => mapping.HasFile)
                .Select(static mapping => mapping.MappingId),
        ];
        if (GeneralMergeMappings.Any(static mapping =>
                mapping.HasFile && mapping.AcceptedFileStamp is null))
        {
            _ = WorkbenchCompositionService.PrepareGeneralMergeSelectionSession(
                _authoringSessions.GeneralMerge,
                SelectedIc,
                selectedMappingIds);
        }
        _generalMergeDraft =
            _generalMergeAuthoringStates.Count > 0 &&
            TryResolveGeneralMergeOutputInitializer(out WorkbenchGeneralMergeInitializer? initializer) &&
            WorkbenchCompositionService.TryCreateGeneralMergeAuthoringDraft(
                _generalMergeAuthoringStates,
                out GeneralMappingDraftState? mappings,
                out _)
                ? WorkbenchCompositionService.CreateGeneralMergeDraft(initializer!, mappings!)
                : null;
        if (_generalMergeDraft is not null)
        {
            _generalMergeAdmission = WorkbenchCompositionService
                .GetGeneralMergeAuthoringAdmission(SelectedIc, _generalMergeDraft);
            _generalMergeActionReadiness = WorkbenchCompositionService
                .GetGeneralMergeActionReadiness(
                    _authoringSessions.GeneralMerge,
                    SelectedIc,
                    _generalMergeDraft);
            while (_generalMergeActionReadiness is null &&
                TryAcceptCachedGeneralMergeInspection(_generalMergeDraft) &&
                _generalMergeDraft is { } acceptedDraft)
            {
                _generalMergeAdmission = WorkbenchCompositionService
                    .GetGeneralMergeAuthoringAdmission(SelectedIc, acceptedDraft);
                _generalMergeActionReadiness = WorkbenchCompositionService
                    .GetGeneralMergeActionReadiness(
                        _authoringSessions.GeneralMerge,
                        SelectedIc,
                        acceptedDraft);
            }
            if (_generalMergeActionReadiness is null)
            {
                _ = WorkbenchCompositionService.PrepareGeneralMergeSelectionSession(
                        _authoringSessions.GeneralMerge,
                        SelectedIc,
                        _generalMergeDraft.Mappings.Rows.Select(static row => row.MappingId)) &&
                    _authoringSessions.GeneralMerge.SetDraft(_generalMergeDraft).Succeeded;
            }
        }
    }

    internal AuthoringPublicationLease? CaptureGeneralMergePrebindingLease(
        GeneralMergeMappingViewModel mapping,
        string path)
    {
        return mapping.CapturePrebindingLease(_authoringSessions.GeneralMerge, path);
    }

    internal bool TryCacheGeneralMergeInspection(
        GeneralMergeMappingViewModel mapping,
        AuthoringPublicationLease lease,
        GeneralSelectedFileInspectionResult result)
    {
        return mapping.TryCacheInspection(_authoringSessions.GeneralMerge, lease, result);
    }

    private bool TryAcceptCachedGeneralMergeInspection(GeneralMergeDraftState draft)
    {
        return GeneralMergeMappings
            .OrderBy(mapping => mapping.IsInspectionVerified(_authoringSessions.GeneralMerge))
            .Any(mapping => mapping.TryAcceptCachedInspection(
                _authoringSessions.GeneralMerge,
                cached => WorkbenchCompositionService.BeginGeneralMergeSelectedFileInspection(
                    _authoringSessions.GeneralMerge,
                    SelectedIc,
                    draft,
                    mapping.MappingId,
                    cached.FileStamp.AcceptedLength)));
    }

    internal AuthoringSlotInspectionStartResult BeginGeneralMergeFileInspection(
        GeneralMergeMappingViewModel mapping,
        long observedLength)
    {
        return _generalMergeDraft is null
            ? new AuthoringSlotInspectionStartResult(
                _authoringSessions.GeneralMerge.CurrentSnapshot,
                Lease: null,
                new AuthoringSessionIssue(
                    AuthoringSessionIssueCodes.DraftUnavailable,
                    "General Merge requires one valid typed draft before file inspection.",
                    mapping.MappingId))
            : WorkbenchCompositionService.BeginGeneralMergeSelectedFileInspection(
                _authoringSessions.GeneralMerge,
                SelectedIc,
                _generalMergeDraft,
                mapping.MappingId,
                observedLength);
    }

    internal bool TryPublishGeneralMergeFileInspection(
        GeneralMergeMappingViewModel mapping,
        AuthoringSlotInspectionLease lease,
        GeneralSelectedFileInspectionResult result)
    {
        return mapping.TryPublishInspection(_authoringSessions.GeneralMerge, lease, result);
    }

    internal bool RemoveGeneralMapping(GeneralMergeMappingViewModel mapping)
    {
        if (GeneralMergeMappings.Count <= 1)
        {
            return false;
        }

        mapping.PropertyChanged -= GeneralMergeMappingPropertyChanged;
        if (!GeneralMergeMappings.Remove(mapping))
        {
            return false;
        }

        int index = 1;
        foreach (GeneralMergeMappingViewModel row in GeneralMergeMappings)
        {
            row.SetIndex(index++);
        }

        RefreshMergeMemoryMapState();
        RefreshCommandState();
        return true;
    }

    private void GeneralMergeMappingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GeneralMergeMappingViewModel.SourceStartAddress) or
            nameof(GeneralMergeMappingViewModel.TargetStartAddress) or
            nameof(GeneralMergeMappingViewModel.Length) or
            nameof(GeneralMergeMappingViewModel.FilePath) or
            nameof(GeneralMergeMappingViewModel.AcceptedFileStamp))
        {
            RefreshMergeMemoryMapState();
            _stateBindings.ResetRunResult();
            RefreshCommandState();
        }
    }
}
