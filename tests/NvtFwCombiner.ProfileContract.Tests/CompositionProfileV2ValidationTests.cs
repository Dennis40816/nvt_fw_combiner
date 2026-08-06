using System.Numerics;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests immutable map-independent v2 validation values.</summary>
public sealed class CompositionProfileV2ValidationTests
{
    /// <summary>Verifies all five validation kinds retain exact logical references.</summary>
    [Fact]
    public void ValidationKindsKeepTypedLogicalReferences()
    {
        CompiledValidationFieldReference field = Field("cmd", "major");
        ValidationRequirementDefinition validation = new CompiledMetadataValueValidation(
            "version-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "VERSION_INVALID",
            field,
            CompiledValidationMetadataComparison.OneOf,
            [new CompiledValidationIntegerLiteral(1), new CompiledValidationIntegerLiteral(2)]);
        var pid = new CompiledPidSanityValidation(
            "pid-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "PID_INVALID",
            Field("fwconfig", "pid"));
        var equality = new CompiledMetadataEqualityValidation(
            "version-parity",
            CompiledValidationStage.ProfileCompile,
            CompiledValidationSeverity.Error,
            "VERSION_MISMATCH",
            Field("cmd", "major"),
            Field("legacy", "major"));
        var rejected = new CompiledRejectMetadataBytePatternValidation(
            "identity-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "IDENTITY_INVALID",
            Field("fwconfig", "pid"),
            [CompiledValidationRejectedBytePattern.AllFF, CompiledValidationRejectedBytePattern.AllZero]);
        var assertion = new CompiledViewByteAssertionValidation(
            "header-valid",
            CompiledValidationStage.FinalOutput,
            CompiledValidationSeverity.Error,
            "HEADER_INVALID",
            "header",
            new CompiledValidationBytes([0xA0]),
            new CompiledValidationBytes([0xF0]));

        CompiledMetadataValueValidation metadataValue =
            Assert.IsType<CompiledMetadataValueValidation>(validation);
        Assert.Equal(2, metadataValue.ExpectedValues.Count);
        Assert.Equal("pid", pid.Field.FieldId);
        Assert.Equal("legacy", equality.Right.BindingId);
        Assert.Equal(
            [CompiledValidationRejectedBytePattern.AllZero, CompiledValidationRejectedBytePattern.AllFF],
            rejected.RejectedPatterns);
        Assert.Equal("a0", assertion.Expected.Hex);
        Assert.Equal("f0", assertion.Mask?.Hex);
    }

    /// <summary>Verifies scalar literals preserve arbitrary integers without guessing field type.</summary>
    [Fact]
    public void ScalarLiteralsRemainUnboundAndLossless()
    {
        var integer = BigInteger.Parse(
            "18446744073709551616",
            System.Globalization.CultureInfo.InvariantCulture);
        CompiledValidationScalarLiteral integerValue = new CompiledValidationIntegerLiteral(integer);
        CompiledValidationScalarLiteral textValue = new CompiledValidationTextLiteral("0010");
        CompiledValidationIntegerLiteral integerLiteral =
            Assert.IsType<CompiledValidationIntegerLiteral>(integerValue);
        CompiledValidationTextLiteral textLiteral =
            Assert.IsType<CompiledValidationTextLiteral>(textValue);

        Assert.Equal(integer, integerLiteral.Value);
        Assert.Equal("0010", textLiteral.Value);
        _ = Assert.Throws<ArgumentException>(() => new CompiledValidationTextLiteral(string.Empty));
    }

    /// <summary>Verifies metadata comparison cardinality and value uniqueness are closed.</summary>
    [Fact]
    public void MetadataComparisonsRejectInvalidValueSets()
    {
        CompiledValidationFieldReference field = Field("cmd", "major");
        _ = Assert.Throws<ArgumentException>(() => MetadataValue(
            field,
            CompiledValidationMetadataComparison.Equal,
            []));
        _ = Assert.Throws<ArgumentException>(() => MetadataValue(
            field,
            CompiledValidationMetadataComparison.Equal,
            [new CompiledValidationIntegerLiteral(1), new CompiledValidationIntegerLiteral(2)]));
        _ = Assert.Throws<ArgumentException>(() => MetadataValue(
            field,
            CompiledValidationMetadataComparison.OneOf,
            [new CompiledValidationIntegerLiteral(1), new CompiledValidationIntegerLiteral(1)]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => MetadataValue(
            field,
            (CompiledValidationMetadataComparison)99,
            [new CompiledValidationIntegerLiteral(1)]));
    }

    /// <summary>Verifies rejected byte-pattern sets are immutable, known, and unambiguous.</summary>
    [Fact]
    public void RejectedBytePatternsAreClosedAndImmutable()
    {
        var patterns = new List<CompiledValidationRejectedBytePattern>
        {
            CompiledValidationRejectedBytePattern.AllFF,
            CompiledValidationRejectedBytePattern.AllZero,
        };
        var validation = new CompiledRejectMetadataBytePatternValidation(
            "identity-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "IDENTITY_INVALID",
            Field("fwconfig", "pid"),
            patterns);
        patterns.Clear();

        Assert.Equal(2, validation.RejectedPatterns.Count);
        _ = Assert.Throws<ArgumentException>(() => new CompiledRejectMetadataBytePatternValidation(
            "identity-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "IDENTITY_INVALID",
            Field("fwconfig", "pid"),
            []));
        _ = Assert.Throws<ArgumentException>(() => new CompiledRejectMetadataBytePatternValidation(
            "identity-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "IDENTITY_INVALID",
            Field("fwconfig", "pid"),
            [CompiledValidationRejectedBytePattern.AllZero, CompiledValidationRejectedBytePattern.AllZero]));
    }

    /// <summary>Verifies masked view assertions require equal non-empty byte widths.</summary>
    [Fact]
    public void ViewAssertionsRejectMaskWidthMismatch()
    {
        _ = Assert.Throws<ArgumentException>(() => new CompiledViewByteAssertionValidation(
            "header-valid",
            CompiledValidationStage.FinalOutput,
            CompiledValidationSeverity.Error,
            "HEADER_INVALID",
            "header",
            new CompiledValidationBytes([0xA0, 0x00]),
            new CompiledValidationBytes([0xF0])));
        _ = Assert.Throws<ArgumentException>(() => Assertion(
            new CompiledValidationBytes([0x00]),
            new CompiledValidationBytes([0x00])));
        _ = Assert.Throws<ArgumentException>(() => Assertion(
            new CompiledValidationBytes([0xAA]),
            new CompiledValidationBytes([0xFF])));
        _ = Assert.Throws<ArgumentException>(() => Assertion(
            new CompiledValidationBytes([0xA1]),
            new CompiledValidationBytes([0xF0])));
    }

    /// <summary>Verifies common validation identity and enum carriers fail closed.</summary>
    [Fact]
    public void ValidationsRejectInvalidCommonValuesAndNullReferences()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            CanonicalValidationDefinitionRules.RequireProfileDefinition(new CompiledPidSanityValidation(
                "Pid-Valid",
                CompiledValidationStage.InputLoad,
                CompiledValidationSeverity.Error,
                "PID_INVALID",
                Field("fwconfig", "pid"))));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompiledPidSanityValidation(
            "pid-valid",
            (CompiledValidationStage)99,
            CompiledValidationSeverity.Error,
            "PID_INVALID",
            Field("fwconfig", "pid")));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompiledPidSanityValidation(
            "pid-valid",
            CompiledValidationStage.InputLoad,
            (CompiledValidationSeverity)99,
            "PID_INVALID",
            Field("fwconfig", "pid")));
        _ = Assert.Throws<ArgumentException>(() =>
            CanonicalValidationDefinitionRules.RequireProfileDefinition(new CompiledPidSanityValidation(
                "pid-valid",
                CompiledValidationStage.InputLoad,
                CompiledValidationSeverity.Error,
                "pid-invalid",
                Field("fwconfig", "pid"))));
        _ = Assert.Throws<ArgumentNullException>(() => new CompiledPidSanityValidation(
            "pid-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "PID_INVALID",
            null!));
    }

    private static CompiledValidationFieldReference Field(string bindingId, string fieldId)
    {
        return new CompiledValidationFieldReference(bindingId, fieldId);
    }

    private static CompiledMetadataValueValidation MetadataValue(
        CompiledValidationFieldReference field,
        CompiledValidationMetadataComparison comparison,
        IEnumerable<CompiledValidationScalarLiteral> expectedValues)
    {
        return new CompiledMetadataValueValidation(
            "metadata-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "METADATA_INVALID",
            field,
            comparison,
            expectedValues);
    }

    private static CompiledViewByteAssertionValidation Assertion(
        CompiledValidationBytes expected,
        CompiledValidationBytes? mask)
    {
        return CanonicalValidationDefinitionRules.RequireProfileDefinition(
            new CompiledViewByteAssertionValidation(
                "header-valid",
                CompiledValidationStage.FinalOutput,
                CompiledValidationSeverity.Error,
                "HEADER_INVALID",
                "header",
                expected,
                mask));
    }
}
