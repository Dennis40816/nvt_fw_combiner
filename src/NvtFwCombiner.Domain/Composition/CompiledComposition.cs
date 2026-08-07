namespace NvtFwCombiner.Domain.Composition;

/// <summary>Atomic compiler output containing one executable plan and its run identity.</summary>
public sealed partial class CompiledComposition
{
    private CompiledComposition(
        CompositionPlan plan,
        V2CompiledCompositionDetails details,
        CompiledIcNumberPolicy icNumberPolicy,
        CompiledCompositionEligibility eligibility)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(details);
        if (details.Provenance.Promotion.Stage < CompiledProfilePromotionStage.Compilable)
        {
            throw new ArgumentException(
                "Only compilable v2 profiles may produce a complete composition plan.",
                nameof(details));
        }

        ValidateIcNumberPolicy(details.CompositionKind, icNumberPolicy);
        ValidateDefaultOutputFileName(details.OutputNamingRequirement.FileNameTemplate);
        ValidateV2InputRequirements(plan, details.CompositionKind, details.ExperienceId, details);
        ValidateV2Eligibility(
            details,
            details.CompositionKind,
            eligibility);

        Plan = plan;
        IcNumberPolicy = icNumberPolicy;
        Eligibility = eligibility;
        V2Details = details;
        ValidateValidationRequirements(plan, ValidationRequirements);
        IntegrityFingerprint = CalculateIntegrityFingerprint(plan);
        CompilationFingerprint = CalculateCompilationFingerprint(this);
    }

    private CompiledComposition(
        CompiledComposition source,
        string capabilityFingerprint)
    {
        Plan = source.Plan;
        IcNumberPolicy = source.IcNumberPolicy;
        Eligibility = source.Eligibility;
        V2Details = source.V2Details;
        IntegrityFingerprint = source.IntegrityFingerprint;
        CapabilityFingerprint = capabilityFingerprint;
        CompilationFingerprint = CalculateCompilationFingerprint(this);
    }

    /// <summary>The sole validated byte-execution plan.</summary>
    public CompositionPlan Plan { get; }

    /// <summary>Stable profile id.</summary>
    public string ProfileId => V2Details.ProfileId;

    /// <summary>Profile content version.</summary>
    public string ProfileVersion => V2Details.ProfileVersion;

    /// <summary>IC id declared by the profile.</summary>
    public string IcId => V2Details.Provenance.Context.MemberId;

    /// <summary>Mode id declared by the profile.</summary>
    public string ModeId => V2Details.Provenance.Context.ModeId;

    /// <summary>Experience id declared by the profile.</summary>
    public string ExperienceId => V2Details.ExperienceId;

    /// <summary>Merge or Replace composition kind.</summary>
    public CompositionKind CompositionKind => V2Details.CompositionKind;

    /// <summary>Profile-rendered default output file name.</summary>
    public string DefaultOutputFileName => V2Details.OutputNamingRequirement.FileNameTemplate;

    /// <summary>Compiled IC-number input policy.</summary>
    public CompiledIcNumberPolicy IcNumberPolicy { get; }

    /// <summary>Execution eligibility established by the compiler authority.</summary>
    public CompiledCompositionEligibility Eligibility { get; }

    /// <summary>
    /// Whether this profile-bundle candidate is the deliberately narrow AB Code
    /// function-open route: executable product behavior with only golden or
    /// firmware-owner certification debt remaining.
    /// </summary>
    public bool IsV2AbFunctionOpenCandidate =>
        Eligibility == CompiledCompositionEligibility.V2PlanCompiled &&
        V2Details.Provenance.Promotion.Stage == CompiledProfilePromotionStage.ExecutableCandidate &&
        V2Details.Provenance.Context is ResolvedMapV2CompilationContext &&
        CompositionKind == CompositionKind.Merge &&
        V2Details.OutputNamingRequirement.RendererKind == CompiledOutputNameRendererKind.AbCodeV1 &&
        V2Details.Provenance.Promotion.Blockers.Count != 0 &&
        V2Details.Provenance.Promotion.Blockers.All(static blocker =>
            blocker.Kind is CompiledProfilePromotionBlockerKind.Golden or
                CompiledProfilePromotionBlockerKind.HumanReview);

    /// <summary>
    /// Whether this trusted V2 artifact is an AB Merge route executable by the
    /// current runtime, either as the narrow function-open candidate or as a
    /// fully supported profile.
    /// </summary>
    public bool IsV2AbMergeRuntimeRoute =>
        (Eligibility == CompiledCompositionEligibility.V2RuntimeExecutable ||
         IsV2AbFunctionOpenCandidate) &&
        V2Details.Provenance.Context is ResolvedMapV2CompilationContext &&
        CompositionKind == CompositionKind.Merge &&
        V2Details.OutputNamingRequirement.RendererKind == CompiledOutputNameRendererKind.AbCodeV1;

    /// <summary>Paired trusted profile-bundle provenance and compiled requirements.</summary>
    public V2CompiledCompositionDetails V2Details { get; }

    /// <summary>Closed validation requirements retained by the compiler authority.</summary>
    public IReadOnlyList<CompiledValidationRequirement> ValidationRequirements =>
        V2Details.Provenance.ValidationRequirements;

    /// <summary>Canonical lowercase SHA-256 over the complete compiled policy and plan.</summary>
    public string CompilationFingerprint { get; }

    /// <summary>Reviewed capability definition chained into this canonical compilation; null before catalog binding.</summary>
    public string? CapabilityFingerprint { get; }

    /// <summary>
    /// Canonical lowercase SHA-256 over profile-declared external processors and
    /// host-side scalar relocation operations; null when neither is present.
    /// </summary>
    public string? IntegrityFingerprint { get; }

    /// <summary>Returns the immutable canonical artifact whose compilation identity references one reviewed capability.</summary>
    public CompiledComposition BindCapabilityFingerprint(
        string capabilityFingerprint)
    {
        string acceptedFingerprint = CanonicalSha256.Require(
            capabilityFingerprint,
            nameof(capabilityFingerprint));

        return CapabilityFingerprint is not null
            ? StringComparer.Ordinal.Equals(
                    CapabilityFingerprint,
                    acceptedFingerprint)
                ? this
                : throw new InvalidOperationException(
                    "A compiled composition cannot be rebound to another capability definition.")
            : new CompiledComposition(this, acceptedFingerprint);
    }

    /// <summary>Creates a complete but non-executable profile-bundle-v2 plan artifact.</summary>
    internal static CompiledComposition CreateV2(
        CompositionPlan plan,
        V2CompiledCompositionDetails details,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        return new CompiledComposition(plan, details, icNumberPolicy, CompiledCompositionEligibility.V2PlanCompiled);
    }

    /// <summary>Creates a trusted profile-bundle-v2 artifact admitted to the current closed runtime subset.</summary>
    internal static CompiledComposition CreateV2RuntimeExecutable(
        CompositionPlan plan,
        V2CompiledCompositionDetails details,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        return new CompiledComposition(plan, details, icNumberPolicy, CompiledCompositionEligibility.V2RuntimeExecutable);
    }

    private static void ValidateIcNumberPolicy(
        CompositionKind compositionKind,
        CompiledIcNumberPolicy icNumberPolicy)
    {
        ClosedEnum.ThrowIfUndefined(icNumberPolicy, "Unknown compiled IC-number policy.");

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
        CompositionKind compositionKind,
        string experienceId,
        V2CompiledCompositionDetails details)
    {
        if (details.Provenance.Context is LogicalOutputV2CompilationContext)
        {
            ValidateLogicalOutputInputRequirements(plan, compositionKind, details);
            return;
        }

        if (details.Provenance.Context is RuntimeReferenceReplaceV2CompilationContext)
        {
            ValidateRuntimeReferenceReplaceInputRequirements(plan, compositionKind, experienceId, details);
            return;
        }

        if (details.InputContract.SpaceBindings.Any(static binding =>
                binding.InstancePolicy == CompiledInputInstancePolicy.PerBinding))
        {
            throw new ArgumentException(
                "Per-binding V2 inputs require the explicit runtime-reference-replace compilation context.",
                nameof(details));
        }

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
        var declaredPrefixInputRequirements = new List<(AddressSpace AddressSpace, CompiledDeclaredPrefixWithWarningInputLengthRequirement Requirement)>();
        var sourceViewInputRequirements = new List<(AddressSpace AddressSpace, CompiledSourceViewCoverageInputLengthRequirement Requirement)>();
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
                requirement.Normalization is not (
                    CompiledNoInputNormalization or
                    CompiledPadShorterInputNormalization or
                    CompiledTruncateCtrlRamInputNormalization))
            {
                throw new ArgumentException("Current V2 plan artifacts require singleton required inputs with no normalization, DP short-input padding, or CtrlRAM truncation.", nameof(details));
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
                case CompiledExactBytesInputLengthRequirement exact:
                    ValidateExactBytesInputGeometry(
                        compositionKind,
                        requirement,
                        exact,
                        addressSpace);
                    break;
                case CompiledExactResolvedMapCapacityInputLengthRequirement exact:
                    ValidateExactResolvedMapCapacityInputGeometry(
                        plan,
                        compositionKind,
                        details,
                        requirement,
                        exact,
                        addressSpace);

                    break;
                case CompiledTpMaximum256KInputLengthRequirement:
                    if (addressSpace.Length > CompiledTpMaximum256KInputLengthRequirement.MaximumBytes ||
                        addressSpace.InputPaddingByte is not null ||
                        addressSpace.InputOversizePolicy != InputOversizePolicy.ExtractDeclaredRange ||
                        addressSpace.AllowedInputLengths.Count != 0)
                    {
                        throw new ArgumentException(
                            "TP maximum input requirements must extract their declared source span from any input within the 256 KiB limit.",
                            nameof(details));
                    }

                    tpInputSpaces.Add(addressSpace);
                    break;
                case CompiledDeclaredPrefixWithWarningInputLengthRequirement declaredPrefix:
                    if (compositionKind != CompositionKind.Merge ||
                        requirement.Normalization is not CompiledNoInputNormalization)
                    {
                        throw new ArgumentException(
                            "Declared-prefix input requirements are restricted to unnormalized immutable Merge sources.",
                            nameof(details));
                    }

                    declaredPrefixInputRequirements.Add((addressSpace, declaredPrefix));
                    break;
                case CompiledSourceViewCoverageInputLengthRequirement sourceView:
                    if (requirement.Normalization is not CompiledNoInputNormalization)
                    {
                        throw new ArgumentException(
                            "Source-view coverage requires an unnormalized immutable section source.",
                            nameof(details));
                    }

                    sourceViewInputRequirements.Add((addressSpace, sourceView));
                    break;
                default:
                    throw new ArgumentException(
                        "Current V2 plan artifacts support only exact-map-capacity, source-view, declared-prefix, TP-maximum, or exact TP input requirements.",
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

        foreach ((AddressSpace addressSpace, CompiledDeclaredPrefixWithWarningInputLengthRequirement requirement) in declaredPrefixInputRequirements)
        {
            ValidateDeclaredPrefixInputGeometry(addressSpace, requirement);
        }

        foreach ((AddressSpace addressSpace, CompiledSourceViewCoverageInputLengthRequirement requirement) in sourceViewInputRequirements)
        {
            ValidateSourceViewInputGeometry(addressSpace, requirement);
        }
    }

    private static void ValidateLogicalOutputInputRequirements(
        CompositionPlan plan,
        CompositionKind compositionKind,
        V2CompiledCompositionDetails details)
    {
        if (compositionKind != CompositionKind.Merge ||
            plan.OutputInitialization.Kind != ImageInitializationKind.Blank ||
            details.RegionAccessContract.Requirements.Count != 0 ||
            details.RegionAccessContract.ResolvedViews.Count != 0)
        {
            throw new ArgumentException(
                "Logical-output V2 artifacts require a blank Merge output with no physical region access.",
                nameof(details));
        }

        if (details.InputContract.Slots.Count != 1 || details.InputContract.SpaceBindings.Count == 0)
        {
            throw new ArgumentException(
                "Logical-output V2 artifacts require one slot bound to one or more concrete immutable spaces.",
                nameof(details));
        }

        CompiledInputSlotRequirement slot = details.InputContract.Slots[0];
        if (!slot.Required ||
            slot.ArtifactClass != CompiledInputArtifactClass.Auxiliary ||
            slot.Cardinality != CompiledInputSlotCardinality.OneOrMore ||
            slot.Normalization is not CompiledNoInputNormalization ||
            slot.LengthRequirement is not CompiledBoundedInputLengthRequirement
            {
                MinimumBytes: 1,
                MaximumBytes: int.MaxValue,
            })
        {
            throw new ArgumentException(
                "Logical-output V2 artifacts require one unnormalized auxiliary one-or-more slot bounded to Int32.MaxValue.",
                nameof(details));
        }

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
        string[] bindingAddressSpaceIds =
        [
            .. details.InputContract.SpaceBindings
                .Select(static binding => binding.AddressSpaceId)
                .Order(StringComparer.Ordinal),
        ];
        if (!immutableAddressSpaceIds.SequenceEqual(bindingAddressSpaceIds, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Logical-output V2 artifacts must bind every immutable plan space exactly once.",
                nameof(details));
        }

        foreach (CompiledInputSpaceBinding binding in details.InputContract.SpaceBindings)
        {
            if (!StringComparer.Ordinal.Equals(binding.SlotId, slot.SlotId) ||
                binding.InstancePolicy != CompiledInputInstancePolicy.PerBinding ||
                !addressSpaces.TryGetValue(binding.AddressSpaceId, out AddressSpace? addressSpace) ||
                addressSpace.Mutability != AddressSpaceMutability.Immutable ||
                addressSpace.Length is < 1 or > int.MaxValue ||
                addressSpace.InputPaddingByte is not null ||
                addressSpace.InputOversizePolicy != InputOversizePolicy.Reject ||
                addressSpace.AllowedInputLengths.Count != 0 ||
                addressSpace.ExpectedInputLengths.Count != 0)
            {
                throw new ArgumentException(
                    "Logical-output V2 bindings must be one-to-one unnormalized immutable in-memory input spaces.",
                    nameof(details));
            }
        }
    }

    private static void ValidateExactResolvedMapCapacityInputGeometry(
        CompositionPlan plan,
        CompositionKind compositionKind,
        V2CompiledCompositionDetails details,
        CompiledInputSlotRequirement requirement,
        CompiledExactResolvedMapCapacityInputLengthRequirement exact,
        AddressSpace addressSpace)
    {
        if (exact.Bytes != details.Provenance.ResolvedMap.CapacityBytes ||
            addressSpace.Length != exact.Bytes)
        {
            throw new ArgumentException(
                "Exact resolved-map-capacity input requirements must agree with their immutable plan spaces.",
                nameof(details));
        }

        if (requirement.Normalization is CompiledNoInputNormalization)
        {
            if (addressSpace.InputPaddingByte is not null ||
                addressSpace.InputOversizePolicy != InputOversizePolicy.Reject ||
                addressSpace.ExpectedInputLengths.Count != 0 ||
                (addressSpace.AllowedInputLengths.Count != 0 &&
                 !addressSpace.AllowedInputLengths.SequenceEqual([exact.Bytes])))
            {
                throw new ArgumentException(
                    "Unnormalized exact-map inputs must reject oversize bytes and accept only exact capacity when an alternate length is declared.",
                    nameof(details));
            }

            return;
        }

        if (requirement.Normalization is CompiledPadShorterInputNormalization padded &&
            compositionKind == CompositionKind.Replace &&
            plan.OutputInitialization.Kind == ImageInitializationKind.Reference &&
            addressSpace.InputPaddingByte == padded.FillByte &&
            addressSpace.InputOversizePolicy == InputOversizePolicy.Reject &&
            addressSpace.AllowedInputLengths.Count == 0 &&
            addressSpace.ExpectedInputLengths.Count == 0)
        {
            return;
        }

        throw new ArgumentException(
            "DP short-input padding requires a Replace output cloned from its exact-capacity reference and a padded immutable source with no alternate lengths.",
            nameof(details));
    }

    private static void ValidateV2Eligibility(
        V2CompiledCompositionDetails details,
        CompositionKind compositionKind,
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

        CompiledOutputNamingRequirement output = details.OutputNamingRequirement;
        if (output.RendererKind is
            CompiledOutputNameRendererKind.NormalFlashCodeV1 or
            CompiledOutputNameRendererKind.TpFirmwareV1)
        {
            CompiledOutputNamingRequirement.ValidateCanonicalIcIdentity(
                details.Provenance.Context.MemberId,
                nameof(details));
        }

        if (details.Provenance.Promotion.Stage != CompiledProfilePromotionStage.Supported ||
            details.Provenance.Promotion.Blockers.Count != 0 ||
            !output.AllowsRuntimeExecution(compositionKind))
        {
            throw new ArgumentException(
                "V2 runtime execution requires a supported, unblocked profile with a typed reject output renderer admitted for its composition kind.",
                nameof(details));
        }
    }

}
