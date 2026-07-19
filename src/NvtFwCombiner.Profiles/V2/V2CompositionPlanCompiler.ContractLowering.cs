using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
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
        bool runtimeExecutable = false)
    {
        var provenance = new V2CompilationProvenance(
            selection.BundleIdentity,
            selection.ProfileEntryIdentity,
            context,
            new CompiledProfilePromotion(
                MapPromotionStage(profile.Promotion.Stage),
                profile.Promotion.Blockers.Select(MapPromotionBlocker)),
            profile.EvidenceRefs,
            [],
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
                new CompiledInputContract(inputSlots, inputBindings),
                regionAccess,
                new CompiledOutputNamingRequirement(
                    profile.Output.FileNameTemplate,
                    profile.Output.AllowOverride,
                    MapOutputPolicy(profile.Output.InvalidCharacterPolicy),
                    profile.Output.RequiredTokenIds)));
        CompiledComposition artifact = runtimeExecutable
            ? CompiledComposition.CreateV2RuntimeExecutable(plan, identity, icNumberPolicy)
            : CompiledComposition.CreateV2(plan, identity, icNumberPolicy);
        return V2CompositionPlanCompileResult.Succeeded(artifact);
    }

    private static CompiledInputSlotRequirement MapInputSlot(
        CompositionProfileInputSlot slot,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap)
    {
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
            slot.Required,
            slot.Cardinality switch
            {
                CompositionProfileSlotCardinality.ExactlyOne => CompiledInputSlotCardinality.ExactlyOne,
                CompositionProfileSlotCardinality.ZeroOrOne => CompiledInputSlotCardinality.ZeroOrOne,
                CompositionProfileSlotCardinality.OneOrMore => CompiledInputSlotCardinality.OneOrMore,
                _ => throw new ArgumentOutOfRangeException(nameof(slot), slot.Cardinality, "Unknown profile input slot cardinality."),
            },
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
            NormalDpExtractWithWarningLengthRule normalDp =>
                new CompiledNormalDpExtractWithWarningInputLengthRequirement(
                    normalDp.IssueCode,
                    ResolveNormalDpExpectedInputLengths(normalDp, resolvedMapCapacity)),
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

    private static void AddUnsupported(List<CompositionIssue> issues, string message, string? operationId = null)
    {
        issues.Add(new CompositionIssue(UnsupportedDeclaration, message, operationId));
    }
}
