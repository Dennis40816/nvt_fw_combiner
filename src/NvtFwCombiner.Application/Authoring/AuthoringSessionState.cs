using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>
/// Host-independent state and transition policy for one workflow mode. The
/// desktop owns one instance per mode; CLI creates an ephemeral instance.
/// </summary>
public sealed partial class AuthoringSessionState
{
    private readonly Lock _transitionLock = new();
    private readonly object _publicationIdentity = new();
    private AuthoringCapabilityCatalogSnapshot? _catalog;
    private ActiveSessionSnapshot? _current;

    /// <summary>Creates one isolated mode session without process-global state.</summary>
    public AuthoringSessionState(string workflowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        WorkflowId = workflowId;
    }

    /// <summary>Mode/workflow identity permanently owned by this instance.</summary>
    public string WorkflowId { get; }

    /// <summary>Current coherent snapshot, or null before successful activation.</summary>
    public ActiveSessionSnapshot? CurrentSnapshot => Volatile.Read(ref _current);

    /// <summary>
    /// Invalidates the retained canonical publication and every result derived
    /// from it. Hosts use this fail-closed transition before rebinding visible
    /// selections after a canonical catalog reload.
    /// </summary>
    public void InvalidateCanonicalPublication()
    {
        lock (_transitionLock)
        {
            _catalog = null;
            Volatile.Write(ref _current, null);
        }
    }

    /// <summary>
    /// Activates one canonical publication, preserving only compatible selected
    /// paths and clearing all derived publications.
    /// </summary>
    public AuthoringSessionTransitionResult Activate(
        AuthoringCapabilityCatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!StringComparer.Ordinal.Equals(WorkflowId, catalog.WorkflowId))
        {
            throw new ArgumentException(
                "An authoring session can activate only its own workflow catalog.",
                nameof(catalog));
        }

        lock (_transitionLock)
        {
            if (catalog.Routes.Count == 0)
            {
                return Failure(
                    AuthoringSessionIssueCodes.CatalogUnavailable,
                    "The workflow has no authorable route in the current catalog.",
                    WorkflowId);
            }

            ActiveSessionSnapshot? previous = _current;
            string icId = previous?.SelectedIc ?? catalog.IcChoices[0];
            IReadOnlyList<string> countChoices = catalog.GetIcCountChoices(icId);
            if (countChoices.Count == 0)
            {
                icId = catalog.IcChoices[0];
                countChoices = catalog.GetIcCountChoices(icId);
            }

            string icCount = previous is not null &&
                countChoices.Contains(previous.SelectedIcCount, StringComparer.Ordinal)
                    ? previous.SelectedIcCount
                    : countChoices[0];
            AuthoringSessionTransitionResult resolution =
                ResolveUniqueRoute(catalog, icId, icCount);
            if (!resolution.Succeeded)
            {
                return resolution;
            }

            AuthoringCapabilityRoute route = FindSelectedRoute(
                catalog,
                resolution.Snapshot!.SelectedRouteId);
            bool sameSelection = previous is not null &&
                StringComparer.Ordinal.Equals(
                    previous.SelectedRouteId,
                    route.Identity.RouteId);
            bool sameCompilation = previous is not null &&
                StringComparer.Ordinal.Equals(
                    previous.CompilationFingerprint,
                    route.CompilationFingerprint);
            bool sameSlots = previous is not null &&
                previous.Slots.Select(static slot => slot.DefinitionId)
                    .SequenceEqual(route.SlotDefinitions.Select(static slot => slot.DefinitionId));
            bool sameExactCapability = previous is not null &&
                ReferenceEquals(previous.ExactCapability, route.ExactCapability);
            if (sameSelection &&
                sameCompilation &&
                sameSlots &&
                previous!.ResolutionToken == catalog.ResolutionToken &&
                sameExactCapability)
            {
                return new AuthoringSessionTransitionResult(previous, null);
            }
            if (sameSelection && sameCompilation && sameSlots &&
                previous!.ResolutionToken == catalog.ResolutionToken)
            {
                return Failure(
                    AuthoringSessionIssueCodes.InvalidPublication,
                    "Matching fingerprints cannot replace the retained exact compilation instance.",
                    route.Identity.RouteId);
            }

            AuthoringDraftState? draftState = ProjectDraft(route, previous);
            bool compatibleCapability = previous is not null &&
                StringComparer.Ordinal.Equals(
                    previous.CapabilityFingerprint,
                    route.CapabilityFingerprint);
            AuthoringRevision revision = previous is null
                ? new AuthoringRevision(1)
                : sameSelection && compatibleCapability && sameCompilation && sameSlots
                    ? previous.AuthoringRevision
                    : previous.AuthoringRevision.Next();
            ActiveSessionSnapshot snapshot = CreateSnapshot(
                catalog,
                route,
                revision,
                ProjectSlots(route, previous),
                draftState,
                draftState is null
                    ? null
                    : route.CapabilityFingerprint,
                []);
            _catalog = catalog;
            Volatile.Write(ref _current, snapshot);
            return new AuthoringSessionTransitionResult(snapshot, null);
        }
    }

    /// <summary>Activates one compiled selection and retains its readiness on the same revision.</summary>
    public AuthoringSessionTransitionResult Activate(
        CompiledAuthoringSelectionSnapshot selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        AuthoringSessionTransitionResult activated = Activate(selection.Catalog);
        if (!activated.Succeeded)
        {
            return activated;
        }

        lock (_transitionLock)
        {
            if (!ReferenceEquals(_current, activated.Snapshot))
            {
                return Failure(
                    AuthoringSessionIssueCodes.StalePublication,
                    "The compiled selection changed before its readiness could be retained.",
                    WorkflowId);
            }

            ActiveSessionSnapshot snapshot = CopySnapshot(
                _current!,
                _current!.AuthoringRevision,
                _current.Slots,
                _current.DraftState,
                _current.DraftCapabilityFingerprint,
                _current.DerivedPublications,
                _current.InputSlotStatuses,
                selection.Slots,
                _current.MetadataInspection);
            Volatile.Write(ref _current, snapshot);
            return new AuthoringSessionTransitionResult(snapshot, null);
        }
    }

    /// <summary>Selects one IC and IC Count without accepting a caller-owned map guess.</summary>
    public AuthoringSessionTransitionResult Select(
        string icId,
        string icCountVariant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(icCountVariant);
        lock (_transitionLock)
        {
            if (_catalog is null || _current is null)
            {
                return Failure(
                    AuthoringSessionIssueCodes.CatalogUnavailable,
                    "The authoring session is not active.",
                    WorkflowId);
            }

            AuthoringSessionTransitionResult resolution = ResolveUniqueRoute(
                _catalog,
                icId,
                icCountVariant);
            if (!resolution.Succeeded)
            {
                return resolution;
            }

            AuthoringCapabilityRoute route = FindSelectedRoute(
                _catalog,
                resolution.Snapshot!.SelectedRouteId);
            if (StringComparer.Ordinal.Equals(
                    _current.SelectedRouteId,
                    route.Identity.RouteId))
            {
                return new AuthoringSessionTransitionResult(_current, null);
            }

            AuthoringDraftState? draftState = ProjectDraft(route, _current);
            ActiveSessionSnapshot snapshot = CreateSnapshot(
                _catalog,
                route,
                _current.AuthoringRevision.Next(),
                ProjectSlots(route, _current),
                draftState,
                draftState is null
                    ? null
                    : route.CapabilityFingerprint,
                []);
            Volatile.Write(ref _current, snapshot);
            return new AuthoringSessionTransitionResult(snapshot, null);
        }
    }

    /// <summary>
    /// Selects or clears one file. A real host captures the stamp outside
    /// Application and supplies it with the path.
    /// </summary>
    public AuthoringSessionTransitionResult SetSlotFile(
        string slotDefinitionId,
        string? selectedPath,
        FileStamp? fileStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotDefinitionId);
        if (selectedPath is null && fileStamp is not null)
        {
            throw new ArgumentException(
                "A selected-file stamp requires one selected path.",
                nameof(selectedPath));
        }

        lock (_transitionLock)
        {
            if (_current is null)
            {
                return Failure(
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
                return Failure(
                    AuthoringSessionIssueCodes.SlotUnavailable,
                    "The selected slot is not part of the active resolved route.",
                    slotDefinitionId);
            }

            AuthoringSlotState existing = _current.Slots[index];
            if (StringComparer.Ordinal.Equals(
                    existing.SelectedPath,
                    selectedPath) &&
                existing.FileStamp == fileStamp)
            {
                return new AuthoringSessionTransitionResult(_current, null);
            }

            AuthoringSlotState[] slots = [.. _current.Slots];
            slots[index] = new AuthoringSlotState(
                slotDefinitionId,
                selectedPath,
                fileStamp,
                selectedPath is null
                    ? AuthoringSlotLifecycle.Empty
                    : AuthoringSlotLifecycle.Selected);
            ActiveSessionSnapshot snapshot = CopySnapshot(
                _current,
                _current.AuthoringRevision.Next(),
                slots,
                _current.DraftState,
                _current.DraftCapabilityFingerprint,
                []);
            Volatile.Write(ref _current, snapshot);
            return new AuthoringSessionTransitionResult(snapshot, null);
        }
    }

    /// <summary>
    /// Replaces or clears the current immutable typed draft. Draft content is
    /// owned by its concrete Application contract; this session owns lifetime.
    /// </summary>
    public AuthoringSessionTransitionResult SetDraft(
        AuthoringDraftState? draftState)
    {
        lock (_transitionLock)
        {
            if (_current is null)
            {
                return Failure(
                    AuthoringSessionIssueCodes.CatalogUnavailable,
                    "The authoring session is not active.",
                    WorkflowId);
            }

            if (!SupportsDraftState(WorkflowId, draftState?.DraftKind))
            {
                return Failure(
                    AuthoringSessionIssueCodes.DraftUnavailable,
                    "The active workflow does not declare the requested authoring-draft contract.",
                    WorkflowId);
            }

            AuthoringDraftState? immutableDraft =
                draftState?.CreateImmutableSnapshot();
            if (_current.DraftState is null
                    ? immutableDraft is null
                    : immutableDraft is not null &&
                        _current.DraftState.HasSameValue(immutableDraft))
            {
                return new AuthoringSessionTransitionResult(_current, null);
            }

            ActiveSessionSnapshot snapshot = CopySnapshot(
                _current,
                _current.AuthoringRevision.Next(),
                _current.Slots,
                immutableDraft,
                immutableDraft is null
                    ? null
                    : _current.CapabilityFingerprint,
                []);
            Volatile.Write(ref _current, snapshot);
            return new AuthoringSessionTransitionResult(snapshot, null);
        }
    }

    /// <summary>Captures the complete identity required by one asynchronous result.</summary>
    public AuthoringPublicationLease CapturePublicationLease(
        AuthoringDerivedResultKind kind,
        string? compilationFingerprint = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unknown authoring result kind.");
        }

        lock (_transitionLock)
        {
            ActiveSessionSnapshot snapshot = _current ??
                throw new InvalidOperationException(
                    "An authoring session must be active before work begins.");
            return new AuthoringPublicationLease(
                _publicationIdentity,
                kind,
                snapshot.ResolutionToken,
                snapshot.AuthoringRevision,
                snapshot.SelectedRouteId,
                snapshot.CapabilityFingerprint,
                snapshot.Slots.Select(static slot =>
                    new AuthoringSlotPublicationIdentity(
                        slot.DefinitionId,
                        slot.SelectedPath,
                        slot.FileStamp)),
                compilationFingerprint);
        }
    }

    /// <summary>Checks whether one asynchronous result still belongs to the active inputs.</summary>
    public bool IsPublicationCurrent(AuthoringPublicationLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        lock (_transitionLock)
        {
            return _current is not null &&
                LeaseMatches(lease, _current, _publicationIdentity);
        }
    }

    /// <summary>Publishes only when every captured identity still matches.</summary>
    public AuthoringPublicationResult TryPublish(
        AuthoringPublicationLease lease,
        AuthoringDerivedPublication publication)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(publication);
        lock (_transitionLock)
        {
            if (lease.Kind != publication.Kind ||
                !StringComparer.Ordinal.Equals(
                    lease.CompilationFingerprint,
                    publication.CompilationFingerprint))
            {
                return PublicationFailure(
                    AuthoringSessionIssueCodes.InvalidPublication,
                    "The derived result kind or compilation does not match its publication lease.");
            }

            if (_current is null ||
                !LeaseMatches(lease, _current, _publicationIdentity))
            {
                return PublicationFailure(
                    AuthoringSessionIssueCodes.StalePublication,
                    "The authoring selection, catalog, slot, or file changed before publication.");
            }

            AuthoringDerivedPublication[] publications =
            [
                .. _current.DerivedPublications
                    .Where(existing => existing.Kind != publication.Kind),
                publication,
            ];
            Array.Sort(
                publications,
                static (left, right) => left.Kind.CompareTo(right.Kind));
            ActiveSessionSnapshot snapshot = CopySnapshot(
                _current,
                _current.AuthoringRevision,
                _current.Slots,
                _current.DraftState,
                _current.DraftCapabilityFingerprint,
                publications,
                _current.InputSlotStatuses,
                metadataInspection: _current.MetadataInspection);
            Volatile.Write(ref _current, snapshot);
            return new AuthoringPublicationResult(true, null);
        }
    }

    private AuthoringSessionTransitionResult ResolveUniqueRoute(
        AuthoringCapabilityCatalogSnapshot catalog,
        string icId,
        string icCountVariant)
    {
        AuthoringCapabilityRoute[] routes = catalog.FindRoutes(
            icId,
            icCountVariant);
        if (routes.Length == 0)
        {
            return Failure(
                AuthoringSessionIssueCodes.RouteUnavailable,
                "The selected IC and IC Count are absent from the authoring catalog.",
                $"{icId}/{icCountVariant}");
        }

        if (routes.Length > 1)
        {
            return Failure(
                AuthoringSessionIssueCodes.RouteAmbiguous,
                "The selected IC and IC Count identify more than one map variant.",
                $"{icId}/{icCountVariant}");
        }

        AuthoringDraftState? draftState = ProjectDraft(routes[0], _current);
        ActiveSessionSnapshot routeIdentity = CreateSnapshot(
            catalog,
            routes[0],
            _current?.AuthoringRevision ?? new AuthoringRevision(1),
            ProjectSlots(routes[0], _current),
            draftState,
            draftState is null
                ? null
                : routes[0].CapabilityFingerprint,
            []);
        return new AuthoringSessionTransitionResult(routeIdentity, null);
    }

    private static AuthoringCapabilityRoute FindSelectedRoute(
        AuthoringCapabilityCatalogSnapshot catalog,
        string routeId)
    {
        return catalog.Routes.Single(route =>
            StringComparer.Ordinal.Equals(route.Identity.RouteId, routeId));
    }

    private static AuthoringSlotState[] ProjectSlots(
        AuthoringCapabilityRoute route,
        ActiveSessionSnapshot? previous)
    {
        bool canPreserve = previous is not null &&
            StringComparer.Ordinal.Equals(
                previous.CapabilityFingerprint,
                route.CapabilityFingerprint);
        return
        [
            .. route.SlotDefinitions.Select(definition =>
            {
                AuthoringSlotState? compatible = canPreserve
                    ? previous!.Slots.FirstOrDefault(slot =>
                        StringComparer.Ordinal.Equals(
                            slot.DefinitionId,
                            definition.DefinitionId))
                    : null;
                return compatible is null
                    ? new AuthoringSlotState(
                        definition.DefinitionId,
                        null,
                        null,
                        AuthoringSlotLifecycle.Empty)
                    : new AuthoringSlotState(
                        definition.DefinitionId,
                        compatible.SelectedPath,
                        compatible.FileStamp,
                        compatible.SelectedPath is null
                            ? AuthoringSlotLifecycle.Empty
                            : AuthoringSlotLifecycle.Selected);
            }),
        ];
    }

    private static AuthoringDraftState? ProjectDraft(
        AuthoringCapabilityRoute route,
        ActiveSessionSnapshot? previous)
    {
        return previous is not null &&
            StringComparer.Ordinal.Equals(
                previous.DraftCapabilityFingerprint,
                route.CapabilityFingerprint)
                ? previous.DraftState
                : null;
    }

    private static bool SupportsDraftState(
        string workflowId,
        AuthoringDraftKind? draftKind)
    {
        return workflowId switch
        {
            ExperienceIds.GeneralMerge =>
                draftKind is null or AuthoringDraftKind.GeneralMerge,
            ExperienceIds.GeneralReplace =>
                draftKind is null or AuthoringDraftKind.GeneralMapping,
            ExperienceIds.CtrlRamReplace =>
                draftKind is null or AuthoringDraftKind.CtrlRamFirmwareVersionEdit,
            _ => draftKind is null,
        };
    }

    private static ActiveSessionSnapshot CreateSnapshot(
        AuthoringCapabilityCatalogSnapshot catalog,
        AuthoringCapabilityRoute route,
        AuthoringRevision revision,
        IEnumerable<AuthoringSlotState> slots,
        AuthoringDraftState? draftState,
        string? draftCapabilityFingerprint,
        IEnumerable<AuthoringDerivedPublication> publications,
        IEnumerable<AuthoringInputSlotStatus>? inputSlotStatuses = null,
        IEnumerable<InputSelectionMemberReadiness>? inputSelectionReadiness = null,
        MetadataInspectionSnapshot? metadataInspection = null)
    {
        return new ActiveSessionSnapshot(
            catalog.WorkflowId,
            catalog.ResolutionToken,
            revision,
            route.Identity.RouteId,
            route.CapabilityFingerprint,
            route.ExecutionAdmitted,
            route.Identity.IcId,
            route.Identity.IcCountVariant,
            route.Identity.MapVariant,
            catalog.IcChoices,
            catalog.GetIcCountChoices(route.Identity.IcId),
            slots,
            draftState,
            draftCapabilityFingerprint,
            publications,
            route.CompilationFingerprint,
            route.ExactCapability,
            inputSlotStatuses,
            inputSelectionReadiness,
            metadataInspection);
    }

    private static ActiveSessionSnapshot CopySnapshot(
        ActiveSessionSnapshot current,
        AuthoringRevision revision,
        IEnumerable<AuthoringSlotState> slots,
        AuthoringDraftState? draftState,
        string? draftCapabilityFingerprint,
        IEnumerable<AuthoringDerivedPublication> publications,
        IEnumerable<AuthoringInputSlotStatus>? inputSlotStatuses = null,
        IEnumerable<InputSelectionMemberReadiness>? inputSelectionReadiness = null,
        MetadataInspectionSnapshot? metadataInspection = null)
    {
        return new ActiveSessionSnapshot(
            current.WorkflowId,
            current.ResolutionToken,
            revision,
            current.SelectedRouteId,
            current.CapabilityFingerprint,
            current.ExecutionAdmitted,
            current.SelectedIc,
            current.SelectedIcCount,
            current.SelectedMapVariant,
            current.IcChoices,
            current.IcCountChoices,
            slots,
            draftState,
            draftCapabilityFingerprint,
            publications,
            current.CompilationFingerprint,
            current.ExactCapability,
            inputSlotStatuses,
            inputSelectionReadiness ?? current.InputSelectionReadiness,
            metadataInspection);
    }

    private static bool LeaseMatches(
        AuthoringPublicationLease lease,
        ActiveSessionSnapshot snapshot,
        object publicationIdentity)
    {
        if (!ReferenceEquals(lease.SessionIdentity, publicationIdentity) ||
            lease.ResolutionToken != snapshot.ResolutionToken ||
            lease.AuthoringRevision != snapshot.AuthoringRevision ||
            !StringComparer.Ordinal.Equals(
                lease.SelectedRouteId,
                snapshot.SelectedRouteId) ||
            !StringComparer.Ordinal.Equals(
                lease.CapabilityFingerprint,
                snapshot.CapabilityFingerprint) ||
            (lease.CompilationFingerprint is not null &&
                snapshot.CompilationFingerprint is not null &&
                !StringComparer.Ordinal.Equals(
                    lease.CompilationFingerprint,
                    snapshot.CompilationFingerprint)) ||
            lease.Slots.Count != snapshot.Slots.Count)
        {
            return false;
        }

        var currentSlots = snapshot.Slots.ToDictionary(
            static slot => slot.DefinitionId,
            StringComparer.Ordinal);
        return lease.Slots.All(captured =>
            currentSlots.TryGetValue(
                captured.DefinitionId,
                out AuthoringSlotState? current) &&
            StringComparer.Ordinal.Equals(
                captured.SelectedPath,
                current.SelectedPath) &&
            captured.FileStamp == current.FileStamp);
    }

    private AuthoringSessionTransitionResult Failure(
        string code,
        string message,
        string? subject)
    {
        return new AuthoringSessionTransitionResult(
            _current,
            new AuthoringSessionIssue(code, message, subject));
    }

    private static AuthoringPublicationResult PublicationFailure(
        string code,
        string message)
    {
        return new AuthoringPublicationResult(
            false,
            new AuthoringSessionIssue(code, message));
    }

}
