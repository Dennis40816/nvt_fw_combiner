using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Definition-level bounds used to admit one per-authoring compilation.</summary>
public sealed record CanonicalCapabilityCompilationContract
{
    private readonly string[] _allowedMapVariantIds;

    /// <summary>Creates one immutable generic compiler admission contract.</summary>
    public CanonicalCapabilityCompilationContract(
        string profileId,
        string profileVersion,
        IEnumerable<string> allowedMapVariantIds,
        bool allowsLogicalOutput = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentNullException.ThrowIfNull(allowedMapVariantIds);
        _allowedMapVariantIds =
        [
            .. allowedMapVariantIds
                .Select(value => string.IsNullOrWhiteSpace(value)
                    ? throw new ArgumentException(
                        "Allowed map variants must be non-empty.",
                        nameof(allowedMapVariantIds))
                    : value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        if (_allowedMapVariantIds.Length == 0)
        {
            throw new ArgumentException(
                "A compilation contract requires at least one map variant.",
                nameof(allowedMapVariantIds));
        }

        ProfileId = profileId;
        ProfileVersion = profileVersion;
        AllowedMapVariantIds = Array.AsReadOnly(_allowedMapVariantIds);
        AllowsLogicalOutput = allowsLogicalOutput;
    }

    /// <summary>Exact trusted profile selected by this capability.</summary>
    public string ProfileId { get; }

    /// <summary>Exact trusted profile version selected by this capability.</summary>
    public string ProfileVersion { get; }

    /// <summary>Closed physical-map set, or the logical route's generic axis.</summary>
    public IReadOnlyList<string> AllowedMapVariantIds { get; }

    /// <summary>Whether the contract admits the closed logical-output compiler context.</summary>
    public bool AllowsLogicalOutput { get; }

    internal void ValidateCompilation(
        CapabilityRouteIdentity identity,
        CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(composition);
        if (!StringComparer.Ordinal.Equals(identity.IcId, composition.IcId) ||
            !StringComparer.Ordinal.Equals(identity.WorkflowId, composition.ExperienceId) ||
            !StringComparer.Ordinal.Equals(ProfileId, composition.ProfileId) ||
            !StringComparer.Ordinal.Equals(ProfileVersion, composition.ProfileVersion))
        {
            throw new ArgumentException(
                "Compiled composition identity does not match its canonical capability definition.",
                nameof(composition));
        }

        if (composition.Authority is LegacyProfileCompilationAuthority)
        {
            _ = _allowedMapVariantIds.Contains(
                    identity.MapVariant,
                    StringComparer.Ordinal)
                ? true
                : throw new ArgumentException(
                    "Legacy compiled composition selected a route map outside its canonical capability definition.",
                    nameof(composition));
            return;
        }

        V2CompilationContext? context = composition.V2Details?.Provenance.Context;
        if (context is MapBoundV2CompilationContext mapContext)
        {
            string mapId = mapContext.ResolvedMap.ImageMap.MapId;
            if (!_allowedMapVariantIds.Contains(mapId, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "Compiled composition selected a map outside its canonical capability definition.",
                    nameof(composition));
            }

            return;
        }

        if (context is not LogicalOutputV2CompilationContext ||
            !AllowsLogicalOutput ||
            !_allowedMapVariantIds.Contains("generic", StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Compiled composition context is outside its canonical capability definition.",
                nameof(composition));
        }
    }

    internal static CanonicalCapabilityCompilationContract FromCompiled(
        CapabilityRouteIdentity identity,
        CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(composition);
        string mapId = composition.V2Details?.Provenance.Context is
            MapBoundV2CompilationContext mapContext
                ? mapContext.ResolvedMap.ImageMap.MapId
                : identity.MapVariant;
        string validatedMapId = StringComparer.Ordinal.Equals(identity.MapVariant, mapId)
            ? mapId
            : throw new ArgumentException(
                "A fixed compiled capability route must name its exact resolved map. " +
                "Variant-set routes require an explicit compilation contract.",
                nameof(identity));

        return new CanonicalCapabilityCompilationContract(
            composition.ProfileId,
            composition.ProfileVersion,
            [validatedMapId],
            allowsLogicalOutput:
                composition.V2Details?.Provenance.Context is
                    LogicalOutputV2CompilationContext);
    }
}

/// <summary>Policy-bound capability whose exact composition is compiled from current authoring state.</summary>
public sealed record CanonicalDynamicCapabilityDefinition
{
    /// <summary>Creates one reviewed dynamic capability definition.</summary>
    public CanonicalDynamicCapabilityDefinition(
        CapabilityRouteIdentity identity,
        string capabilityFingerprint,
        CanonicalCapabilityCompilationContract compilationContract,
        PinnedCapabilityDecision<CapabilityAuthoringAvailability> authoring,
        PinnedCapabilityDecision<CapabilityPublicationStatus> publication,
        PinnedCapabilityDecision<CapabilityEvidenceStatus> evidence)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(compilationContract);
        ValidateFingerprint(capabilityFingerprint);
        ValidateDecision(identity, capabilityFingerprint, authoring);
        ValidateDecision(identity, capabilityFingerprint, publication);
        ValidateDecision(identity, capabilityFingerprint, evidence);
        Identity = identity;
        CapabilityFingerprint = capabilityFingerprint;
        CompilationContract = compilationContract;
        Authoring = authoring;
        Publication = publication;
        Evidence = evidence;
    }

    /// <summary>Stable exact route identity.</summary>
    public CapabilityRouteIdentity Identity { get; }

    /// <summary>Reviewed complete capability-definition identity.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Bounds for one exact per-authoring compilation.</summary>
    public CanonicalCapabilityCompilationContract CompilationContract { get; }

    /// <summary>Shared authoring decision.</summary>
    public PinnedCapabilityDecision<CapabilityAuthoringAvailability> Authoring { get; }

    /// <summary>Independent publication decision.</summary>
    public PinnedCapabilityDecision<CapabilityPublicationStatus> Publication { get; }

    /// <summary>Independent evidence decision.</summary>
    public PinnedCapabilityDecision<CapabilityEvidenceStatus> Evidence { get; }

    private static void ValidateFingerprint(string fingerprint)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        if (!CapabilityRouteIdentity.IsSha256(fingerprint))
        {
            throw new ArgumentException(
                "Dynamic capabilities require a lowercase SHA-256 definition fingerprint.",
                nameof(fingerprint));
        }
    }

    private static void ValidateDecision<TValue>(
        CapabilityRouteIdentity identity,
        string fingerprint,
        PinnedCapabilityDecision<TValue> decision)
        where TValue : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (!StringComparer.Ordinal.Equals(identity.RouteId, decision.RouteId) ||
            !StringComparer.Ordinal.Equals(fingerprint, decision.CapabilityFingerprint))
        {
            throw new ArgumentException(
                "Dynamic capability decisions must pin the current route and definition fingerprint.",
                nameof(decision));
        }
    }
}

/// <summary>Published definition snapshot awaiting one exact compilation.</summary>
public sealed record ResolvedCapabilityRoute
{
    internal ResolvedCapabilityRoute(
        CanonicalDynamicCapabilityDefinition definition,
        ResolutionToken resolutionToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        resolutionToken.EnsureValid(nameof(resolutionToken));
        Identity = definition.Identity;
        CapabilityFingerprint = definition.CapabilityFingerprint;
        CompilationContract = definition.CompilationContract;
        Authoring = definition.Authoring;
        Publication = definition.Publication;
        Evidence = definition.Evidence;
        ResolutionToken = resolutionToken;
    }

    /// <summary>Stable exact route identity.</summary>
    public CapabilityRouteIdentity Identity { get; }

    /// <summary>Reviewed complete capability-definition identity.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Bounds for the one accepted compilation.</summary>
    public CanonicalCapabilityCompilationContract CompilationContract { get; }

    /// <summary>Shared authoring decision.</summary>
    public PinnedCapabilityDecision<CapabilityAuthoringAvailability> Authoring { get; }

    /// <summary>Independent publication decision.</summary>
    public PinnedCapabilityDecision<CapabilityPublicationStatus> Publication { get; }

    /// <summary>Independent evidence decision.</summary>
    public PinnedCapabilityDecision<CapabilityEvidenceStatus> Evidence { get; }

    /// <summary>Publication identity shared by the resulting capability.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Binds one compiler-produced artifact to this definition and publication.</summary>
    public ResolvedCapability BindCompilation(
        CompiledComposition composition,
        MetadataPlanDefinition? metadataPlan = null)
    {
        ArgumentNullException.ThrowIfNull(composition);
        CompiledComposition bound = composition.BindCapabilityFingerprint(
            CapabilityFingerprint);
        CompilationContract.ValidateCompilation(Identity, bound);
        return new ResolvedCapability(
            Identity,
            CapabilityFingerprint,
            bound,
            Authoring,
            Publication,
            Evidence,
            (metadataPlan ?? MetadataPlanDefinition.Empty).Resolve(ResolutionToken),
            ResolutionToken,
            CompilationContract);
    }
}

/// <summary>Result of resolving a definition before dynamic compilation.</summary>
public sealed record CapabilityRouteResolutionResult(
    ResolvedCapabilityRoute? Route,
    CapabilityCatalogIssue? Issue)
{
    /// <summary>True only when the route is available for authoring.</summary>
    public bool Succeeded => Route is not null && Issue is null;
}
