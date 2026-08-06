using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests fail-closed internal references in map-independent v2 definitions.</summary>
public sealed class CompositionProfileV2DefinitionGraphTests
{
    /// <summary>Verifies spaces and clone initializers reference declared input slots only.</summary>
    [Fact]
    public void DefinitionRejectsUnknownAndOrphanSlotReferences()
    {
        CompositionProfileV2DefinitionParts parts = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        var unknown = new InputArtifactProfileSpace(
            "source",
            "unknown-slot",
            CompiledInputInstancePolicy.Singleton);
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Spaces = [unknown, parts.Spaces[1]] }));

        var orphan = new CompositionProfileInputSlot(
            "orphan-input",
            "auxiliary",
            CompiledInputArtifactClass.Auxiliary,
            required: false,
            CompiledInputSlotCardinality.ZeroOrOne,
            [".bin"],
            new ExactBytesLengthRule(16),
            new CompiledNoInputNormalization());
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { InputSlots = [parts.InputSlots[0], orphan] }));
    }

    /// <summary>Verifies views reference spaces and declare every canonical region requirement.</summary>
    [Fact]
    public void DefinitionRejectsInvalidViewGraphAndRegionRequirements()
    {
        CompositionProfileV2DefinitionParts parts = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        var unknownSpace = new CompositionProfileView(
            "source-view",
            "unknown-space",
            new MapRegionViewSelector("dp-code"));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Views = [unknownSpace, parts.Views[1]] }));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { MapBinding = CompositionProfileV2DefinitionTestData.MapBinding(regionIds: ["other"]) }));
    }

    /// <summary>Verifies metadata and access references remain inside declared map requirements.</summary>
    [Fact]
    public void DefinitionRejectsInvalidMetadataAndAccessGraph()
    {
        CompositionProfileV2DefinitionParts parts = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with
            {
                MapBinding = CompositionProfileV2DefinitionTestData.MapBinding(structureIds: ["other"]),
            }));

        var unknownSpace = new CompositionProfileMetadataBinding(
            "fwconfig",
            "unknown-space",
            "firmware-config",
            ["pid"],
            [CompositionProfileMetadataPurpose.Validation]);
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { MetadataBindings = [unknownSpace] }));

        var unknownAccess = new CompositionProfileRegionAccess(
            "other",
            RegionAccessKind.ReadOnly,
            "Other region is read-only.");
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { RegionAccessRules = [unknownAccess] }));
    }

    /// <summary>Verifies operations reference known views and can mutate only engine-owned spaces.</summary>
    [Fact]
    public void DefinitionRejectsInvalidOperationViewGraph()
    {
        CompositionProfileV2DefinitionParts parts = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        var unknownSource = new CopyOrReplaceProfileOperation(
            "copy-code", 0, OverlapPolicy.Reject, "copy", CompositionProfileOperationKind.CopyRange,
            "unknown-view", "target-view");
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Operations = [unknownSource] }));

        var immutableTarget = new FillRangeProfileOperation(
            "fill-source", 0, OverlapPolicy.Reject, "fill", "source-view", 0);
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Operations = [immutableTarget] }));
    }

    /// <summary>Verifies validations reference declared bindings, selected fields, and views.</summary>
    [Fact]
    public void DefinitionRejectsInvalidValidationGraph()
    {
        CompositionProfileV2DefinitionParts parts = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        var unknownBinding = new PidSanityProfileValidation(
            "pid-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "PID_INVALID",
            new CompositionProfileMetadataFieldReference("unknown", "pid"));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Validations = [unknownBinding] }));

        var unselectedField = new PidSanityProfileValidation(
            "pid-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "PID_INVALID",
            new CompositionProfileMetadataFieldReference("fwconfig", "chip-number"));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Validations = [unselectedField] }));

        var unknownView = new ViewByteAssertionProfileValidation(
            "header-valid",
            CompiledValidationStage.FinalOutput,
            CompiledValidationSeverity.Error,
            "HEADER_INVALID",
            "unknown-view",
            new CompositionProfileByteValue([0]));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Validations = [unknownView] }));
    }

    /// <summary>Verifies processor stages are invoked and all authority views use the target space.</summary>
    [Fact]
    public void DefinitionRejectsInvalidProcessorGraph()
    {
        CompositionProfileV2DefinitionParts parts = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        LegacyCombinerProfileProcessorStage stage = LegacyStage();
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { ProcessorStages = [stage] }));

        var unknownRun = new RunProcessorProfileOperation(
            "postbuild", 1, OverlapPolicy.ReplaceExisting, "postbuild", "unknown-stage");
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Operations = [parts.Operations[0], unknownRun] }));

        var run = new RunProcessorProfileOperation(
            "postbuild", 1, OverlapPolicy.ReplaceExisting, "postbuild", "legacy-postbuild");
        var wrongAuthority = new LegacyCombinerProfileProcessorStage(
            "legacy-postbuild",
            "combiner-1-13",
            "profile",
            "output",
            CompositionProfileProcessorPurpose.Relocation,
            CompositionProfileIntegrityDisposition.None,
            ["source-view"],
            ["target-view"],
            [],
            [],
            "combiner-evidence");
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Operations = [parts.Operations[0], run], ProcessorStages = [wrongAuthority] }));

        var transformInput = new LegacyCombinerProfileProcessorStage(
            "legacy-postbuild",
            "combiner-1-13",
            "profile",
            "source",
            CompositionProfileProcessorPurpose.Relocation,
            CompositionProfileIntegrityDisposition.None,
            ["source-view"],
            ["source-view"],
            [],
            [],
            "combiner-evidence");
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Operations = [parts.Operations[0], run], ProcessorStages = [transformInput] }));

        var crc = new CrcWorkerProfileProcessorStage(
            "crc-check",
            "1.0.0",
            "display-crc",
            "source",
            ["source-view"]);
        var runCrc = new RunProcessorProfileOperation(
            "verify-crc", 1, OverlapPolicy.Reject, "Verify source CRC.", "crc-check");
        CompositionProfileDefinition calculateInput = CompositionProfileV2DefinitionTestData.Create(
            parts with { Operations = [parts.Operations[0], runCrc], ProcessorStages = [crc] });

        CompositionProfileDefinition valid = CompositionProfileV2DefinitionTestData.Create(
            parts with { Operations = [parts.Operations[0], run], ProcessorStages = [stage] });
        _ = Assert.Single(valid.ProcessorStages);
    }

    /// <summary>Verifies output required-token declarations are present in the template.</summary>
    [Fact]
    public void DefinitionRejectsMissingOutputTemplateTokens()
    {
        CompositionProfileV2DefinitionParts parts = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        var output = new CompositionProfileOutput(
            "merged.bin",
            false,
            CompiledOutputInvalidCharacterPolicy.Reject,
            ["original-name"]);

        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Output = output }));
    }

    private static LegacyCombinerProfileProcessorStage LegacyStage()
    {
        return new LegacyCombinerProfileProcessorStage(
            "legacy-postbuild",
            "combiner-1-13",
            "profile",
            "output",
            CompositionProfileProcessorPurpose.Relocation,
            CompositionProfileIntegrityDisposition.None,
            ["target-view"],
            ["target-view"],
            [],
            [],
            "combiner-evidence");
    }
}
