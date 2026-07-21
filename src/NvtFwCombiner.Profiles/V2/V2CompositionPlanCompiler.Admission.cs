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
            (profile.Output.RequiredTokenIds.Count != 0 ||
             profile.Output.InvalidCharacterPolicy != CompositionProfileInvalidCharacterPolicy.Reject))
        {
            AddUnsupported(issues, "supported profiles require a token-free reject output template until token rendering is lowered");
        }

        if (profile.MetadataBindings.Count != 0 || profile.Validations.Count != 0)
        {
            AddUnsupported(issues, "metadata bindings and validations are not lowered in this slice");
        }

        MutableCompositionProfileSpace output = AssertOutputSpace(profile);
        ValidateMapBoundOutputShape(profile, output, issues);

        foreach (InputArtifactProfileSpace inputSpace in profile.Spaces.OfType<InputArtifactProfileSpace>())
        {
            CompositionProfileInputSlot? slot = profile.InputSlots.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.SlotId, inputSpace.SlotId));
            if (inputSpace.InstancePolicy != CompositionProfileInstancePolicy.Singleton ||
                slot is null ||
                !slot.Required ||
                slot.Cardinality != CompositionProfileSlotCardinality.ExactlyOne ||
                slot.Normalization is not (NoInputNormalization or PadShorterInputNormalization or TruncateCtrlRamInputNormalization) ||
                !IsCurrentInputLengthRuleSupported(slot))
            {
                AddUnsupported(
                    issues,
                    $"input space '{inputSpace.SpaceId}' must bind one required singleton exact-map-capacity, normal-DP, tp-maximum-256k, exact TP, or truncatable CtrlRAM slot with approved normalization");
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
}
