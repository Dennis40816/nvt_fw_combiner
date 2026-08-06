using System.Numerics;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests immutable map-independent v2 validation values.</summary>
public sealed class CompositionProfileV2ValidationTests
{
    /// <summary>Verifies all five validation kinds retain exact logical references.</summary>
    [Fact]
    public void ValidationKindsKeepTypedLogicalReferences()
    {
        CompositionProfileMetadataFieldReference field = Field("cmd", "major");
        CompositionProfileValidation validation = new MetadataValueProfileValidation(
            "version-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "VERSION_INVALID",
            field,
            CompiledValidationMetadataComparison.OneOf,
            [new CompositionProfileIntegerLiteral(1), new CompositionProfileIntegerLiteral(2)]);
        var pid = new PidSanityProfileValidation(
            "pid-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "PID_INVALID",
            Field("fwconfig", "pid"));
        var equality = new MetadataEqualityProfileValidation(
            "version-parity",
            CompiledValidationStage.ProfileCompile,
            CompiledValidationSeverity.Error,
            "VERSION_MISMATCH",
            Field("cmd", "major"),
            Field("legacy", "major"));
        var rejected = new RejectMetadataBytePatternProfileValidation(
            "identity-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "IDENTITY_INVALID",
            Field("fwconfig", "pid"),
            [CompiledValidationRejectedBytePattern.AllFF, CompiledValidationRejectedBytePattern.AllZero]);
        var assertion = new ViewByteAssertionProfileValidation(
            "header-valid",
            CompiledValidationStage.FinalOutput,
            CompiledValidationSeverity.Error,
            "HEADER_INVALID",
            "header",
            new CompositionProfileByteValue([0xA0]),
            new CompositionProfileByteValue([0xF0]));

        MetadataValueProfileValidation metadataValue =
            Assert.IsType<MetadataValueProfileValidation>(validation);
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
        CompositionProfileScalarLiteral integerValue = new CompositionProfileIntegerLiteral(integer);
        CompositionProfileScalarLiteral textValue = new CompositionProfileTextLiteral("0010");
        CompositionProfileIntegerLiteral integerLiteral =
            Assert.IsType<CompositionProfileIntegerLiteral>(integerValue);
        CompositionProfileTextLiteral textLiteral =
            Assert.IsType<CompositionProfileTextLiteral>(textValue);

        Assert.Equal(integer, integerLiteral.Value);
        Assert.Equal("0010", textLiteral.Value);
        _ = Assert.Throws<ArgumentException>(() => new CompositionProfileTextLiteral(string.Empty));
    }

    /// <summary>Verifies metadata comparison cardinality and value uniqueness are closed.</summary>
    [Fact]
    public void MetadataComparisonsRejectInvalidValueSets()
    {
        CompositionProfileMetadataFieldReference field = Field("cmd", "major");
        _ = Assert.Throws<ArgumentException>(() => MetadataValue(
            field,
            CompiledValidationMetadataComparison.Equal,
            []));
        _ = Assert.Throws<ArgumentException>(() => MetadataValue(
            field,
            CompiledValidationMetadataComparison.Equal,
            [new CompositionProfileIntegerLiteral(1), new CompositionProfileIntegerLiteral(2)]));
        _ = Assert.Throws<ArgumentException>(() => MetadataValue(
            field,
            CompiledValidationMetadataComparison.OneOf,
            [new CompositionProfileIntegerLiteral(1), new CompositionProfileIntegerLiteral(1)]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => MetadataValue(
            field,
            (CompiledValidationMetadataComparison)99,
            [new CompositionProfileIntegerLiteral(1)]));
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
        var validation = new RejectMetadataBytePatternProfileValidation(
            "identity-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "IDENTITY_INVALID",
            Field("fwconfig", "pid"),
            patterns);
        patterns.Clear();

        Assert.Equal(2, validation.RejectedPatterns.Count);
        _ = Assert.Throws<ArgumentException>(() => new RejectMetadataBytePatternProfileValidation(
            "identity-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "IDENTITY_INVALID",
            Field("fwconfig", "pid"),
            []));
        _ = Assert.Throws<ArgumentException>(() => new RejectMetadataBytePatternProfileValidation(
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
        _ = Assert.Throws<ArgumentException>(() => new ViewByteAssertionProfileValidation(
            "header-valid",
            CompiledValidationStage.FinalOutput,
            CompiledValidationSeverity.Error,
            "HEADER_INVALID",
            "header",
            new CompositionProfileByteValue([0xA0, 0x00]),
            new CompositionProfileByteValue([0xF0])));
        _ = Assert.Throws<ArgumentException>(() => Assertion(
            new CompositionProfileByteValue([0x00]),
            new CompositionProfileByteValue([0x00])));
        _ = Assert.Throws<ArgumentException>(() => Assertion(
            new CompositionProfileByteValue([0xAA]),
            new CompositionProfileByteValue([0xFF])));
        _ = Assert.Throws<ArgumentException>(() => Assertion(
            new CompositionProfileByteValue([0xA1]),
            new CompositionProfileByteValue([0xF0])));
    }

    /// <summary>Verifies common validation identity and enum carriers fail closed.</summary>
    [Fact]
    public void ValidationsRejectInvalidCommonValuesAndNullReferences()
    {
        _ = Assert.Throws<ArgumentException>(() => new PidSanityProfileValidation(
            "Pid-Valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "PID_INVALID",
            Field("fwconfig", "pid")));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new PidSanityProfileValidation(
            "pid-valid",
            (CompiledValidationStage)99,
            CompiledValidationSeverity.Error,
            "PID_INVALID",
            Field("fwconfig", "pid")));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new PidSanityProfileValidation(
            "pid-valid",
            CompiledValidationStage.InputLoad,
            (CompiledValidationSeverity)99,
            "PID_INVALID",
            Field("fwconfig", "pid")));
        _ = Assert.Throws<ArgumentException>(() => new PidSanityProfileValidation(
            "pid-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "pid-invalid",
            Field("fwconfig", "pid")));
        _ = Assert.Throws<ArgumentNullException>(() => new PidSanityProfileValidation(
            "pid-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "PID_INVALID",
            null!));
    }

    private static CompositionProfileMetadataFieldReference Field(string bindingId, string fieldId)
    {
        return new CompositionProfileMetadataFieldReference(bindingId, fieldId);
    }

    private static MetadataValueProfileValidation MetadataValue(
        CompositionProfileMetadataFieldReference field,
        CompiledValidationMetadataComparison comparison,
        IEnumerable<CompositionProfileScalarLiteral> expectedValues)
    {
        return new MetadataValueProfileValidation(
            "metadata-valid",
            CompiledValidationStage.InputLoad,
            CompiledValidationSeverity.Error,
            "METADATA_INVALID",
            field,
            comparison,
            expectedValues);
    }

    private static ViewByteAssertionProfileValidation Assertion(
        CompositionProfileByteValue expected,
        CompositionProfileByteValue? mask)
    {
        return new ViewByteAssertionProfileValidation(
            "header-valid",
            CompiledValidationStage.FinalOutput,
            CompiledValidationSeverity.Error,
            "HEADER_INVALID",
            "header",
            expected,
            mask);
    }
}
