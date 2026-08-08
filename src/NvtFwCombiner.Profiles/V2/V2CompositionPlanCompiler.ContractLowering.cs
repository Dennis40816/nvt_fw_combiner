using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    /// <summary>Atomically admits and lowers one exact catalog-owned map-bound profile.</summary>
    internal static bool TryCompileAdmitted(
        TrustedProfileBundleCatalog catalog,
        TrustedCompositionProfileCatalogEntry profileEntry,
        FirmwareMapResolutionInputs resolutionInputs,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out V2CompositionPlanCompileResult? compilation,
        out IReadOnlyList<CompositionIssue> issues)
    {
        compilation = null;
        if (!V2CompositionPreparationService.TryPrepare(
                catalog,
                profileEntry,
                resolutionInputs,
                out FirmwareMapResolutionResult? mapResolution,
                out IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> capabilityAdmissions,
                out issues))
        {
            return false;
        }

        compilation = CompileAdmittedCore(
            catalog.BundleIdentity,
            profileEntry,
            mapResolution!.ResolvedMap!,
            capabilityAdmissions,
            selectedInputSlotIds);
        return true;
    }

    private static Dictionary<string, AddressSpace> LowerAddressSpaces(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        List<CompositionIssue> issues,
        IReadOnlySet<string>? activeSlotIds = null)
    {
        var spaces = new Dictionary<string, AddressSpace>(StringComparer.Ordinal);
        InputArtifactProfileSpace[] inputSpaces =
        [
            .. profile.Spaces.OfType<InputArtifactProfileSpace>(),
        ];
        if (profile.InputSlots.Any(slot => inputSpaces.Count(space =>
                StringComparer.Ordinal.Equals(space.SlotId, slot.SlotId)) != 1))
        {
            AddUnsupported(issues, "current runtime lowering requires exactly one immutable address space per input slot");
        }

        foreach (InputArtifactProfileSpace input in inputSpaces)
        {
            CompositionInputSlotDefinition slot = profile.InputSlots.Single(candidate =>
                StringComparer.Ordinal.Equals(candidate.SlotId, input.SlotId));
            bool requiredSingleton = slot is
            {
                Required: true,
                Cardinality: CompiledInputSlotCardinality.ExactlyOne,
            };
            bool groupedOptionalSingleton = slot is
            {
                Required: false,
                Cardinality: CompiledInputSlotCardinality.ZeroOrOne,
            } && profile.InputSelectionGroups.Any(group =>
                group.MemberSlotIds.Contains(slot.SlotId, StringComparer.Ordinal));
            if (input.InstancePolicy != CompiledInputInstancePolicy.Singleton ||
                (!requiredSingleton && !groupedOptionalSingleton) ||
                slot.Normalization is not (CompiledNoInputNormalization or
                    CompiledPadShorterInputNormalization or
                    CompiledTruncateCtrlRamInputNormalization) ||
                !IsCurrentInputLengthRequirementSupported(slot))
            {
                AddUnsupported(
                    issues,
                    $"input space '{input.SpaceId}' must bind one required exact-one or grouped optional zero-or-one singleton slot with approved length and normalization");
                continue;
            }

            if (activeSlotIds is not null && !activeSlotIds.Contains(input.SlotId))
            {
                continue;
            }

            if (!TryResolveInputSpaceLength(
                    profile,
                    family,
                    input,
                    slot,
                    resolvedMap,
                    issues,
                    out long length))
            {
                continue;
            }

            spaces.Add(input.SpaceId, CreateInputAddressSpace(
                input.SpaceId,
                length,
                slot,
                resolvedMap.CapacityBytes,
                profile.CompositionKind,
                IsCloneSourceSlot(profile, input.SlotId)));
        }

        foreach (MutableCompositionProfileSpace mutableSpace in profile.Spaces.OfType<MutableCompositionProfileSpace>())
        {
            spaces.Add(
                mutableSpace.SpaceId,
                new AddressSpace(
                    mutableSpace.SpaceId,
                    ResolveMutableSpaceCapacity(mutableSpace, resolvedMap.CapacityBytes),
                    AddressSpaceMutability.Mutable));
        }

        return spaces;
    }

    private static MutableCompositionProfileSpace AssertOutputSpace(CompositionProfileDefinition profile)
    {
        return profile.Spaces.OfType<MutableCompositionProfileSpace>().Single(space =>
            space.Kind == CompositionProfileSpaceKind.OutputImage);
    }

    private static V2CompositionPlanCompileResult Succeed(
        CompositionProfileDefinition profile,
        ProfileBundleIdentity bundleIdentity,
        ProfileBundleEntryIdentity profileEntryIdentity,
        V2CompilationContext context,
        CompositionPlan plan,
        IEnumerable<CompiledInputSlotRequirement> inputSlots,
        IEnumerable<CompiledInputSpaceBinding> inputBindings,
        CompiledRegionAccessContract regionAccess,
        IEnumerable<FirmwareMapFactBinding<FirmwareCapabilityFact>>? capabilityAdmissions = null,
        bool runtimeExecutable = false,
        IEnumerable<CompiledValidationRequirement>? additionalValidationRequirements = null,
        IEnumerable<CompiledInputSelectionGroup>? inputSelectionGroups = null)
    {
        var provenance = new V2CompilationProvenance(
            bundleIdentity,
            profileEntryIdentity,
            context,
            profile.Promotion,
            profile.EvidenceRefs,
            additionalValidationRequirements ?? [],
            capabilityAdmissions ?? []);
        var details = new V2CompiledCompositionDetails(
            profile.ProfileId,
            profile.ProfileVersion,
            profile.Header.ExperienceId,
            profile.CompositionKind,
            provenance,
            new CompiledInputContract(inputSlots, inputBindings, inputSelectionGroups),
            regionAccess,
            profile.Output,
            profile.IcNumberInputMode);
        ValidateArtifactAdmission(details, runtimeExecutable);
        CompiledComposition artifact = runtimeExecutable
            ? CompiledComposition.CreateV2RuntimeExecutable(plan, details)
            : CompiledComposition.CreateV2(plan, details);
        return V2CompositionPlanCompileResult.Succeeded(artifact);
    }

    private static void ValidateArtifactAdmission(
        V2CompiledCompositionDetails details,
        bool runtimeExecutable)
    {
        if (details.Provenance.Promotion.Stage < CompiledProfilePromotionStage.Compilable)
        {
            throw new ArgumentException(
                "Only compilable v2 profiles may produce a complete composition plan.",
                nameof(details));
        }

        if (runtimeExecutable)
        {
            CompiledOutputNamingRequirement output = details.OutputNamingRequirement;
            if (output.RendererKind is
                CompiledOutputNameRendererKind.NormalFlashCodeV1 or
                CompiledOutputNameRendererKind.TpFirmwareV1)
            {
                CompiledOutputNamingRequirement.ValidateCanonicalIcIdentity(
                    details.Provenance.Context.MemberId,
                    nameof(details));
            }

        }
    }

    private static CompiledInputSlotRequirement MapInputSlot(
        CompositionInputSlotDefinition slot,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        bool forceRequired = false)
    {
        return new CompiledInputSlotRequirement(
            slot,
            ResolveInputLengthRequirement(slot.LengthRequirement, resolvedMap.CapacityBytes),
            forceRequired);
    }

    private static CompiledInputSpaceBinding MapInputSpaceBinding(InputArtifactProfileSpace space)
    {
        return new CompiledInputSpaceBinding(
            space.SpaceId,
            space.SlotId,
            space.InstancePolicy);
    }

    private static CompiledInputLengthRequirement ResolveInputLengthRequirement(
        InputLengthRequirementDefinition lengthRequirement,
        long resolvedMapCapacity)
    {
        return lengthRequirement switch
        {
            ResolvedMapCapacityInputLengthDefinition => new CompiledExactResolvedMapCapacityInputLengthRequirement(
                resolvedMapCapacity),
            SourceViewCoverageInputLengthDefinition sourceView =>
                new CompiledSourceViewCoverageInputLengthRequirement(
                    ResolveSourceViewExpectedOuterLengths(sourceView, resolvedMapCapacity),
                    sourceView.UnexpectedOuterLengthIssueCode,
                    sourceView.RequiredEndExclusive,
                    sourceView.ShortInputIssueCode,
                    sourceView.MaximumBytes),
            CompiledInputLengthRequirement compiled => compiled,
            _ => throw new ArgumentOutOfRangeException(
                nameof(lengthRequirement),
                "Unknown canonical input length definition."),
        };
    }

    private static void AddUnsupported(List<CompositionIssue> issues, string message, string? operationId = null)
    {
        issues.Add(new CompositionIssue(UnsupportedDeclaration, message, operationId));
    }
}
