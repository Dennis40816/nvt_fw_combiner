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
        string capabilityFingerprint,
        CompiledComposition compiledComposition,
        PinnedCapabilityDecision<CapabilityAuthoringAvailability> authoring,
        PinnedCapabilityDecision<CapabilityPublicationStatus> publication,
        PinnedCapabilityDecision<CapabilityEvidenceStatus> evidence,
        MetadataPlanDefinition? metadataPlan = null,
        CanonicalCapabilityCompilationContract? compilationContract = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityFingerprint);
        if (!CapabilityRouteIdentity.IsSha256(capabilityFingerprint))
        {
            throw new ArgumentException(
                "Canonical capabilities require a lowercase SHA-256 definition fingerprint.",
                nameof(capabilityFingerprint));
        }
        ArgumentNullException.ThrowIfNull(compiledComposition);
        ArgumentNullException.ThrowIfNull(authoring);
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(evidence);
        MetadataPlanDefinition effectiveMetadataPlan =
            metadataPlan ?? MetadataPlanDefinition.Empty;
        CompiledComposition boundComposition = compiledComposition
            .BindCapabilityFingerprint(capabilityFingerprint);
        CanonicalCapabilityCompilationContract effectiveCompilationContract =
            compilationContract ??
            CanonicalCapabilityCompilationContract.FromCompiled(
                identity,
                boundComposition);
        CapabilityPublicationCoherence.ValidateDefinition(
            identity,
            capabilityFingerprint,
            boundComposition,
            effectiveCompilationContract,
            authoring,
            publication,
            evidence,
            effectiveMetadataPlan);

        Identity = identity;
        CompiledComposition = boundComposition;
        CapabilityFingerprint = capabilityFingerprint;
        CompilationContract = effectiveCompilationContract;
        MetadataPlan = effectiveMetadataPlan;
        Authoring = authoring;
        Publication = publication;
        Evidence = evidence;
    }

    /// <summary>Stable exact route identity.</summary>
    public CapabilityRouteIdentity Identity { get; }

    /// <summary>The existing sole executable composition artifact.</summary>
    public CompiledComposition CompiledComposition { get; }

    /// <summary>Reviewed capability-definition fingerprint.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Definition-level bounds for the published compilation.</summary>
    public CanonicalCapabilityCompilationContract CompilationContract { get; }

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
        CapabilityPublicationCoherence.IsExecutionAdmitted(
            CompiledComposition);

}

/// <summary>Complete candidate loaded and compiled before atomic publication.</summary>
public sealed record CanonicalCapabilityCatalogCandidate
{
    /// <summary>Creates one immutable candidate snapshot.</summary>
    public CanonicalCapabilityCatalogCandidate(
        string catalogId,
        string catalogVersion,
        string sourceSha256,
        IEnumerable<CanonicalCapabilityDefinition> definitions,
        IEnumerable<CanonicalDynamicCapabilityDefinition>? dynamicDefinitions = null)
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
        DynamicDefinitions = Array.AsReadOnly([.. dynamicDefinitions ?? []]);
    }

    /// <summary>Stable catalog id.</summary>
    public string CatalogId { get; }

    /// <summary>Version of the loaded source policy/catalog.</summary>
    public string CatalogVersion { get; }

    /// <summary>SHA-256 of the exact loaded source policy/catalog.</summary>
    public string SourceSha256 { get; }

    /// <summary>Compiler-proved definitions awaiting publication.</summary>
    public IReadOnlyList<CanonicalCapabilityDefinition> Definitions { get; }

    /// <summary>Policy-bound definitions compiled only after current authoring resolution.</summary>
    public IReadOnlyList<CanonicalDynamicCapabilityDefinition> DynamicDefinitions { get; }
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

    internal void EnsureValid(string parameterName)
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            throw new ArgumentException(
                "Resolution tokens must retain a non-empty publication identity.",
                parameterName);
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        EnsureValid(nameof(ResolutionToken));
        return Value;
    }
}

/// <summary>Application-owned resolved capability bound to one catalog publication.</summary>
public sealed record ResolvedCapability
{
    /// <summary>Creates one checked capability bound to one exact publication.</summary>
    public ResolvedCapability(
        CapabilityRouteIdentity identity,
        string capabilityFingerprint,
        CompiledComposition compiledComposition,
        PinnedCapabilityDecision<CapabilityAuthoringAvailability> authoring,
        PinnedCapabilityDecision<CapabilityPublicationStatus> publication,
        PinnedCapabilityDecision<CapabilityEvidenceStatus> evidence,
        ResolvedMetadataPlan metadataPlan,
        ResolutionToken resolutionToken,
        CanonicalCapabilityCompilationContract? compilationContract = null,
        RuntimeReferenceCompilationProof? runtimeReferenceProof = null)
    {
        ArgumentNullException.ThrowIfNull(compiledComposition);
        CompiledComposition boundComposition = compiledComposition
            .BindCapabilityFingerprint(capabilityFingerprint);
        CanonicalCapabilityCompilationContract effectiveCompilationContract =
            compilationContract ??
            CanonicalCapabilityCompilationContract.FromCompiled(
                identity,
                boundComposition);
        CapabilityPublicationCoherence.ValidateResolved(
            identity,
            capabilityFingerprint,
            boundComposition,
            effectiveCompilationContract,
            authoring,
            publication,
            evidence,
            metadataPlan,
            resolutionToken,
            runtimeReferenceProof);
        Identity = identity;
        CapabilityFingerprint = capabilityFingerprint;
        CompiledComposition = boundComposition;
        CompilationContract = effectiveCompilationContract;
        Authoring = authoring;
        Publication = publication;
        Evidence = evidence;
        MetadataPlan = metadataPlan;
        ResolutionToken = resolutionToken;
        RuntimeReferenceProof = runtimeReferenceProof;
    }

    /// <summary>Stable exact route identity.</summary>
    public CapabilityRouteIdentity Identity { get; }

    /// <summary>Reviewed capability-definition fingerprint.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Exact compiled composition published by this capability.</summary>
    public CompiledComposition CompiledComposition { get; }

    /// <summary>Definition-level bounds which admitted this exact compilation.</summary>
    public CanonicalCapabilityCompilationContract CompilationContract { get; }

    /// <summary>Typed plan proof bound to this exact runtime-reference compilation.</summary>
    public RuntimeReferenceCompilationProof? RuntimeReferenceProof { get; }

    /// <summary>Shared UI/CLI authoring decision.</summary>
    public PinnedCapabilityDecision<CapabilityAuthoringAvailability> Authoring { get; }

    /// <summary>Independent publication decision.</summary>
    public PinnedCapabilityDecision<CapabilityPublicationStatus> Publication { get; }

    /// <summary>Independent evidence declaration.</summary>
    public PinnedCapabilityDecision<CapabilityEvidenceStatus> Evidence { get; }

    /// <summary>Canonical metadata plan bound to this publication.</summary>
    public ResolvedMetadataPlan MetadataPlan { get; }

    /// <summary>Unique token for this exact catalog publication.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Compiler-proved execution admission.</summary>
    public bool ExecutionAdmitted =>
        CapabilityPublicationCoherence.IsExecutionAdmitted(
            CompiledComposition);
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
                resolutionToken,
                definition.CompilationContract);
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
        DynamicRoutes = Array.AsReadOnly(
        [
            .. candidate.DynamicDefinitions
                .Select(definition => new ResolvedCapabilityRoute(
                    definition,
                    resolutionToken))
                .OrderBy(static route => route.Identity.RouteId, StringComparer.Ordinal),
        ]);
        _dynamicByRouteId = new ReadOnlyDictionary<string, ResolvedCapabilityRoute>(
            DynamicRoutes.ToDictionary(
                static route => route.Identity.RouteId,
                StringComparer.Ordinal));
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

    private readonly ReadOnlyDictionary<string, ResolvedCapabilityRoute> _dynamicByRouteId;

    /// <summary>Published definitions awaiting current-authoring compilation.</summary>
    public IReadOnlyList<ResolvedCapabilityRoute> DynamicRoutes { get; }

    internal bool TryGet(
        string routeId,
        out ResolvedCapability? capability)
    {
        return _byRouteId.TryGetValue(routeId, out capability);
    }

    internal bool TryGetDynamic(
        string routeId,
        out ResolvedCapabilityRoute? route)
    {
        return _dynamicByRouteId.TryGetValue(routeId, out route);
    }
}
