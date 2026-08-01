namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Shared exact-route authoring decision used by every caller surface.</summary>
public enum CapabilityAuthoringAvailability
{
    /// <summary>The route may be selected for authoring.</summary>
    Available,

    /// <summary>The route is intentionally unavailable for authoring.</summary>
    Unavailable,
}

/// <summary>Owner-approved publication classification, independent from Build.</summary>
public enum CapabilityPublicationStatus
{
    /// <summary>No owner-approved publication classification exists yet.</summary>
    Unclassified,

    /// <summary>The route is publicly supported.</summary>
    Supported,

    /// <summary>The route is a reviewed candidate without a support claim.</summary>
    Candidate,

    /// <summary>The route is retained for internal use.</summary>
    Internal,

    /// <summary>The route is retained only for tests.</summary>
    TestOnly,
}

/// <summary>Exact evidence classification, independent from authoring and Build.</summary>
public enum CapabilityEvidenceStatus
{
    /// <summary>The exact route has an owner-approved complete golden.</summary>
    DirectGolden,

    /// <summary>The route uses a separately approved fact-scoped alias.</summary>
    ApprovedAlias,

    /// <summary>The route is covered by an approved synthetic oracle.</summary>
    SyntheticOracle,

    /// <summary>The route has contract evidence without a complete golden.</summary>
    ContractOnly,

    /// <summary>No approved evidence declaration exists for the exact route.</summary>
    Missing,
}

/// <summary>One decision pinned to both stable route identity and executable semantics.</summary>
public sealed record PinnedCapabilityDecision<TValue>
    where TValue : struct, Enum
{
    /// <summary>Creates one immutable route/fingerprint-pinned decision.</summary>
    public PinnedCapabilityDecision(
        string decisionId,
        string routeId,
        string capabilityFingerprint,
        TValue value,
        string sourceReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentNullException.ThrowIfNull(capabilityFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);
        if (!CapabilityRouteIdentity.IsSha256(capabilityFingerprint))
        {
            throw new ArgumentException(
                "Capability decisions require a lowercase SHA-256 fingerprint.",
                nameof(capabilityFingerprint));
        }

        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Capability decision value is invalid.");
        }

        DecisionId = decisionId;
        RouteId = routeId;
        CapabilityFingerprint = capabilityFingerprint;
        Value = value;
        SourceReference = sourceReference;
    }

    /// <summary>Stable decision/declaration id.</summary>
    public string DecisionId { get; }

    /// <summary>Stable exact route pinned by this decision.</summary>
    public string RouteId { get; }

    /// <summary>Reviewed capability-definition fingerprint pinned by this decision.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Typed decision value.</summary>
    public TValue Value { get; }

    /// <summary>Traceable owner/policy/evidence source.</summary>
    public string SourceReference { get; }
}
