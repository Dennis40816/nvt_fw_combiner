using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private static void ValidateSupportedProfile(CompositionProfileDefinition profile, List<CompositionIssue> issues)
    {
        if (profile.CompositionKind is not (CompositionKind.Merge or CompositionKind.Replace))
        {
            AddUnsupported(issues, "composition kind must be Merge or Replace");
        }

        bool isDpReplace = StringComparer.Ordinal.Equals(profile.Experience.ExperienceId, ExperienceIds.DpReplace);
        bool isCtrlRamReplace = StringComparer.Ordinal.Equals(profile.Experience.ExperienceId, ExperienceIds.CtrlRamReplace);
        if (profile.CompositionKind == CompositionKind.Replace && !isDpReplace && !isCtrlRamReplace)
        {
            AddUnsupported(issues, "Replace runtime lowering currently supports only dp-replace or ctrlram-replace experiences");
        }

        if (profile.Promotion.Stage < CompositionProfilePromotionStage.Compilable)
        {
            AddUnsupported(issues, "promotion stage must be Compilable or later");
        }

        if (profile.Promotion.Stage == CompositionProfilePromotionStage.Supported &&
            !HasRuntimeExecutableOutputContract(profile))
        {
            AddUnsupported(issues, "supported profiles require a typed reject output renderer admitted for the declared experience");
        }

        if (profile.Validations.Any(static validation =>
                validation is not NonUniformRegionProfileValidation))
        {
            AddUnsupported(issues, "only warning-only non-uniform-region validations are lowered in this slice");
        }

        MutableCompositionProfileSpace output = AssertOutputSpace(profile);
        ValidateMapBoundOutputShape(profile, output, issues);

        foreach (InputArtifactProfileSpace inputSpace in profile.Spaces.OfType<InputArtifactProfileSpace>())
        {
            CompositionProfileInputSlot? slot = profile.InputSlots.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.SlotId, inputSpace.SlotId));
            bool requiredSingleton = slot is
            {
                Required: true,
                Cardinality: CompositionProfileSlotCardinality.ExactlyOne,
            };
            bool groupedOptionalSingleton = slot is
            {
                Required: false,
                Cardinality: CompositionProfileSlotCardinality.ZeroOrOne,
            } && profile.InputSelectionGroups.Any(group =>
                group.MemberSlotIds.Contains(slot.SlotId, StringComparer.Ordinal));
            if (inputSpace.InstancePolicy != CompositionProfileInstancePolicy.Singleton ||
                slot is null ||
                (!requiredSingleton && !groupedOptionalSingleton) ||
                slot.Normalization is not (NoInputNormalization or PadShorterInputNormalization or TruncateCtrlRamInputNormalization) ||
                (slot.LengthRule is SourceViewCoverageLengthRule { RequiredEndExclusive: not null } &&
                 profile.CompositionKind != CompositionKind.Merge) ||
                !IsCurrentInputLengthRuleSupported(slot))
            {
                AddUnsupported(
                    issues,
                    $"input space '{inputSpace.SpaceId}' must bind one required exact-one or grouped optional zero-or-one singleton slot with approved length and normalization");
            }
        }

        InputArtifactProfileSpace[] inputSpaces = [.. profile.Spaces.OfType<InputArtifactProfileSpace>()];
        if (profile.InputSlots.Any(slot => inputSpaces.Count(space =>
                StringComparer.Ordinal.Equals(space.SlotId, slot.SlotId)) != 1))
        {
            AddUnsupported(issues, "current runtime lowering requires exactly one immutable address space per input slot");
        }

        string? replaceReferenceSourceSpaceId = profile.CompositionKind == CompositionKind.Replace
            ? AssertOutputSpace(profile).Initializer is CloneProfileInitializer clone
                ? ResolveCloneReferenceSourceSpaceId(profile, clone)
                : null
            : null;

        foreach (CompositionProfileOperation operation in profile.Operations)
        {
            bool isCopyRange = operation is CopyOrReplaceProfileOperation
            {
                Kind: CompositionProfileOperationKind.CopyRange,
            };
            bool isDpReplaceRange = isDpReplace &&
                operation is CopyOrReplaceProfileOperation replace &&
                replace.Kind == CompositionProfileOperationKind.ReplaceRange &&
                IsDpReplacePayloadInputSource(profile, replace);
            bool isProcessorRun = operation is RunProcessorProfileOperation;
            bool isCtrlRamProcessorRun = isCtrlRamReplace && isProcessorRun;
            bool isReferenceRestore = operation is CopyOrReplaceProfileOperation copy &&
                copy.Kind == CompositionProfileOperationKind.CopyRange &&
                copy.OverlapPolicy == OverlapPolicy.ReplaceExisting &&
                StringComparer.Ordinal.Equals(
                    replaceReferenceSourceSpaceId,
                    profile.Views.Single(view => StringComparer.Ordinal.Equals(view.ViewId,
                        copy.SourceViewId)).SpaceId);
            bool isSupportedOperation = profile.CompositionKind == CompositionKind.Merge
                ? isCopyRange || operation is FillRangeProfileOperation or PatchScalarProfileOperation or TransformScalarProfileOperation ||
                  isProcessorRun
                : isDpReplaceRange || isReferenceRestore || isCtrlRamProcessorRun;
            bool hasSupportedOverlapPolicy = profile.CompositionKind == CompositionKind.Merge
                ? operation.OverlapPolicy == OverlapPolicy.Reject ||
                  ((isCopyRange || isProcessorRun) && operation.OverlapPolicy == OverlapPolicy.ReplaceExisting)
                : ((isDpReplaceRange || isCtrlRamProcessorRun) &&
                   operation.OverlapPolicy == OverlapPolicy.Reject) ||
                  isReferenceRestore;
            if (!isSupportedOperation || !hasSupportedOverlapPolicy)
            {
                AddUnsupported(
                    issues,
                    $"operation '{operation.OperationId}' is outside the Merge copy/fill/patch/transform, DP Replace replace-range plus reference-restoring copy-range, or CtrlRAM Replace processor subset",
                    operation.OperationId);
            }
        }
    }

    private static bool HasRuntimeExecutableOutputContract(CompositionProfileDefinition profile)
    {
        return profile.Output.InvalidCharacterPolicy == CompositionProfileInvalidCharacterPolicy.Reject &&
            (profile.Output.RequiredTokenIds.Count == 0 ||
             StringComparer.Ordinal.Equals(
                 profile.Output.RuleId,
                 CompiledOutputNamingRequirement.NormalFlashCodeV1RuleId) ||
             StringComparer.Ordinal.Equals(
                 profile.Output.RuleId,
                 CompiledOutputNamingRequirement.TpFirmwareV1RuleId) ||
             (profile.CompositionKind == CompositionKind.Merge &&
              StringComparer.Ordinal.Equals(profile.Experience.ExperienceId, ExperienceIds.AbMerge) &&
              StringComparer.Ordinal.Equals(profile.Output.FileNameTemplate, CompiledOutputNamingRequirement.AbCodeV1Template) &&
              profile.Output.RequiredTokenIds.SequenceEqual(
                  ["date", "dp-a", "dp-b", "ic", "tp-a", "tp-b"],
                  StringComparer.Ordinal)));
    }
}
