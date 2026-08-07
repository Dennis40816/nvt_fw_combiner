using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests immutable map-independent v2 input slot policy.</summary>
public sealed class CompositionProfileV2InputTests
{
    /// <summary>Verifies closed length rules retain checked typed values.</summary>
    [Fact]
    public void LengthRulesKeepClosedTypedValues()
    {
        var exact = new CompiledExactBytesInputLengthRequirement(16);
        var map = new ResolvedMapCapacityInputLengthDefinition();
        var bounded = new CompiledBoundedInputLengthRequirement(4, 32);
        var sourceViewWithResolvedContainer = new SourceViewCoverageInputLengthDefinition(
            unexpectedOuterLengthIssueCode: "DP_SIZE_WARNING");
        var sourceViewWithContainers = new SourceViewCoverageInputLengthDefinition(
            [0x80000, 0x200000],
            "DP_SIZE_WARNING");
        _ = new CompiledTpMaximum256KInputLengthRequirement();
        var declaredPrefix = new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
            0x80000,
            [0x80000],
            "INPUT_SHORT",
            "INPUT_OUTER_LENGTH");

        Assert.Equal(16, exact.Bytes);
        _ = Assert.IsType<ResolvedMapCapacityInputLengthDefinition>(map);
        Assert.Equal(4, bounded.MinimumBytes);
        Assert.Equal(32, bounded.MaximumBytes);
        Assert.Equal("DP_SIZE_WARNING", sourceViewWithResolvedContainer.UnexpectedOuterLengthIssueCode);
        Assert.Empty(sourceViewWithResolvedContainer.ExpectedOuterLengths);
        Assert.Equal([0x80000L, 0x200000L], sourceViewWithContainers.ExpectedOuterLengths);
        Assert.Equal(0x80000, declaredPrefix.RequiredEndExclusive);
        Assert.Equal([0x80000L], declaredPrefix.ExpectedOuterLengths);
        Assert.Equal("INPUT_SHORT", declaredPrefix.ShortInputIssueCode);
        Assert.Equal("INPUT_OUTER_LENGTH", declaredPrefix.UnexpectedOuterLengthIssueCode);
    }

    /// <summary>Verifies invalid lengths and issue codes never enter normalized rules.</summary>
    [Fact]
    public void LengthRulesRejectInvalidValues()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompiledExactBytesInputLengthRequirement(0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompiledBoundedInputLengthRequirement(0, 4));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompiledBoundedInputLengthRequirement(8, 4));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
                0,
                [16],
                "INPUT_SHORT",
                "INPUT_OUTER_LENGTH"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageInputLengthDefinition([], "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageInputLengthDefinition([0], "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageInputLengthDefinition([8, 8], "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageInputLengthDefinition([9, 8], "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageInputLengthDefinition(
            [.. Enumerable.Range(1, InputLengthPolicyLimits.MaximumExpectedInputLengths + 1).Select(static value => (long)value)],
            "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() =>
            new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
                16,
                [15],
                "INPUT_SHORT",
                "INPUT_OUTER_LENGTH"));
        _ = Assert.Throws<ArgumentException>(() =>
            new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
                16,
                [32, 16],
                "INPUT_SHORT",
                "INPUT_OUTER_LENGTH"));
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
            new ResolvedMapCapacityInputLengthDefinition(),
            new CompiledPadShorterInputNormalization(0, "Evidence")));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.CtrlRamReplacement,
            new CompiledBoundedInputLengthRequirement(1, 16),
            new CompiledTruncateCtrlRamInputNormalization("ctrlram-truncated", "evidence")));
    }

    /// <summary>Verifies every artifact class accepts its approved map-independent policy shape.</summary>
    [Fact]
    public void ArtifactClassesAcceptApprovedPolicies()
    {
        CompositionInputSlotDefinition tp = Slot(
            CompiledInputArtifactClass.TpFirmware,
            new CompiledTpMaximum256KInputLengthRequirement(),
            new CompiledNoInputNormalization());
        CompositionInputSlotDefinition exactTp = Slot(
            CompiledInputArtifactClass.TpFirmware,
            new CompiledExactBytesInputLengthRequirement(CompiledTpMaximum256KInputLengthRequirement.MaximumBytes),
            new CompiledNoInputNormalization());
        CompositionInputSlotDefinition sourceView = Slot(
            CompiledInputArtifactClass.DpFirmware,
            new SourceViewCoverageInputLengthDefinition(unexpectedOuterLengthIssueCode: "DP_SIZE_WARNING"),
            new CompiledNoInputNormalization());
        CompositionInputSlotDefinition paddedDp = Slot(
            CompiledInputArtifactClass.DpFirmware,
            new ResolvedMapCapacityInputLengthDefinition(),
            new CompiledPadShorterInputNormalization(0xFF, "padding-evidence"));
        CompositionInputSlotDefinition reference = Slot(
            CompiledInputArtifactClass.ReferenceImage,
            new ResolvedMapCapacityInputLengthDefinition(),
            new CompiledNoInputNormalization());
        CompositionInputSlotDefinition ctrlRam = Slot(
            CompiledInputArtifactClass.CtrlRamReplacement,
            new CompiledBoundedInputLengthRequirement(1, 4096),
            new CompiledTruncateCtrlRamInputNormalization("CTRLRAM_TRUNCATED", "truncation-evidence"));
        CompositionInputSlotDefinition auxiliary = Slot(
            CompiledInputArtifactClass.Auxiliary,
            new CompiledExactBytesInputLengthRequirement(32),
            new CompiledNoInputNormalization(),
            required: false,
            cardinality: CompiledInputSlotCardinality.OneOrMore);
        CompositionInputSlotDefinition declaredPrefix = Slot(
            CompiledInputArtifactClass.Auxiliary,
            new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
                16,
                [16],
                "INPUT_SHORT",
                "INPUT_OUTER_LENGTH"),
            new CompiledNoInputNormalization());

        Assert.Equal(CompiledInputArtifactClass.TpFirmware, tp.ArtifactClass);
        Assert.Equal(
            CompiledTpMaximum256KInputLengthRequirement.MaximumBytes,
            Assert.IsType<CompiledExactBytesInputLengthRequirement>(exactTp.LengthRequirement).Bytes);
        _ = Assert.IsType<SourceViewCoverageInputLengthDefinition>(sourceView.LengthRequirement);
        _ = Assert.IsType<CompiledPadShorterInputNormalization>(paddedDp.Normalization);
        Assert.Equal(CompiledInputArtifactClass.ReferenceImage, reference.ArtifactClass);
        _ = Assert.IsType<CompiledTruncateCtrlRamInputNormalization>(ctrlRam.Normalization);
        Assert.False(auxiliary.Required);
        Assert.Equal(CompiledInputSlotCardinality.OneOrMore, auxiliary.Cardinality);
        _ = Assert.IsType<CompiledDeclaredPrefixWithWarningInputLengthRequirement>(
            declaredPrefix.LengthRequirement);
    }

    /// <summary>Verifies firmware-specific size and normalization policy fails closed.</summary>
    [Fact]
    public void ArtifactClassesRejectUnapprovedPolicies()
    {
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.TpFirmware,
            new ResolvedMapCapacityInputLengthDefinition(),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.TpFirmware,
            new CompiledExactBytesInputLengthRequirement(CompiledTpMaximum256KInputLengthRequirement.MaximumBytes + 1),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.TpFirmware,
            new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
                CompiledTpMaximum256KInputLengthRequirement.MaximumBytes + 1,
                [CompiledTpMaximum256KInputLengthRequirement.MaximumBytes + 1],
                "INPUT_SHORT",
                "INPUT_OUTER_LENGTH"),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.DpFirmware,
            new CompiledExactBytesInputLengthRequirement(16),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.ReferenceImage,
            new CompiledBoundedInputLengthRequirement(1, 16),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.Auxiliary,
            new CompiledExactBytesInputLengthRequirement(16),
            new CompiledPadShorterInputNormalization(0xFF, "padding-evidence")));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.DpFirmware,
            new SourceViewCoverageInputLengthDefinition(unexpectedOuterLengthIssueCode: "DP_SIZE_WARNING"),
            new CompiledPadShorterInputNormalization(0xFF, "padding-evidence")));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.DpFirmware,
            new ResolvedMapCapacityInputLengthDefinition(),
            new CompiledTruncateCtrlRamInputNormalization("CTRLRAM_TRUNCATED", "truncation-evidence")));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.ReferenceImage,
            new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
                16,
                [16],
                "INPUT_SHORT",
                "INPUT_OUTER_LENGTH"),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.DpFirmware,
            new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
                16,
                [16],
                "INPUT_SHORT",
                "INPUT_OUTER_LENGTH"),
            new CompiledPadShorterInputNormalization(0xFF, "padding-evidence")));
    }

    /// <summary>Verifies slot cardinality, extensions, and caller collections stay canonical and immutable.</summary>
    [Fact]
    public void SlotIdentityAndExtensionsAreClosedAndImmutable()
    {
        var extensions = new List<string> { ".BIN", ".bin" };
        CompositionInputSlotDefinition slot = Slot(
            CompiledInputArtifactClass.Auxiliary,
            new CompiledExactBytesInputLengthRequirement(8),
            new CompiledNoInputNormalization(),
            extensions: extensions);
        extensions.Clear();

        Assert.Equal([".BIN", ".bin"], slot.AcceptedExtensions);
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.Auxiliary,
            new CompiledExactBytesInputLengthRequirement(8),
            new CompiledNoInputNormalization(),
            extensions: []));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.Auxiliary,
            new CompiledExactBytesInputLengthRequirement(8),
            new CompiledNoInputNormalization(),
            extensions: ["bin"]));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompiledInputArtifactClass.Auxiliary,
            new CompiledExactBytesInputLengthRequirement(8),
            new CompiledNoInputNormalization(),
            extensions: [".bin", ".bin"]));
    }

    /// <summary>Verifies independent required/cardinality state and enum carriers remain closed.</summary>
    [Fact]
    public void SlotKeepsRequiredAndCardinalityIndependentAndRejectsUnknownEnums()
    {
        CompositionInputSlotDefinition requiredOptionalCardinality = Slot(
            CompiledInputArtifactClass.Auxiliary,
            new CompiledExactBytesInputLengthRequirement(8),
            new CompiledNoInputNormalization(),
            required: true,
            cardinality: CompiledInputSlotCardinality.ZeroOrOne);

        Assert.True(requiredOptionalCardinality.Required);
        Assert.Equal(CompiledInputSlotCardinality.ZeroOrOne, requiredOptionalCardinality.Cardinality);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Slot(
            (CompiledInputArtifactClass)99,
            new CompiledExactBytesInputLengthRequirement(8),
            new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Slot(
            CompiledInputArtifactClass.Auxiliary,
            new CompiledExactBytesInputLengthRequirement(8),
            new CompiledNoInputNormalization(),
            required: true,
            cardinality: (CompiledInputSlotCardinality)99));
    }

    private static CompositionInputSlotDefinition Slot(
        CompiledInputArtifactClass artifactClass,
        InputLengthRequirementDefinition lengthRequirement,
        CompiledInputNormalization normalization,
        bool required = true,
        CompiledInputSlotCardinality cardinality = CompiledInputSlotCardinality.ExactlyOne,
        IEnumerable<string>? extensions = null)
    {
        return new CompositionInputSlotDefinition(
            "source-input",
            "source",
            artifactClass,
            required,
            cardinality,
            extensions ?? [".bin"],
            lengthRequirement,
            normalization);
    }
}
