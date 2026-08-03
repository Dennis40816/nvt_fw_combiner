namespace NvtFwCombiner.Application.Capabilities;

public sealed partial record CanonicalCapabilityCatalogSnapshot
{
    /// <summary>
    /// Publishes the reviewed exact-route set that one deterministic discovery
    /// binding may resolve after its compiler-owned prerequisite is read.
    /// </summary>
    public ReviewedDiscoveryTransition ResolveReviewedDiscoveryTransition(
        ResolvedCapability discoveryCapability,
        string prerequisiteSlotId)
    {
        ArgumentNullException.ThrowIfNull(discoveryCapability);
        ArgumentException.ThrowIfNullOrWhiteSpace(prerequisiteSlotId);
        if (ResolutionToken != discoveryCapability.ResolutionToken)
        {
            throw new ArgumentException(
                "Discovery capability and reviewed members must share one canonical publication.",
                nameof(discoveryCapability));
        }

        CapabilityRouteIdentity identity = discoveryCapability.Identity;
        ReviewedDiscoveryExactMember[] allowed =
        [
            .. Capabilities
                .Where(capability => MatchesAxes(capability.Identity, identity) &&
                    capability.Authoring.Value == CapabilityAuthoringAvailability.Available)
                .Select(static capability => new ReviewedDiscoveryExactMember(
                    capability.Identity.RouteId,
                    capability.CapabilityFingerprint))
                .Concat(DynamicRoutes
                    .Where(route => MatchesAxes(route.Identity, identity) &&
                        route.Authoring.Value == CapabilityAuthoringAvailability.Available)
                    .Select(static route => new ReviewedDiscoveryExactMember(
                        route.Identity.RouteId,
                        route.CapabilityFingerprint)))
                .Distinct(),
        ];
        var discoveryMember = new ReviewedDiscoveryExactMember(
            identity.RouteId,
            discoveryCapability.CapabilityFingerprint);
        _ = allowed.Length != 0 && allowed.Contains(discoveryMember)
            ? true
            : throw new InvalidOperationException(
                "The discovery capability is not an authorable member of its canonical publication.");

        return new ReviewedDiscoveryTransition(
            ResolutionToken,
            identity.WorkflowId,
            identity.IcId,
            identity.IcCountVariant,
            discoveryMember,
            prerequisiteSlotId,
            allowed);
    }

    private static bool MatchesAxes(
        CapabilityRouteIdentity candidate,
        CapabilityRouteIdentity discovery)
    {
        return StringComparer.Ordinal.Equals(candidate.WorkflowId, discovery.WorkflowId) &&
            StringComparer.Ordinal.Equals(candidate.IcId, discovery.IcId) &&
            StringComparer.Ordinal.Equals(
                candidate.IcCountVariant,
                discovery.IcCountVariant);
    }
}
