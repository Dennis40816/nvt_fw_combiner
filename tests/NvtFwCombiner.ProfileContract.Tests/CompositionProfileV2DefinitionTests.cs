using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests complete map-independent v2 profile aggregation invariants.</summary>
public sealed class CompositionProfileV2DefinitionTests
{
    /// <summary>Verifies valid Merge and Replace definitions retain one unconditional output model.</summary>
    [Fact]
    public void ValidMergeAndReplaceDefinitionsUseRequiredOutputInitializer()
    {
        CompositionProfileDefinition merge = CompositionProfileV2DefinitionTestData.Create(
            CompositionProfileV2DefinitionTestData.ValidMergeParts());
        CompositionProfileDefinition replace = CompositionProfileV2DefinitionTestData.Create(
            CompositionProfileV2DefinitionTestData.ValidReplaceParts());

        Assert.Equal(CompositionKind.Merge, merge.CompositionKind);
        _ = Assert.IsType<BlankProfileInitializer>(Output(merge).Initializer);
        Assert.Equal(CompositionKind.Replace, replace.CompositionKind);
        _ = Assert.IsType<CloneProfileInitializer>(Output(replace).Initializer);
        Assert.Equal("synthetic-family", merge.MapBinding.FamilyId);
        Assert.Equal("{original-name}_merged.bin", merge.Output.FileNameTemplate);
    }

    /// <summary>Verifies caller collections are snapshotted and operations normalize by unique sequence.</summary>
    [Fact]
    public void DefinitionSnapshotsCollectionsAndOrdersOperations()
    {
        CompositionProfileV2DefinitionParts parts = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        var slots = parts.InputSlots.ToList();
        var operations = new List<CompositionOperationDefinition>
        {
            CompositionOperationDefinition.FillRange(
                "fill-target", 2, OverlapPolicy.ReplaceExisting, "Fill target.", "target-view", 0xFF),
            parts.Operations[0],
        };
        parts = parts with { InputSlots = slots, Operations = operations };

        CompositionProfileDefinition definition = CompositionProfileV2DefinitionTestData.Create(parts);
        slots.Clear();
        operations.Clear();

        _ = Assert.Single(definition.InputSlots);
        Assert.Equal(["copy-code", "fill-target"], definition.Operations.Select(static operation => operation.OperationId));
        Assert.Equal(["profile-evidence"], definition.EvidenceRefs);
    }

    /// <summary>Verifies root collections reject missing values, duplicate ids, and ambiguous sequences.</summary>
    [Fact]
    public void DefinitionRejectsInvalidRootCollections()
    {
        CompositionProfileV2DefinitionParts parts = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { InputSlots = [] }));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Spaces = [parts.Spaces[0]] }));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Views = [] }));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Operations = [] }));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { EvidenceRefs = [] }));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { InputSlots = [parts.InputSlots[0], parts.InputSlots[0]] }));

        var duplicateSequence = CompositionOperationDefinition.FillRange(
            "fill-target", 0, OverlapPolicy.ReplaceExisting, "Fill target.", "target-view", 0xFF);
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            parts with { Operations = [parts.Operations[0], duplicateSequence] }));
    }

    /// <summary>Verifies exactly one output exists and Merge/Replace initialization cannot cross.</summary>
    [Fact]
    public void DefinitionRejectsOutputShapeContradictions()
    {
        CompositionProfileV2DefinitionParts merge = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        MutableCompositionProfileSpace output = Output(CompositionProfileV2DefinitionTestData.Create(merge));
        var secondOutput = new MutableCompositionProfileSpace(
            "other-output",
            CompositionProfileSpaceKind.OutputImage,
            new FixedProfileCapacity(16),
            new BlankProfileInitializer(0));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            merge with { Spaces = [.. merge.Spaces, secondOutput] }));

        var cloneOutput = new MutableCompositionProfileSpace(
            "output",
            CompositionProfileSpaceKind.OutputImage,
            output.Capacity,
            new CloneProfileInitializer("tp-input"));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            merge with { Spaces = [merge.Spaces[0], cloneOutput] }));

        CompositionProfileV2DefinitionParts replace = CompositionProfileV2DefinitionTestData.ValidReplaceParts();
        var blankOutput = new MutableCompositionProfileSpace(
            "output",
            CompositionProfileSpaceKind.OutputImage,
            new ResolvedMapProfileCapacity(),
            new BlankProfileInitializer(0xFF));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            replace with { Spaces = [replace.Spaces[0], replace.Spaces[1], blankOutput] }));
    }

    /// <summary>Verifies clone sources are required singleton reference images for Replace output.</summary>
    [Fact]
    public void DefinitionRejectsInvalidCloneSourceSlots()
    {
        CompositionProfileV2DefinitionParts replace = CompositionProfileV2DefinitionTestData.ValidReplaceParts();
        var optionalReference = new CompositionInputSlotDefinition(
            "reference-input",
            "reference",
            CompiledInputArtifactClass.ReferenceImage,
            required: false,
            CompiledInputSlotCardinality.ZeroOrOne,
            [".bin"],
            new ResolvedMapCapacityInputLengthDefinition(),
            new CompiledNoInputNormalization());
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            replace with { InputSlots = [replace.InputSlots[0], optionalReference] }));

        MutableCompositionProfileSpace output = replace.Spaces.OfType<MutableCompositionProfileSpace>().Single();
        var wrongSource = new MutableCompositionProfileSpace(
            output.SpaceId,
            output.Kind,
            output.Capacity,
            new CloneProfileInitializer("tp-input"));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            replace with { Spaces = [replace.Spaces[0], replace.Spaces[1], wrongSource] }));
    }

    /// <summary>Verifies profile-level padding and truncation restrictions match the contract.</summary>
    [Fact]
    public void DefinitionEnforcesProfileLevelInputNormalizationPolicy()
    {
        CompositionProfileV2DefinitionParts merge = CompositionProfileV2DefinitionTestData.ValidMergeParts();
        var paddedDp = new CompositionInputSlotDefinition(
            "tp-input",
            "dp",
            CompiledInputArtifactClass.DpFirmware,
            required: true,
            CompiledInputSlotCardinality.ExactlyOne,
            [".bin"],
            new ResolvedMapCapacityInputLengthDefinition(),
            new CompiledPadShorterInputNormalization(0xFF, "padding-evidence"));
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            merge with { InputSlots = [paddedDp] }));

        CompositionProfileV2DefinitionParts replace = CompositionProfileV2DefinitionTestData.ValidReplaceParts();
        var truncatedCtrlRam = new CompositionInputSlotDefinition(
            "tp-input",
            "ctrlram",
            CompiledInputArtifactClass.CtrlRamReplacement,
            required: true,
            CompiledInputSlotCardinality.ExactlyOne,
            [".bin"],
            new CompiledBoundedInputLengthRequirement(1, 16),
            new CompiledTruncateCtrlRamInputNormalization("CTRLRAM_TRUNCATED", "truncation-evidence"));
        _ = CompositionProfileV2DefinitionTestData.Create(
            replace with { InputSlots = [truncatedCtrlRam, replace.InputSlots[1]] });

        var declaredPrefix = new CompositionInputSlotDefinition(
            "tp-input",
            "tp",
            CompiledInputArtifactClass.TpFirmware,
            required: true,
            CompiledInputSlotCardinality.ExactlyOne,
            [".bin"],
            new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
                16,
                [16],
                "INPUT_SHORT",
                "INPUT_OUTER_LENGTH"),
            new CompiledNoInputNormalization());
        _ = Assert.Throws<ArgumentException>(() => CompositionProfileV2DefinitionTestData.Create(
            replace with { InputSlots = [declaredPrefix, replace.InputSlots[1]] }));
    }

    private static MutableCompositionProfileSpace Output(CompositionProfileDefinition definition)
    {
        return definition.Spaces.OfType<MutableCompositionProfileSpace>()
            .Single(static space => space.Kind == CompositionProfileSpaceKind.OutputImage);
    }
}
