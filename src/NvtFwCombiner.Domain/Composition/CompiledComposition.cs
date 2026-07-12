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
        CompiledIcNumberPolicy icNumberPolicy,
        CompiledCompositionEligibility eligibility)
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
        ValidateV2Eligibility(identity.Details, eligibility);

        Plan = plan;
        ProfileId = identity.ProfileId;
        ProfileVersion = identity.ProfileVersion;
        IcId = identity.Details.Provenance.ResolvedMap.MemberId;
        ModeId = identity.Details.Provenance.ResolvedMap.ModeId;
        ExperienceId = identity.ExperienceId;
        CompositionKind = identity.CompositionKind;
        DefaultOutputFileName = identity.Details.OutputNamingRequirement.FileNameTemplate;
        IcNumberPolicy = icNumberPolicy;
        Eligibility = eligibility;
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
        return new CompiledComposition(plan, identity, icNumberPolicy, CompiledCompositionEligibility.V2PlanCompiled);
    }

    /// <summary>Creates a trusted profile-bundle-v2 artifact admitted to the current closed runtime subset.</summary>
    internal static CompiledComposition CreateV2RuntimeExecutable(
        CompositionPlan plan,
        V2CompiledCompositionIdentity identity,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        return new CompiledComposition(plan, identity, icNumberPolicy, CompiledCompositionEligibility.V2RuntimeExecutable);
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
        var tpInputSpaces = new List<AddressSpace>();
        var normalDpInputRequirements = new List<(AddressSpace AddressSpace, CompiledNormalDpExtractWithWarningInputLengthRequirement Requirement)>();
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
                requirement.Normalization is not CompiledNoInputNormalization)
            {
                throw new ArgumentException("Current V2 plan artifacts require singleton required unnormalized inputs.", nameof(details));
            }

            if (!addressSpaces.TryGetValue(addressSpaceId, out AddressSpace? addressSpace) ||
                addressSpace.Mutability != AddressSpaceMutability.Immutable ||
                !declaredAddressSpaceIds.Add(addressSpaceId))
            {
                throw new ArgumentException(
                    "Every compiled input address space must exist once and be immutable.",
                    nameof(details));
            }

            switch (requirement.LengthRequirement)
            {
                case CompiledExactResolvedMapCapacityInputLengthRequirement exact:
                    if (exact.Bytes != details.Provenance.ResolvedMap.CapacityBytes ||
                        addressSpace.Length != exact.Bytes ||
                        !addressSpace.AllowedInputLengths.SequenceEqual([exact.Bytes]))
                    {
                        throw new ArgumentException(
                            "Exact resolved-map-capacity input requirements must agree with their immutable plan spaces.",
                            nameof(details));
                    }

                    break;
                case CompiledTpMaximum256KInputLengthRequirement:
                    if (addressSpace.Length > CompiledTpMaximum256KInputLengthRequirement.MaximumBytes ||
                        !addressSpace.AllowedInputLengths.SequenceEqual([addressSpace.Length]))
                    {
                        throw new ArgumentException(
                            "TP maximum input requirements must bind one exact plan source span within the 256 KiB limit.",
                            nameof(details));
                    }

                    tpInputSpaces.Add(addressSpace);
                    break;
                case CompiledNormalDpExtractWithWarningInputLengthRequirement normalDp:
                    normalDpInputRequirements.Add((addressSpace, normalDp));
                    break;
                default:
                    throw new ArgumentException(
                        "Current V2 plan artifacts support only exact-map-capacity, normal-DP extraction, or TP-maximum input requirements.",
                        nameof(details));
            }
        }

        if (!immutableAddressSpaceIds.SequenceEqual(declaredAddressSpaceIds.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Every immutable plan address space must belong to exactly one compiled input slot.",
                nameof(details));
        }

        foreach (CompiledResolvedPhysicalView view in details.RegionAccessContract.ResolvedViews)
        {
            if (!addressSpaces.TryGetValue(view.AddressSpaceId, out AddressSpace? addressSpace) ||
                !addressSpace.Contains(view.Range))
            {
                throw new ArgumentException(
                    "Every resolved physical view must name an existing plan address space and remain within its bounds.",
                    nameof(details));
            }
        }

        foreach (AddressSpace tpInputSpace in tpInputSpaces)
        {
            ValidateTpMaximumInputGeometry(tpInputSpace, details.RegionAccessContract.ResolvedViews);
        }

        foreach ((AddressSpace addressSpace, CompiledNormalDpExtractWithWarningInputLengthRequirement requirement) in normalDpInputRequirements)
        {
            ValidateNormalDpExtractionInputGeometry(
                addressSpace,
                requirement,
                details.RegionAccessContract.ResolvedViews);
        }
    }

    private static void ValidateV2Eligibility(
        V2CompiledCompositionDetails details,
        CompiledCompositionEligibility eligibility)
    {
        if (eligibility == CompiledCompositionEligibility.V2PlanCompiled)
        {
            return;
        }

        if (eligibility != CompiledCompositionEligibility.V2RuntimeExecutable)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eligibility),
                eligibility,
                "Unknown profile-bundle-v2 composition eligibility.");
        }

        if (details.Provenance.Promotion.Stage != CompiledProfilePromotionStage.Supported ||
            details.Provenance.Promotion.Blockers.Count != 0 ||
            details.OutputNamingRequirement.RequiredTokenIds.Count != 0 ||
            details.OutputNamingRequirement.InvalidCharacterPolicy != CompiledOutputInvalidCharacterPolicy.Reject)
        {
            throw new ArgumentException(
                "V2 runtime execution requires a supported, unblocked profile with a token-free reject output template.",
                nameof(details));
        }
    }

    private static void ValidateTpMaximumInputGeometry(
        AddressSpace addressSpace,
        IReadOnlyList<CompiledResolvedPhysicalView> resolvedViews)
    {
        long maximumEndExclusive = 0;
        bool hasSourceView = false;
        foreach (CompiledResolvedPhysicalView view in resolvedViews.Where(view =>
                     StringComparer.Ordinal.Equals(view.AddressSpaceId, addressSpace.AddressSpaceId)))
        {
            maximumEndExclusive = Math.Max(maximumEndExclusive, view.Range.EndExclusive);
            hasSourceView = true;
        }

        if (!hasSourceView ||
            maximumEndExclusive > CompiledTpMaximum256KInputLengthRequirement.MaximumBytes ||
            addressSpace.Length != maximumEndExclusive ||
            !addressSpace.AllowedInputLengths.SequenceEqual([maximumEndExclusive]))
        {
            throw new ArgumentException(
                "TP maximum input requirements must bind exactly the maximum end-exclusive span of their resolved source views.",
                nameof(addressSpace));
        }
    }

    private static void ValidateNormalDpExtractionInputGeometry(
        AddressSpace addressSpace,
        CompiledNormalDpExtractWithWarningInputLengthRequirement requirement,
        IReadOnlyList<CompiledResolvedPhysicalView> resolvedViews)
    {
        long maximumEndExclusive = 0;
        bool hasSourceView = false;
        foreach (CompiledResolvedPhysicalView view in resolvedViews.Where(view =>
                     StringComparer.Ordinal.Equals(view.AddressSpaceId, addressSpace.AddressSpaceId)))
        {
            maximumEndExclusive = Math.Max(maximumEndExclusive, view.Range.EndExclusive);
            hasSourceView = true;
        }

        if (!hasSourceView ||
            addressSpace.Length != maximumEndExclusive ||
            addressSpace.InputPaddingByte is not null ||
            addressSpace.InputOversizePolicy != InputOversizePolicy.ExtractDeclaredRange ||
            addressSpace.AllowedInputLengths.Count != 0 ||
            !addressSpace.ExpectedInputLengths.SequenceEqual(requirement.ExpectedInputLengths) ||
            requirement.ExpectedInputLengths.Any(length => length < maximumEndExclusive) ||
            !StringComparer.Ordinal.Equals(addressSpace.UnexpectedInputLengthIssueCode, requirement.IssueCode))
        {
            throw new ArgumentException(
                "Normal DP extraction requirements must bind the declared source span, expected container lengths, extraction policy, and warning code.",
                nameof(addressSpace));
        }
    }
}
