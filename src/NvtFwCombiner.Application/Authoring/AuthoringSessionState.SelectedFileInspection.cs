namespace NvtFwCombiner.Application.Authoring;

public sealed partial class AuthoringSessionState
{
    /// <summary>
    /// Begins an explicit selected-file inspection or reload. The transition
    /// advances revision immediately, clears derived state, preserves the
    /// editable draft, and publishes a Checking slot without an accepted stamp.
    /// </summary>
    public AuthoringSlotInspectionStartResult BeginSlotFileInspection(
        string slotDefinitionId,
        string selectedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotDefinitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        lock (_transitionLock)
        {
            if (_current is null)
            {
                return InspectionFailure(
                    AuthoringSessionIssueCodes.CatalogUnavailable,
                    "The authoring session is not active.",
                    WorkflowId);
            }

            int index = Array.FindIndex(
                [.. _current.Slots],
                slot => StringComparer.Ordinal.Equals(
                    slot.DefinitionId,
                    slotDefinitionId));
            if (index < 0)
            {
                return InspectionFailure(
                    AuthoringSessionIssueCodes.SlotUnavailable,
                    "The selected slot is not part of the active resolved route.",
                    slotDefinitionId);
            }

            AuthoringSlotState[] slots = [.. _current.Slots];
            slots[index] = new AuthoringSlotState(
                slotDefinitionId,
                selectedPath,
                fileStamp: null,
                AuthoringSlotLifecycle.Checking);
            AuthoringDraftState? pendingDraft = ClearGeneralDraftStamp(
                _current.DraftState,
                slotDefinitionId,
                selectedPath);
            ActiveSessionSnapshot snapshot = CopySnapshot(
                _current,
                _current.AuthoringRevision.Next(),
                slots,
                pendingDraft,
                _current.DraftCapabilityFingerprint,
                []);
            var lease = new AuthoringSlotInspectionLease(
                _publicationIdentity,
                snapshot.ResolutionToken,
                snapshot.AuthoringRevision,
                snapshot.SelectedRouteId,
                snapshot.CapabilityFingerprint,
                slotDefinitionId,
                selectedPath);
            Volatile.Write(ref _current, snapshot);
            return new AuthoringSlotInspectionStartResult(
                snapshot,
                lease,
                Issue: null);
        }
    }

    /// <summary>
    /// Accepts an inspected content stamp only for the exact current
    /// definition, revision, route, and selected-path hint.
    /// </summary>
    public AuthoringSessionTransitionResult TryAcceptSlotFileInspection(
        AuthoringSlotInspectionLease lease,
        GeneralSelectedFileInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(inspection);
        lock (_transitionLock)
        {
            if (_current is null ||
                !ReferenceEquals(lease.SessionIdentity, _publicationIdentity) ||
                lease.ResolutionToken != _current.ResolutionToken ||
                lease.AuthoringRevision != _current.AuthoringRevision ||
                !StringComparer.Ordinal.Equals(
                    lease.SelectedRouteId,
                    _current.SelectedRouteId) ||
                !StringComparer.Ordinal.Equals(
                    lease.CapabilityFingerprint,
                    _current.CapabilityFingerprint) ||
                !StringComparer.Ordinal.Equals(
                    lease.DefinitionId,
                    inspection.DefinitionId) ||
                lease.AuthoringRevision != inspection.AuthoringRevision ||
                !StringComparer.Ordinal.Equals(
                    lease.SelectedPath,
                    inspection.SelectedPathHint))
            {
                return Failure(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The selected-file inspection belongs to older authoring state.",
                    inspection.DefinitionId);
            }

            int index = Array.FindIndex(
                [.. _current.Slots],
                slot =>
                    StringComparer.Ordinal.Equals(
                        slot.DefinitionId,
                        lease.DefinitionId) &&
                    StringComparer.Ordinal.Equals(
                        slot.SelectedPath,
                        lease.SelectedPath) &&
                    slot.Lifecycle == AuthoringSlotLifecycle.Checking &&
                    slot.FileStamp is null);
            if (index < 0)
            {
                return Failure(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The selected-file inspection no longer matches a checking slot.",
                    inspection.DefinitionId);
            }

            AuthoringDraftState? acceptedDraft = AcceptGeneralDraftStamp(
                _current.DraftState,
                lease.DefinitionId,
                lease.SelectedPath,
                inspection.FileStamp);
            if (_current.DraftState is GeneralMappingDraftState &&
                acceptedDraft is null)
            {
                return Failure(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The selected-file inspection no longer matches the General mapping draft.",
                    inspection.DefinitionId);
            }

            AuthoringSlotState[] slots = [.. _current.Slots];
            slots[index] = new AuthoringSlotState(
                lease.DefinitionId,
                lease.SelectedPath,
                inspection.FileStamp,
                AuthoringSlotLifecycle.Verified);
            ActiveSessionSnapshot snapshot = CopySnapshot(
                _current,
                _current.AuthoringRevision,
                slots,
                acceptedDraft,
                _current.DraftCapabilityFingerprint,
                []);
            Volatile.Write(ref _current, snapshot);
            return new AuthoringSessionTransitionResult(snapshot, null);
        }
    }

    private AuthoringSlotInspectionStartResult InspectionFailure(
        string code,
        string message,
        string? subject)
    {
        return new AuthoringSlotInspectionStartResult(
            _current,
            Lease: null,
            new AuthoringSessionIssue(code, message, subject));
    }

    private static AuthoringDraftState? ClearGeneralDraftStamp(
        AuthoringDraftState? draftState,
        string definitionId,
        string selectedPath)
    {
        return draftState is GeneralMappingDraftState generalDraft
            ? ReplaceGeneralDraftRowByDefinition(
                generalDraft,
                definitionId,
                row => row.RebindSelectedFile(selectedPath)) ??
              draftState
            : draftState;
    }

    private static AuthoringDraftState? AcceptGeneralDraftStamp(
        AuthoringDraftState? draftState,
        string definitionId,
        string selectedPath,
        FileStamp fileStamp)
    {
        return draftState is GeneralMappingDraftState generalDraft
            ? ReplaceGeneralDraftRow(
                generalDraft,
                definitionId,
                selectedPath,
                row => row.WithAcceptedFileStamp(fileStamp))
            : draftState;
    }

    private static GeneralMappingDraftState? ReplaceGeneralDraftRow(
        GeneralMappingDraftState draft,
        string definitionId,
        string selectedPath,
        Func<GeneralMappingDraftRow, GeneralMappingDraftRow> replace)
    {
        GeneralMappingDraftRow? selected = draft.Rows.SingleOrDefault(row =>
            StringComparer.Ordinal.Equals(row.MappingId, definitionId) &&
            row.Source.Kind == GeneralMappingSourceKind.FileArtifact &&
            StringComparer.Ordinal.Equals(row.Source.Reference, selectedPath));
        return selected is null
            ? null
            : new GeneralMappingDraftState(
                draft.Rows.Select(row =>
                    ReferenceEquals(row, selected)
                        ? replace(row)
                        : row));
    }

    private static GeneralMappingDraftState? ReplaceGeneralDraftRowByDefinition(
        GeneralMappingDraftState draft,
        string definitionId,
        Func<GeneralMappingDraftRow, GeneralMappingDraftRow> replace)
    {
        GeneralMappingDraftRow? selected = draft.Rows.SingleOrDefault(row =>
            StringComparer.Ordinal.Equals(row.MappingId, definitionId) &&
            row.Source.Kind == GeneralMappingSourceKind.FileArtifact);
        return selected is null
            ? null
            : new GeneralMappingDraftState(
                draft.Rows.Select(row =>
                    ReferenceEquals(row, selected)
                        ? replace(row)
                        : row));
    }
}
