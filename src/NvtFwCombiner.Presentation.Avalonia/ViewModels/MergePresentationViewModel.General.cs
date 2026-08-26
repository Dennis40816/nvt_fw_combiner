using System.ComponentModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MergePresentationViewModel
{
    private IReadOnlyList<AuthoringMappingState> _generalMergeAuthoringStates = [];
    private GeneralMergeDraftState? _generalMergeDraft;
    private GeneralAuthoringAdmissionResult? _generalMergeAdmission;
    private string? _preparedGeneralMergeAdmissionIc;
    private GeneralAuthoringAdmissionResult? _preparedGeneralMergeAdmission;
    private CapabilityActionReadinessSnapshot? _generalMergeActionReadiness;
    private bool _isApplyingGeneralMergePreparation;

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

    private void RefreshGeneralMergeAuthoringState()
    {
        bool isAuthorable = HasSelectedIc &&
            _stateBindings.IsWorkflowAuthorable(SelectedIc, GeneralMergeMode);
        foreach (GeneralMergeMappingViewModel mapping in GeneralMergeMappings)
        {
            mapping.SetFileSelectionAvailability(
                isAuthorable,
                Text.FirmwareSlotPendingFactDetail);
        }

        _generalMergeAuthoringStates =
        [
            .. GeneralMergeMappings
                .Where(mapping => mapping.HasFile)
                .Select(mapping => GeneralAuthoringMappingUseCase.CreateGeneralMergeAuthoringState(
                    mapping.MappingId,
                    mapping.FilePath!,
                    mapping.SourceStartAddress,
                    mapping.TargetStartAddress,
                    mapping.Length,
                    acceptedFileStamp: mapping.AcceptedFileStamp)),
        ];
        _generalMergeAdmission = null;
        _generalMergeActionReadiness = null;
        _generalMergeDraft =
            isAuthorable &&
            _generalMergeAuthoringStates.Count > 0 &&
            TryResolveGeneralMergeOutputInitializer(out GeneralMergeInitializer? initializer) &&
            GeneralAuthoringMappingUseCase.TryCreateGeneralMergeAuthoringDraft(
                _generalMergeAuthoringStates,
                out GeneralMappingDraftState? mappings,
                out _)
                ? GeneralMergeAuthoringUseCase.CreateDraft(initializer!, mappings!)
                : null;
        if (_generalMergeDraft is not null)
        {
            if (string.Equals(
                _preparedGeneralMergeAdmissionIc,
                SelectedIc,
                StringComparison.Ordinal))
            {
                _generalMergeAdmission = _preparedGeneralMergeAdmission;
                _preparedGeneralMergeAdmissionIc = null;
                _preparedGeneralMergeAdmission = null;
            }
            else
            {
                _generalMergeAdmission = _compositionServices.GeneralAuthoring.GetMergeAdmission(
                    SelectedIc,
                    _generalMergeDraft);
            }
            _ = PrepareGeneralMergeSessionAsync(_generalMergeDraft);
        }
        else
        {
            InspectionLifecycles[GeneralMergeMode].Invalidate();
        }
    }

    private void ValidateGeneralMergeContextRefresh(
        string icId,
        string outputLength,
        string outputFillByte)
    {
        _preparedGeneralMergeAdmissionIc = null;
        _preparedGeneralMergeAdmission = null;
        IReadOnlyList<AuthoringMappingState> states =
        [
            .. GeneralMergeMappings
                .Where(mapping => mapping.HasFile)
                .Select(mapping => GeneralAuthoringMappingUseCase.CreateGeneralMergeAuthoringState(
                    mapping.MappingId,
                    mapping.FilePath!,
                    mapping.SourceStartAddress,
                    mapping.TargetStartAddress,
                    mapping.Length,
                    acceptedFileStamp: mapping.AcceptedFileStamp)),
        ];
        if (states.Count == 0 ||
            !GeneralMergeAuthoringUseCase.TryResolveOutputInitializer(
                outputLength,
                outputFillByte,
                out GeneralMergeInitializer? initializer) ||
            !GeneralAuthoringMappingUseCase.TryCreateGeneralMergeAuthoringDraft(
                states,
                out GeneralMappingDraftState? mappings,
                out _))
        {
            return;
        }

        GeneralMergeDraftState draft =
            GeneralMergeAuthoringUseCase.CreateDraft(initializer!, mappings!);
        GeneralAuthoringAdmissionResult admission =
            _compositionServices.GeneralAuthoring.GetMergeAdmission(icId, draft);
        _preparedGeneralMergeAdmissionIc = icId;
        _preparedGeneralMergeAdmission = admission;
    }

    private Task<WorkflowInspectionAttemptState> PrepareGeneralMergeSessionAsync(
        GeneralMergeDraftState draft)
    {
        string icId = SelectedIc;
        return InspectionLifecycles[GeneralMergeMode].StartAsync(
            Text,
            async (progress, isCurrent, cancellationToken) =>
            {
                if (!ReferenceEquals(_generalMergeDraft, draft))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                GeneralAuthoringSessionPreparation prepared =
                    await _compositionServices.GeneralAuthoring.PrepareMergeSessionAsync(
                        _generalMergeSession,
                        icId,
                        draft,
                        cancellationToken,
                        progress);
                if (!isCurrent() || !ReferenceEquals(_generalMergeDraft, draft))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                _isApplyingGeneralMergePreparation = true;
                try
                {
                    _generalMergeAdmission = prepared.Admission ?? _generalMergeAdmission;
                    _generalMergeActionReadiness = prepared.Readiness;
                    if (prepared.AcceptedSession?.DraftState is GeneralMergeDraftState accepted)
                    {
                        _generalMergeDraft = accepted;
                    }
                    foreach (GeneralMergeMappingViewModel mapping in GeneralMergeMappings)
                    {
                        mapping.ApplyPreparation(prepared);
                    }
                }
                finally
                {
                    _isApplyingGeneralMergePreparation = false;
                }
                RefreshMergeMemoryMapState(refreshAuthoring: false);
                RefreshCommandState();
                return new(prepared.Succeeded, prepared.Issues is [var issue, ..] ? issue.Code : null);
            },
            CancellationToken.None);
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

    internal void ClearGeneralMergeMappingFilesWithoutRefresh()
    {
        _isApplyingGeneralMergePreparation = true;
        try
        {
            foreach (GeneralMergeMappingViewModel mapping in GeneralMergeMappings)
            {
                mapping.FilePath = null;
            }
        }
        finally
        {
            _isApplyingGeneralMergePreparation = false;
        }

        _generalMergeAuthoringStates = [];
        _generalMergeDraft = null;
        _generalMergeAdmission = null;
        _generalMergeActionReadiness = null;
        InspectionLifecycles[GeneralMergeMode].Invalidate();
    }

    private void GeneralMergeMappingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingGeneralMergePreparation ||
            e.PropertyName is not (
                nameof(GeneralMappingRowViewModel.FilePath) or
                nameof(GeneralMappingRowViewModel.SourceStartAddress) or
                nameof(GeneralMappingRowViewModel.TargetStartAddress) or
                nameof(GeneralMappingRowViewModel.Length)))
        {
            return;
        }
        RefreshMergeMemoryMapState();
        _stateBindings.ResetRunResult();
        RefreshCommandState();
    }
}
