using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Definition-level bounds used to admit one per-authoring compilation.</summary>
public sealed record CanonicalCapabilityCompilationContract
{
    private readonly string[] _allowedMapVariantIds;
    private readonly string[] _semanticBindingIds;

    /// <summary>Creates one immutable generic compiler admission contract.</summary>
    public CanonicalCapabilityCompilationContract(
        string profileId,
        string profileVersion,
        string trustedDefinitionSha256,
        IEnumerable<string> allowedMapVariantIds,
        string compilerSemanticId,
        IEnumerable<string>? semanticBindingIds = null,
        bool allowsLogicalOutput = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentNullException.ThrowIfNull(trustedDefinitionSha256);
        ArgumentNullException.ThrowIfNull(allowedMapVariantIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(compilerSemanticId);
        if (!CapabilityRouteIdentity.IsSha256(trustedDefinitionSha256))
        {
            throw new ArgumentException(
                "Compilation contracts require an exact lowercase SHA-256 trusted definition.",
                nameof(trustedDefinitionSha256));
        }

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

        _semanticBindingIds =
        [
            .. (semanticBindingIds ?? [])
                .Select(value => string.IsNullOrWhiteSpace(value)
                    ? throw new ArgumentException(
                        "Semantic binding ids must be non-empty.",
                        nameof(semanticBindingIds))
                    : value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        bool compilerProducesLogicalOutput = StringComparer.Ordinal.Equals(
            compilerSemanticId,
            CapabilityDefinitionFingerprint.LogicalOutputCompilerSemanticId);
        if (allowsLogicalOutput != compilerProducesLogicalOutput)
        {
            throw new ArgumentException(
                "Logical-output admission must match the reviewed compiler semantic id.",
                nameof(allowsLogicalOutput));
        }

        ProfileId = profileId;
        ProfileVersion = profileVersion;
        TrustedDefinitionSha256 = trustedDefinitionSha256;
        AllowedMapVariantIds = Array.AsReadOnly(_allowedMapVariantIds);
        CompilerSemanticId = compilerSemanticId;
        SemanticBindingIds = Array.AsReadOnly(_semanticBindingIds);
        AllowsLogicalOutput = allowsLogicalOutput;
    }

    /// <summary>Exact trusted profile selected by this capability.</summary>
    public string ProfileId { get; }

    /// <summary>Exact trusted profile version selected by this capability.</summary>
    public string ProfileVersion { get; }

    /// <summary>Exact trusted bundle/definition identity admitted by this capability.</summary>
    public string TrustedDefinitionSha256 { get; }

    /// <summary>Closed physical-map set, or the logical route's generic axis.</summary>
    public IReadOnlyList<string> AllowedMapVariantIds { get; }

    /// <summary>Reviewed compiler/lowering semantics admitted by this capability.</summary>
    public string CompilerSemanticId { get; }

    /// <summary>Closed definition-level bindings that constrain compilation.</summary>
    public IReadOnlyList<string> SemanticBindingIds { get; }

    /// <summary>Whether the contract admits the closed logical-output compiler context.</summary>
    public bool AllowsLogicalOutput { get; }

    internal void ValidateCompilation(
        CapabilityRouteIdentity identity,
        CompiledComposition composition,
        MetadataPlanDefinition metadataPlan,
        RuntimeReferenceCompilationProof? runtimeReferenceProof)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(metadataPlan);
        if (!StringComparer.Ordinal.Equals(identity.IcId, composition.V2Details.Provenance.Context.MemberId) ||
            !StringComparer.Ordinal.Equals(identity.WorkflowId, composition.V2Details.ExperienceId) ||
            !StringComparer.Ordinal.Equals(ProfileId, composition.V2Details.ProfileId) ||
            !StringComparer.Ordinal.Equals(ProfileVersion, composition.V2Details.ProfileVersion))
        {
            throw new ArgumentException(
                "Compiled composition identity does not match its canonical capability definition.",
                nameof(composition));
        }

        V2CompilationProvenance provenance = composition.V2Details.Provenance;
        if (!StringComparer.Ordinal.Equals(
                TrustedDefinitionSha256,
                provenance.Bundle.ContentHash))
        {
            throw new ArgumentException(
                "Compiled composition trusted definition does not match its canonical capability definition.",
                nameof(composition));
        }

        V2CompilationContext context = provenance.Context;
        if (context is MapBoundV2CompilationContext mapContext)
        {
            string mapId = mapContext.ResolvedMap.ImageMap.MapId;
            if (!_allowedMapVariantIds.Contains(mapId, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "Compiled composition selected a map outside its canonical capability definition.",
                    nameof(composition));
            }

            string expectedSemantic = context is RuntimeReferenceReplaceV2CompilationContext
                ? CapabilityDefinitionFingerprint.RuntimeReferenceReplaceCompilerSemanticId
                : CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId;
            if (!StringComparer.Ordinal.Equals(
                    CompilerSemanticId,
                    expectedSemantic))
            {
                throw new ArgumentException(
                    "Compiled composition context does not match its reviewed compiler semantics.",
                    nameof(composition));
            }

            ValidateSemanticBindings(
                expectedSemantic ==
                    CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId
                    ? GetSelectionGroupBindings(composition)
                    : GetRuntimeReferenceBindings(
                        composition,
                        metadataPlan,
                        runtimeReferenceProof),
                composition);
            return;
        }

        if (runtimeReferenceProof is not null)
        {
            throw new ArgumentException(
                "Only runtime-reference compilation can retain a runtime-reference proof.",
                nameof(runtimeReferenceProof));
        }

        if (context is not LogicalOutputV2CompilationContext logicalContext ||
            !AllowsLogicalOutput ||
            !StringComparer.Ordinal.Equals(
                CompilerSemanticId,
                CapabilityDefinitionFingerprint.LogicalOutputCompilerSemanticId) ||
            !_allowedMapVariantIds.Contains("generic", StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Compiled composition context is outside its canonical capability definition.",
                nameof(composition));
        }

        ValidateSemanticBindings(
            [$"family:{logicalContext.FamilyId}"],
            composition);
    }

    private static IEnumerable<string> GetSelectionGroupBindings(
        CompiledComposition composition)
    {
        return composition.V2Details.InputContract.SelectionGroups
            .SelectMany(static group => group.MemberSlotIds);
    }

    private static IEnumerable<string> GetRuntimeReferenceBindings(
        CompiledComposition composition,
        MetadataPlanDefinition metadataPlan,
        RuntimeReferenceCompilationProof? runtimeReferenceProof)
    {
        string[] processorBindings =
        [
            .. composition.Plan.OrderedOperations
                .Where(static operation =>
                    operation.Kind == CompositionOperationKind.RunExternalProcessor)
                .Select(static operation =>
                    $"postbuild-processor:{operation.ExternalProcessorInvocation!.ProcessorId}"),
        ];
        string[] reportMetadataBindings =
        [
            .. metadataPlan.ReportProjections.Select(static projection =>
                $"report-metadata-slot:{projection.SpaceId}<-{projection.SlotId}"),
            .. metadataPlan.Entries
                .Where(static entry => entry.Purposes.Contains(
                    MetadataReferencePurpose.ReportClassification))
                .Select(static entry =>
                    $"report-metadata-map:{entry.ResolvedMap.ImageMap.MapId}")
                .Distinct(StringComparer.Ordinal),
        ];
        MetadataPlanSourceIdentity? sourceIdentity =
            reportMetadataBindings.Length == 0
                ? null
                : metadataPlan.SourceIdentity;
        return
        [
            .. processorBindings,
            .. reportMetadataBindings,
            .. sourceIdentity is null
                ? []
                : new[]
                {
                    $"report-metadata-profile:{sourceIdentity.ProfileId}@{sourceIdentity.ProfileVersion}",
                    $"report-metadata-bundle:{sourceIdentity.TrustedDefinitionSha256}",
                },
            .. runtimeReferenceProof?.ValidateAndGetSemanticBindings(
                composition) ?? [],
        ];
    }

    private void ValidateSemanticBindings(
        IEnumerable<string> actualBindings,
        CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(actualBindings);
        string[] normalized =
        [
            .. actualBindings
                .Select(value => string.IsNullOrWhiteSpace(value)
                    ? throw new ArgumentException(
                        "Compilation semantic bindings must be non-empty.",
                        nameof(composition))
                    : value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        if (!normalized.SequenceEqual(_semanticBindingIds, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Compiled semantic bindings do not match their reviewed capability definition " +
                $"(expected [{string.Join(", ", _semanticBindingIds)}], " +
                $"actual [{string.Join(", ", normalized)}]).",
                nameof(composition));
        }
    }

    internal static CanonicalCapabilityCompilationContract FromCompiled(
        CapabilityRouteIdentity identity,
        CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(composition);
        string mapId = composition.V2Details.Provenance.Context is
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
            composition.V2Details.ProfileId,
            composition.V2Details.ProfileVersion,
            composition.V2Details.Provenance.Bundle.ContentHash,
            [validatedMapId],
            composition.V2Details.Provenance.Context switch
            {
                RuntimeReferenceReplaceV2CompilationContext =>
                    CapabilityDefinitionFingerprint.RuntimeReferenceReplaceCompilerSemanticId,
                LogicalOutputV2CompilationContext =>
                    CapabilityDefinitionFingerprint.LogicalOutputCompilerSemanticId,
                _ => CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId,
            },
            composition.V2Details.InputContract.SelectionGroups
                .SelectMany(static group => group.MemberSlotIds),
            allowsLogicalOutput:
                composition.V2Details.Provenance.Context is
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
        PinnedCapabilityDecision<CapabilityEvidenceStatus> evidence,
        CapabilityNumberChoice? numberChoice = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(compilationContract);
        ValidateFingerprint(capabilityFingerprint);
        string expectedFingerprint = CapabilityDefinitionFingerprint.Compute(
            identity,
            compilationContract.ProfileId,
            compilationContract.ProfileVersion,
            compilationContract.TrustedDefinitionSha256,
            compilationContract.AllowedMapVariantIds,
            compilationContract.CompilerSemanticId,
            compilationContract.SemanticBindingIds);
        if (!StringComparer.Ordinal.Equals(
                capabilityFingerprint,
                expectedFingerprint))
        {
            throw new ArgumentException(
                "Dynamic capability fingerprint does not match its compilation contract.",
                nameof(capabilityFingerprint));
        }

        ValidateDecision(identity, capabilityFingerprint, authoring);
        ValidateDecision(identity, capabilityFingerprint, publication);
        ValidateDecision(identity, capabilityFingerprint, evidence);
        bool requiresNumberChoice = StringComparer.Ordinal.Equals(
            identity.WorkflowId,
            ExperienceIds.GeneralReplace);
        if (requiresNumberChoice != (numberChoice is not null))
        {
            throw new ArgumentException(
                "Only General Replace dynamic routes require one typed IC-number choice.",
                nameof(numberChoice));
        }
        if (numberChoice is not null &&
            (string.IsNullOrWhiteSpace(numberChoice.Token) ||
             string.IsNullOrWhiteSpace(numberChoice.DisplayLabel)))
        {
            throw new ArgumentException(
                "A dynamic route IC-number choice requires a token and display label.",
                nameof(numberChoice));
        }
        Identity = identity;
        CapabilityFingerprint = capabilityFingerprint;
        CompilationContract = compilationContract;
        Authoring = authoring;
        Publication = publication;
        Evidence = evidence;
        NumberChoice = numberChoice;
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

    /// <summary>Typed workflow-scoped count choice, when the route requires one.</summary>
    public CapabilityNumberChoice? NumberChoice { get; }

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
        NumberChoice = definition.NumberChoice;
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

    /// <summary>Typed workflow-scoped count choice, when the route requires one.</summary>
    public CapabilityNumberChoice? NumberChoice { get; }

    /// <summary>Publication identity shared by the resulting capability.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Binds one compiler-produced artifact to this definition and publication.</summary>
    public ResolvedCapability BindCompilation(
        CompiledComposition composition,
        MetadataPlanDefinition? metadataPlan = null,
        RuntimeReferenceCompilationProof? runtimeReferenceProof = null)
    {
        ArgumentNullException.ThrowIfNull(composition);
        MetadataPlanDefinition resolvedMetadataPlan =
            metadataPlan ?? MetadataPlanDefinition.Empty;
        CompiledComposition bound = composition.BindCapabilityFingerprint(
            CapabilityFingerprint);
        WorkflowIcNumberChoiceProjection.ValidateCompilation(NumberChoice, bound);
        RuntimeReferenceCompilationProof? boundRuntimeReferenceProof =
            runtimeReferenceProof?.BindCapabilityCompilation(
                composition,
                bound);
        CompilationContract.ValidateCompilation(
            Identity,
            bound,
            resolvedMetadataPlan,
            boundRuntimeReferenceProof);
        return new ResolvedCapability(
            Identity,
            CapabilityFingerprint,
            bound,
            Authoring,
            Publication,
            Evidence,
            resolvedMetadataPlan.Resolve(ResolutionToken),
            ResolutionToken,
            CompilationContract,
            boundRuntimeReferenceProof);
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
