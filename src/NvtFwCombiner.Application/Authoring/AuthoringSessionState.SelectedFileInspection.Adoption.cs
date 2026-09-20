using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;

namespace NvtFwCombiner.Application.Authoring;

public sealed partial class AuthoringSessionState
{
    /// <summary>Refreshes only blocked prerequisite results for the same retained immutable sources.</summary>
    internal AuthoringSessionTransitionResult TryRefreshBlockedInputInspection(ActiveSessionSnapshot expected,
        AuthoringCapabilityCatalogSnapshot catalog, IReadOnlyCollection<AuthoringInputSlotStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(statuses);
        lock (_transitionLock)
        {
            AuthoringInputSlotStatus[] captured = [.. statuses];
            AuthoringCapabilityRoute? route = catalog.Routes.Count == 1 ? catalog.Routes[0] : null;
            bool valid = ReferenceEquals(_current, expected) && HasCoherentCapturedSources(expected) &&
                catalog.WorkflowId == expected.WorkflowId && catalog.ResolutionToken == expected.ResolutionToken &&
                route?.Identity.RouteId == expected.SelectedRouteId && route.CompilationFingerprint is null &&
                route.CapabilityFingerprint == expected.CapabilityFingerprint &&
                captured.Length == expected.InputSlotStatuses.Count &&
                captured.Select(static status => status.SlotId).Distinct(StringComparer.Ordinal).Count() == captured.Length &&
                captured.All(status =>
                {
                    AuthoringInputSlotStatus? previous = expected.InputSlotStatuses.SingleOrDefault(item => item.SlotId == status.SlotId);
                    return previous is not null && status.WorkflowId == expected.WorkflowId &&
                        status.RouteId == expected.SelectedRouteId && status.ResolutionToken == expected.ResolutionToken &&
                        status.CapabilityFingerprint == expected.CapabilityFingerprint && status.AuthoringRevision == expected.AuthoringRevision &&
                        status.CompilationFingerprint is null && !status.IsTerminal && status.AcceptedBytes is null &&
                        status.Readiness == ResolvedChildReadiness.Blocked && status.AddressSpaceId == previous.AddressSpaceId &&
                        status.SelectedPathHint == previous.SelectedPathHint && status.FileStamp == previous.FileStamp &&
                        status.CapturedSource?.AcceptedBytes is { } source && previous.CapturedSource?.AcceptedBytes is { } retained &&
                        source.Span.SequenceEqual(retained.Span);
                });
            if (!valid)
            {
                return Failure(AuthoringSessionIssueCodes.StaleInspection,
                    "The blocked inspection no longer owns the current retained inputs.", WorkflowId);
            }
            Dictionary<string, AuthoringInputSlotStatus> bySlot = captured.ToDictionary(static status => status.SlotId, StringComparer.Ordinal);
            string reference = FormattableString.Invariant($"inspection-batch:{expected.AuthoringRevision.Value}:pre-compilation");
            AuthoringSlotState[] slots = [.. expected.Slots.Select(slot => bySlot.TryGetValue(slot.DefinitionId, out AuthoringInputSlotStatus? status)
                ? new AuthoringSlotState(slot.DefinitionId, slot.SelectedPath, slot.FileStamp, AuthoringSlotLifecycle.Error,
                    new AuthoringSlotIssueReference(AuthoringDerivedResultKind.Inspection, reference,
                        status.SelectionReadiness.IssueCode ?? InputSelectionReadinessIssueCodes.SelectionNotApplicable))
                : slot)];
            ActiveSessionSnapshot refreshed = CopySnapshot(expected, expected.AuthoringRevision, slots,
                expected.DraftState, expected.DraftCapabilityFingerprint, [], captured, expected.InputSelectionReadiness);
            Volatile.Write(ref _current, refreshed);
            return new(refreshed, null);
        }
    }

    /// <summary>Retains source inspection while revoking derived action results for one reinspection attempt.</summary>
    internal AuthoringSessionTransitionResult TryBeginAcceptedInputReinspection(ActiveSessionSnapshot expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        lock (_transitionLock)
        {
            if (!ReferenceEquals(_current, expected) ||
                (!expected.HasCurrentInputInspection && !HasCoherentCapturedSources(expected)))
            {
                return Failure(AuthoringSessionIssueCodes.StaleInspection, "The accepted inputs changed before reinspection.", WorkflowId);
            }
            ActiveSessionSnapshot retained = CopySnapshot(expected, expected.AuthoringRevision, expected.Slots,
                expected.DraftState, expected.DraftCapabilityFingerprint,
                expected.DerivedPublications.Where(static publication => publication.Kind == AuthoringDerivedResultKind.Inspection),
                expected.InputSlotStatuses, expected.InputSelectionReadiness, expected.MetadataInspection);
            Volatile.Write(ref _current, retained);
            return new(retained, null);
        }
    }

    private static bool HasCoherentCapturedSources(ActiveSessionSnapshot snapshot)
    {
        AuthoringSlotState[] selected = [.. snapshot.Slots.Where(static slot => slot.SelectedPath is not null)];
        return snapshot.CompilationFingerprint is null && selected.Length > 0 &&
            selected.Length == snapshot.InputSlotStatuses.Count && selected.All(slot =>
            {
                AuthoringInputSlotStatus? status = snapshot.InputSlotStatuses.SingleOrDefault(candidate => candidate.SlotId == slot.DefinitionId);
                return slot.Lifecycle != AuthoringSlotLifecycle.Checking && status?.CapturedSource is { AcceptedBytes: not null } source &&
                    status.SelectedPathHint == slot.SelectedPath && status.FileStamp == slot.FileStamp && source.FileStamp == slot.FileStamp &&
                    status.AuthoringRevision == snapshot.AuthoringRevision && status.ResolutionToken == snapshot.ResolutionToken &&
                    status.RouteId == snapshot.SelectedRouteId && status.CapabilityFingerprint == snapshot.CapabilityFingerprint &&
                    status.WorkflowId == snapshot.WorkflowId && status.CompilationFingerprint is null &&
                    status.AcceptedBytes is null && status.Readiness == ResolvedChildReadiness.Blocked;
            });
    }

    /// <summary>Completes reinspection only if its original source snapshot is still current.</summary>
    internal AuthoringSessionTransitionResult TryAdoptExactSlotFileInspectionBatch(ActiveSessionSnapshot expected,
        AuthoringCapabilityCatalogSnapshot catalog, IReadOnlyCollection<AuthoringInputSlotStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(statuses);
        lock (_transitionLock)
        {
            return ReferenceEquals(_current, expected) && expected.ResolutionToken == catalog.ResolutionToken
                ? TryAdoptExactSlotFileInspectionBatch(catalog, statuses)
                : Failure(AuthoringSessionIssueCodes.StaleInspection, "The accepted inputs changed during reinspection.", WorkflowId);
        }
    }

    /// <summary>Adopts a format-selected exact batch only while every original source lease remains current.</summary>
    internal AuthoringSessionTransitionResult TryAdoptExactSlotFileInspectionBatch(
        AuthoringCapabilityCatalogSnapshot catalog, IReadOnlyList<AuthoringSlotInspectionLease> leases,
        IReadOnlyCollection<AuthoringInputSlotStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(leases);
        ArgumentNullException.ThrowIfNull(statuses);
        lock (_transitionLock)
        {
            AuthoringSlotInspectionLease[] capturedLeases = [.. leases];
            AuthoringInputSlotStatus[] capturedStatuses = [.. statuses];
            HashSet<string> ids = capturedLeases.Select(static lease => lease.DefinitionId).ToHashSet(StringComparer.Ordinal);
            bool valid = _current is not null && _current.ResolutionToken == catalog.ResolutionToken &&
                _current.WorkflowId == catalog.WorkflowId && capturedLeases.Length > 0 && ids.Count == capturedLeases.Length &&
                capturedStatuses.Length == capturedLeases.Length &&
                ids.SetEquals(_current.Slots.Where(static slot => slot.Lifecycle == AuthoringSlotLifecycle.Checking)
                    .Select(static slot => slot.DefinitionId)) &&
                capturedLeases.All(lease => InspectionLeaseMatches(lease, _current) &&
                    capturedStatuses.Count(status => status.SlotId == lease.DefinitionId &&
                        status.SelectedPathHint == lease.SelectedPath && status.AuthoringRevision == lease.AuthoringRevision) == 1);
            return valid
                ? TryAdoptExactSlotFileInspectionBatch(catalog, capturedStatuses)
                : Failure(AuthoringSessionIssueCodes.StaleInspection,
                    "The format inspection no longer owns the complete current selected-file batch.", WorkflowId);
        }
    }

    /// <summary>Atomically adopts one complete exact inspection without re-reading its content.</summary>
    internal AuthoringSessionTransitionResult TryAdoptExactSlotFileInspectionBatch(
        AuthoringCapabilityCatalogSnapshot catalog,
        IReadOnlyCollection<AuthoringInputSlotStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(statuses);
        lock (_transitionLock)
        {
            AuthoringCapabilityRoute? route = catalog.Routes.Count == 1
                ? catalog.Routes[0]
                : null;
            ResolvedCapability? inspected = route?.ExactCapability;
            AuthoringInputSlotStatus[] captured = [.. statuses];
            AuthoringRevision expectedRevision =
                _current?.AuthoringRevision ?? new AuthoringRevision(1);
            if (captured.Any(status => status.AuthoringRevision != expectedRevision))
            {
                return Failure(
                    AuthoringSessionIssueCodes.StaleInspection,
                    "The inspection batch belongs to an older authoring revision.",
                    WorkflowId);
            }

            var definitionIds = captured.Select(static status => status.SlotId)
                .ToHashSet(StringComparer.Ordinal);
            bool invalid = route is null ||
                inspected is null ||
                captured.Length == 0 ||
                definitionIds.Count != captured.Length ||
                !definitionIds.SetEquals(route.SlotDefinitions.Select(
                    static definition => definition.DefinitionId)) ||
                captured.Any(status =>
                    !status.IsTerminal ||
                    string.IsNullOrWhiteSpace(status.SelectedPathHint) ||
                    !StringComparer.Ordinal.Equals(status.WorkflowId, catalog.WorkflowId) ||
                    !StringComparer.Ordinal.Equals(status.RouteId, route.Identity.RouteId) ||
                    status.ResolutionToken != catalog.ResolutionToken ||
                    !StringComparer.Ordinal.Equals(
                        status.CapabilityFingerprint,
                        route.CapabilityFingerprint) ||
                    !StringComparer.Ordinal.Equals(
                        status.CompilationFingerprint,
                        route.CompilationFingerprint));
            if (invalid)
            {
                return Failure(
                    AuthoringSessionIssueCodes.InvalidPublication,
                    "The inspection batch does not match one complete exact catalog publication.",
                    WorkflowId);
            }

            ResolvedCapability exact = inspected!;
            ResolvedCapability effective = _current?.ExactCapability is { } retained &&
                CompiledAuthoringWorkflowService.IsEquivalentExactCapability(retained, exact)
                    ? retained
                    : exact;
            AuthoringCapabilityCatalogSnapshot effectiveCatalog = ReferenceEquals(effective, exact)
                ? catalog
                : AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(
                    effective,
                    route!.DiscoveryTransition);
            AuthoringSessionTransitionResult activated = Activate(effectiveCatalog);
            if (!activated.Succeeded)
            {
                return activated;
            }

            AuthoringSlotInspectionBatchStartResult started = BeginSlotFileInspections(
                captured.ToDictionary(
                    static status => status.SlotId,
                    static status => status.SelectedPathHint!,
                    StringComparer.Ordinal));
            if (!started.Succeeded)
            {
                return new AuthoringSessionTransitionResult(started.Snapshot, started.Issue);
            }

            Dictionary<string, AuthoringInputSlotStatus> rebound = captured.ToDictionary(
                static status => status.SlotId,
                status => status.RebindEquivalentCapability(
                    effective,
                    started.Snapshot!.AuthoringRevision),
                StringComparer.Ordinal);
            return TryCompleteSlotFileInspectionBatch(
                effectiveCatalog,
                started.Leases,
                rebound);
        }
    }
}
