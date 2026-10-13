// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Path-free policy snapshot for the exact capability admitted for one run.</summary>
public sealed class CapabilityRunSummary
{
    /// <summary>Creates immutable audit facts; verification is always derived from evidence.</summary>
    [JsonConstructor]
    public CapabilityRunSummary(
        string routeId,
        string capabilityFingerprint,
        CapabilityPublicationStatus publication,
        CapabilityEvidenceStatus evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentNullException.ThrowIfNull(capabilityFingerprint);
        if (!CapabilityRouteIdentity.IsSha256(capabilityFingerprint))
        {
            throw new ArgumentException("Capability fingerprint must be a lowercase SHA-256 value.",
                nameof(capabilityFingerprint));
        }
        if (!Enum.IsDefined(publication))
        {
            throw new ArgumentOutOfRangeException(nameof(publication), publication, null);
        }
        if (!Enum.IsDefined(evidence))
        {
            throw new ArgumentOutOfRangeException(nameof(evidence), evidence, null);
        }
        RouteId = routeId;
        CapabilityFingerprint = capabilityFingerprint;
        Publication = publication;
        Evidence = evidence;
    }

    /// <summary>Stable exact route identity captured from the resolved policy decision.</summary>
    public string RouteId { get; }
    /// <summary>Capability-definition fingerprint, separate from the compilation fingerprint.</summary>
    public string CapabilityFingerprint { get; }
    /// <summary>Publication classification at execution admission.</summary>
    public CapabilityPublicationStatus Publication { get; }
    /// <summary>Exact evidence class; verified never implies direct Golden evidence.</summary>
    public CapabilityEvidenceStatus Evidence { get; }
    /// <summary>Derived unverified/verified token from the shared disclosure owner.</summary>
    public string VerificationStatus => CapabilityWorkflowReadinessProjector.GetVerificationStatus(Evidence);

    internal static CapabilityRunSummary? FromResolvedCapability(ResolvedCapability? capability) =>
        capability is null ? null : new(
            capability.Identity.RouteId,
            capability.CapabilityFingerprint,
            capability.Publication.Value,
            capability.Evidence.Value);
}

[JsonSerializable(typeof(CapabilityRunSummary))]
internal sealed partial class CapabilityRunReportJsonContext : JsonSerializerContext;
