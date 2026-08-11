using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

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
        AuthoringSlotInspectionBatchStartResult started = BeginSlotFileInspections(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [slotDefinitionId] = selectedPath,
            });
        return new AuthoringSlotInspectionStartResult(
            started.Snapshot,
            started.Leases.SingleOrDefault(),
            started.Issue);
    }

    /// <summary>
    /// Begins an atomic selected-file inspection batch. Every slot enters Checking
    /// at one revision so one compiled batch can return mutually current results.
    /// </summary>
    public AuthoringSlotInspectionBatchStartResult BeginSlotFileInspections(
        IReadOnlyDictionary<string, string> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        if (selections.Count == 0)
        {
            throw new ArgumentException(
                "At least one selected-file inspection is required.",
                nameof(selections));
        }

        foreach ((string definitionId, string selectedPath) in selections)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        }

        lock (_transitionLock)
        {
            if (_current is null)
            {
                return InspectionBatchFailure(
                    AuthoringSessionIssueCodes.CatalogUnavailable,
                    "The authoring session is not active.",
                    WorkflowId);
            }

            KeyValuePair<string, string>[] orderedSelections =
            [
                .. selections.OrderBy(static selection => selection.Key, StringComparer.Ordinal),
            ];
            AuthoringSlotState[] slots = [.. _current.Slots];
            foreach ((string definitionId, _) in orderedSelections)
            {
                if (!slots.Any(slot => StringComparer.Ordinal.Equals(
                        slot.DefinitionId,
                        definitionId)))
                {
                    return InspectionBatchFailure(
                        AuthoringSessionIssueCodes.SlotUnavailable,
                        "The selected slot is not part of the active resolved route.",
                        definitionId);
                }
            }

            AuthoringDraftState? pendingDraft = _current.DraftState;
            foreach ((string definitionId, string selectedPath) in orderedSelections)
            {
                int index = Array.FindIndex(slots, slot =>
                    StringComparer.Ordinal.Equals(slot.DefinitionId, definitionId));
                slots[index] = new AuthoringSlotState(
                    definitionId,
                    selectedPath,
                    fileStamp: null,
                    AuthoringSlotLifecycle.Checking);
                pendingDraft = ClearGeneralDraftStamp(
                    pendingDraft,
                    definitionId,
                    selectedPath);
            }

            ActiveSessionSnapshot snapshot = CopySnapshot(
                _current,
                _current.AuthoringRevision.Next(),
                slots,
                pendingDraft,
                _current.DraftCapabilityFingerprint,
                []);
            ReviewedDiscoveryTransition? discoveryTransition = _catalog?.Routes
                .SingleOrDefault(route => StringComparer.Ordinal.Equals(
                    route.Identity.RouteId,
                    snapshot.SelectedRouteId))
                ?.DiscoveryTransition;
            AuthoringSlotInspectionLease[] leases =
            [
                .. orderedSelections.Select(selection => new AuthoringSlotInspectionLease(
                    _publicationIdentity,
                    snapshot.ResolutionToken,
                    snapshot.AuthoringRevision,
                    snapshot.SelectedRouteId,
                    snapshot.CapabilityFingerprint,
                    snapshot.CompilationFingerprint,
                    snapshot.ExactCapability,
                    discoveryTransition,
                    selection.Key,
                    selection.Value)),
            ];
            Volatile.Write(ref _current, snapshot);
            return new AuthoringSlotInspectionBatchStartResult(
                snapshot,
                Array.AsReadOnly(leases),
                Issue: null);
        }
    }

    /// <summary>
    /// Atomically accepts one complete compiled inspection batch. Any stale or
    /// incomplete member rejects the whole batch without publishing partial
    /// slot health.
    /// </summary>
    public AuthoringSessionTransitionResult TryCompleteSlotFileInspectionBatch(
        AuthoringCapabilityCatalogSnapshot catalog,
        IReadOnlyCollection<AuthoringSlotInspectionLease> leases,
        IReadOnlyDictionary<string, AuthoringInputSlotStatus> statuses,
        MetadataInspectionSnapshot? metadataInspection = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(leases);
        ArgumentNullException.ThrowIfNull(statuses);
        if (!StringComparer.Ordinal.Equals(catalog.WorkflowId, WorkflowId))
        {
            throw new ArgumentException(
                "An inspection batch can complete only its owning workflow.",
                nameof(catalog));
        }

        lock (_transitionLock)
        {
            AuthoringSlotInspectionLease[] captured = [.. leases];
            var capturedDefinitions = captured
                .Select(static lease => lease.DefinitionId)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> checkingDefinitions = _current?.Slots
                .Where(static slot => slot.Lifecycle == AuthoringSlotLifecycle.Checking)
                .Select(static slot => slot.DefinitionId)
                .ToHashSet(StringComparer.Ordinal) ?? [];
            if (_current is null ||
                captured.Length == 0 ||
                captured.Length != statuses.Count ||
                capturedDefinitions.Count != captured.Length ||
                !capturedDefinitions.SetEquals(checkingDefinitions) ||
                !capturedDefinitions.SetEquals(statuses.Keys))
            {
                return Failure(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The inspection batch is empty, incomplete, or no longer current.",
                    WorkflowId);
            }

            AuthoringCapabilityRoute? currentRoute = _catalog?.Routes.SingleOrDefault(route =>
                StringComparer.Ordinal.Equals(route.Identity.RouteId, _current.SelectedRouteId));
            if (currentRoute is null)
            {
                return Failure(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The current authoring route is no longer part of its catalog publication.",
                    _current.SelectedRouteId);
            }

            if (catalog.ResolutionToken != _current.ResolutionToken)
            {
                return Failure(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The completed inspection belongs to another catalog publication.",
                    _current.SelectedRouteId);
            }

            AuthoringCapabilityRoute[] routes = catalog.FindRoutes(
                _current.SelectedIc,
                _current.SelectedIcCount);
            if (routes.Length != 1)
            {
                return Failure(
                    AuthoringSessionIssueCodes.RouteUnavailable,
                    "The completed inspection has no unique current authoring route.",
                    _current.SelectedRouteId);
            }

            AuthoringCapabilityRoute route = routes[0];
            bool axesMatch = StringComparer.Ordinal.Equals(
                    route.Identity.IcId,
                    _current.SelectedIc) &&
                StringComparer.Ordinal.Equals(
                    route.Identity.IcCountVariant,
                    _current.SelectedIcCount);
            bool sameRouteAndCapability = StringComparer.Ordinal.Equals(
                    route.Identity.RouteId,
                    _current.SelectedRouteId) &&
                StringComparer.Ordinal.Equals(
                    route.CapabilityFingerprint,
                    _current.CapabilityFingerprint);
            bool exactCurrentMatches = _current.CompilationFingerprint is not null &&
                sameRouteAndCapability &&
                StringComparer.Ordinal.Equals(
                    route.CompilationFingerprint,
                    _current.CompilationFingerprint) &&
                ReferenceEquals(route.ExactCapability, currentRoute.ExactCapability);
            bool unresolvedDiscoveryMatches = _current.CompilationFingerprint is null &&
                route.CompilationFingerprint is null &&
                sameRouteAndCapability;
            ReviewedDiscoveryTransition? transition = currentRoute.DiscoveryTransition;
            bool reviewedExactTransition = _current.CompilationFingerprint is null &&
                route.CompilationFingerprint is not null &&
                transition is not null &&
                transition.Matches(route.DiscoveryTransition) &&
                transition.Allows(
                    route.Identity.RouteId,
                    route.CapabilityFingerprint);
            bool routeMatches = axesMatch &&
                (exactCurrentMatches || unresolvedDiscoveryMatches || reviewedExactTransition);
            if (!routeMatches || captured.Any(lease =>
                    !InspectionLeaseMatches(lease, _current) ||
                    !InspectionCompletionMatchesLease(lease, catalog, route)))
            {
                return Failure(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The completed inspection belongs to older authoring state.",
                    _current.SelectedRouteId);
            }

            foreach (AuthoringSlotInspectionLease lease in captured)
            {
                if (!statuses.TryGetValue(lease.DefinitionId, out AuthoringInputSlotStatus? status) ||
                    !StringComparer.Ordinal.Equals(status.SlotId, lease.DefinitionId) ||
                    status.ResolutionToken != catalog.ResolutionToken ||
                    status.AuthoringRevision != _current.AuthoringRevision ||
                    !StringComparer.Ordinal.Equals(status.RouteId, route.Identity.RouteId) ||
                    !StringComparer.Ordinal.Equals(status.CapabilityFingerprint, route.CapabilityFingerprint) ||
                    !StringComparer.Ordinal.Equals(status.CompilationFingerprint, route.CompilationFingerprint) ||
                    !StringComparer.Ordinal.Equals(status.SelectedPathHint, lease.SelectedPath) ||
                    (route.CompilationFingerprint is not null && !status.IsTerminal) ||
                    (route.CompilationFingerprint is null &&
                        status.Readiness != ResolvedChildReadiness.Blocked))
                {
                    return Failure(
                        AuthoringSessionIssueCodes.StaleInspection,
                        "One inspection member does not match the complete current batch.",
                        lease.DefinitionId);
                }
            }

            string resultReference = FormattableString.Invariant(
                $"inspection-batch:{_current.AuthoringRevision.Value}:{route.CompilationFingerprint ?? "pre-compilation"}");
            AuthoringInputSlotStatus[] orderedStatuses =
            [
                .. statuses.Values.OrderBy(static status => status.SlotId, StringComparer.Ordinal),
            ];
            Dictionary<string, AuthoringInputSlotStatus> statusesBySlot = orderedStatuses.ToDictionary(
                static status => status.SlotId,
                StringComparer.Ordinal);
            AuthoringSlotState[] slots =
            [
                .. route.SlotDefinitions.Select(definition =>
                {
                    AuthoringSlotState? current = _current.Slots.SingleOrDefault(slot =>
                        StringComparer.Ordinal.Equals(slot.DefinitionId, definition.DefinitionId));
                    if (!statusesBySlot.TryGetValue(
                            definition.DefinitionId,
                            out AuthoringInputSlotStatus? status))
                    {
                        return current is null
                            ? new AuthoringSlotState(
                                definition.DefinitionId,
                                null,
                                null,
                                AuthoringSlotLifecycle.Empty)
                            : current;
                    }

                    AuthoringSlotLifecycle lifecycle = status.InspectionLifecycle ??
                        AuthoringSlotLifecycle.Error;
                    string issueId = status.InspectionIssueCode ??
                        status.SelectionReadiness.IssueCode ??
                        InputSelectionReadinessIssueCodes.SelectionNotApplicable;
                    return new AuthoringSlotState(
                        definition.DefinitionId,
                        status.SelectedPathHint,
                        status.FileStamp,
                        lifecycle,
                        lifecycle == AuthoringSlotLifecycle.Error
                            ? new AuthoringSlotIssueReference(
                                AuthoringDerivedResultKind.Inspection,
                                resultReference,
                                issueId)
                            : null);
                }),
            ];
            AuthoringDerivedPublication[] publications = route.CompilationFingerprint is not null &&
                orderedStatuses.All(static status => status.IsTerminal)
                    ? [new AuthoringDerivedPublication(
                        AuthoringDerivedResultKind.Inspection,
                        resultReference,
                        route.CompilationFingerprint)]
                    : [];
            FirmwareArtifactPayload[] acceptedArtifacts =
            [
                .. orderedStatuses
                    .Where(static status => status.AcceptedBytes is { Length: > 0 })
                    .Select(static status => new FirmwareArtifactPayload(
                        status.AddressSpaceId,
                        status.AcceptedBytes!.Value.Span)),
            ];
            metadataInspection ??= route.ExactCapability is null ||
                route.CompilationFingerprint is null
                    ? null
                    : FirmwareMetadataInspector.Inspect(
                        new MetadataInspectionRequest(
                            route.ExactCapability.MetadataPlan,
                            _current.AuthoringRevision.Value,
                            acceptedArtifacts));
            if (metadataInspection is not null &&
                (route.ExactCapability is null ||
                 !MetadataInspectionPublicationGate.IsCurrent(
                     metadataInspection,
                     route.ExactCapability.MetadataPlan,
                     _current.AuthoringRevision.Value,
                     acceptedArtifacts)))
            {
                return Failure(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The metadata inspection does not match the complete current input batch.",
                    _current.SelectedRouteId);
            }

            IReadOnlyList<InputSelectionMemberReadiness> selectionReadiness =
                route.ExactCapability is null
                    ? _current.InputSelectionReadiness
                    :
                    [
                        .. InputSelectionReadinessResolver.Resolve(
                                _current.AuthoringRevision,
                                route.ExactCapability.CompiledComposition.V2Details.InputContract
                                    .SelectionGroups,
                                orderedStatuses.Select(static status => status.SlotId))
                            .Groups.SelectMany(static group => group.Members),
                    ];
            ActiveSessionSnapshot snapshot = CreateSnapshot(
                catalog,
                route,
                _current.AuthoringRevision,
                slots,
                _current.DraftState,
                _current.DraftCapabilityFingerprint,
                publications,
                orderedStatuses,
                selectionReadiness,
                metadataInspection);
            _catalog = catalog;
            Volatile.Write(ref _current, snapshot);
            return new AuthoringSessionTransitionResult(snapshot, null);
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
                !InspectionLeaseMatches(lease, _current) ||
                _current.CompilationFingerprint is null ||
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

            AuthoringSlotDefinitionReference definition = _catalog!.Routes
                .Single(route => StringComparer.Ordinal.Equals(
                    route.Identity.RouteId,
                    _current.SelectedRouteId))
                .SlotDefinitions.Single(slot => StringComparer.Ordinal.Equals(
                    slot.DefinitionId,
                    lease.DefinitionId));
            if (definition.ExpectedLength is { } expectedLength &&
                inspection.FileStamp.AcceptedLength != expectedLength)
            {
                return Failure(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The selected-file length no longer matches its exact compiled input contract.",
                    inspection.DefinitionId);
            }

            AuthoringDraftState? acceptedDraft = AcceptGeneralDraftStamp(
                _current.DraftState,
                lease.DefinitionId,
                lease.SelectedPath,
                inspection.FileStamp);
            GeneralMappingDraftState? generalMappings = GetGeneralMappings(_current.DraftState);
            bool isGeneralMappingSlot = generalMappings?.Rows.Any(row =>
                StringComparer.Ordinal.Equals(row.MappingId, lease.DefinitionId)) == true;
            bool isGeneralReferenceSlot = _current.ExactCapability?.CompiledComposition.Plan
                    .OutputInitialization is { Kind: ImageInitializationKind.Reference } initialization &&
                StringComparer.Ordinal.Equals(
                    initialization.ReferenceSpaceId,
                    lease.DefinitionId);
            if (generalMappings is not null &&
                acceptedDraft is null &&
                (isGeneralMappingSlot || !isGeneralReferenceSlot))
            {
                return Failure(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The selected-file inspection no longer matches the General mapping draft.",
                    inspection.DefinitionId);
            }
            acceptedDraft ??= _current.DraftState;

            AuthoringSlotState[] slots = [.. _current.Slots];
            int index = Array.FindIndex(slots, slot =>
                StringComparer.Ordinal.Equals(slot.DefinitionId, lease.DefinitionId));
            slots[index] = new AuthoringSlotState(
                lease.DefinitionId,
                lease.SelectedPath,
                inspection.FileStamp,
                AuthoringSlotLifecycle.Verified,
                acceptedBytes: inspection.AcceptedByteArray);
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

    /// <summary>Publishes one typed inspection failure only for its current lease.</summary>
    public AuthoringSessionTransitionResult TryRejectSlotFileInspection(
        AuthoringSlotInspectionLease lease,
        GeneralSelectedFileInspectionIssue issue)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(issue);
        lock (_transitionLock)
        {
            if (_current is null ||
                !InspectionLeaseMatches(lease, _current) ||
                _current.CompilationFingerprint is null ||
                !StringComparer.Ordinal.Equals(lease.DefinitionId, issue.DefinitionId))
            {
                return Failure(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The selected-file inspection failure belongs to older authoring state.",
                    issue.DefinitionId);
            }

            AuthoringSlotState[] slots = [.. _current.Slots];
            int index = Array.FindIndex(slots, slot =>
                StringComparer.Ordinal.Equals(slot.DefinitionId, lease.DefinitionId));
            string resultReference = FormattableString.Invariant(
                $"selected-file:{lease.AuthoringRevision.Value}:{lease.DefinitionId}");
            slots[index] = new AuthoringSlotState(
                lease.DefinitionId,
                lease.SelectedPath,
                fileStamp: null,
                AuthoringSlotLifecycle.Error,
                new AuthoringSlotIssueReference(
                    AuthoringDerivedResultKind.Inspection,
                    resultReference,
                    issue.Code));
            ActiveSessionSnapshot snapshot = CopySnapshot(
                _current,
                _current.AuthoringRevision,
                slots,
                _current.DraftState,
                _current.DraftCapabilityFingerprint,
                [new AuthoringDerivedPublication(
                    AuthoringDerivedResultKind.Inspection,
                    resultReference,
                    _current.CompilationFingerprint)]);
            Volatile.Write(ref _current, snapshot);
            return new AuthoringSessionTransitionResult(snapshot, null);
        }
    }

    private AuthoringSlotInspectionBatchStartResult InspectionBatchFailure(
        string code,
        string message,
        string? subject)
    {
        return new AuthoringSlotInspectionBatchStartResult(
            _current,
            [],
            new AuthoringSessionIssue(code, message, subject));
    }

    private bool InspectionLeaseMatches(
        AuthoringSlotInspectionLease lease,
        ActiveSessionSnapshot snapshot)
    {
        return ReferenceEquals(lease.SessionIdentity, _publicationIdentity) &&
            lease.ResolutionToken == snapshot.ResolutionToken &&
            lease.AuthoringRevision == snapshot.AuthoringRevision &&
            StringComparer.Ordinal.Equals(lease.SelectedRouteId, snapshot.SelectedRouteId) &&
            StringComparer.Ordinal.Equals(lease.CapabilityFingerprint, snapshot.CapabilityFingerprint) &&
            StringComparer.Ordinal.Equals(lease.CompilationFingerprint, snapshot.CompilationFingerprint) &&
            snapshot.Slots.Any(slot =>
                StringComparer.Ordinal.Equals(slot.DefinitionId, lease.DefinitionId) &&
                StringComparer.Ordinal.Equals(slot.SelectedPath, lease.SelectedPath) &&
                slot.Lifecycle == AuthoringSlotLifecycle.Checking &&
                slot.FileStamp is null);
    }

    private static bool InspectionCompletionMatchesLease(
        AuthoringSlotInspectionLease lease,
        AuthoringCapabilityCatalogSnapshot catalog,
        AuthoringCapabilityRoute route)
    {
        if (lease.ResolutionToken != catalog.ResolutionToken)
        {
            return false;
        }

        bool sameRouteAndCapability = StringComparer.Ordinal.Equals(
                lease.SelectedRouteId,
                route.Identity.RouteId) &&
            StringComparer.Ordinal.Equals(
                lease.CapabilityFingerprint,
                route.CapabilityFingerprint);
        return lease.CompilationFingerprint is not null
            ? sameRouteAndCapability &&
                StringComparer.Ordinal.Equals(
                    lease.CompilationFingerprint,
                    route.CompilationFingerprint)
            : route.CompilationFingerprint is null
                ? sameRouteAndCapability
                : lease.DiscoveryTransition is { } transition &&
                    transition.Matches(route.DiscoveryTransition) &&
                    transition.Allows(
                        route.Identity.RouteId,
                        route.CapabilityFingerprint);
    }

    private static AuthoringDraftState? ClearGeneralDraftStamp(
        AuthoringDraftState? draftState,
        string definitionId,
        string selectedPath)
    {
        GeneralMappingDraftState? mappings = GetGeneralMappings(draftState);
        if (mappings is null)
        {
            return draftState;
        }

        GeneralMappingDraftState? updated =
            ReplaceGeneralDraftRowByDefinition(
                mappings,
                definitionId,
                row => row.RebindSelectedFile(selectedPath));
        return updated is null
            ? draftState
            : ReplaceGeneralMappings(draftState!, updated);
    }

    private static AuthoringDraftState? AcceptGeneralDraftStamp(
        AuthoringDraftState? draftState,
        string definitionId,
        string selectedPath,
        FileStamp fileStamp)
    {
        GeneralMappingDraftState? mappings = GetGeneralMappings(draftState);
        if (mappings is null)
        {
            return draftState;
        }

        GeneralMappingDraftState? updated = ReplaceGeneralDraftRow(
                mappings,
                definitionId,
                selectedPath,
                row => row.WithAcceptedFileStamp(fileStamp));
        return updated is null
            ? null
            : ReplaceGeneralMappings(draftState!, updated);
    }

    private static GeneralMappingDraftState? GetGeneralMappings(
        AuthoringDraftState? draftState)
    {
        return draftState switch
        {
            GeneralMappingDraftState mappings => mappings,
            GeneralMergeDraftState merge => merge.Mappings,
            _ => null,
        };
    }

    private static AuthoringDraftState ReplaceGeneralMappings(
        AuthoringDraftState draftState,
        GeneralMappingDraftState mappings)
    {
        return draftState switch
        {
            GeneralMappingDraftState => mappings,
            GeneralMergeDraftState merge =>
                new GeneralMergeDraftState(
                    merge.OutputInitializer,
                    mappings),
            _ => throw new InvalidOperationException(
                "The draft does not own General mapping rows."),
        };
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
                        : row),
                draft.SavedRuleResourcePolicy);
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
                        : row),
                draft.SavedRuleResourcePolicy);
    }
}
