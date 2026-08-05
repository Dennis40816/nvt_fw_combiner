using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests immutable map-independent v2 input slot policy.</summary>
public sealed class CompositionProfileV2InputTests
{
    /// <summary>Verifies all six closed length rules retain checked typed values.</summary>
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
        var tp = new TpMaximum256KLengthRule();
        var declaredPrefix = new DeclaredPrefixWithWarningLengthRule(
            0x80000,
            [0x80000],
            "INPUT_SHORT",
            "INPUT_OUTER_LENGTH");

        Assert.Equal(16, exact.Bytes);
        Assert.Equal(CompositionProfileLengthRuleKind.ExactResolvedMapCapacity, map.Kind);
        Assert.Equal(4, bounded.MinimumBytes);
        Assert.Equal(32, bounded.MaximumBytes);
        Assert.Equal("DP_SIZE_WARNING", sourceViewWithResolvedContainer.UnexpectedOuterLengthIssueCode);
        Assert.Empty(sourceViewWithResolvedContainer.ExpectedOuterLengths);
        Assert.Equal([0x80000L, 0x200000L], sourceViewWithContainers.ExpectedOuterLengths);
        Assert.Equal(262144, TpMaximum256KLengthRule.MaximumBytes);
        Assert.Equal(CompositionProfileLengthRuleKind.TpMaximum256K, tp.Kind);
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
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule(
            unexpectedOuterLengthIssueCode: "dp-warning"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule([], "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule([0], "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule([8, 8], "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule([9, 8], "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new SourceViewCoverageLengthRule(
            [.. Enumerable.Range(1, InputLengthPolicyLimits.MaximumExpectedInputLengths + 1).Select(static value => (long)value)],
            "DP_SIZE_WARNING"));
        _ = Assert.Throws<ArgumentException>(() => new DeclaredPrefixWithWarningLengthRule(
            16,
            [15],
            "INPUT_SHORT",
            "INPUT_OUTER_LENGTH"));
        _ = Assert.Throws<ArgumentException>(() => new DeclaredPrefixWithWarningLengthRule(
            16,
            [32, 16],
            "INPUT_SHORT",
            "INPUT_OUTER_LENGTH"));
    }

    /// <summary>Verifies evidenced normalization values retain exact byte and issue policy.</summary>
    [Fact]
    public void NormalizationValuesAreTypedAndCanonical()
    {
        var none = new NoInputNormalization();
        var padding = new PadShorterInputNormalization(0xFF, "padding-evidence");
        var truncation = new TruncateCtrlRamInputNormalization(
            "CTRLRAM_TRUNCATED",
            "truncation-evidence");

        Assert.Equal(CompositionProfileInputNormalizationKind.None, none.Kind);
        Assert.Equal(0xFF, padding.FillByte);
        Assert.Equal("padding-evidence", padding.EvidenceRef);
        Assert.Equal("CTRLRAM_TRUNCATED", truncation.WarningIssueCode);
        Assert.Equal("truncation-evidence", truncation.EvidenceRef);
        _ = Assert.Throws<ArgumentException>(() => new PadShorterInputNormalization(0, "Evidence"));
        _ = Assert.Throws<ArgumentException>(() => new TruncateCtrlRamInputNormalization(
            "ctrlram-truncated",
            "evidence"));
    }

    /// <summary>Verifies every artifact class accepts its approved map-independent policy shape.</summary>
    [Fact]
    public void ArtifactClassesAcceptApprovedPolicies()
    {
        CompositionProfileInputSlot tp = Slot(
            CompositionProfileArtifactClass.TpFirmware,
            new TpMaximum256KLengthRule(),
            new NoInputNormalization());
        CompositionProfileInputSlot exactTp = Slot(
            CompositionProfileArtifactClass.TpFirmware,
            new ExactBytesLengthRule(TpMaximum256KLengthRule.MaximumBytes),
            new NoInputNormalization());
        CompositionProfileInputSlot sourceView = Slot(
            CompositionProfileArtifactClass.DpFirmware,
            new SourceViewCoverageLengthRule(unexpectedOuterLengthIssueCode: "DP_SIZE_WARNING"),
            new NoInputNormalization());
        CompositionProfileInputSlot paddedDp = Slot(
            CompositionProfileArtifactClass.DpFirmware,
            new ExactResolvedMapCapacityLengthRule(),
            new PadShorterInputNormalization(0xFF, "padding-evidence"));
        CompositionProfileInputSlot reference = Slot(
            CompositionProfileArtifactClass.ReferenceImage,
            new ExactResolvedMapCapacityLengthRule(),
            new NoInputNormalization());
        CompositionProfileInputSlot ctrlRam = Slot(
            CompositionProfileArtifactClass.CtrlRamReplacement,
            new BoundedLengthRule(1, 4096),
            new TruncateCtrlRamInputNormalization("CTRLRAM_TRUNCATED", "truncation-evidence"));
        CompositionProfileInputSlot auxiliary = Slot(
            CompositionProfileArtifactClass.Auxiliary,
            new ExactBytesLengthRule(32),
            new NoInputNormalization(),
            required: false,
            cardinality: CompositionProfileSlotCardinality.OneOrMore);
        CompositionProfileInputSlot declaredPrefix = Slot(
            CompositionProfileArtifactClass.Auxiliary,
            new DeclaredPrefixWithWarningLengthRule(
                16,
                [16],
                "INPUT_SHORT",
                "INPUT_OUTER_LENGTH"),
            new NoInputNormalization());

        Assert.Equal(CompositionProfileArtifactClass.TpFirmware, tp.ArtifactClass);
        Assert.Equal(TpMaximum256KLengthRule.MaximumBytes, Assert.IsType<ExactBytesLengthRule>(exactTp.LengthRule).Bytes);
        Assert.Equal(CompositionProfileLengthRuleKind.SourceViewCoverage, sourceView.LengthRule.Kind);
        Assert.Equal(CompositionProfileInputNormalizationKind.PadShorter, paddedDp.Normalization.Kind);
        Assert.Equal(CompositionProfileArtifactClass.ReferenceImage, reference.ArtifactClass);
        Assert.Equal(CompositionProfileInputNormalizationKind.TruncateCtrlRam, ctrlRam.Normalization.Kind);
        Assert.False(auxiliary.Required);
        Assert.Equal(CompositionProfileSlotCardinality.OneOrMore, auxiliary.Cardinality);
        Assert.Equal(CompositionProfileLengthRuleKind.DeclaredPrefixWithWarning, declaredPrefix.LengthRule.Kind);
    }

    /// <summary>Verifies firmware-specific size and normalization policy fails closed.</summary>
    [Fact]
    public void ArtifactClassesRejectUnapprovedPolicies()
    {
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.TpFirmware,
            new ExactResolvedMapCapacityLengthRule(),
            new NoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.TpFirmware,
            new ExactBytesLengthRule(TpMaximum256KLengthRule.MaximumBytes + 1),
            new NoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.TpFirmware,
            new DeclaredPrefixWithWarningLengthRule(
                TpMaximum256KLengthRule.MaximumBytes + 1,
                [TpMaximum256KLengthRule.MaximumBytes + 1],
                "INPUT_SHORT",
                "INPUT_OUTER_LENGTH"),
            new NoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.DpFirmware,
            new ExactBytesLengthRule(16),
            new NoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.ReferenceImage,
            new BoundedLengthRule(1, 16),
            new NoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.Auxiliary,
            new ExactBytesLengthRule(16),
            new PadShorterInputNormalization(0xFF, "padding-evidence")));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.DpFirmware,
            new SourceViewCoverageLengthRule(unexpectedOuterLengthIssueCode: "DP_SIZE_WARNING"),
            new PadShorterInputNormalization(0xFF, "padding-evidence")));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.DpFirmware,
            new ExactResolvedMapCapacityLengthRule(),
            new TruncateCtrlRamInputNormalization("CTRLRAM_TRUNCATED", "truncation-evidence")));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.Auxiliary,
            new TpMaximum256KLengthRule(),
            new NoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.CtrlRamReplacement,
            new TpMaximum256KLengthRule(),
            new NoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.ReferenceImage,
            new DeclaredPrefixWithWarningLengthRule(
                16,
                [16],
                "INPUT_SHORT",
                "INPUT_OUTER_LENGTH"),
            new NoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.DpFirmware,
            new DeclaredPrefixWithWarningLengthRule(
                16,
                [16],
                "INPUT_SHORT",
                "INPUT_OUTER_LENGTH"),
            new PadShorterInputNormalization(0xFF, "padding-evidence")));
    }

    /// <summary>Verifies slot cardinality, extensions, and caller collections stay canonical and immutable.</summary>
    [Fact]
    public void SlotIdentityAndExtensionsAreClosedAndImmutable()
    {
        var extensions = new List<string> { ".BIN", ".bin" };
        CompositionProfileInputSlot slot = Slot(
            CompositionProfileArtifactClass.Auxiliary,
            new ExactBytesLengthRule(8),
            new NoInputNormalization(),
            extensions: extensions);
        extensions.Clear();

        Assert.Equal([".BIN", ".bin"], slot.AcceptedExtensions);
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.Auxiliary,
            new ExactBytesLengthRule(8),
            new NoInputNormalization(),
            extensions: []));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.Auxiliary,
            new ExactBytesLengthRule(8),
            new NoInputNormalization(),
            extensions: ["bin"]));
        _ = Assert.Throws<ArgumentException>(() => Slot(
            CompositionProfileArtifactClass.Auxiliary,
            new ExactBytesLengthRule(8),
            new NoInputNormalization(),
            extensions: [".bin", ".bin"]));
    }

    /// <summary>Verifies independent required/cardinality state and enum carriers remain closed.</summary>
    [Fact]
    public void SlotKeepsRequiredAndCardinalityIndependentAndRejectsUnknownEnums()
    {
        CompositionProfileInputSlot requiredOptionalCardinality = Slot(
            CompositionProfileArtifactClass.Auxiliary,
            new ExactBytesLengthRule(8),
            new NoInputNormalization(),
            required: true,
            cardinality: CompositionProfileSlotCardinality.ZeroOrOne);

        Assert.True(requiredOptionalCardinality.Required);
        Assert.Equal(CompositionProfileSlotCardinality.ZeroOrOne, requiredOptionalCardinality.Cardinality);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Slot(
            (CompositionProfileArtifactClass)99,
            new ExactBytesLengthRule(8),
            new NoInputNormalization()));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Slot(
            CompositionProfileArtifactClass.Auxiliary,
            new ExactBytesLengthRule(8),
            new NoInputNormalization(),
            required: true,
            cardinality: (CompositionProfileSlotCardinality)99));
    }

    private static CompositionProfileInputSlot Slot(
        CompositionProfileArtifactClass artifactClass,
        CompositionProfileLengthRule lengthRule,
        CompositionProfileInputNormalization normalization,
        bool required = true,
        CompositionProfileSlotCardinality cardinality = CompositionProfileSlotCardinality.ExactlyOne,
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
