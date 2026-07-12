namespace NvtFwCombiner.Domain.Composition;

/// <summary>Atomic compiler output containing one executable plan and its run identity.</summary>
public sealed partial class CompiledComposition
{
    private CompiledComposition(
        CompositionPlan plan,
        LegacyCompiledCompositionIdentity identity,
        string defaultOutputFileName,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(identity);
        ValidateIcNumberPolicy(identity.CompositionKind, icNumberPolicy);
        ValidateDefaultOutputFileName(defaultOutputFileName);

        Plan = plan;
        ProfileId = identity.ProfileId;
        ProfileVersion = identity.ProfileVersion;
        IcId = identity.IcId;
        ModeId = identity.ModeId;
        ExperienceId = identity.ExperienceId;
        CompositionKind = identity.CompositionKind;
        DefaultOutputFileName = defaultOutputFileName;
        IcNumberPolicy = icNumberPolicy;
        Eligibility = CompiledCompositionEligibility.LegacyRuntimeExecutable;
        Authority = new LegacyProfileCompilationAuthority();
        V2Details = null;
        CompilationFingerprint = CalculateCompilationFingerprint(this);
    }

    private CompiledComposition(
        CompositionPlan plan,
        V2CompiledCompositionIdentity identity,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(identity);
        if (identity.Details.Provenance.Promotion.Stage < CompiledProfilePromotionStage.Compilable)
        {
            throw new ArgumentException(
                "Only compilable v2 profiles may produce a complete composition plan.",
                nameof(identity));
        }

        ValidateIcNumberPolicy(identity.CompositionKind, icNumberPolicy);
        ValidateDefaultOutputFileName(identity.Details.OutputNamingRequirement.FileNameTemplate);
        ValidateV2InputRequirements(plan, identity.Details);

        Plan = plan;
        ProfileId = identity.ProfileId;
        ProfileVersion = identity.ProfileVersion;
        IcId = identity.Details.Provenance.ResolvedMap.MemberId;
        ModeId = identity.Details.Provenance.ResolvedMap.ModeId;
        ExperienceId = identity.ExperienceId;
        CompositionKind = identity.CompositionKind;
        DefaultOutputFileName = identity.Details.OutputNamingRequirement.FileNameTemplate;
        IcNumberPolicy = icNumberPolicy;
        Eligibility = CompiledCompositionEligibility.V2PlanCompiled;
        Authority = new ProfileBundleV2CompilationAuthority();
        V2Details = identity.Details;
        CompilationFingerprint = CalculateCompilationFingerprint(this);
    }

    /// <summary>The sole validated byte-execution plan.</summary>
    public CompositionPlan Plan { get; }

    /// <summary>Stable profile id.</summary>
    public string ProfileId { get; }

    /// <summary>Profile content version.</summary>
    public string ProfileVersion { get; }

    /// <summary>IC id declared by the profile.</summary>
    public string IcId { get; }

    /// <summary>Mode id declared by the profile.</summary>
    public string ModeId { get; }

    /// <summary>Experience id declared by the profile.</summary>
    public string ExperienceId { get; }

    /// <summary>Merge or Replace composition kind.</summary>
    public CompositionKind CompositionKind { get; }

    /// <summary>Profile-rendered default output file name.</summary>
    public string DefaultOutputFileName { get; }

    /// <summary>Compiled IC-number input policy.</summary>
    public CompiledIcNumberPolicy IcNumberPolicy { get; }

    /// <summary>Execution eligibility established by the compiler authority.</summary>
    public CompiledCompositionEligibility Eligibility { get; }

    /// <summary>Authority that established this artifact.</summary>
    public CompositionCompilationAuthority Authority { get; }

    /// <summary>Paired profile-bundle-v2 provenance and output requirements; null only for legacy artifacts.</summary>
    public V2CompiledCompositionDetails? V2Details { get; }

    /// <summary>Canonical lowercase SHA-256 over the complete compiled policy and plan.</summary>
    public string CompilationFingerprint { get; }

    /// <summary>Creates an artifact from the existing typed profile compiler without bundle or map claims.</summary>
    internal static CompiledComposition CreateLegacy(
        CompositionPlan plan,
        LegacyCompiledCompositionIdentity identity,
        string defaultOutputFileName,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        return new CompiledComposition(plan, identity, defaultOutputFileName, icNumberPolicy);
    }

    /// <summary>Creates a complete but non-executable profile-bundle-v2 plan artifact.</summary>
    internal static CompiledComposition CreateV2(
        CompositionPlan plan,
        V2CompiledCompositionIdentity identity,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        return new CompiledComposition(plan, identity, icNumberPolicy);
    }

    private static void ValidateIcNumberPolicy(
        CompositionKind compositionKind,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        if (!Enum.IsDefined(icNumberPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(icNumberPolicy),
                icNumberPolicy,
                "Unknown compiled IC-number policy.");
        }

        if (compositionKind == CompositionKind.Merge && icNumberPolicy != CompiledIcNumberPolicy.NotApplicable)
        {
            throw new ArgumentException("Merge compositions cannot accept IC-number input.", nameof(icNumberPolicy));
        }

        if (compositionKind == CompositionKind.Replace && icNumberPolicy == CompiledIcNumberPolicy.NotApplicable)
        {
            throw new ArgumentException("Replace compositions require an IC-number input policy.", nameof(icNumberPolicy));
        }
    }

    private static void ValidateDefaultOutputFileName(string defaultOutputFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultOutputFileName);
        if (defaultOutputFileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            defaultOutputFileName is "." or ".." ||
            defaultOutputFileName.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Default output file name must be a plain filename without path or control syntax.",
                nameof(defaultOutputFileName));
        }
    }

    private static void ValidateV2InputRequirements(
        CompositionPlan plan,
        V2CompiledCompositionDetails details)
    {
        var addressSpaces = plan.AddressSpaces.ToDictionary(
            static space => space.AddressSpaceId,
            StringComparer.Ordinal);
        string[] immutableAddressSpaceIds =
        [
            .. plan.AddressSpaces
                .Where(static space => space.Mutability == AddressSpaceMutability.Immutable)
                .Select(static space => space.AddressSpaceId)
                .Order(StringComparer.Ordinal),
        ];
        var declaredAddressSpaceIds = new HashSet<string>(StringComparer.Ordinal);
        var slots = details.InputContract.Slots.ToDictionary(
            static requirement => requirement.SlotId,
            StringComparer.Ordinal);
        foreach (CompiledInputSpaceBinding binding in details.InputContract.SpaceBindings)
        {
            CompiledInputSlotRequirement requirement = slots[binding.SlotId];
            string addressSpaceId = binding.AddressSpaceId;
            if (binding.InstancePolicy != CompiledInputInstancePolicy.Singleton ||
                !requirement.Required ||
                requirement.Cardinality != CompiledInputSlotCardinality.ExactlyOne ||
                requirement.LengthRequirement is not CompiledExactResolvedMapCapacityInputLengthRequirement ||
                requirement.Normalization is not CompiledNoInputNormalization)
            {
                throw new ArgumentException(
                    "Current V2 plan artifacts require singleton required exact-map-capacity inputs without normalization.",
                    nameof(details));
            }

            if (!addressSpaces.TryGetValue(addressSpaceId, out AddressSpace? addressSpace) ||
                addressSpace.Mutability != AddressSpaceMutability.Immutable ||
                !declaredAddressSpaceIds.Add(addressSpaceId))
            {
                throw new ArgumentException(
                    "Every compiled input address space must exist once and be immutable.",
                    nameof(details));
            }

            var exact = (CompiledExactResolvedMapCapacityInputLengthRequirement)requirement.LengthRequirement;
            if (exact.Bytes != details.Provenance.ResolvedMap.CapacityBytes ||
                addressSpace.Length != exact.Bytes ||
                !addressSpace.AllowedInputLengths.SequenceEqual([exact.Bytes]))
            {
                throw new ArgumentException(
                    "Exact resolved-map-capacity input requirements must agree with their immutable plan spaces.",
                    nameof(details));
            }
        }

        if (!immutableAddressSpaceIds.SequenceEqual(declaredAddressSpaceIds.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Every immutable plan address space must belong to exactly one compiled input slot.",
                nameof(details));
        }
    }
}
