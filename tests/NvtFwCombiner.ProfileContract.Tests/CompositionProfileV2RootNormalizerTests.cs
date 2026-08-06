using System.Text.Json;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests complete composition-profile-v2 DTO normalization and graph assembly.</summary>
public sealed class CompositionProfileV2RootNormalizerTests
{
    private const string FamilyHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    /// <summary>Verifies a complete Merge document becomes one immutable map-independent definition.</summary>
    [Fact]
    public void RootNormalizerBuildsCompleteMergeDefinition()
    {
        CompositionProfileDefinition definition = CompositionProfileNormalizer.Normalize(ValidMerge());

        Assert.Equal("synthetic-merge", definition.ProfileId);
        Assert.Equal(CompositionKind.Merge, definition.CompositionKind);
        Assert.Null(definition.IcNumberInputMode);
        Assert.Equal(CompiledProfilePromotionStage.Known, definition.Promotion.Stage);
        Assert.Equal(ExperienceIds.StandardMerge, definition.ExperienceId);
        Assert.Equal("standard-map", Assert.Single(definition.MapBinding.MapIds));
        _ = Assert.IsType<CompiledTpMaximum256KInputLengthRequirement>(
            Assert.Single(definition.InputSlots).LengthRequirement);
        Assert.Equal(2, definition.Spaces.Count);
        Assert.Equal(2, definition.Views.Count);
        Assert.Equal(CompositionOperationKind.CopyRange, Assert.Single(definition.Operations).Kind);
        _ = Assert.IsType<CompiledPidSanityValidation>(Assert.Single(definition.Validations));
        Assert.Empty(definition.ProcessorStages);
        Assert.Equal(["profile-evidence"], definition.EvidenceRefs);
    }

    /// <summary>Verifies Replace assembly requires and clones one reference-image input.</summary>
    [Fact]
    public void RootNormalizerBuildsCompleteReplaceDefinition()
    {
        CompositionProfileDefinition definition = CompositionProfileNormalizer.Normalize(ValidReplace());
        MutableCompositionProfileSpace output = Assert.Single(
            definition.Spaces.OfType<MutableCompositionProfileSpace>(),
            static space => space.Kind == CompositionProfileSpaceKind.OutputImage);

        Assert.Equal(CompositionKind.Replace, definition.CompositionKind);
        Assert.Equal(IcNumberInputMode.SingleSelector, definition.IcNumberInputMode);
        Assert.Equal(ExperienceIds.DpReplace, definition.ExperienceId);
        Assert.Equal(2, definition.InputSlots.Count);
        Assert.Equal("reference-input", Assert.IsType<CloneProfileInitializer>(output.Initializer).SourceSlotId);
    }

    /// <summary>Verifies every closed Replace selector token retains its exact profile-owned mode.</summary>
    [Theory]
    [InlineData("single-selector", IcNumberInputMode.SingleSelector)]
    [InlineData("cascade-selector", IcNumberInputMode.CascadeSelector)]
    [InlineData("numeric-selector", IcNumberInputMode.NumericSelector)]
    public void RootNormalizerMapsEveryReplaceIcNumberInputMode(
        string token,
        IcNumberInputMode expectedMode)
    {
        CompositionProfileDefinition definition = CompositionProfileNormalizer.Normalize(
            ValidReplace() with { IcNumberInputMode = token });

        Assert.Equal(expectedMode, definition.IcNumberInputMode);
    }

    /// <summary>Verifies the normalizer admits each pinned profile schema version before section assembly.</summary>
    [Theory]
    [InlineData("2.0")]
    [InlineData("2.1")]
    [InlineData("2.2")]
    [InlineData("2.3")]
    public void RootNormalizerAcceptsPinnedSchemaVersions(string schemaVersion)
    {
        CompositionProfileDefinition definition = CompositionProfileNormalizer.Normalize(
            ValidMerge() with { SchemaVersion = schemaVersion });

        Assert.Equal("synthetic-merge", definition.ProfileId);
    }

    /// <summary>Verifies schema 2.3 retains the 2.2 versioned Combiner binding grammar through full graph assembly.</summary>
    [Fact]
    public void RootNormalizerBuildsV23ProfileWithPublishedCombinerBinding()
    {
        CompositionProfileDocument valid = ValidMerge();
        CompositionProfileDefinition definition = CompositionProfileNormalizer.Normalize(valid with
        {
            SchemaVersion = "2.3",
            Operations = [CopyOperation("copy-range"), RunProcessorOperation()],
            ProcessorStages = [LegacyCombinerStage()],
        });

        LegacyCombinerProfileProcessorStage stage = Assert.IsType<LegacyCombinerProfileProcessorStage>(
            Assert.Single(definition.ProcessorStages));
        Assert.Equal("legacy-combiner-1.13.0", stage.ToolBindingId);
    }

    /// <summary>Verifies unsupported root schema and composition tokens fail before section or graph assembly.</summary>
    [Fact]
    public void RootNormalizerRejectsUnsupportedSchemaAndCompositionKindWithPaths()
    {
        CompositionProfileNormalizationException schema = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(ValidMerge() with { SchemaVersion = "3.0" }));
        CompositionProfileNormalizationException kind = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(ValidMerge() with { CompositionKind = "future" }));

        Assert.Equal("schemaVersion", schema.Path);
        Assert.Equal("compositionKind", kind.Path);
    }

    /// <summary>Verifies IC-number selector authority is explicit for Replace and absent for Merge.</summary>
    [Fact]
    public void RootNormalizerEnforcesCompositionOwnedIcNumberInputMode()
    {
        CompositionProfileNormalizationException unknown = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(ValidMerge() with { IcNumberInputMode = "future" }));
        CompositionProfileNormalizationException merge = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(ValidMerge() with { IcNumberInputMode = "single-selector" }));
        CompositionProfileNormalizationException replace = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(ValidReplace() with { IcNumberInputMode = null }));

        Assert.Equal("icNumberInputMode", unknown.Path);
        Assert.Equal("$", merge.Path);
        Assert.Equal("$", replace.Path);
    }

    /// <summary>Verifies missing top-level objects and arrays retain their field paths.</summary>
    [Fact]
    public void RootNormalizerRejectsMissingSectionsWithPaths()
    {
        CompositionProfileNormalizationException promotion = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(ValidMerge() with { Promotion = null! }));
        CompositionProfileNormalizationException inputs = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(ValidMerge() with { InputSlots = null! }));
        CompositionProfileNormalizationException output = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(ValidMerge() with { Output = null! }));
        CompositionProfileNormalizationException evidence = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(ValidMerge() with { EvidenceRefs = null! }));

        Assert.Equal("promotion", promotion.Path);
        Assert.Equal("inputSlots", inputs.Path);
        Assert.Equal("output", output.Path);
        Assert.Equal("evidenceRefs", evidence.Path);
    }

    /// <summary>Verifies array element errors preserve their exact indexed source paths.</summary>
    [Fact]
    public void RootNormalizerPreservesIndexedSectionPaths()
    {
        CompositionProfileDocument valid = ValidMerge();
        var invalidView = new CompositionProfileViewDocument(
            "invalid-view",
            "source",
            new CompositionProfileViewSelectorDocument("future"));
        CompositionProfileNormalizationException selector = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(valid with { Views = [.. valid.Views, invalidView] }));
        CompositionProfileNormalizationException nullElement = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(valid with { Validations = [null!] }));

        Assert.Equal("views[2].selector.kind", selector.Path);
        Assert.Equal("validations[0]", nullElement.Path);
    }

    /// <summary>Verifies cross-section graph failures remain attributed to the whole definition.</summary>
    [Fact]
    public void RootNormalizerRejectsInvalidReferenceGraphAtRootPath()
    {
        CompositionProfileDocument valid = ValidMerge();
        CompositionProfileOperationDocument operation = Assert.Single(valid.Operations) with
        {
            TargetViewId = "unknown-view",
        };

        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(valid with { Operations = [operation] }));

        Assert.Equal("$", exception.Path);
        _ = Assert.IsType<ArgumentException>(exception.InnerException, exactMatch: false);
    }

    private static CompositionProfileDocument ValidMerge()
    {
        return new CompositionProfileDocument(
            "2.0",
            "synthetic-merge",
            "1.0.0",
            new CompositionProfilePromotionDocument("known", []),
            "merge",
            null,
            Experience(ExperienceIds.StandardMerge),
            MapBinding(),
            [TpInput()],
            [
                new CompositionProfileSpaceDocument(
                    "source",
                    "input-artifact",
                    SlotId: "tp-input",
                    InstancePolicy: "singleton"),
                new CompositionProfileSpaceDocument(
                    "output",
                    "output-image",
                    Capacity: new CompositionProfileCapacityDocument("resolved-map"),
                    Initializer: new CompositionProfileInitializerDocument("blank", Number("255"))),
            ],
            Views(),
            [new CompositionProfileMetadataBindingDocument(
                "fwconfig", "source", "firmware-config", ["pid"], ["validation"])],
            [new CompositionProfileRegionAccessRuleDocument(
                "dp-code", "read-only", "Source region is immutable.")],
            [CopyOperation("copy-range")],
            [new CompositionProfileValidationDocument(
                "pid-valid",
                "input-load",
                "error",
                "PID_INVALID",
                "pid-sanity",
                Field: new CompositionProfileMetadataFieldReferenceDocument("fwconfig", "pid"))],
            [],
            new CompositionProfileOutputDocument(
                "{original-name}_merged.bin",
                false,
                "replace-underscore",
                ["original-name"]),
            ["profile-evidence"]);
    }

    private static CompositionProfileDocument ValidReplace()
    {
        CompositionProfileDocument merge = ValidMerge();
        var reference = new CompositionProfileInputSlotDocument(
            "reference-input",
            "reference",
            "reference-image",
            true,
            "exactly-one",
            [".bin"],
            new CompositionProfileInputAcceptanceDocument(
                new CompositionProfileLengthRuleDocument("exact-resolved-map-capacity"),
                new CompositionProfileInputNormalizationDocument("none")));
        return merge with
        {
            ProfileId = "synthetic-replace",
            CompositionKind = "replace",
            IcNumberInputMode = "single-selector",
            Experience = Experience(ExperienceIds.DpReplace),
            InputSlots = [.. merge.InputSlots, reference],
            Spaces =
            [
                merge.Spaces[0],
                new CompositionProfileSpaceDocument(
                    "reference",
                    "input-artifact",
                    SlotId: "reference-input",
                    InstancePolicy: "singleton"),
                new CompositionProfileSpaceDocument(
                    "output",
                    "output-image",
                    Capacity: new CompositionProfileCapacityDocument("resolved-map"),
                    Initializer: new CompositionProfileInitializerDocument(
                        "clone",
                        SourceSlotId: "reference-input")),
            ],
            Operations = [CopyOperation("replace-range")],
        };
    }

    private static CompositionProfileExperienceDocument Experience(string experienceId)
    {
        return new CompositionProfileExperienceDocument(
            experienceId,
            "system",
            "fixed",
            "fixed",
            "hidden",
            $"experience.{experienceId}");
    }

    private static CompositionProfileMapBindingDocument MapBinding()
    {
        return new CompositionProfileMapBindingDocument(
            "synthetic-family",
            "1.0.0",
            FamilyHash,
            ["standard-map"],
            ["dp-code"],
            ["firmware-config"],
            []);
    }

    private static CompositionProfileInputSlotDocument TpInput()
    {
        return new CompositionProfileInputSlotDocument(
            "tp-input",
            "tp",
            "tp-firmware",
            true,
            "exactly-one",
            [".bin"],
            new CompositionProfileInputAcceptanceDocument(
                new CompositionProfileLengthRuleDocument(
                    "tp-maximum-256k",
                    MaximumBytes: Number("262144")),
                new CompositionProfileInputNormalizationDocument("none")));
    }

    private static CompositionProfileViewDocument[] Views()
    {
        return
        [
            new CompositionProfileViewDocument(
                "source-view",
                "source",
                new CompositionProfileViewSelectorDocument("map-region", RegionId: "dp-code")),
            new CompositionProfileViewDocument(
                "target-view",
                "output",
                new CompositionProfileViewSelectorDocument(
                    "space-range",
                    Range: new CompositionProfileRelativeRangeDocument(Number("0"), Number("16")))),
        ];
    }

    private static CompositionProfileOperationDocument CopyOperation(string kind)
    {
        return new CompositionProfileOperationDocument(
            "copy-code",
            Number("0"),
            "reject",
            "Copy source bytes.",
            kind,
            SourceViewId: "source-view",
            TargetViewId: "target-view");
    }

    private static CompositionProfileOperationDocument RunProcessorOperation()
    {
        return new CompositionProfileOperationDocument(
            "run-combiner",
            Number("1"),
            "reject",
            "Run the staged Combiner.",
            "run-processor",
            ProcessorStageId: "legacy-postbuild");
    }

    private static CompositionProfileProcessorStageDocument LegacyCombinerStage()
    {
        return new CompositionProfileProcessorStageDocument(
            "legacy-postbuild",
            "legacy-combiner-v1",
            "output",
            "transform",
            "relocation",
            "none",
            ["target-view"],
            ["target-view"],
            "fail-closed",
            ToolBindingId: "legacy-combiner-1.13.0",
            InvocationProfileId: "synthetic-profile",
            StagedSourceBindings: [],
            EvidenceRef: "processor-evidence",
            StagedArtifactBindings: []);
    }

    private static JsonElement Number(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
