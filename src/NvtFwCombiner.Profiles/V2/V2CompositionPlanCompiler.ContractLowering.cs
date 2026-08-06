using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private static Dictionary<string, AddressSpace> LowerAddressSpaces(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        List<CompositionIssue> issues,
        IReadOnlySet<string>? activeSlotIds = null)
    {
        var spaces = new Dictionary<string, AddressSpace>(StringComparer.Ordinal);
        foreach (InputArtifactProfileSpace input in profile.Spaces.OfType<InputArtifactProfileSpace>())
        {
            if (activeSlotIds is not null && !activeSlotIds.Contains(input.SlotId))
            {
                continue;
            }

            CompositionInputSlotDefinition slot = profile.InputSlots.Single(candidate =>
                StringComparer.Ordinal.Equals(candidate.SlotId, input.SlotId));
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
        TrustedProfileBundleCatalog.ProfileSelection selection,
        V2CompilationContext context,
        CompositionPlan plan,
        IEnumerable<CompiledInputSlotRequirement> inputSlots,
        IEnumerable<CompiledInputSpaceBinding> inputBindings,
        CompiledRegionAccessContract regionAccess,
        CompiledIcNumberPolicy icNumberPolicy,
        CompositionProfileMapAdmission? admission = null,
        bool runtimeExecutable = false,
        IEnumerable<CompiledValidationRequirement>? additionalValidationRequirements = null,
        IEnumerable<CompiledInputSelectionGroup>? inputSelectionGroups = null)
    {
        var provenance = new V2CompilationProvenance(
            selection.BundleIdentity,
            selection.ProfileEntryIdentity,
            context,
            profile.Promotion,
            profile.EvidenceRefs,
            additionalValidationRequirements ?? [],
            admission?.RequiredCapabilities.Select(static capability => new CompiledCapabilityAdmission(
                capability.RequiredCapabilityId,
                capability.Binding)) ?? []);
        var identity = new V2CompiledCompositionIdentity(
            profile.ProfileId,
            profile.ProfileVersion,
            profile.Experience.ExperienceId,
            profile.CompositionKind,
            new V2CompiledCompositionDetails(
                provenance,
                new CompiledInputContract(inputSlots, inputBindings, inputSelectionGroups),
                regionAccess,
                profile.Output));
        CompiledComposition artifact = runtimeExecutable
            ? CompiledComposition.CreateV2RuntimeExecutable(plan, identity, icNumberPolicy)
            : CompiledComposition.CreateV2(plan, identity, icNumberPolicy);
        return V2CompositionPlanCompileResult.Succeeded(artifact);
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
                    sourceView.UnexpectedOuterLengthIssueCode),
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
