using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private static void ValidateSupportedProfile(CompositionProfileDefinition profile, List<CompositionIssue> issues)
    {
        if (profile.Promotion.Stage < CompiledProfilePromotionStage.Compilable)
        {
            AddUnsupported(issues, "promotion stage must be Compilable or later");
        }

        if (profile.Promotion.Stage == CompiledProfilePromotionStage.Supported &&
            !profile.Output.AllowsRuntimeExecution(profile.CompositionKind))
        {
            AddUnsupported(issues, "supported profiles require a typed reject output renderer admitted for the declared composition kind");
        }

        if (profile.Validations.Any(static validation =>
                validation is not SourceViewNonUniformValidationDefinition))
        {
            AddUnsupported(issues, "only warning-only non-uniform-region validations are lowered in this slice");
        }

        MutableCompositionProfileSpace output = AssertOutputSpace(profile);
        ValidateMapBoundOutputShape(profile, output, issues);

        foreach (InputArtifactProfileSpace inputSpace in profile.Spaces.OfType<InputArtifactProfileSpace>())
        {
            CompositionInputSlotDefinition? slot = profile.InputSlots.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.SlotId, inputSpace.SlotId));
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
            if (inputSpace.InstancePolicy != CompiledInputInstancePolicy.Singleton ||
                slot is null ||
                (!requiredSingleton && !groupedOptionalSingleton) ||
                slot.Normalization is not (CompiledNoInputNormalization or CompiledPadShorterInputNormalization or CompiledTruncateCtrlRamInputNormalization) ||
                (slot.LengthRequirement is SourceViewCoverageInputLengthDefinition { RequiredEndExclusive: not null } &&
                 profile.CompositionKind != CompositionKind.Merge) ||
                !IsCurrentInputLengthRequirementSupported(slot))
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

        foreach (CompositionOperationDefinition operation in profile.Operations)
        {
            bool isCopyRange = operation.Kind == CompositionOperationKind.CopyRange;
            bool isReplaceRange = operation.Kind == CompositionOperationKind.ReplaceRange &&
                IsReplacePayloadInputSource(profile, operation);
            bool isProcessorRun = operation.Kind == CompositionOperationKind.RunExternalProcessor;
            bool isReferenceRestore = operation.Kind == CompositionOperationKind.CopyRange &&
                operation.OverlapPolicy == OverlapPolicy.ReplaceExisting &&
                StringComparer.Ordinal.Equals(
                    replaceReferenceSourceSpaceId,
                    profile.Views.Single(view => StringComparer.Ordinal.Equals(view.ViewId,
                        operation.SourceViewId)).SpaceId);
            bool isSupportedOperation = profile.CompositionKind == CompositionKind.Merge
                ? operation.Kind is CompositionOperationKind.CopyRange or
                    CompositionOperationKind.FillRange or
                    CompositionOperationKind.PatchScalar or
                    CompositionOperationKind.TransformScalar or
                    CompositionOperationKind.RunExternalProcessor
                : isReplaceRange || isReferenceRestore || isProcessorRun;
            bool hasSupportedOverlapPolicy = profile.CompositionKind == CompositionKind.Merge
                ? operation.OverlapPolicy == OverlapPolicy.Reject ||
                  ((isCopyRange || isProcessorRun) && operation.OverlapPolicy == OverlapPolicy.ReplaceExisting)
                : ((isReplaceRange || isProcessorRun) &&
                   operation.OverlapPolicy == OverlapPolicy.Reject) ||
                  isReferenceRestore;
            if (!isSupportedOperation || !hasSupportedOverlapPolicy)
            {
                AddUnsupported(
                    issues,
                    $"operation '{operation.OperationId}' is outside the closed Merge or reference-clone Replace operation subset",
                    operation.OperationId);
            }
        }
    }

}
