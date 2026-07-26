using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>One compiler-proved capability and its independently pinned decisions.</summary>
public sealed record CanonicalCapabilityDefinition
{
    /// <summary>Creates one candidate capability definition.</summary>
    public CanonicalCapabilityDefinition(
        CapabilityRouteIdentity identity,
        CompiledComposition compiledComposition,
        PinnedCapabilityDecision<CapabilityAuthoringAvailability> authoring,
        PinnedCapabilityDecision<CapabilityPublicationStatus> publication,
        PinnedCapabilityDecision<CapabilityEvidenceStatus> evidence,
        MetadataPlanDefinition? metadataPlan = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(compiledComposition);
        ArgumentNullException.ThrowIfNull(authoring);
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(evidence);

        if (!StringComparer.Ordinal.Equals(identity.IcId, compiledComposition.IcId) ||
            !StringComparer.Ordinal.Equals(
                identity.WorkflowId,
                compiledComposition.ExperienceId))
        {
            throw new ArgumentException(
                "Capability route identity must match the compiled IC and workflow.",
                nameof(identity));
        }

        Identity = identity;
        CompiledComposition = compiledComposition;
        CapabilityFingerprint = compiledComposition.CompilationFingerprint;
        MetadataPlan = metadataPlan ?? MetadataPlanDefinition.Empty;
        ValidateDecision(authoring, "authoring decision");
        ValidateDecision(publication, "publication decision");
        ValidateDecision(evidence, "evidence decision");
        Authoring = authoring;
        Publication = publication;
        Evidence = evidence;
    }

    /// <summary>Stable exact route identity.</summary>
    public CapabilityRouteIdentity Identity { get; }

    /// <summary>The existing sole executable composition artifact.</summary>
    public CompiledComposition CompiledComposition { get; }

    /// <summary>Firmware-semantic revision derived by the compiler.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Canonical reference-only metadata plan selected by this route.</summary>
    public MetadataPlanDefinition MetadataPlan { get; }

    /// <summary>Shared UI/CLI authoring decision.</summary>
    public PinnedCapabilityDecision<CapabilityAuthoringAvailability> Authoring { get; }

    /// <summary>Independent publication decision.</summary>
    public PinnedCapabilityDecision<CapabilityPublicationStatus> Publication { get; }

    /// <summary>Independent evidence declaration.</summary>
    public PinnedCapabilityDecision<CapabilityEvidenceStatus> Evidence { get; }

    /// <summary>True only when the compiler admitted the artifact for runtime execution.</summary>
    public bool ExecutionAdmitted =>
        CompiledComposition.Eligibility is
            CompiledCompositionEligibility.LegacyRuntimeExecutable or
            CompiledCompositionEligibility.V2RuntimeExecutable;

    private void ValidateDecision<TValue>(
        PinnedCapabilityDecision<TValue> decision,
        string label)
        where TValue : struct, Enum
    {
        if (!StringComparer.Ordinal.Equals(decision.RouteId, Identity.RouteId) ||
            !StringComparer.Ordinal.Equals(
                decision.CapabilityFingerprint,
                CapabilityFingerprint))
        {
            throw new ArgumentException(
                $"Capability {label} must pin the current route id and capability fingerprint.",
                nameof(decision));
        }
    }
}

/// <summary>Complete candidate loaded and compiled before atomic publication.</summary>
public sealed record CanonicalCapabilityCatalogCandidate
{
    /// <summary>Creates one immutable candidate snapshot.</summary>
    public CanonicalCapabilityCatalogCandidate(
        string catalogId,
        string catalogVersion,
        string sourceSha256,
        IEnumerable<CanonicalCapabilityDefinition> definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogId);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogVersion);
        ArgumentNullException.ThrowIfNull(sourceSha256);
        ArgumentNullException.ThrowIfNull(definitions);
        if (!CapabilityRouteIdentity.IsSha256(sourceSha256))
        {
            throw new ArgumentException(
                "Canonical capability source identity must be a lowercase SHA-256 value.",
                nameof(sourceSha256));
        }

        CatalogId = catalogId;
        CatalogVersion = catalogVersion;
        SourceSha256 = sourceSha256;
        Definitions = Array.AsReadOnly([.. definitions]);
    }

    /// <summary>Stable catalog id.</summary>
    public string CatalogId { get; }

    /// <summary>Version of the loaded source policy/catalog.</summary>
    public string CatalogVersion { get; }

    /// <summary>SHA-256 of the exact loaded source policy/catalog.</summary>
    public string SourceSha256 { get; }

    /// <summary>Compiler-proved definitions awaiting publication.</summary>
    public IReadOnlyList<CanonicalCapabilityDefinition> Definitions { get; }
}

/// <summary>Unique identity for one published in-process resolution snapshot.</summary>
public readonly record struct ResolutionToken
{
    /// <summary>Creates a non-empty resolution token.</summary>
    public ResolutionToken(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Stable token text for equality and report provenance.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>Application-owned resolved capability bound to one catalog publication.</summary>
public sealed record ResolvedCapability(
    CapabilityRouteIdentity Identity,
    string CapabilityFingerprint,
    CompiledComposition CompiledComposition,
    PinnedCapabilityDecision<CapabilityAuthoringAvailability> Authoring,
    PinnedCapabilityDecision<CapabilityPublicationStatus> Publication,
    PinnedCapabilityDecision<CapabilityEvidenceStatus> Evidence,
    ResolvedMetadataPlan MetadataPlan,
    ResolutionToken ResolutionToken)
{
    /// <summary>Compiler-proved execution admission.</summary>
    public bool ExecutionAdmitted =>
        CompiledComposition.Eligibility is
            CompiledCompositionEligibility.LegacyRuntimeExecutable or
            CompiledCompositionEligibility.V2RuntimeExecutable;
}

/// <summary>One immutable published catalog projection.</summary>
public sealed record CanonicalCapabilityCatalogSnapshot
{
    private readonly ReadOnlyDictionary<string, ResolvedCapability> _byRouteId;

    internal CanonicalCapabilityCatalogSnapshot(
        CanonicalCapabilityCatalogCandidate candidate,
        ResolutionToken resolutionToken)
    {
        CatalogId = candidate.CatalogId;
        CatalogVersion = candidate.CatalogVersion;
        SourceSha256 = candidate.SourceSha256;
        ResolutionToken = resolutionToken;

        var byRouteId = new Dictionary<string, ResolvedCapability>(StringComparer.Ordinal);
        foreach (CanonicalCapabilityDefinition definition in candidate.Definitions)
        {
            var resolved = new ResolvedCapability(
                definition.Identity,
                definition.CapabilityFingerprint,
                definition.CompiledComposition,
                definition.Authoring,
                definition.Publication,
                definition.Evidence,
                definition.MetadataPlan.Resolve(resolutionToken),
                resolutionToken);
            if (!byRouteId.TryAdd(definition.Identity.RouteId, resolved))
            {
                throw new ArgumentException(
                    $"Duplicate canonical capability route '{definition.Identity.RouteId}'.",
                    nameof(candidate));
            }
        }

        _byRouteId = new ReadOnlyDictionary<string, ResolvedCapability>(byRouteId);
        Capabilities = Array.AsReadOnly(
        [
            .. byRouteId.Values.OrderBy(
                static capability => capability.Identity.RouteId,
                StringComparer.Ordinal),
        ]);
        CertificationIssues = Array.AsReadOnly(
        [
            .. Capabilities
                .Where(static capability =>
                    capability.Publication.Value == CapabilityPublicationStatus.Supported &&
                    capability.Evidence.Value == CapabilityEvidenceStatus.Missing)
                .Select(static capability => new CapabilityCatalogIssue(
                    CapabilityCatalogIssueCodes.SupportedWithoutEvidence,
                    "A supported route has no approved evidence declaration.",
                    capability.Identity.RouteId)),
        ]);
    }

    /// <summary>Stable catalog id.</summary>
    public string CatalogId { get; }

    /// <summary>Published catalog version.</summary>
    public string CatalogVersion { get; }

    /// <summary>Hash of the exact source catalog.</summary>
    public string SourceSha256 { get; }

    /// <summary>Unique token for this publication.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Resolved exact routes in stable identity order.</summary>
    public IReadOnlyList<ResolvedCapability> Capabilities { get; }

    /// <summary>Certification inconsistencies that do not rewrite Build admission.</summary>
    public IReadOnlyList<CapabilityCatalogIssue> CertificationIssues { get; }

    internal bool TryGet(
        string routeId,
        out ResolvedCapability? capability)
    {
        return _byRouteId.TryGetValue(routeId, out capability);
    }
}
