using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.ProfileContract.Tests;

internal sealed record CompositionProfileV2DefinitionParts(
    string ProfileId,
    string ProfileVersion,
    CompiledProfilePromotion Promotion,
    CompositionKind CompositionKind,
    IcNumberInputMode? IcNumberInputMode,
    (string ExperienceId, LayoutPolicy LayoutPolicy, InputPolicy InputPolicy) Experience,
    CompositionProfileMapBinding MapBinding,
    IReadOnlyList<CompositionInputSlotDefinition> InputSlots,
    IReadOnlyList<CompositionProfileSpace> Spaces,
    IReadOnlyList<CompositionProfileView> Views,
    IReadOnlyList<CompositionProfileMetadataBinding> MetadataBindings,
    IReadOnlyList<CompositionProfileRegionAccess> RegionAccessRules,
    IReadOnlyList<CompositionOperationDefinition> Operations,
    IReadOnlyList<ValidationRequirementDefinition> Validations,
    IReadOnlyList<CompositionProfileProcessorStage> ProcessorStages,
    CompiledOutputNamingRequirement Output,
    IReadOnlyList<string> EvidenceRefs);

internal static class CompositionProfileV2DefinitionTestData
{
    private const string FamilyHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    internal static CompositionProfileV2DefinitionParts ValidMergeParts()
    {
        return new CompositionProfileV2DefinitionParts(
            "synthetic-merge",
            "1.0.0",
            new CompiledProfilePromotion(CompiledProfilePromotionStage.Known, []),
            CompositionKind.Merge,
            null,
            Experience(ExperienceIds.StandardMerge),
            MapBinding(),
            [TpSlot()],
            [
                new InputArtifactProfileSpace("source", "tp-input", CompiledInputInstancePolicy.Singleton),
                new MutableCompositionProfileSpace(
                    "output",
                    CompositionProfileSpaceKind.OutputImage,
                    new ResolvedMapProfileCapacity(),
                    new BlankProfileInitializer(0xFF)),
            ],
            [
                new CompositionProfileView("source-view", "source", new MapRegionViewSelector("dp-code")),
                new CompositionProfileView(
                    "target-view",
                    "output",
                    new SpaceRangeViewSelector(new ByteRange(0, 16))),
            ],
            [new CompositionProfileMetadataBinding(
                "fwconfig", "source", "firmware-config", ["pid"],
                [CompositionProfileMetadataPurpose.Validation])],
            [new CompositionProfileRegionAccess(
                "dp-code", RegionAccessKind.ReadOnly, "Source region is immutable.")],
            [CompositionOperationDefinition.CopyOrReplace(
                "copy-code", 0, OverlapPolicy.Reject, "Copy source code.",
                CompositionOperationKind.CopyRange, "source-view", "target-view")],
            [new CompiledPidSanityValidation(
                "pid-valid", CompiledValidationStage.InputLoad,
                CompiledValidationSeverity.Error, "PID_INVALID",
                new CompiledValidationFieldReference("fwconfig", "pid"))],
            [],
            new CompiledOutputNamingRequirement(
                "{original-name}_merged.bin",
                false,
                CompiledOutputInvalidCharacterPolicy.ReplaceUnderscore,
                ["original-name"]),
            ["profile-evidence"]);
    }

    internal static CompositionProfileV2DefinitionParts ValidReplaceParts()
    {
        CompositionProfileV2DefinitionParts merge = ValidMergeParts();
        var reference = new CompositionInputSlotDefinition(
            "reference-input",
            "reference",
            CompiledInputArtifactClass.ReferenceImage,
            required: true,
            CompiledInputSlotCardinality.ExactlyOne,
            [".bin"],
            new ResolvedMapCapacityInputLengthDefinition(),
            new CompiledNoInputNormalization());
        return merge with
        {
            ProfileId = "synthetic-replace",
            CompositionKind = CompositionKind.Replace,
            IcNumberInputMode = IcNumberInputMode.SingleSelector,
            Experience = Experience(ExperienceIds.DpReplace),
            InputSlots = [.. merge.InputSlots, reference],
            Spaces =
            [
                merge.Spaces[0],
                new InputArtifactProfileSpace(
                    "reference",
                    "reference-input",
                    CompiledInputInstancePolicy.Singleton),
                new MutableCompositionProfileSpace(
                    "output",
                    CompositionProfileSpaceKind.OutputImage,
                    new ResolvedMapProfileCapacity(),
                    new CloneProfileInitializer("reference-input")),
            ],
            Operations = [CompositionOperationDefinition.CopyOrReplace(
                "replace-code", 0, OverlapPolicy.ReplaceExisting, "Replace source code.",
                CompositionOperationKind.ReplaceRange, "source-view", "target-view")],
        };
    }

    internal static CompositionProfileDefinition Create(CompositionProfileV2DefinitionParts parts)
    {
        var header = new CompositionProfileHeader(
            parts.Experience.ExperienceId,
            parts.Experience.LayoutPolicy,
            parts.Experience.InputPolicy,
            V2CompilationContextKind.ResolvedMap,
            parts.MapBinding,
            parts.MapBinding.FamilyId,
            parts.MapBinding.FamilyVersion,
            parts.MapBinding.FamilyContentHash,
            Array.AsReadOnly(Array.Empty<string>()),
            AllowsConditionalProcessor: false);
        return new CompositionProfileDefinition(
            parts.ProfileId,
            parts.ProfileVersion,
            parts.Promotion,
            parts.CompositionKind,
            parts.IcNumberInputMode,
            header,
            parts.InputSlots,
            parts.Spaces,
            parts.Views,
            parts.MetadataBindings,
            parts.RegionAccessRules,
            parts.Operations,
            parts.Validations,
            parts.ProcessorStages,
            parts.Output,
            parts.EvidenceRefs);
    }

    internal static (string ExperienceId, LayoutPolicy LayoutPolicy, InputPolicy InputPolicy) Experience(
        string experienceId)
    {
        return (experienceId, LayoutPolicy.Fixed, InputPolicy.Fixed);
    }

    internal static CompositionProfileMapBinding MapBinding(
        IEnumerable<string>? regionIds = null,
        IEnumerable<string>? structureIds = null)
    {
        return new CompositionProfileMapBinding(
            "synthetic-family",
            "1.0.0",
            FamilyHash,
            ["standard-map"],
            regionIds ?? ["dp-code"],
            structureIds ?? ["firmware-config"],
            []);
    }

    internal static CompositionInputSlotDefinition TpSlot()
    {
        return new CompositionInputSlotDefinition(
            "tp-input",
            "tp",
            CompiledInputArtifactClass.TpFirmware,
            required: true,
            CompiledInputSlotCardinality.ExactlyOne,
            [".bin"],
            new CompiledTpMaximum256KInputLengthRequirement(),
            new CompiledNoInputNormalization());
    }
}
