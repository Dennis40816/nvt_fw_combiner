using System.Text.Json;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests DTO normalization for v2 input slots and acceptance policies.</summary>
public sealed class CompositionProfileV2InputNormalizerTests
{
    /// <summary>Verifies every closed artifact class maps without fallback.</summary>
    [Fact]
    public void InputSlotMapsEveryArtifactClass()
    {
        CompositionProfileInputSlot[] slots =
        [
            Normalize("tp-firmware", TpMaximum(), None()),
            Normalize("dp-firmware", ExactMapCapacity(), None()),
            Normalize("reference-image", ExactMapCapacity(), None()),
            Normalize("ctrlram-replacement", ExactBytes("16"), None()),
            Normalize("auxiliary", ExactBytes("16"), None()),
        ];

        Assert.Equal(Enum.GetValues<CompositionProfileArtifactClass>(), slots.Select(static slot => slot.ArtifactClass));
        Assert.All(slots, static slot => Assert.Equal([".bin", ".hex"], slot.AcceptedExtensions));
    }

    /// <summary>Verifies every closed cardinality maps to its normalized value.</summary>
    [Fact]
    public void InputSlotMapsEveryCardinality()
    {
        (string Token, CompositionProfileSlotCardinality Expected)[] cases =
        [
            ("exactly-one", CompositionProfileSlotCardinality.ExactlyOne),
            ("zero-or-one", CompositionProfileSlotCardinality.ZeroOrOne),
            ("one-or-more", CompositionProfileSlotCardinality.OneOrMore),
        ];

        foreach ((string token, CompositionProfileSlotCardinality expected) in cases)
        {
            CompositionProfileInputSlot slot = CompositionProfileNormalizer.NormalizeInputSlot(
                Slot("auxiliary", ExactBytes("1"), None(), cardinality: token));
            Assert.Equal(expected, slot.Cardinality);
        }
    }

    /// <summary>Verifies all closed length rules preserve exact numeric values and diagnostics.</summary>
    [Fact]
    public void InputSlotMapsEveryLengthRule()
    {
        ExactBytesLengthRule exact = Assert.IsType<ExactBytesLengthRule>(
            Normalize("auxiliary", ExactBytes("16.0"), None()).LengthRule);
        _ = Assert.IsType<ExactResolvedMapCapacityLengthRule>(
            Normalize("dp-firmware", ExactMapCapacity(), None()).LengthRule);
        BoundedLengthRule bounded = Assert.IsType<BoundedLengthRule>(
            Normalize("auxiliary", Bounded("1e1", "32"), None()).LengthRule);
        NormalDpExtractWithWarningLengthRule dpWarning = Assert.IsType<NormalDpExtractWithWarningLengthRule>(
            Normalize("dp-firmware", NormalDpWarning(), None()).LengthRule);
        TpMaximum256KLengthRule tpMaximum = Assert.IsType<TpMaximum256KLengthRule>(
            Normalize("tp-firmware", TpMaximum(), None()).LengthRule);

        Assert.Equal(16, exact.Bytes);
        Assert.Equal(10, bounded.MinimumBytes);
        Assert.Equal(32, bounded.MaximumBytes);
        Assert.Equal("DP_SIZE_WARNING", dpWarning.IssueCode);
        Assert.Equal(262144, TpMaximum256KLengthRule.MaximumBytes);
        Assert.Equal(CompositionProfileLengthRuleKind.TpMaximum256K, tpMaximum.Kind);
    }

    /// <summary>Verifies all transient normalization policies preserve their evidence and values.</summary>
    [Fact]
    public void InputSlotMapsEveryNormalization()
    {
        _ = Assert.IsType<NoInputNormalization>(
            Normalize("auxiliary", ExactBytes("16"), None()).Normalization);
        PadShorterInputNormalization padding = Assert.IsType<PadShorterInputNormalization>(
            Normalize("dp-firmware", ExactMapCapacity(), PadShorter("255")).Normalization);
        TruncateCtrlRamInputNormalization truncation = Assert.IsType<TruncateCtrlRamInputNormalization>(
            Normalize("ctrlram-replacement", Bounded("1", "16"), TruncateCtrlRam()).Normalization);

        Assert.Equal(0xFF, padding.FillByte);
        Assert.Equal("dp-padding-evidence", padding.EvidenceRef);
        Assert.Equal("CTRLRAM_TRUNCATED", truncation.WarningIssueCode);
        Assert.Equal("ctrlram-truncation-evidence", truncation.EvidenceRef);
    }

    /// <summary>Verifies unknown closed-union tokens fail at their exact discriminator paths.</summary>
    [Fact]
    public void InputSlotRejectsUnknownTokensWithPaths()
    {
        CompositionProfileNormalizationException artifact = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeInputSlot(Slot("future", ExactBytes("1"), None())));
        CompositionProfileNormalizationException cardinality = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeInputSlot(
                Slot("auxiliary", ExactBytes("1"), None(), cardinality: "future")));
        CompositionProfileNormalizationException length = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeInputSlot(
                Slot("auxiliary", new CompositionProfileLengthRuleDocument("future"), None())));
        CompositionProfileNormalizationException normalization = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeInputSlot(
                Slot(
                    "auxiliary",
                    ExactBytes("1"),
                    new CompositionProfileInputNormalizationDocument("future"))));

        Assert.Equal("inputSlots[0].artifactClass", artifact.Path);
        Assert.Equal("inputSlots[0].cardinality", cardinality.Path);
        Assert.Equal("inputSlots[0].acceptance.lengthRule.kind", length.Path);
        Assert.Equal("inputSlots[0].acceptance.normalization.kind", normalization.Path);
    }

    /// <summary>Verifies required union members and fixed TP capacity fail at source paths.</summary>
    [Fact]
    public void InputSlotRejectsMissingOrInvalidUnionMembersWithPaths()
    {
        CompositionProfileNormalizationException bytes = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeInputSlot(
                Slot("auxiliary", new CompositionProfileLengthRuleDocument("exact-bytes"), None())));
        CompositionProfileNormalizationException issueCode = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeInputSlot(
                Slot(
                    "dp-firmware",
                    new CompositionProfileLengthRuleDocument("normal-dp-extract-with-warning"),
                    None())));
        CompositionProfileNormalizationException tpMaximum = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeInputSlot(
                Slot("tp-firmware", TpMaximum("262143"), None())));
        CompositionProfileNormalizationException evidence = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeInputSlot(
                Slot(
                    "dp-firmware",
                    ExactMapCapacity(),
                    new CompositionProfileInputNormalizationDocument("pad-shorter", FillByte: Number("255")))));

        Assert.Equal("inputSlots[0].acceptance.lengthRule.bytes", bytes.Path);
        Assert.Equal("inputSlots[0].acceptance.lengthRule.issueCode", issueCode.Path);
        Assert.Equal("inputSlots[0].acceptance.lengthRule.maximumBytes", tpMaximum.Path);
        Assert.Equal("inputSlots[0].acceptance.normalization.evidenceRef", evidence.Path);
    }

    /// <summary>Verifies exact integer parsing rejects fractions, overflow, and invalid fill bytes.</summary>
    [Fact]
    public void InputSlotRejectsInvalidNumericValuesWithPaths()
    {
        CompositionProfileNormalizationException fraction = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeInputSlot(
                Slot("auxiliary", ExactBytes("1.5"), None())));
        CompositionProfileNormalizationException overflow = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeInputSlot(
                Slot("auxiliary", ExactBytes("9223372036854775808"), None())));
        CompositionProfileNormalizationException fillByte = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeInputSlot(
                Slot("dp-firmware", ExactMapCapacity(), PadShorter("256"))));

        Assert.Equal("inputSlots[0].acceptance.lengthRule.bytes", fraction.Path);
        Assert.Equal("inputSlots[0].acceptance.lengthRule.bytes", overflow.Path);
        Assert.Equal("inputSlots[0].acceptance.normalization.fillByte", fillByte.Path);
    }

    /// <summary>Verifies graph-independent firmware policy errors remain attributable to the slot.</summary>
    [Fact]
    public void InputSlotRejectsInvalidFirmwarePolicyAtSlotPath()
    {
        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeInputSlot(
                Slot("tp-firmware", ExactBytes("16"), PadShorter("255"))));

        Assert.Equal("inputSlots[0]", exception.Path);
        _ = Assert.IsType<ArgumentException>(exception.InnerException, exactMatch: false);
    }

    /// <summary>Verifies a Normal-DP warning rule retains its declared outer-container expectations.</summary>
    [Fact]
    public void NormalDpWarningRetainsDeclaredOuterContainerLengths()
    {
        CompositionProfileInputSlot slot = Normalize(
            "dp-firmware",
            new CompositionProfileLengthRuleDocument(
                "normal-dp-extract-with-warning",
                IssueCode: "DP_SIZE_WARNING",
                ExpectedInputLengths: [Number("524288"), Number("2097152")]),
            None());

        NormalDpExtractWithWarningLengthRule rule = Assert.IsType<NormalDpExtractWithWarningLengthRule>(
            slot.LengthRule);
        Assert.Equal([0x80000L, 0x200000L], rule.ExpectedInputLengths);
    }

    /// <summary>Verifies an exact TP source is admitted only within the fixed 256 KiB owner limit.</summary>
    [Fact]
    public void ExactTpInputRetainsExactGeometryWithinTheOwnerLimit()
    {
        CompositionProfileInputSlot exact = Normalize(
            "tp-firmware",
            ExactBytes("262144"),
            None());
        CompositionProfileNormalizationException oversized = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(
                "tp-firmware",
                ExactBytes("262145"),
                None()));

        Assert.Equal(262144, Assert.IsType<ExactBytesLengthRule>(exact.LengthRule).Bytes);
        Assert.Equal("inputSlots[0]", oversized.Path);
    }

    /// <summary>Verifies Normal-DP outer-container lengths fail closed when their declaration is not canonical.</summary>
    [Fact]
    public void NormalDpWarningRejectsUnsortedOuterContainerLengths()
    {
        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(
                "dp-firmware",
                new CompositionProfileLengthRuleDocument(
                    "normal-dp-extract-with-warning",
                    IssueCode: "DP_SIZE_WARNING",
                    ExpectedInputLengths: [Number("2097152"), Number("524288")]),
                None()));

        Assert.Equal("inputSlots[0].acceptance.lengthRule", exception.Path);
        _ = Assert.IsType<ArgumentException>(exception.InnerException, exactMatch: false);
    }

    /// <summary>Verifies the TP-only 256 KiB rule cannot be assigned to another artifact class during normalization.</summary>
    [Theory]
    [InlineData("auxiliary")]
    [InlineData("ctrlram-replacement")]
    public void InputSlotRejectsTpMaximumRuleForNonTpArtifact(string artifactClass)
    {
        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeInputSlot(Slot(artifactClass, TpMaximum(), None())));

        Assert.Equal("inputSlots[0]", exception.Path);
        _ = Assert.IsType<ArgumentException>(exception.InnerException, exactMatch: false);
    }

    private static CompositionProfileInputSlot Normalize(
        string artifactClass,
        CompositionProfileLengthRuleDocument lengthRule,
        CompositionProfileInputNormalizationDocument normalization)
    {
        return CompositionProfileNormalizer.NormalizeInputSlot(Slot(artifactClass, lengthRule, normalization));
    }

    private static CompositionProfileInputSlotDocument Slot(
        string artifactClass,
        CompositionProfileLengthRuleDocument lengthRule,
        CompositionProfileInputNormalizationDocument normalization,
        string cardinality = "exactly-one")
    {
        return new CompositionProfileInputSlotDocument(
            "input",
            "source",
            artifactClass,
            true,
            cardinality,
            [".hex", ".bin"],
            new CompositionProfileInputAcceptanceDocument(lengthRule, normalization));
    }

    private static CompositionProfileLengthRuleDocument ExactBytes(string bytes)
    {
        return new CompositionProfileLengthRuleDocument("exact-bytes", Bytes: Number(bytes));
    }

    private static CompositionProfileLengthRuleDocument ExactMapCapacity()
    {
        return new CompositionProfileLengthRuleDocument("exact-resolved-map-capacity");
    }

    private static CompositionProfileLengthRuleDocument Bounded(string minimumBytes, string maximumBytes)
    {
        return new CompositionProfileLengthRuleDocument(
            "bounded",
            MinimumBytes: Number(minimumBytes),
            MaximumBytes: Number(maximumBytes));
    }

    private static CompositionProfileLengthRuleDocument NormalDpWarning()
    {
        return new CompositionProfileLengthRuleDocument(
            "normal-dp-extract-with-warning",
            IssueCode: "DP_SIZE_WARNING");
    }

    private static CompositionProfileLengthRuleDocument TpMaximum(string maximumBytes = "262144")
    {
        return new CompositionProfileLengthRuleDocument(
            "tp-maximum-256k",
            MaximumBytes: Number(maximumBytes));
    }

    private static CompositionProfileInputNormalizationDocument None()
    {
        return new CompositionProfileInputNormalizationDocument("none");
    }

    private static CompositionProfileInputNormalizationDocument PadShorter(string fillByte)
    {
        return new CompositionProfileInputNormalizationDocument(
            "pad-shorter",
            FillByte: Number(fillByte),
            EvidenceRef: "dp-padding-evidence");
    }

    private static CompositionProfileInputNormalizationDocument TruncateCtrlRam()
    {
        return new CompositionProfileInputNormalizationDocument(
            "truncate-ctrlram",
            WarningIssueCode: "CTRLRAM_TRUNCATED",
            EvidenceRef: "ctrlram-truncation-evidence");
    }

    private static JsonElement Number(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
