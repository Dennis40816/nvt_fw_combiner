using System.Numerics;
using System.Text.Json;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests DTO normalization for v2 closed validation declarations.</summary>
public sealed class CompositionProfileV2ValidationNormalizerTests
{
    /// <summary>Verifies all five validation kinds retain exact logical references.</summary>
    [Fact]
    public void ValidationMapsEveryKind()
    {
        CompiledMetadataValueValidation metadata = Assert.IsType<CompiledMetadataValueValidation>(Normalize(Validation(
            "metadata-value",
            field: Field("cmd", "major"),
            comparison: "one-of",
            expectedValues: [Number("1"), Text("02")])));
        CompiledPidSanityValidation pid = Assert.IsType<CompiledPidSanityValidation>(Normalize(Validation(
            "pid-sanity",
            field: Field("fwconfig", "pid"))));
        CompiledMetadataEqualityValidation equality = Assert.IsType<CompiledMetadataEqualityValidation>(Normalize(Validation(
            "metadata-equality",
            left: Field("cmd", "major"),
            right: Field("legacy", "major"))));
        CompiledRejectMetadataBytePatternValidation patterns = Assert.IsType<CompiledRejectMetadataBytePatternValidation>(Normalize(Validation(
            "reject-metadata-byte-pattern",
            field: Field("fwconfig", "pid"),
            rejectedPatterns: ["all-ff", "all-zero"])));
        CompiledViewByteAssertionValidation assertion = Assert.IsType<CompiledViewByteAssertionValidation>(Normalize(Validation(
            "view-byte-assertion",
            viewId: "header",
            expectedHex: "a0",
            maskHex: "f0")));

        Assert.Equal(CompiledValidationMetadataComparison.OneOf, metadata.Comparison);
        _ = Assert.IsType<CompiledValidationIntegerLiteral>(metadata.ExpectedValues[0]);
        _ = Assert.IsType<CompiledValidationTextLiteral>(metadata.ExpectedValues[1]);
        Assert.Equal("pid", pid.Field.FieldId);
        Assert.Equal("legacy", equality.Right.BindingId);
        Assert.Equal(
            [CompiledValidationRejectedBytePattern.AllZero, CompiledValidationRejectedBytePattern.AllFF],
            patterns.RejectedPatterns);
        Assert.Equal("a0", assertion.Expected.Hex);
        Assert.Equal("f0", assertion.Mask?.Hex);
    }

    /// <summary>Verifies all stages, severities, and metadata comparisons map without fallback.</summary>
    [Fact]
    public void ValidationMapsEveryCommonPolicyToken()
    {
        string[] stages = ["profile-compile", "input-load", "pre-operation", "post-operation", "final-output"];
        string[] severities = ["info", "warning", "error"];
        string[] comparisons = ["equals", "not-equals", "one-of"];

        Assert.Equal(
            Enum.GetValues<CompiledValidationStage>(),
            stages.Select(stage => Normalize(Validation("pid-sanity", stage: stage, field: Field())).Stage));
        Assert.Equal(
            Enum.GetValues<CompiledValidationSeverity>(),
            severities.Select(severity => Normalize(Validation(
                "pid-sanity",
                severity: severity,
                field: Field())).Severity));
        Assert.Equal(
            Enum.GetValues<CompiledValidationMetadataComparison>(),
            comparisons.Select(comparison => Assert.IsType<CompiledMetadataValueValidation>(Normalize(Validation(
                "metadata-value",
                field: Field(),
                comparison: comparison,
                expectedValues: comparison == "one-of"
                    ? [Number("1"), Number("2")]
                    : [Number("1")]))).Comparison));
    }

    /// <summary>Verifies scalar literals remain lossless and unbound to family field encoding.</summary>
    [Fact]
    public void MetadataValuePreservesIntegerAndTextLiterals()
    {
        CompiledMetadataValueValidation validation = Assert.IsType<CompiledMetadataValueValidation>(Normalize(Validation(
            "metadata-value",
            field: Field(),
            comparison: "one-of",
            expectedValues: [Number("18446744073709551616.0"), Text("0010")])));

        CompiledValidationIntegerLiteral integer = Assert.IsType<CompiledValidationIntegerLiteral>(validation.ExpectedValues[0]);
        CompiledValidationTextLiteral text = Assert.IsType<CompiledValidationTextLiteral>(validation.ExpectedValues[1]);
        Assert.Equal(
            BigInteger.Parse("18446744073709551616", System.Globalization.CultureInfo.InvariantCulture),
            integer.Value);
        Assert.Equal("0010", text.Value);
    }

    /// <summary>Verifies unknown closed-union tokens fail at exact source paths.</summary>
    [Fact]
    public void ValidationRejectsUnknownTokensWithPaths()
    {
        CompositionProfileNormalizationException kind = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation("future")));
        CompositionProfileNormalizationException stage = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation("pid-sanity", stage: "future", field: Field())));
        CompositionProfileNormalizationException severity = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation("pid-sanity", severity: "future", field: Field())));
        CompositionProfileNormalizationException comparison = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation(
                "metadata-value",
                field: Field(),
                comparison: "future",
                expectedValues: [Number("1")])));
        CompositionProfileNormalizationException pattern = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation(
                "reject-metadata-byte-pattern",
                field: Field(),
                rejectedPatterns: ["future"])));

        Assert.Equal("validations[0].kind", kind.Path);
        Assert.Equal("validations[0].stage", stage.Path);
        Assert.Equal("validations[0].severity", severity.Path);
        Assert.Equal("validations[0].operator", comparison.Path);
        Assert.Equal("validations[0].rejectedPatterns[0]", pattern.Path);
    }

    /// <summary>Verifies noncanonical metadata references fail at their exact source members.</summary>
    [Fact]
    public void ValidationRejectsNonCanonicalFieldReferencesWithPaths()
    {
        CompositionProfileNormalizationException field = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation("pid-sanity", field: Field("Fwconfig", "pid"))));
        CompositionProfileNormalizationException left = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation("metadata-equality", left: Field("Cmd", "major"), right: Field())));
        CompositionProfileNormalizationException right = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation("metadata-equality", left: Field(), right: Field("Legacy", "major"))));

        Assert.Equal("validations[0].field", field.Path);
        Assert.Equal("validations[0].left", left.Path);
        Assert.Equal("validations[0].right", right.Path);
    }

    /// <summary>Verifies invalid scalar and hexadecimal values retain exact element paths.</summary>
    [Fact]
    public void ValidationRejectsInvalidLiteralValuesWithPaths()
    {
        CompositionProfileNormalizationException scalarKind = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation(
                "metadata-value",
                field: Field(),
                comparison: "equals",
                expectedValues: [Boolean(true)])));
        CompositionProfileNormalizationException emptyText = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation(
                "metadata-value",
                field: Field(),
                comparison: "equals",
                expectedValues: [Text(string.Empty)])));
        CompositionProfileNormalizationException expectedHex = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation("view-byte-assertion", viewId: "header", expectedHex: "AA")));
        CompositionProfileNormalizationException maskHex = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation(
                "view-byte-assertion",
                viewId: "header",
                expectedHex: "aa",
                maskHex: "F0")));

        Assert.Equal("validations[0].expectedValues[0]", scalarKind.Path);
        Assert.Equal("validations[0].expectedValues[0]", emptyText.Path);
        Assert.Equal("validations[0].expectedHex", expectedHex.Path);
        Assert.Equal("validations[0].maskHex", maskHex.Path);
    }

    /// <summary>Verifies comparison and mask cross-field invariants fail at the rule path.</summary>
    [Fact]
    public void ValidationRejectsInvalidCrossFieldValuesAtRulePath()
    {
        CompositionProfileNormalizationException comparison = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation(
                "metadata-value",
                field: Field(),
                comparison: "equals",
                expectedValues: [Number("1"), Number("2")])));
        CompositionProfileNormalizationException mask = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Validation(
                "view-byte-assertion",
                viewId: "header",
                expectedHex: "a000",
                maskHex: "f0")));

        Assert.Equal("validations[0]", comparison.Path);
        Assert.Equal("validations[0]", mask.Path);
        _ = Assert.IsType<ArgumentException>(comparison.InnerException, exactMatch: false);
        _ = Assert.IsType<ArgumentException>(mask.InnerException, exactMatch: false);
    }

    private static ValidationRequirementDefinition Normalize(CompositionProfileValidationDocument document)
    {
        return CompositionProfileNormalizer.NormalizeValidation(document);
    }

    private static CompositionProfileValidationDocument Validation(
        string kind,
        string stage = "input-load",
        string severity = "error",
        CompositionProfileMetadataFieldReferenceDocument? field = null,
        string? comparison = null,
        IReadOnlyList<JsonElement>? expectedValues = null,
        CompositionProfileMetadataFieldReferenceDocument? left = null,
        CompositionProfileMetadataFieldReferenceDocument? right = null,
        IReadOnlyList<string>? rejectedPatterns = null,
        string? viewId = null,
        string? expectedHex = null,
        string? maskHex = null)
    {
        return new CompositionProfileValidationDocument(
            "validation",
            stage,
            severity,
            "VALIDATION_FAILED",
            kind,
            Field: field,
            Operator: comparison,
            ExpectedValues: expectedValues,
            Left: left,
            Right: right,
            RejectedPatterns: rejectedPatterns,
            ViewId: viewId,
            ExpectedHex: expectedHex,
            MaskHex: maskHex);
    }

    private static CompositionProfileMetadataFieldReferenceDocument Field(
        string bindingId = "fwconfig",
        string fieldId = "pid")
    {
        return new CompositionProfileMetadataFieldReferenceDocument(bindingId, fieldId);
    }

    private static JsonElement Number(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static JsonElement Text(string value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.Clone();
    }

    private static JsonElement Boolean(bool value)
    {
        using var document = JsonDocument.Parse(value ? "true" : "false");
        return document.RootElement.Clone();
    }
}
