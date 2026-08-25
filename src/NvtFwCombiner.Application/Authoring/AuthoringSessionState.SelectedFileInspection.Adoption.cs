using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Application.Authoring;

public sealed partial class AuthoringSessionState
{
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
