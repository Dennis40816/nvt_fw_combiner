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
        ValidateV2InputRequirements(plan, identity.CompositionKind, identity.Details);
        ValidateV2Eligibility(identity.Details, eligibility);

        Plan = plan;
        ProfileId = identity.ProfileId;
        ProfileVersion = identity.ProfileVersion;
        IcId = identity.Details.Provenance.Context.MemberId;
        ModeId = identity.Details.Provenance.Context.ModeId;
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
        CompositionKind compositionKind,
        V2CompiledCompositionDetails details)
    {
        if (details.Provenance.Context is LogicalOutputV2CompilationContext)
        {
            ValidateLogicalOutputInputRequirements(plan, compositionKind, details);
            return;
        }

        if (details.InputContract.SpaceBindings.Any(static binding =>
                binding.InstancePolicy == CompiledInputInstancePolicy.PerBinding))
        {
            ValidateRuntimeReferenceReplaceInputRequirements(plan, compositionKind, details);
            return;
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
                requirement.Normalization is not (CompiledNoInputNormalization or CompiledPadShorterInputNormalization))
            {
                throw new ArgumentException("Current V2 plan artifacts require singleton required inputs with no normalization or DP short-input padding.", nameof(details));
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
                    ValidateExactBytesInputGeometry(requirement, exact, addressSpace);
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
                case CompiledNormalDpExtractWithWarningInputLengthRequirement normalDp:
                    normalDpInputRequirements.Add((addressSpace, normalDp));
                    break;
                default:
                    throw new ArgumentException(
                        "Current V2 plan artifacts support only exact-map-capacity, normal-DP extraction, TP-maximum, or exact TP input requirements.",
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

    private static void ValidateRuntimeReferenceReplaceInputRequirements(
        CompositionPlan plan,
        CompositionKind compositionKind,
        V2CompiledCompositionDetails details)
    {
        if (compositionKind != CompositionKind.Replace ||
            details.Provenance.Context is not ResolvedMapV2CompilationContext ||
            plan.OutputInitialization.Kind != ImageInitializationKind.Reference ||
            plan.OutputInitialization.ReferenceSpaceId is null ||
            details.RegionAccessContract.Requirements.Count == 0 ||
            details.RegionAccessContract.ResolvedViews.Count != 0)
        {
            throw new ArgumentException(
                "Map-bound runtime reference-replace artifacts require a reference-cloned Replace output with declared physical access and no static views.",
                nameof(details));
        }

        CompiledInputSlotRequirement[] referenceSlots =
        [
            .. details.InputContract.Slots.Where(static slot =>
                slot.ArtifactClass == CompiledInputArtifactClass.ReferenceImage),
        ];
        CompiledInputSlotRequirement[] sourceSlots =
        [
            .. details.InputContract.Slots.Where(static slot =>
                slot.ArtifactClass == CompiledInputArtifactClass.Auxiliary),
        ];
        if (details.InputContract.Slots.Count != 2 || referenceSlots.Length != 1 || sourceSlots.Length != 1 ||
            referenceSlots[0] is not
            {
                Required: true,
                Cardinality: CompiledInputSlotCardinality.ExactlyOne,
                LengthRequirement: CompiledExactResolvedMapCapacityInputLengthRequirement,
                Normalization: CompiledNoInputNormalization,
            } ||
            sourceSlots[0] is not
            {
                Required: true,
                Cardinality: CompiledInputSlotCardinality.OneOrMore,
                LengthRequirement: CompiledBoundedInputLengthRequirement
                {
                    MinimumBytes: 1,
                    MaximumBytes: int.MaxValue,
                },
                Normalization: CompiledNoInputNormalization,
            })
        {
            throw new ArgumentException(
                "Map-bound runtime reference-replace artifacts require one exact reference slot and one unnormalized per-binding auxiliary source slot.",
                nameof(details));
        }

        CompiledInputSpaceBinding[] referenceBindings =
        [
            .. details.InputContract.SpaceBindings.Where(binding =>
                StringComparer.Ordinal.Equals(binding.SlotId, referenceSlots[0].SlotId)),
        ];
        CompiledInputSpaceBinding[] sourceBindings =
        [
            .. details.InputContract.SpaceBindings.Where(binding =>
                StringComparer.Ordinal.Equals(binding.SlotId, sourceSlots[0].SlotId)),
        ];
        if (referenceBindings.Length != 1 || sourceBindings.Length == 0 ||
            referenceBindings[0].InstancePolicy != CompiledInputInstancePolicy.Singleton ||
            sourceBindings.Any(static binding => binding.InstancePolicy != CompiledInputInstancePolicy.PerBinding) ||
            !StringComparer.Ordinal.Equals(
                plan.OutputInitialization.ReferenceSpaceId,
                referenceBindings[0].AddressSpaceId))
        {
            throw new ArgumentException(
                "Runtime reference-replace bindings must contain exactly one singleton output reference and one or more per-binding auxiliary sources.",
                nameof(details));
        }

        var spaces = plan.AddressSpaces.ToDictionary(static space => space.AddressSpaceId, StringComparer.Ordinal);
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
        if (!immutableAddressSpaceIds.SequenceEqual(bindingAddressSpaceIds, StringComparer.Ordinal) ||
            plan.AddressSpaces.Count != bindingAddressSpaceIds.Length + 1 ||
            plan.OutputInitialization.Capacity != details.Provenance.ResolvedMap.CapacityBytes)
        {
            throw new ArgumentException(
                "Runtime reference-replace artifacts must bind every immutable plan space once and declare only the reference-cloned output as mutable.",
                nameof(details));
        }

        if (!spaces.TryGetValue(referenceBindings[0].AddressSpaceId, out AddressSpace? referenceSpace) ||
            referenceSpace.Mutability != AddressSpaceMutability.Immutable ||
            referenceSpace.Length != details.Provenance.ResolvedMap.CapacityBytes ||
            referenceSpace.InputPaddingByte is not null ||
            referenceSpace.InputOversizePolicy != InputOversizePolicy.Reject ||
            referenceSpace.AllowedInputLengths.Count != 0 ||
            referenceSpace.ExpectedInputLengths.Count != 0)
        {
            throw new ArgumentException(
                "Runtime reference-replace output must clone one exact unnormalized immutable resolved-map reference.",
                nameof(details));
        }

        var sourceAddressSpaceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CompiledInputSpaceBinding sourceBinding in sourceBindings)
        {
            if (!spaces.TryGetValue(sourceBinding.AddressSpaceId, out AddressSpace? sourceSpace) ||
                sourceSpace.Mutability != AddressSpaceMutability.Immutable ||
                sourceSpace.Length is < 1 or > int.MaxValue ||
                sourceSpace.InputPaddingByte is not null ||
                sourceSpace.InputOversizePolicy != InputOversizePolicy.Reject ||
                sourceSpace.AllowedInputLengths.Count != 0 ||
                sourceSpace.ExpectedInputLengths.Count != 0 ||
                !sourceAddressSpaceIds.Add(sourceBinding.AddressSpaceId))
            {
                throw new ArgumentException(
                    "Runtime reference-replace auxiliary bindings must be unique unnormalized immutable in-memory sources.",
                    nameof(details));
            }
        }

        var referencedSourceAddressSpaceIds = new HashSet<string>(StringComparer.Ordinal);
        if (plan.OrderedOperations.Count == 0 || plan.OrderedOperations.Any(operation =>
                operation.Kind != CompositionOperationKind.ReplaceRange ||
                operation.OverlapPolicy != OverlapPolicy.Reject ||
                !StringComparer.Ordinal.Equals(operation.TargetSpaceId, plan.OutputSpaceId) ||
                operation.SourceSpaceId is null ||
                !sourceAddressSpaceIds.Contains(operation.SourceSpaceId)))
        {
            throw new ArgumentException(
                "Runtime reference-replace plans require only reject-overlap ReplaceRange operations from declared auxiliary sources into the output.",
                nameof(plan));
        }

        foreach (CompositionOperation operation in plan.OrderedOperations)
        {
            _ = referencedSourceAddressSpaceIds.Add(operation.SourceSpaceId!);
        }

        if (!referencedSourceAddressSpaceIds.SetEquals(sourceAddressSpaceIds))
        {
            throw new ArgumentException(
                "Every runtime reference-replace auxiliary source binding must participate in at least one operation.",
                nameof(plan));
        }
    }

    private static void ValidateLogicalOutputInputRequirements(
        CompositionPlan plan,
        CompositionKind compositionKind,
        V2CompiledCompositionDetails details)
    {
        if (compositionKind != CompositionKind.Merge ||
            plan.OutputInitialization.Kind != ImageInitializationKind.Blank ||
            plan.OutputInitialization.FillByte != 0 ||
            details.RegionAccessContract.Requirements.Count != 0 ||
            details.RegionAccessContract.ResolvedViews.Count != 0)
        {
            throw new ArgumentException(
                "Logical-output V2 artifacts require a zero-filled Merge output with no physical region access.",
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

    private static void ValidateExactBytesInputGeometry(
        CompiledInputSlotRequirement requirement,
        CompiledExactBytesInputLengthRequirement exact,
        AddressSpace addressSpace)
    {
        if (requirement.ArtifactClass != CompiledInputArtifactClass.TpFirmware ||
            requirement.Normalization is not CompiledNoInputNormalization ||
            exact.Bytes > CompiledTpMaximum256KInputLengthRequirement.MaximumBytes ||
            addressSpace.Length != exact.Bytes ||
            addressSpace.InputPaddingByte is not null ||
            addressSpace.InputOversizePolicy != InputOversizePolicy.Reject ||
            addressSpace.AllowedInputLengths.Count != 0 ||
            addressSpace.ExpectedInputLengths.Count != 0)
        {
            throw new ArgumentException(
                "Exact TP input requirements must be unnormalized, within 256 KiB, and match an exact immutable plan space.",
                nameof(requirement));
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
            addressSpace.InputPaddingByte is not null ||
            addressSpace.InputOversizePolicy != InputOversizePolicy.ExtractDeclaredRange ||
            addressSpace.AllowedInputLengths.Count != 0)
        {
            throw new ArgumentException(
                "TP maximum input requirements must extract the maximum resolved source span while accepting inputs through the 256 KiB limit.",
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
