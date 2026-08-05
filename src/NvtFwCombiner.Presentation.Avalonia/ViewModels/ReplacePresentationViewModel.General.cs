using System.ComponentModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReplacePresentationViewModel
{
    private IReadOnlyList<AuthoringMappingState> _generalReplaceAuthoringStates = [];
    private GeneralMappingDraftState? _generalReplaceDraft;
    private GeneralAuthoringAdmissionResult? _generalReplaceAdmission;
    private CapabilityActionReadinessSnapshot? _generalReplaceActionReadiness;

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
                .Select(mapping => _compositionServices.Authoring.CreateGeneralReplaceAuthoringState(
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
        long? inspectedCapacity = _stateBindings.GetInspectedFileLength(ReplaceBaseSlot);
        string[] fileMappingIds =
        [
            .. GeneralReplaceMappings
                .Where(static mapping => mapping.UsesFileSource)
                .Select(static mapping => mapping.MappingId),
        ];
        bool canSelectFile = inspectedCapacity is > 0 &&
            (fileMappingIds.Length == 0 ||
                _compositionServices.AuthoringSession.PrepareGeneralReplaceSelectionSession(
                    _authoringSessions.GeneralReplace,
                    SelectedIc,
                    inspectedCapacity.Value,
                    fileMappingIds));
        foreach (GeneralReplaceMappingViewModel mapping in GeneralReplaceMappings)
        {
            mapping.SetFileSelectionAvailability(
                canSelectFile,
                Text.FirmwareSlotPendingFactDetail);
        }
        _generalReplaceDraft =
            _generalReplaceAuthoringStates.Count > 0 &&
            _compositionServices.Authoring.TryCreateGeneralReplaceAuthoringDraft(
                _generalReplaceAuthoringStates,
                out GeneralMappingDraftState? draft,
                out _)
                ? draft
                : null;
        if (_generalReplaceDraft is not null &&
            inspectedCapacity is long capacity &&
            capacity > 0)
        {
            _generalReplaceAdmission = _compositionServices.Authoring
                .GetGeneralReplaceAuthoringAdmission(
                    SelectedIc,
                    capacity,
                    _generalReplaceDraft);
            if (_generalReplaceDraft.Rows.Any(static row =>
                    row.Source.Kind == GeneralMappingSourceKind.FileArtifact &&
                    row.Source.AcceptedFileStamp is null))
            {
                _ = _compositionServices.AuthoringSession.PrepareGeneralReplaceSelectionSession(
                        _authoringSessions.GeneralReplace,
                        SelectedIc,
                        capacity,
                        _generalReplaceDraft.Rows.Select(static row => row.MappingId)) &&
                    _authoringSessions.GeneralReplace.SetDraft(_generalReplaceDraft).Succeeded;
            }
            GeneralReplaceReadinessRefreshTask = RefreshGeneralReplaceActionReadinessAsync(
                _generalReplaceDraft,
                capacity);
        }
    }

    internal AuthoringPublicationLease? CaptureGeneralReplacePrebindingLease(
        GeneralReplaceMappingViewModel mapping,
        string path)
    {
        return mapping.CapturePrebindingLease(_authoringSessions.GeneralReplace, path);
    }

    internal bool TryCacheGeneralReplaceInspection(
        GeneralReplaceMappingViewModel mapping,
        AuthoringPublicationLease lease,
        GeneralSelectedFileInspectionResult result)
    {
        return mapping.TryCacheInspection(_authoringSessions.GeneralReplace, lease, result);
    }

    private bool TryAcceptCachedGeneralReplaceInspection(
        GeneralMappingDraftState draft,
        long capacity)
    {
        return TryGetAcceptedGeneralReplaceReference(
                out string referencePath,
                out FileStamp referenceStamp) &&
            GeneralReplaceMappings
            .Where(static mapping => mapping.UsesFileSource)
            .OrderBy(mapping => mapping.IsInspectionVerified(_authoringSessions.GeneralReplace))
            .Any(mapping => mapping.TryAcceptCachedInspection(
                _authoringSessions.GeneralReplace,
                cached => _compositionServices.AuthoringSession.BeginGeneralReplaceSelectedFileInspection(
                    _authoringSessions.GeneralReplace,
                    SelectedIc,
                    SelectedNumber,
                    capacity,
                    draft,
                    referencePath,
                    referenceStamp,
                    mapping.MappingId,
                    cached.FileStamp.AcceptedLength)));
    }

    private async Task RefreshGeneralReplaceActionReadinessAsync(
        GeneralMappingDraftState draft,
        long capacity)
    {
        if (!TryGetAcceptedGeneralReplaceReference(out string referencePath, out FileStamp referenceStamp))
        {
            return;
        }

        CapabilityActionReadinessSnapshot? readiness =
            await _compositionServices.AuthoringSession.GetGeneralReplaceActionReadinessAsync(
                _authoringSessions.GeneralReplace,
                SelectedIc,
                SelectedNumber,
                capacity,
                draft,
                referencePath,
                referenceStamp,
                _stateBindings.GetBaseInspection()?.FirmwareConfig,
                CancellationToken.None);
        while (readiness is null &&
            TryAcceptCachedGeneralReplaceInspection(draft, capacity) &&
            _generalReplaceDraft is { } acceptedDraft)
        {
            draft = acceptedDraft;
            readiness = await _compositionServices.AuthoringSession.GetGeneralReplaceActionReadinessAsync(
                _authoringSessions.GeneralReplace,
                SelectedIc,
                SelectedNumber,
                capacity,
                draft,
                referencePath,
                referenceStamp,
                _stateBindings.GetBaseInspection()?.FirmwareConfig,
                CancellationToken.None);
        }
        if (readiness is null)
        {
            return;
        }

        _generalReplaceActionReadiness = readiness;
        RefreshCommandState();
    }

    internal AuthoringSlotInspectionStartResult BeginGeneralReplaceFileInspection(
        GeneralReplaceMappingViewModel mapping,
        long observedLength)
    {
        long? capacity = _stateBindings.GetInspectedFileLength(ReplaceBaseSlot);
        return capacity is null ||
            _generalReplaceDraft is null ||
            !TryGetAcceptedGeneralReplaceReference(
                out string referencePath,
                out FileStamp referenceStamp)
            ? new AuthoringSlotInspectionStartResult(
                _authoringSessions.GeneralReplace.CurrentSnapshot,
                Lease: null,
                new AuthoringSessionIssue(
                    AuthoringSessionIssueCodes.DraftUnavailable,
                    "General Replace requires an inspected Reference and valid typed draft before file inspection.",
                    mapping.MappingId))
            : _compositionServices.AuthoringSession.BeginGeneralReplaceSelectedFileInspection(
            _authoringSessions.GeneralReplace,
            SelectedIc,
            SelectedNumber,
            capacity.Value,
            _generalReplaceDraft,
            referencePath,
            referenceStamp,
            mapping.MappingId,
            observedLength);
    }

    private bool TryGetAcceptedGeneralReplaceReference(
        out string referencePath,
        out FileStamp referenceStamp)
    {
        referencePath = ReplaceBaseSlot.FilePath ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(referencePath) &&
            _stateBindings.GetBaseInspection()?.FileStamp is { } acceptedStamp)
        {
            referenceStamp = acceptedStamp;
            return true;
        }

        referenceStamp = default;
        return false;
    }

    internal bool TryPublishGeneralReplaceFileInspection(
        GeneralReplaceMappingViewModel mapping,
        AuthoringSlotInspectionLease lease,
        GeneralSelectedFileInspectionResult result)
    {
        return mapping.TryPublishInspection(_authoringSessions.GeneralReplace, lease, result);
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
        RefreshReplaceMemoryMapState();
        RefreshCommandState();
    }
}
