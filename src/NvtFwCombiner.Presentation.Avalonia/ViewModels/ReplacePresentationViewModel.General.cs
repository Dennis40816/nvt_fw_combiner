using System.ComponentModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReplacePresentationViewModel
{
    private IReadOnlyList<AuthoringMappingState> _generalReplaceAuthoringStates = [];
    private GeneralMappingDraftState? _generalReplaceDraft;
    private GeneralAuthoringAdmissionResult? _generalReplaceAdmission;
    private CapabilityActionReadinessSnapshot? _generalReplaceActionReadiness;
    private CompositionRunReport? _generalReplaceDiagnosticPreviewReport;
    private bool _isApplyingGeneralReplacePreparation;
    private readonly SerialTaskQueue _generalReplacePreparationQueue = new();

    internal Task GeneralReplaceReadinessRefreshTask { get; private set; } = Task.CompletedTask;

    internal void AddGeneralReplaceMapping()
    {
        _generalReplaceMappingCounter++;
        var mapping = new GeneralReplaceMappingViewModel(
            $"general-map-{_generalReplaceMappingCounter}",
            GeneralReplaceMappings.Count + 1,
            Text);
        mapping.PropertyChanged += GeneralReplaceMappingPropertyChanged;
        GeneralReplaceMappings.Add(mapping);
        RefreshReplaceMemoryMapState();
        RefreshCommandState();
    }

    private IReadOnlyList<AuthoringMappingState> CreateGeneralReplaceAuthoringStates()
    {
        return
        [
            .. GeneralReplaceMappings
                .Where(mapping => mapping.HasSource)
                .Select(mapping => GeneralAuthoringMappingUseCase.CreateGeneralReplaceAuthoringState(
                    mapping.MappingId,
                    mapping.SelectedSource.Kind,
                    mapping.UsesFileSource ? mapping.FilePath! : mapping.InlineValue,
                    mapping.TargetStartAddress,
                    mapping.Length,
                    mapping.UsesFileSource ? mapping.AcceptedFileStamp : null)),
        ];
    }

    private void RefreshGeneralReplaceAuthoringState()
    {
        _generalReplaceAuthoringStates = CreateGeneralReplaceAuthoringStates();
        _generalReplaceAdmission = null;
        _generalReplaceActionReadiness = null;
        _generalReplaceDiagnosticPreviewReport = null;
        long? inspectedCapacity = _stateBindings.GetInspectedFileLength(ReplaceBaseSlot);
        bool canSelectFile = inspectedCapacity is > 0 &&
            _compositionServices.Capabilities.IsReplaceWorkflowAvailable(
                SelectedIc,
                GeneralReplaceMode);
        foreach (GeneralReplaceMappingViewModel mapping in GeneralReplaceMappings)
        {
            mapping.ApplyAuthoringIssue(null);
            mapping.SetFileSelectionAvailability(
                canSelectFile,
                Text.FirmwareSlotPendingFactDetail);
        }
        GeneralMappingDraftState? draft = null;
        IReadOnlyList<CompositionIssue> draftIssues = [];
        bool hasDraft = _generalReplaceAuthoringStates.Count > 0 &&
            GeneralAuthoringMappingUseCase.TryCreateGeneralReplaceAuthoringDraft(
                _generalReplaceAuthoringStates,
                out draft,
                out draftIssues);
        _generalReplaceDraft = hasDraft ? draft : null;
        ApplyGeneralReplaceAuthoringIssues(
            draftIssues
                .Where(static issue => !string.IsNullOrWhiteSpace(issue.OperationId))
                .Select(static issue => (issue.OperationId!, issue.Message)));
        if (_generalReplaceDraft is not null &&
            inspectedCapacity is long capacity &&
            capacity > 0)
        {
            _generalReplaceAdmission = _compositionServices.GeneralAuthoring
                .GetReplaceAdmission(
                    SelectedIc,
                    capacity,
                    _generalReplaceDraft);
            ApplyGeneralReplaceAuthoringIssues(
                _generalReplaceAdmission?.Issues.SelectMany(static issue =>
                    issue.MappingIds.Select(mappingId => (mappingId, issue.Message))) ?? []);
            if (!string.IsNullOrWhiteSpace(ReplaceBaseSlot.FilePath))
            {
                GeneralReplaceReadinessRefreshTask = PrepareGeneralReplaceSessionAsync(
                    _generalReplaceDraft,
                    ReplaceBaseSlot.FilePath);
            }
        }
    }

    private Task PrepareGeneralReplaceSessionAsync(
        GeneralMappingDraftState draft,
        string referencePath)
    {
        return _generalReplacePreparationQueue.Enqueue(
            () => PrepareGeneralReplaceSessionCoreAsync(draft, referencePath));
    }

    private async Task PrepareGeneralReplaceSessionCoreAsync(
        GeneralMappingDraftState draft,
        string referencePath)
    {
        if (!ReferenceEquals(_generalReplaceDraft, draft) ||
            !StringComparer.Ordinal.Equals(ReplaceBaseSlot.FilePath, referencePath))
        {
            return;
        }

        GeneralAuthoringSessionPreparation prepared =
            await _compositionServices.GeneralAuthoring.PrepareReplaceSessionAsync(
                _generalReplaceSession,
                SelectedIc,
                SelectedNumber,
                referencePath,
                draft,
                CancellationToken.None);
        if (!ReferenceEquals(_generalReplaceDraft, draft) ||
            !StringComparer.Ordinal.Equals(ReplaceBaseSlot.FilePath, referencePath))
        {
            return;
        }

        _isApplyingGeneralReplacePreparation = true;
        try
        {
            _generalReplaceAdmission = prepared.Admission ?? _generalReplaceAdmission;
            _generalReplaceActionReadiness = prepared.Readiness;
            _generalReplaceDiagnosticPreviewReport = prepared.DiagnosticPreviewReport;
            ApplyGeneralReplaceAuthoringIssues(
                _generalReplaceAdmission?.Issues.SelectMany(static issue =>
                    issue.MappingIds.Select(mappingId => (mappingId, issue.Message))) ?? []);
            if (prepared.AcceptedSession?.DraftState is GeneralMappingDraftState accepted)
            {
                _generalReplaceDraft = accepted;
            }
            foreach (GeneralReplaceMappingViewModel mapping in GeneralReplaceMappings.Where(
                         static mapping => mapping.UsesFileSource))
            {
                mapping.ApplyPreparation(prepared);
            }
        }
        finally
        {
            _isApplyingGeneralReplacePreparation = false;
        }
        RefreshReplaceMemoryMapState(refreshAuthoring: false);
        RefreshCommandState();
    }

    private void ApplyGeneralReplaceAuthoringIssues(
        IEnumerable<(string MappingId, string Message)> issues)
    {
        var messages = issues
            .GroupBy(static issue => issue.MappingId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => string.Join(
                    Environment.NewLine,
                    group.Select(static issue => issue.Message)
                        .Distinct(StringComparer.Ordinal)),
                StringComparer.Ordinal);
        foreach (GeneralReplaceMappingViewModel mapping in GeneralReplaceMappings)
        {
            mapping.ApplyAuthoringIssue(messages.GetValueOrDefault(mapping.MappingId));
        }
    }

    internal bool RemoveGeneralMapping(GeneralReplaceMappingViewModel mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (GeneralReplaceMappings.Count <= 1)
        {
            return false;
        }

        mapping.PropertyChanged -= GeneralReplaceMappingPropertyChanged;
        if (!GeneralReplaceMappings.Remove(mapping))
        {
            return false;
        }

        int index = 1;
        foreach (GeneralMappingRowViewModel row in GeneralReplaceMappings)
        {
            row.SetIndex(index++);
        }

        RefreshReplaceMemoryMapState();
        RefreshCommandState();
        return true;
    }

    private void GeneralReplaceMappingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingGeneralReplacePreparation ||
            e.PropertyName is not (
                nameof(GeneralMappingRowViewModel.FilePath) or
                nameof(GeneralMappingRowViewModel.SourceStartAddress) or
                nameof(GeneralMappingRowViewModel.TargetStartAddress) or
                nameof(GeneralMappingRowViewModel.Length) or
                nameof(GeneralReplaceMappingViewModel.SelectedSource) or
                nameof(GeneralReplaceMappingViewModel.InlineValue)))
        {
            return;
        }
        RefreshReplaceMemoryMapState();
        RefreshCommandState();
    }
}
