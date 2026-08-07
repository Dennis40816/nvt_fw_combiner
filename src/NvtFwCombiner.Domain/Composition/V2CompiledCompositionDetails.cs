using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Immutable identity of one bundle root already verified outside the Domain boundary.</summary>
public sealed class ProfileBundleIdentity
{
    internal ProfileBundleIdentity(
        string bundleId,
        string bundleVersion,
        string contentHash,
        string trustAnchorBindingId)
    {
        BundleId = RequiredValue.NotBlank(bundleId);
        BundleVersion = RequiredValue.NotBlank(bundleVersion);
        ContentHash = CanonicalSha256.Require(contentHash, nameof(contentHash));
        TrustAnchorBindingId = RequiredValue.NotBlank(trustAnchorBindingId);
    }

    /// <summary>Stable bundle identifier.</summary>
    public string BundleId { get; }

    /// <summary>Bundle semantic version declared by the trusted manifest.</summary>
    public string BundleVersion { get; }

    /// <summary>Canonical manifest-root content hash.</summary>
    public string ContentHash { get; }

    /// <summary>External release/install binding that established the bundle root.</summary>
    public string TrustAnchorBindingId { get; }

}

/// <summary>Immutable allowlisted identity of the exact composition-profile document inside one bundle.</summary>
public sealed class ProfileBundleEntryIdentity
{
    internal ProfileBundleEntryIdentity(string entryId, string contentHash)
    {
        EntryId = RequiredValue.NotBlank(entryId);
        ContentHash = CanonicalSha256.Require(contentHash, nameof(contentHash));
    }

    /// <summary>Stable composition-profile entry id from the closed bundle inventory.</summary>
    public string EntryId { get; }

    /// <summary>Canonical content hash of the exact composition-profile entry.</summary>
    public string ContentHash { get; }
}

/// <summary>Monotonic v2 profile stage retained independently from runtime execution eligibility.</summary>
public enum CompiledProfilePromotionStage
{
    /// <inheritdoc/>
    Known,
    /// <inheritdoc/>
    MapResolvable,
    /// <inheritdoc/>
    Inspectable,
    /// <inheritdoc/>
    Authorable,
    /// <inheritdoc/>
    Compilable,
    /// <inheritdoc/>
    ExecutableCandidate,
    /// <inheritdoc/>
    Supported,
}

/// <summary>Closed reason category that prevents a v2 profile promotion.</summary>
public enum CompiledProfilePromotionBlockerKind
{
    /// <inheritdoc/>
    Map,
    /// <inheritdoc/>
    Metadata,
    /// <inheritdoc/>
    Operation,
    /// <inheritdoc/>
    Processor,
    /// <inheritdoc/>
    Integrity,
    /// <inheritdoc/>
    Golden,
    /// <inheritdoc/>
    HumanReview,
    /// <inheritdoc/>
    Ui,
    /// <inheritdoc/>
    Release,
}

/// <summary>One immutable promotion blocker and its evidence references.</summary>
public sealed class CompiledProfilePromotionBlocker
{
    private readonly string[] _evidenceRefs;

    internal CompiledProfilePromotionBlocker(
        string blockerId,
        CompiledProfilePromotionBlockerKind kind,
        string reason,
        IEnumerable<string> evidenceRefs)
    {
        BlockerId = RequiredValue.NotBlank(blockerId);
        ClosedEnum.ThrowIfUndefined(kind, "Unknown profile promotion blocker kind.");

        Reason = RequiredValue.NotBlank(reason);
        _evidenceRefs = ImmutableStringSnapshot.Create(
            evidenceRefs,
            nameof(evidenceRefs),
            null,
            "Identifiers must be non-empty values.",
            "Identifiers must be ordinally unique.");
        Kind = kind;
        EvidenceRefs = Array.AsReadOnly(_evidenceRefs);
    }

    /// <summary>Stable blocker identifier.</summary>
    public string BlockerId { get; }

    /// <summary>Closed blocker category.</summary>
    public CompiledProfilePromotionBlockerKind Kind { get; }

    /// <summary>Evidence-backed blocker explanation.</summary>
    public string Reason { get; }

    /// <summary>Immutable evidence references supporting this blocker.</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }

}

/// <summary>Immutable profile-owned promotion stage and complete blocker snapshot.</summary>
public sealed class CompiledProfilePromotion
{
    private readonly CompiledProfilePromotionBlocker[] _blockers;

    internal CompiledProfilePromotion(
        CompiledProfilePromotionStage stage,
        IEnumerable<CompiledProfilePromotionBlocker> blockers)
    {
        ClosedEnum.ThrowIfUndefined(stage, "Unknown profile promotion stage.");

        _blockers = ImmutableReferenceSnapshot.CreateUnique(
            blockers,
            static blocker => blocker.BlockerId,
            "Promotion blockers must be non-null with ordinally unique ids.",
            "Promotion blockers must be non-null with ordinally unique ids.",
            StringComparer.Ordinal);

        DomainInvariant.Reject(
            stage == CompiledProfilePromotionStage.Supported && _blockers.Length != 0,
            "Supported profiles cannot retain promotion blockers.", nameof(blockers));

        Array.Sort(_blockers, static (left, right) =>
            StringComparer.Ordinal.Compare(left.BlockerId, right.BlockerId));
        Stage = stage;
        Blockers = Array.AsReadOnly(_blockers);
    }

    /// <summary>Monotonic profile-owned evidence stage.</summary>
    public CompiledProfilePromotionStage Stage { get; }

    /// <summary>Complete immutable promotion blockers.</summary>
    public IReadOnlyList<CompiledProfilePromotionBlocker> Blockers { get; }
}

/// <summary>Immutable provenance retained by one profile-bundle-v2 compiled composition.</summary>
public sealed class V2CompilationProvenance
{
    private readonly string[] _profileEvidenceRefs;
    private readonly CompiledValidationRequirement[] _validationRequirements;
    private readonly CompiledCapabilityAdmission[] _requiredCapabilities;

    /// <summary>Creates map-bound provenance through the closed resolved-map context.</summary>
    internal V2CompilationProvenance(
        ProfileBundleIdentity bundle,
        ProfileBundleEntryIdentity profileEntry,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        CompiledProfilePromotion promotion,
        IEnumerable<string> profileEvidenceRefs,
        IEnumerable<CompiledValidationRequirement> validationRequirements,
        IEnumerable<CompiledCapabilityAdmission> requiredCapabilities)
        : this(
            bundle,
            profileEntry,
            new ResolvedMapV2CompilationContext(resolvedMap),
            promotion,
            profileEvidenceRefs,
            validationRequirements,
            requiredCapabilities)
    {
    }

    internal V2CompilationProvenance(
        ProfileBundleIdentity bundle,
        ProfileBundleEntryIdentity profileEntry,
        V2CompilationContext context,
        CompiledProfilePromotion promotion,
        IEnumerable<string> profileEvidenceRefs,
        IEnumerable<CompiledValidationRequirement> validationRequirements,
        IEnumerable<CompiledCapabilityAdmission> requiredCapabilities)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(profileEntry);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(promotion);
        _profileEvidenceRefs = ImmutableStringSnapshot.Create(
            profileEvidenceRefs,
            nameof(profileEvidenceRefs),
            "Identifiers must be non-empty values.",
            "Identifiers must be non-empty values.",
            "Identifiers must be ordinally unique.");
        _validationRequirements = ImmutableReferenceSnapshot.CreateUnique(
            validationRequirements,
            static requirement => requirement.RuleId,
            "Validation requirements must be non-null with ordinally unique rule ids.",
            "Validation requirements must be non-null with ordinally unique rule ids.",
            StringComparer.Ordinal);

        Array.Sort(_validationRequirements, static (left, right) =>
        {
            int stage = left.Stage.CompareTo(right.Stage);
            return stage != 0
                ? stage
                : StringComparer.Ordinal.Compare(left.RuleId, right.RuleId);
        });

        _requiredCapabilities = ImmutableReferenceSnapshot.CreateUnique(
            requiredCapabilities,
            static capability => capability.RequiredCapabilityId,
            "Required capability admissions must be non-null with ordinally unique capability ids.",
            "Required capability admissions must be non-null with ordinally unique capability ids.",
            StringComparer.Ordinal);

        Array.Sort(_requiredCapabilities, static (left, right) => StringComparer.Ordinal.Compare(
            left.RequiredCapabilityId,
            right.RequiredCapabilityId));
        foreach (CompiledCapabilityAdmission capability in _requiredCapabilities)
        {
            FirmwareMapFactBinding<FirmwareCapabilityFact> binding = capability.Binding;
            DomainInvariant.Reject(
                context is not MapBoundV2CompilationContext mapContext ||
                !StringComparer.Ordinal.Equals(binding.EffectiveKey.MemberId, mapContext.ResolvedMap.MemberId) ||
                !StringComparer.Ordinal.Equals(binding.EffectiveKey.MapId, mapContext.ResolvedMap.ImageMap.MapId) ||
                binding.Applicability.Evaluate(mapContext.ResolvedMap) != FirmwareApplicabilityResult.Match,
                "Required capability admissions must apply to the compiled resolved map.",
                nameof(requiredCapabilities));
        }

        DomainInvariant.Reject(
            context is LogicalOutputV2CompilationContext &&
            (_validationRequirements.Length != 0 || _requiredCapabilities.Length != 0),
            "Logical-output provenance cannot claim physical validation or capability admissions.",
            nameof(context));

        Bundle = bundle;
        ProfileEntry = profileEntry;
        Context = context;
        Promotion = promotion;
        ProfileEvidenceRefs = Array.AsReadOnly(_profileEvidenceRefs);
        ValidationRequirements = Array.AsReadOnly(_validationRequirements);
        RequiredCapabilities = Array.AsReadOnly(_requiredCapabilities);
    }

    /// <summary>Trusted bundle-root identity recorded by the compiler.</summary>
    public ProfileBundleIdentity Bundle { get; }

    /// <summary>Exact allowlisted composition-profile entry identity.</summary>
    public ProfileBundleEntryIdentity ProfileEntry { get; }

    /// <summary>Closed physical-map or logical-output context established by the Profiles compiler.</summary>
    public V2CompilationContext Context { get; }

    /// <summary>Resolver-owned physical map for map-bound artifacts only.</summary>
    public FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap ResolvedMap => Context is MapBoundV2CompilationContext mapContext
        ? mapContext.ResolvedMap
        : throw new InvalidOperationException("Logical-output provenance does not contain a resolved firmware image map.");

    /// <summary>Profile-owned promotion stage and blockers.</summary>
    public CompiledProfilePromotion Promotion { get; }

    /// <summary>Profile evidence references consumed by compilation.</summary>
    public IReadOnlyList<string> ProfileEvidenceRefs { get; }

    /// <summary>Complete closed validation stages retained for future runtime admission.</summary>
    public IReadOnlyList<CompiledValidationRequirement> ValidationRequirements { get; }

    internal IReadOnlyList<CompiledCapabilityAdmission> RequiredCapabilities { get; }
}

/// <summary>Single typed v2 artifact payload that keeps provenance and unrendered naming requirements paired.</summary>
public sealed class V2CompiledCompositionDetails
{
    internal V2CompiledCompositionDetails(
        string profileId,
        string profileVersion,
        string experienceId,
        CompositionKind compositionKind,
        V2CompilationProvenance provenance,
        CompiledInputContract inputContract,
        CompiledRegionAccessContract regionAccessContract,
        CompiledOutputNamingRequirement outputNamingRequirement,
        IcNumberInputMode? icNumberInputMode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(experienceId);
        ClosedEnum.ThrowIfUndefined(compositionKind, "Unknown composition kind.");
        if (icNumberInputMode is { } inputMode)
        {
            ClosedEnum.ThrowIfUndefined(inputMode, "Unknown IC-number input mode.");
        }

        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(inputContract);
        ArgumentNullException.ThrowIfNull(regionAccessContract);
        ArgumentNullException.ThrowIfNull(outputNamingRequirement);
        if (provenance.Context is MapBoundV2CompilationContext mapContext)
        {
            ValidateRegionAccessContract(mapContext.ResolvedMap.ImageMap, regionAccessContract);
        }
        else if (regionAccessContract.Requirements.Count != 0 || regionAccessContract.ResolvedViews.Count != 0)
        {
            throw new ArgumentException(
                "Logical-output V2 details cannot retain physical region access.",
                nameof(regionAccessContract));
        }
        ProfileId = profileId;
        ProfileVersion = profileVersion;
        ExperienceId = experienceId;
        CompositionKind = compositionKind;
        Provenance = provenance;
        InputContract = inputContract;
        RegionAccessContract = regionAccessContract;
        OutputNamingRequirement = outputNamingRequirement;
        IcNumberInputMode = icNumberInputMode;
    }

    /// <summary>Stable profile id.</summary>
    public string ProfileId { get; }

    /// <summary>Profile content version.</summary>
    public string ProfileVersion { get; }

    /// <summary>Experience id declared by the profile.</summary>
    public string ExperienceId { get; }

    /// <summary>Merge or Replace composition kind.</summary>
    public CompositionKind CompositionKind { get; }

    /// <summary>Resolved bundle, map, promotion, evidence, and validation provenance.</summary>
    public V2CompilationProvenance Provenance { get; }

    /// <summary>Complete immutable profile input slot policy and its immutable plan address-space bindings.</summary>
    public CompiledInputContract InputContract { get; }

    /// <summary>Complete profile access policy plus the canonical physical constraints governing every logical view.</summary>
    public CompiledRegionAccessContract RegionAccessContract { get; }

    /// <summary>Unrendered profile-owned output naming requirement.</summary>
    public CompiledOutputNamingRequirement OutputNamingRequirement { get; }

    /// <summary>Canonical IC-number input mode, or null when it is not applicable.</summary>
    public IcNumberInputMode? IcNumberInputMode { get; }

    private static void ValidateRegionAccessContract(
        FirmwareImageMap map,
        CompiledRegionAccessContract contract)
    {
        var regionsById = map.Regions.ToDictionary(static region => region.RegionId, StringComparer.Ordinal);
        foreach (CompiledRegionAccessRequirement requirement in contract.Requirements)
        {
            ValidatePhysicalChain(requirement.GoverningRegionChain, regionsById, requirement.RegionId);
            DomainInvariant.Reject(
                requirement.Access == RegionAccessKind.Parts &&
                requirement.AllowedSubregionIds.Any(subregionId =>
                    !regionsById.TryGetValue(subregionId, out FirmwareRegion? subregion) ||
                    !StringComparer.Ordinal.Equals(subregion.ParentRegionId, requirement.RegionId)),
                "Compiled parts access may name only direct children in the selected canonical map.",
                nameof(contract));
        }

        foreach (CompiledResolvedPhysicalView view in contract.ResolvedViews)
        {
            FirmwareRegion terminal = ResolveDeepestContainingRegion(view.Range, regionsById);
            ValidatePhysicalChain(view.GoverningRegionChain, regionsById, terminal.RegionId);
        }
    }

    private static FirmwareRegion ResolveDeepestContainingRegion(
        ByteRange range,
        IReadOnlyDictionary<string, FirmwareRegion> regionsById)
    {
        return regionsById.Values
            .Where(region => region.Range.Contains(range))
            .OrderBy(static region => region.Range.Length)
            .ThenBy(static region => region.RegionId, StringComparer.Ordinal)
            .FirstOrDefault() ?? throw new ArgumentException(
                "Compiled resolved view range does not belong to the selected canonical map.",
                nameof(range));
    }

    private static void ValidatePhysicalChain(
        IReadOnlyList<FirmwareRegion> chain,
        Dictionary<string, FirmwareRegion> regionsById,
        string? expectedTerminalRegionId)
    {
        FirmwareRegion? previous = null;
        for (int index = 0; index < chain.Count; index++)
        {
            FirmwareRegion compiledRegion = chain[index];
            DomainInvariant.Reject(
                !regionsById.TryGetValue(compiledRegion.RegionId, out FirmwareRegion? canonicalRegion) ||
                !ReferenceEquals(canonicalRegion, compiledRegion) ||
                (index == 0 && canonicalRegion.ParentRegionId is not null) ||
                (previous is not null && !StringComparer.Ordinal.Equals(canonicalRegion.ParentRegionId, previous.RegionId)),
                "Compiled region access must retain an exact canonical physical ancestor chain.",
                nameof(chain));

            previous = canonicalRegion;
        }

        DomainInvariant.Reject(
            expectedTerminalRegionId is not null &&
            (previous is null || !StringComparer.Ordinal.Equals(previous.RegionId, expectedTerminalRegionId)),
            "Compiled region access must terminate at its declared canonical region.",
            nameof(expectedTerminalRegionId));
    }
}
