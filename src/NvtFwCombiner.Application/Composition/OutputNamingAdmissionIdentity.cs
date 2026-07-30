using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Application.Composition;

/// <summary>
/// Current capability publication and authoring revision admitted for one
/// normal output-name preview or build.
/// </summary>
public sealed record OutputNamingAdmissionIdentity
{
    internal OutputNamingAdmissionIdentity(
        string routeId,
        string capabilityFingerprint,
        ResolutionToken resolutionToken,
        long authoringRevision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityFingerprint);
        if (!CapabilityRouteIdentity.IsSha256(capabilityFingerprint))
        {
            throw new ArgumentException(
                "Output naming admission requires a lowercase SHA-256 capability fingerprint.",
                nameof(capabilityFingerprint));
        }

        resolutionToken.EnsureValid(nameof(resolutionToken));
        ArgumentOutOfRangeException.ThrowIfNegative(authoringRevision);
        RouteId = routeId;
        CapabilityFingerprint = capabilityFingerprint;
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
    }

    /// <summary>Captures the current publication identity at the run boundary.</summary>
    public static OutputNamingAdmissionIdentity Capture(
        ResolvedCapability capability,
        long currentAuthoringRevision)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentOutOfRangeException.ThrowIfNegative(currentAuthoringRevision);
        CapabilityPublicationCoherence.ValidateResolved(capability);
        return new OutputNamingAdmissionIdentity(
            capability.Identity.RouteId,
            capability.CapabilityFingerprint,
            capability.ResolutionToken,
            currentAuthoringRevision);
    }

    /// <summary>Stable exact capability route.</summary>
    public string RouteId { get; }

    /// <summary>Executable semantic fingerprint for the publication.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Exact capability and metadata-plan publication token.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Authoring revision current at preview or build admission.</summary>
    public long AuthoringRevision { get; }
}
