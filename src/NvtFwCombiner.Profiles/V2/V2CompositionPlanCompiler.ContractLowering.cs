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

            CompositionProfileInputSlot slot = profile.InputSlots.Single(candidate =>
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
                LowerOutputNaming(profile)));
        CompiledComposition artifact = runtimeExecutable
            ? CompiledComposition.CreateV2RuntimeExecutable(plan, identity, icNumberPolicy)
            : CompiledComposition.CreateV2(plan, identity, icNumberPolicy);
        return V2CompositionPlanCompileResult.Succeeded(artifact);
    }

    private static CompiledInputSlotRequirement MapInputSlot(
        CompositionProfileInputSlot slot,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        bool forceRequired = false)
    {
        CompiledInputSlotCardinality cardinality = forceRequired
            ? CompiledInputSlotCardinality.ExactlyOne
            : slot.Cardinality;
        return new CompiledInputSlotRequirement(
            slot.SlotId,
            slot.Role,
            slot.ArtifactClass,
            slot.Required || forceRequired,
            cardinality,
            slot.AcceptedExtensions,
            MapInputLengthRequirement(slot.LengthRule, resolvedMap.CapacityBytes),
            slot.Normalization);
    }

    private static CompiledInputSpaceBinding MapInputSpaceBinding(InputArtifactProfileSpace space)
    {
        return new CompiledInputSpaceBinding(
            space.SpaceId,
            space.SlotId,
            space.InstancePolicy);
    }

    private static CompiledInputLengthRequirement MapInputLengthRequirement(
        CompositionProfileLengthRule lengthRule,
        long resolvedMapCapacity)
    {
        return lengthRule switch
        {
            ExactBytesLengthRule exact => new CompiledExactBytesInputLengthRequirement(exact.Bytes),
            ExactResolvedMapCapacityLengthRule => new CompiledExactResolvedMapCapacityInputLengthRequirement(
                resolvedMapCapacity),
            BoundedLengthRule bounded => new CompiledBoundedInputLengthRequirement(
                bounded.MinimumBytes,
                bounded.MaximumBytes),
            SourceViewCoverageLengthRule
            {
                RequiredEndExclusive: { } requiredEndExclusive,
                ShortInputIssueCode: { } shortInputIssueCode,
                UnexpectedOuterLengthIssueCode: { } unexpectedOuterLengthIssueCode,
            } sourceView => new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
                requiredEndExclusive,
                sourceView.ExpectedOuterLengths,
                shortInputIssueCode,
                unexpectedOuterLengthIssueCode),
            SourceViewCoverageLengthRule { MaximumOuterLength: null } sourceView =>
                new CompiledSourceViewCoverageInputLengthRequirement(
                    ResolveSourceViewExpectedOuterLengths(sourceView, resolvedMapCapacity),
                    sourceView.UnexpectedOuterLengthIssueCode),
            SourceViewCoverageLengthRule
            {
                MaximumOuterLength: CompiledTpMaximum256KInputLengthRequirement.MaximumBytes,
            } => new CompiledTpMaximum256KInputLengthRequirement(),
            _ => throw new ArgumentOutOfRangeException(nameof(lengthRule), "Unknown profile input length rule."),
        };
    }

    private static CompiledOutputNamingRequirement LowerOutputNaming(
        CompositionProfileDefinition profile)
    {
        CompositionProfileOutput output = profile.Output;
        return output.RuleId is null
            ? new CompiledOutputNamingRequirement(
                output.FileNameTemplate,
                output.AllowOverride,
                output.InvalidCharacterPolicy,
                output.RequiredTokenIds)
            : new CompiledOutputNamingRequirement(
                output.FileNameTemplate,
                output.AllowOverride,
                output.InvalidCharacterPolicy,
                output.RequiredTokenIds,
                output.RuleId,
                output.OutputArtifactType,
                output.TokenRequirements.Select(requirement =>
                    MapOutputTokenRequirement(
                        requirement,
                        profile.MetadataBindings)));
    }

    private static CompiledOutputTokenRequirement MapOutputTokenRequirement(
        CompositionProfileOutputTokenRequirement requirement,
        IReadOnlyList<CompositionProfileMetadataBinding> metadataBindings)
    {
        CompositionProfileMetadataBinding? metadataBinding =
            requirement.MetadataBindingId is null
                ? null
                : metadataBindings.Single(binding =>
                    StringComparer.Ordinal.Equals(
                        binding.BindingId,
                        requirement.MetadataBindingId));
        return new CompiledOutputTokenRequirement(
            requirement.TokenId,
            requirement.SourceKind,
            requirement.MetadataBindingId,
            requirement.MissingPolicy,
            requirement.Placeholder,
            metadataBinding?.SpaceId);
    }

    private static void AddUnsupported(List<CompositionIssue> issues, string message, string? operationId = null)
    {
        issues.Add(new CompositionIssue(UnsupportedDeclaration, message, operationId));
    }
}
