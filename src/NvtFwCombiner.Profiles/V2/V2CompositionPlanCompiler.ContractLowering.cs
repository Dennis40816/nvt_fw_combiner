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
            new CompiledProfilePromotion(
                MapPromotionStage(profile.Promotion.Stage),
                profile.Promotion.Blockers.Select(MapPromotionBlocker)),
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
            : slot.Cardinality switch
            {
                CompositionProfileSlotCardinality.ExactlyOne => CompiledInputSlotCardinality.ExactlyOne,
                CompositionProfileSlotCardinality.ZeroOrOne => CompiledInputSlotCardinality.ZeroOrOne,
                CompositionProfileSlotCardinality.OneOrMore => CompiledInputSlotCardinality.OneOrMore,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(slot),
                    slot.Cardinality,
                    "Unknown profile input slot cardinality."),
            };
        return new CompiledInputSlotRequirement(
            slot.SlotId,
            slot.Role,
            slot.ArtifactClass switch
            {
                CompositionProfileArtifactClass.TpFirmware => CompiledInputArtifactClass.TpFirmware,
                CompositionProfileArtifactClass.DpFirmware => CompiledInputArtifactClass.DpFirmware,
                CompositionProfileArtifactClass.ReferenceImage => CompiledInputArtifactClass.ReferenceImage,
                CompositionProfileArtifactClass.CtrlRamReplacement => CompiledInputArtifactClass.CtrlRamReplacement,
                CompositionProfileArtifactClass.Auxiliary => CompiledInputArtifactClass.Auxiliary,
                _ => throw new ArgumentOutOfRangeException(nameof(slot), slot.ArtifactClass, "Unknown profile input artifact class."),
            },
            slot.Required || forceRequired,
            cardinality,
            slot.AcceptedExtensions,
            MapInputLengthRequirement(slot.LengthRule, resolvedMap.CapacityBytes),
            MapInputNormalization(slot.Normalization));
    }

    private static CompiledInputSpaceBinding MapInputSpaceBinding(InputArtifactProfileSpace space)
    {
        return new CompiledInputSpaceBinding(
            space.SpaceId,
            space.SlotId,
            space.InstancePolicy switch
            {
                CompositionProfileInstancePolicy.Singleton => CompiledInputInstancePolicy.Singleton,
                CompositionProfileInstancePolicy.PerBinding => CompiledInputInstancePolicy.PerBinding,
                _ => throw new ArgumentOutOfRangeException(nameof(space), space.InstancePolicy, "Unknown profile input instance policy."),
            });
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
            SourceViewCoverageLengthRule sourceView =>
                new CompiledSourceViewCoverageInputLengthRequirement(
                    ResolveSourceViewExpectedOuterLengths(sourceView, resolvedMapCapacity),
                    sourceView.UnexpectedOuterLengthIssueCode),
            DeclaredPrefixWithWarningLengthRule declaredPrefix =>
                new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
                    declaredPrefix.RequiredEndExclusive,
                    declaredPrefix.ExpectedOuterLengths,
                    declaredPrefix.ShortInputIssueCode,
                    declaredPrefix.UnexpectedOuterLengthIssueCode),
            TpMaximum256KLengthRule => new CompiledTpMaximum256KInputLengthRequirement(),
            _ => throw new ArgumentOutOfRangeException(nameof(lengthRule), "Unknown profile input length rule."),
        };
    }

    private static CompiledInputNormalization MapInputNormalization(
        CompositionProfileInputNormalization normalization)
    {
        return normalization switch
        {
            NoInputNormalization => new CompiledNoInputNormalization(),
            PadShorterInputNormalization padded => new CompiledPadShorterInputNormalization(
                padded.FillByte,
                padded.EvidenceRef),
            TruncateCtrlRamInputNormalization truncated => new CompiledTruncateCtrlRamInputNormalization(
                truncated.WarningIssueCode,
                truncated.EvidenceRef),
            _ => throw new ArgumentOutOfRangeException(nameof(normalization), "Unknown profile input normalization."),
        };
    }

    private static CompiledProfilePromotionStage MapPromotionStage(CompositionProfilePromotionStage stage)
    {
        return stage switch
        {
            CompositionProfilePromotionStage.Known => CompiledProfilePromotionStage.Known,
            CompositionProfilePromotionStage.MapResolvable => CompiledProfilePromotionStage.MapResolvable,
            CompositionProfilePromotionStage.Inspectable => CompiledProfilePromotionStage.Inspectable,
            CompositionProfilePromotionStage.Authorable => CompiledProfilePromotionStage.Authorable,
            CompositionProfilePromotionStage.Compilable => CompiledProfilePromotionStage.Compilable,
            CompositionProfilePromotionStage.ExecutableCandidate => CompiledProfilePromotionStage.ExecutableCandidate,
            CompositionProfilePromotionStage.Supported => CompiledProfilePromotionStage.Supported,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown profile promotion stage."),
        };
    }

    private static CompiledProfilePromotionBlocker MapPromotionBlocker(CompositionProfilePromotionBlocker blocker)
    {
        return new CompiledProfilePromotionBlocker(
            blocker.BlockerId,
            blocker.Kind switch
            {
                CompositionProfileBlockerKind.Map => CompiledProfilePromotionBlockerKind.Map,
                CompositionProfileBlockerKind.Metadata => CompiledProfilePromotionBlockerKind.Metadata,
                CompositionProfileBlockerKind.Operation => CompiledProfilePromotionBlockerKind.Operation,
                CompositionProfileBlockerKind.Processor => CompiledProfilePromotionBlockerKind.Processor,
                CompositionProfileBlockerKind.Integrity => CompiledProfilePromotionBlockerKind.Integrity,
                CompositionProfileBlockerKind.Golden => CompiledProfilePromotionBlockerKind.Golden,
                CompositionProfileBlockerKind.HumanReview => CompiledProfilePromotionBlockerKind.HumanReview,
                CompositionProfileBlockerKind.Ui => CompiledProfilePromotionBlockerKind.Ui,
                CompositionProfileBlockerKind.Release => CompiledProfilePromotionBlockerKind.Release,
                _ => throw new ArgumentOutOfRangeException(nameof(blocker), blocker.Kind, "Unknown profile blocker kind."),
            },
            blocker.Reason,
            blocker.EvidenceRefs);
    }

    private static CompiledOutputInvalidCharacterPolicy MapOutputPolicy(CompositionProfileInvalidCharacterPolicy policy)
    {
        return policy switch
        {
            CompositionProfileInvalidCharacterPolicy.Reject => CompiledOutputInvalidCharacterPolicy.Reject,
            CompositionProfileInvalidCharacterPolicy.ReplaceUnderscore => CompiledOutputInvalidCharacterPolicy.ReplaceUnderscore,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown output invalid-character policy."),
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
                MapOutputPolicy(output.InvalidCharacterPolicy),
                output.RequiredTokenIds)
            : new CompiledOutputNamingRequirement(
                output.FileNameTemplate,
                output.AllowOverride,
                MapOutputPolicy(output.InvalidCharacterPolicy),
                output.RequiredTokenIds,
                output.RuleId,
                output.OutputArtifactType switch
                {
                    CompositionProfileOutputArtifactType.FlashCode =>
                        CompiledOutputArtifactType.FlashCode,
                    CompositionProfileOutputArtifactType.TpFirmware =>
                        CompiledOutputArtifactType.TpFirmware,
                    CompositionProfileOutputArtifactType.Unspecified =>
                        throw new InvalidOperationException(
                            "Typed profile output cannot have an unspecified artifact type."),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(profile),
                        output.OutputArtifactType,
                        "Unknown profile output artifact type."),
                },
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
            requirement.SourceKind switch
            {
                CompositionProfileOutputTokenSourceKind.CompiledIc =>
                    CompiledOutputTokenSourceKind.CompiledIc,
                CompositionProfileOutputTokenSourceKind.RunDateUtc =>
                    CompiledOutputTokenSourceKind.RunDateUtc,
                CompositionProfileOutputTokenSourceKind.DpcmiVersion =>
                    CompiledOutputTokenSourceKind.DpcmiVersion,
                CompositionProfileOutputTokenSourceKind.FirmwareConfigTpVersion =>
                    CompiledOutputTokenSourceKind.FirmwareConfigTpVersion,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(requirement),
                    requirement.SourceKind,
                    "Unknown profile output token source kind."),
            },
            requirement.MetadataBindingId,
            requirement.MissingPolicy switch
            {
                CompositionProfileOutputTokenMissingPolicy.Block =>
                    CompiledOutputTokenMissingPolicy.Block,
                CompositionProfileOutputTokenMissingPolicy.UsePlaceholder =>
                    CompiledOutputTokenMissingPolicy.UsePlaceholder,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(requirement),
                    requirement.MissingPolicy,
                    "Unknown profile output token missing policy."),
            },
            requirement.Placeholder,
            metadataBinding?.SpaceId);
    }

    private static void AddUnsupported(List<CompositionIssue> issues, string message, string? operationId = null)
    {
        issues.Add(new CompositionIssue(UnsupportedDeclaration, message, operationId));
    }
}
