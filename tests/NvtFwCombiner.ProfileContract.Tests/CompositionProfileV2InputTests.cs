using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests immutable map-independent v2 input slot policy.</summary>
public sealed class CompositionProfileV2InputTests
{
    /// <summary>Verifies closed length rules retain checked typed values.</summary>
    [Fact]
    public void LengthRulesKeepClosedTypedValues()
    {
        var exact = new ExactBytesLengthRule(16);
        var map = new ExactResolvedMapCapacityLengthRule();
        var bounded = new BoundedLengthRule(4, 32);
        var sourceViewWithResolvedContainer = new SourceViewCoverageLengthRule(
            unexpectedOuterLengthIssueCode: "DP_SIZE_WARNING");
        var sourceViewWithContainers = new SourceViewCoverageLengthRule(
            [0x80000, 0x200000],
            "DP_SIZE_WARNING");
        var boundedSourceView = new SourceViewCoverageLengthRule(
            maximumOuterLength: CompiledTpMaximum256KInputLengthRequirement.MaximumBytes);
        var declaredPrefix = new SourceViewCoverageLengthRule(
            [0x80000],
            "INPUT_OUTER_LENGTH",
            requiredEndExclusive: 0x80000,
            shortInputIssueCode: "INPUT_SHORT");

        Assert.Equal(16, exact.Bytes);
        _ = Assert.IsType<ExactResolvedMapCapacityLengthRule>(map);
        Assert.Equal(4, bounded.MinimumBytes);
        Assert.Equal(32, bounded.MaximumBytes);
        Assert.Equal("DP_SIZE_WARNING", sourceViewWithResolvedContainer.UnexpectedOuterLengthIssueCode);
        Assert.Empty(sourceViewWithResolvedContainer.ExpectedOuterLengths);
        Assert.Equal([0x80000L, 0x200000L], sourceViewWithContainers.ExpectedOuterLengths);
        Assert.Equal(
            CompiledTpMaximum256KInputLengthRequirement.MaximumBytes,
            boundedSourceView.MaximumOuterLength);
        Assert.Equal(0x80000, declaredPrefix.RequiredEndExclusive);
        Assert.Equal([0x80000L], declaredPrefix.ExpectedOuterLengths);
        Assert.Equal("INPUT_SHORT", declaredPrefix.ShortInputIssueCode);
        Assert.Equal("INPUT_OUTER_LENGTH", declaredPrefix.UnexpectedOuterLengthIssueCode);
    }

    /// <summary>Verifies invalid lengths and issue codes never enter normalized rules.</summary>
    [Fact]
    public void LengthRulesRejectInvalidValues()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ExactBytesLengthRule(0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedLengthRule(0, 4));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedLengthRule(8, 4));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new SourceViewCoverageLengthRule(
            maximumOuterLength: 0));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule(
            unexpectedOuterLengthIssueCode: "dp-warning"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule([], "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule([0], "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule([8, 8], "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule([9, 8], "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule(
            [.. Enumerable.Range(1, InputLengthPolicyLimits.MaximumExpectedInputLengths + 1).Select(static value => (long)value)],
            "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule(
            [15],
            "INPUT_OUTER_LENGTH",
            requiredEndExclusive: 16,
            shortInputIssueCode: "INPUT_SHORT"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule(
            [32, 16],
            "INPUT_OUTER_LENGTH",
            requiredEndExclusive: 16,
            shortInputIssueCode: "INPUT_SHORT"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule(
            [16],
            "INPUT_OUTER_LENGTH",
            requiredEndExclusive: 16));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule(
            [16],
            "INPUT_OUTER_LENGTH",
            maximumOuterLength: 16,
            requiredEndExclusive: 16,
            shortInputIssueCode: "INPUT_SHORT"));
    }

    /// <summary>Verifies canonical compiled normalization values retain exact byte and issue policy.</summary>
    [Fact]
    public void NormalizationValuesRetainExactPolicy()
    {
        _ = new CompiledNoInputNormalization();
        var padding = new CompiledPadShorterInputNormalization(0xFF, "padding-evidence");
        var truncation = new CompiledTruncateCtrlRamInputNormalization(
            "CTRLRAM_TRUNCATED",
            "truncation-evidence");

        Assert.Equal(0xFF, padding.FillByte);
        Assert.Equal("padding-evidence", padding.EvidenceRef);
        Assert.Equal("CTRLRAM_TRUNCATED", truncation.WarningIssueCode);
        Assert.Equal("truncation-evidence", truncation.EvidenceRef);
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.DpFirmware,
            new ExactResolvedMapCapacityLengthRule(),
            new CompiledPadShorterInputNormalization(0, "Evidence")));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.CtrlRamReplacement,
            new BoundedLengthRule(1, 16),
            new CompiledTruncateCtrlRamInputNormalization("ctrlram-truncated", "evidence")));
    }

    /// <summary>Verifies every artifact class accepts its approved map-independent policy shape.</summary>
    [Fact]
    public void ArtifactClassesAcceptApprovedPolicies()
    {
        CompositionProfileInputSlot tp = Slot(
            CompiledInputArtifactClass.TpFirmware,
            new SourceViewCoverageLengthRule(
                maximumOuterLength: CompiledTpMaximum256KInputLengthRequirement.MaximumBytes),
            new CompiledNoInputNormalization());
        CompositionProfileInputSlot exactTp = Slot(
            CompiledInputArtifactClass.TpFirmware,
            new ExactBytesLengthRule(CompiledTpMaximum256KInputLengthRequirement.MaximumBytes),
            new CompiledNoInputNormalization());
        CompositionProfileInputSlot sourceView = Slot(
            CompiledInputArtifactClass.DpFirmware,
            new SourceViewCoverageLengthRule(unexpectedOuterLengthIssueCode: "DP_SIZE_WARNING"),
            new CompiledNoInputNormalization());
        CompositionProfileInputSlot paddedDp = Slot(
            CompiledInputArtifactClass.DpFirmware,
            new ExactResolvedMapCapacityLengthRule(),
            new CompiledPadShorterInputNormalization(0xFF, "padding-evidence"));
        CompositionProfileInputSlot reference = Slot(
            CompiledInputArtifactClass.ReferenceImage,
            new ExactResolvedMapCapacityLengthRule(),
            new CompiledNoInputNormalization());
        CompositionProfileInputSlot ctrlRam = Slot(
            CompiledInputArtifactClass.CtrlRamReplacement,
            new BoundedLengthRule(1, 4096),
            new CompiledTruncateCtrlRamInputNormalization("CTRLRAM_TRUNCATED", "truncation-evidence"));
        CompositionProfileInputSlot auxiliary = Slot(
            CompiledInputArtifactClass.Auxiliary,
            new ExactBytesLengthRule(32),
            new CompiledNoInputNormalization(),
            required: false,
            cardinality: CompiledInputSlotCardinality.OneOrMore);
        CompositionProfileInputSlot declaredPrefix = Slot(
            CompiledInputArtifactClass.Auxiliary,
            new SourceViewCoverageLengthRule(
                [16],
                "INPUT_OUTER_LENGTH",
                requiredEndExclusive: 16,
                shortInputIssueCode: "INPUT_SHORT"),
            new CompiledNoInputNormalization());

        Assert.Equal(CompiledInputArtifactClass.TpFirmware, tp.ArtifactClass);
        Assert.Equal(
            CompiledTpMaximum256KInputLengthRequirement.MaximumBytes,
            Assert.IsType<ExactBytesLengthRule>(exactTp.LengthRule).Bytes);
        _ = Assert.IsType<SourceViewCoverageLengthRule>(sourceView.LengthRule);
        _ = Assert.IsType<CompiledPadShorterInputNormalization>(paddedDp.Normalization);
        Assert.Equal(CompiledInputArtifactClass.ReferenceImage, reference.ArtifactClass);
        _ = Assert.IsType<CompiledTruncateCtrlRamInputNormalization>(ctrlRam.Normalization);
        Assert.False(auxiliary.Required);
        Assert.Equal(CompiledInputSlotCardinality.OneOrMore, auxiliary.Cardinality);
        _ = Assert.IsType<SourceViewCoverageLengthRule>(declaredPrefix.LengthRule);
    }

    /// <summary>Verifies firmware-specific size and normalization policy fails closed.</summary>
    [Fact]
    public void ArtifactClassesRejectUnapprovedPolicies()
    {
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.TpFirmware,
            new ExactResolvedMapCapacityLengthRule(),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.TpFirmware,
            new ExactBytesLengthRule(CompiledTpMaximum256KInputLengthRequirement.MaximumBytes + 1),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.TpFirmware,
            new SourceViewCoverageLengthRule(
                [CompiledTpMaximum256KInputLengthRequirement.MaximumBytes + 1],
                "INPUT_OUTER_LENGTH",
                requiredEndExclusive: CompiledTpMaximum256KInputLengthRequirement.MaximumBytes + 1,
                shortInputIssueCode: "INPUT_SHORT"),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.DpFirmware,
            new ExactBytesLengthRule(16),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.ReferenceImage,
            new BoundedLengthRule(1, 16),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.Auxiliary,
            new ExactBytesLengthRule(16),
            new CompiledPadShorterInputNormalization(0xFF, "padding-evidence")));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.DpFirmware,
            new SourceViewCoverageLengthRule(unexpectedOuterLengthIssueCode: "DP_SIZE_WARNING"),
            new CompiledPadShorterInputNormalization(0xFF, "padding-evidence")));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.DpFirmware,
            new ExactResolvedMapCapacityLengthRule(),
            new CompiledTruncateCtrlRamInputNormalization("CTRLRAM_TRUNCATED", "truncation-evidence")));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.ReferenceImage,
            new SourceViewCoverageLengthRule(
                [16],
                "INPUT_OUTER_LENGTH",
                requiredEndExclusive: 16,
                shortInputIssueCode: "INPUT_SHORT"),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.DpFirmware,
            new SourceViewCoverageLengthRule(
                [16],
                "INPUT_OUTER_LENGTH",
                requiredEndExclusive: 16,
                shortInputIssueCode: "INPUT_SHORT"),
            new CompiledPadShorterInputNormalization(0xFF, "padding-evidence")));
    }

    /// <summary>Verifies slot cardinality, extensions, and caller collections stay canonical and immutable.</summary>
    [Fact]
    public void SlotIdentityAndExtensionsAreClosedAndImmutable()
    {
        var extensions = new List<string> { ".BIN", ".bin" };
        CompositionProfileInputSlot slot = Slot(
            CompiledInputArtifactClass.Auxiliary,
            new ExactBytesLengthRule(8),
            new CompiledNoInputNormalization(),
            extensions: extensions);
        extensions.Clear();

        Assert.Equal([".BIN", ".bin"], slot.AcceptedExtensions);
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.Auxiliary,
            new ExactBytesLengthRule(8),
            new CompiledNoInputNormalization(),
            extensions: []));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.Auxiliary,
            new ExactBytesLengthRule(8),
            new CompiledNoInputNormalization(),
            extensions: ["bin"]));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.Auxiliary,
            new ExactBytesLengthRule(8),
            new CompiledNoInputNormalization(),
            extensions: [".bin", ".bin"]));
    }

    /// <summary>Verifies independent required/cardinality state and enum carriers remain closed.</summary>
    [Fact]
    public void SlotKeepsRequiredAndCardinalityIndependentAndRejectsUnknownEnums()
    {
        CompositionProfileInputSlot requiredOptionalCardinality = Slot(
            CompiledInputArtifactClass.Auxiliary,
            new ExactBytesLengthRule(8),
            new CompiledNoInputNormalization(),
            required: true,
            cardinality: CompiledInputSlotCardinality.ZeroOrOne);

        Assert.True(requiredOptionalCardinality.Required);
        Assert.Equal(CompiledInputSlotCardinality.ZeroOrOne, requiredOptionalCardinality.Cardinality);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Slot(
            (CompiledInputArtifactClass)99,
            new ExactBytesLengthRule(8),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Slot(
            CompiledInputArtifactClass.Auxiliary,
            new ExactBytesLengthRule(8),
            new CompiledNoInputNormalization(),
            required: true,
            cardinality: (CompiledInputSlotCardinality)99));
    }

    private static CompositionProfileInputSlot Slot(
        CompiledInputArtifactClass artifactClass,
        CompositionProfileLengthRule lengthRule,
        CompiledInputNormalization normalization,
        bool required = true,
        CompiledInputSlotCardinality cardinality = CompiledInputSlotCardinality.ExactlyOne,
        IEnumerable<string>? extensions = null)
    {
        return new CompositionProfileInputSlot(
            "source-input",
            "source",
            artifactClass,
            required,
            cardinality,
            extensions ?? [".bin"],
            lengthRule,
            normalization);
    }
}
