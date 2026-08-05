using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

internal sealed record CompositionProfileV2DefinitionParts(
    string ProfileId,
    string ProfileVersion,
    CompositionProfilePromotion Promotion,
    CompositionKind CompositionKind,
    IcNumberInputMode? IcNumberInputMode,
    CompositionProfileExperience Experience,
    CompositionProfileMapBinding MapBinding,
    IReadOnlyList<CompositionProfileInputSlot> InputSlots,
    IReadOnlyList<CompositionProfileSpace> Spaces,
    IReadOnlyList<CompositionProfileView> Views,
    IReadOnlyList<CompositionProfileMetadataBinding> MetadataBindings,
    IReadOnlyList<CompositionProfileRegionAccess> RegionAccessRules,
    IReadOnlyList<CompositionProfileOperation> Operations,
    IReadOnlyList<CompositionProfileValidation> Validations,
    IReadOnlyList<CompositionProfileProcessorStage> ProcessorStages,
    CompositionProfileOutput Output,
    IReadOnlyList<string> EvidenceRefs);

internal static class CompositionProfileV2DefinitionTestData
{
    private const string FamilyHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    internal static CompositionProfileV2DefinitionParts ValidMergeParts()
    {
        return new CompositionProfileV2DefinitionParts(
            "synthetic-merge",
            "1.0.0",
            new CompositionProfilePromotion(CompositionProfilePromotionStage.Known, []),
            CompositionKind.Merge,
            null,
            Experience(ExperienceIds.StandardMerge),
            MapBinding(),
            [TpSlot()],
            [
                new InputArtifactProfileSpace("source", "tp-input", CompositionProfileInstancePolicy.Singleton),
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
            [new CopyOrReplaceProfileOperation(
                "copy-code", 0, OverlapPolicy.Reject, "Copy source code.",
                CompositionProfileOperationKind.CopyRange, "source-view", "target-view")],
            [new PidSanityProfileValidation(
                "pid-valid", CompositionProfileValidationStage.InputLoad,
                CompositionProfileValidationSeverity.Error, "PID_INVALID",
                new CompositionProfileMetadataFieldReference("fwconfig", "pid"))],
            [],
            new CompositionProfileOutput(
                "{original-name}_merged.bin",
                false,
                CompositionProfileInvalidCharacterPolicy.ReplaceUnderscore,
                ["original-name"]),
            ["profile-evidence"]);
    }

    internal static CompositionProfileV2DefinitionParts ValidReplaceParts()
    {
        CompositionProfileV2DefinitionParts merge = ValidMergeParts();
        var reference = new CompositionProfileInputSlot(
            "reference-input",
            "reference",
            CompositionProfileArtifactClass.ReferenceImage,
            required: true,
            CompositionProfileSlotCardinality.ExactlyOne,
            [".bin"],
            new ExactResolvedMapCapacityLengthRule(),
            new NoInputNormalization());
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
                    CompositionProfileInstancePolicy.Singleton),
                new MutableCompositionProfileSpace(
                    "output",
                    CompositionProfileSpaceKind.OutputImage,
                    new ResolvedMapProfileCapacity(),
                    new CloneProfileInitializer("reference-input")),
            ],
            Operations = [new CopyOrReplaceProfileOperation(
                "replace-code", 0, OverlapPolicy.ReplaceExisting, "Replace source code.",
                CompositionProfileOperationKind.ReplaceRange, "source-view", "target-view")],
        };
    }

    internal static CompositionProfileDefinition Create(CompositionProfileV2DefinitionParts parts)
    {
        return new CompositionProfileDefinition(
            parts.ProfileId,
            parts.ProfileVersion,
            parts.Promotion,
            parts.CompositionKind,
            parts.IcNumberInputMode,
            parts.Experience,
            parts.MapBinding,
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

    internal static CompositionProfileExperience Experience(string experienceId)
    {
        return new CompositionProfileExperience(
            experienceId,
            AudienceKind.System,
            LayoutPolicy.Fixed,
            InputPolicy.Fixed,
            CompositionProfileTopologyAuthoring.Hidden,
            $"experience.{experienceId}");
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

    internal static CompositionProfileInputSlot TpSlot()
    {
        return new CompositionProfileInputSlot(
            "tp-input",
            "tp",
            CompositionProfileArtifactClass.TpFirmware,
            required: true,
            CompositionProfileSlotCardinality.ExactlyOne,
            [".bin"],
            new SourceViewCoverageLengthRule(
                maximumOuterLength: CompiledTpMaximum256KInputLengthRequirement.MaximumBytes),
            new NoInputNormalization());
    }
}
