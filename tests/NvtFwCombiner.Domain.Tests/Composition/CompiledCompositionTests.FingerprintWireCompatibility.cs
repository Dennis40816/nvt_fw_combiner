using System.Globalization;
using System.Reflection;
using System.Text;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompiledCompositionTests
{
    /// <summary>Every closed input policy retains its established fingerprint wire discriminator.</summary>
    [Fact]
    public void InputPolicyFingerprintWireCodesRemainCompatible()
    {
        (CompiledInputLengthRequirement Requirement, string Code, string Payload)[] lengths =
        [
            (new CompiledExactBytesInputLengthRequirement(4), "0", "10:input.kind=1:0\n11:input.bytes=1:4\n"),
            (new CompiledExactResolvedMapCapacityInputLengthRequirement(4), "1", "10:input.kind=1:1\n11:input.bytes=1:4\n"),
            (new CompiledBoundedInputLengthRequirement(1, 4), "2",
                "10:input.kind=1:2\n19:input.minimum-bytes=1:1\n19:input.maximum-bytes=1:4\n"),
            (new CompiledSourceViewCoverageInputLengthRequirement(
                maximumBytes: InputLengthPolicyLimits.MaximumTpFirmwareBytes), "4",
                "10:input.kind=1:4\n19:input.maximum-bytes=6:262144\n"),
            (new CompiledSourceViewCoverageInputLengthRequirement(
                [4], "OUTER", requiredEndExclusive: 4, shortInputIssueCode: "SHORT"), "5",
                "10:input.kind=1:5\n28:input.required-end-exclusive=1:4\n" +
                "33:input.expected-outer-length.count=1:1\n29:input.expected-outer-length.0=1:4\n" +
                "28:input.short-input-issue-code=5:SHORT\n" +
                "40:input.unexpected-outer-length-issue-code=5:OUTER\n"),
            (new CompiledSourceViewCoverageInputLengthRequirement([4], "OUTER"), "6",
                "10:input.kind=1:6\n33:input.expected-outer-length.count=1:1\n" +
                "29:input.expected-outer-length.0=1:4\n" +
                "40:input.unexpected-outer-length-issue-code=5:OUTER\n"),
        ];
        foreach ((CompiledInputLengthRequirement requirement, string code, string expectedPayload) in lengths)
        {
            string payload = InvokeFingerprintWriter(
                "AppendInputLengthRequirement",
                "input",
                requirement);
            Assert.Equal(code, ReadFingerprintField(payload, "input.kind"));
            Assert.Equal(expectedPayload, payload);
        }

        Assert.DoesNotContain(lengths, static item => item.Code == "3");

        (CompiledInputNormalization Normalization, string Code)[] normalizations =
        [
            (new CompiledNoInputNormalization(), "0"),
            (new CompiledPadShorterInputNormalization(0xFF, "padding-evidence"), "1"),
            (new CompiledTruncateCtrlRamInputNormalization("TRUNCATED", "truncation-evidence"), "2"),
        ];
        foreach ((CompiledInputNormalization normalization, string code) in normalizations)
        {
            string payload = InvokeFingerprintWriter(
                "AppendInputNormalization",
                "input",
                normalization);
            Assert.Equal(code, ReadFingerprintField(payload, "input.kind"));
        }
    }

    /// <summary>Every closed validation shape retains its established fingerprint wire discriminator.</summary>
    [Fact]
    public void ValidationFingerprintWireCodesRemainCompatible()
    {
        var field = new CompiledValidationFieldReference("metadata", "field");
        CompiledValidationRequirement[] requirements =
        [
            new CompiledMetadataValueValidation(
                "metadata-value", CompiledValidationStage.InputLoad, CompiledValidationSeverity.Error,
                "METADATA", field, CompiledValidationMetadataComparison.Equal,
                [new CompiledValidationIntegerLiteral(1)]),
            new CompiledPidSanityValidation(
                "pid", CompiledValidationStage.InputLoad, CompiledValidationSeverity.Error, "PID", field),
            new CompiledMetadataEqualityValidation(
                "equality", CompiledValidationStage.InputLoad, CompiledValidationSeverity.Error,
                "EQUALITY", field, new CompiledValidationFieldReference("metadata", "other")),
            new CompiledRejectMetadataBytePatternValidation(
                "pattern", CompiledValidationStage.InputLoad, CompiledValidationSeverity.Error,
                "PATTERN", field, [CompiledValidationRejectedBytePattern.AllZero]),
            new CompiledViewByteAssertionValidation(
                "view", CompiledValidationStage.FinalOutput, CompiledValidationSeverity.Error,
                "VIEW", "output", new FirmwareMetadataBytes([0x5A])),
            CompiledValidationRequirements.FirmwareConfigBackupVersion(
                "version", "INVALID", "MISMATCH", 1, 2),
            CompiledValidationRequirements.FirmwareConfigBackupPlacementAuthority(
                "placement", "PLACEMENT", "INACTIVE", "reference", new ByteRange(0, 4), 4),
            CompiledValidationRequirements.FirmwareConfigBackupExpectedAddress(
                "address", "ADDRESS", 0),
            CompiledValidationRequirements.RejectUniformInputRanges(
                "uniform", CompiledValidationSeverity.Error, "UNIFORM", "input", [new ByteRange(0, 1)]),
        ];
        for (int code = 0; code < requirements.Length; code++)
        {
            string payload = InvokeFingerprintWriter(
                "AppendValidationRequirements",
                new List<CompiledValidationRequirement> { requirements[code] });
            Assert.Equal(
                code.ToString(CultureInfo.InvariantCulture),
                ReadFingerprintField(payload, "validation.0.kind"));
        }

        (CompiledValidationScalarLiteral Literal, string Code)[] literals =
        [
            (new CompiledValidationIntegerLiteral(1), "0"),
            (new CompiledValidationTextLiteral("value"), "1"),
        ];
        foreach ((CompiledValidationScalarLiteral literal, string code) in literals)
        {
            string payload = InvokeFingerprintWriter(
                "AppendValidationLiterals",
                "literal",
                new List<CompiledValidationScalarLiteral> { literal });
            Assert.Equal(code, ReadFingerprintField(payload, "literal.0.kind"));
        }
    }

    /// <summary>Runtime-reference and canonical region-access codes remain stable.</summary>
    [Fact]
    public void ContextAndRegionAccessFingerprintWireCodesRemainCompatible()
    {
        var context = new RuntimeReferenceReplaceV2CompilationContext(
            CreateResolvedMap("cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"),
            allowsConditionalProcessor: false);
        string payload = InvokeFingerprintWriter(
            "AppendRuntimeReferenceCompilationContext",
            context,
            false);

        Assert.Equal("2", ReadFingerprintField(payload, "compilation-context"));
        Assert.Equal(0, (int)RegionAccessKind.Hidden);
        Assert.Equal(1, (int)RegionAccessKind.ReadOnly);
        Assert.Equal(2, (int)RegionAccessKind.Whole);
        Assert.Equal(3, (int)RegionAccessKind.Parts);
        Assert.Equal(4, (int)RegionAccessKind.ExplicitRange);
    }

    private static string InvokeFingerprintWriter(string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(CompiledComposition).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new InvalidOperationException($"Fingerprint writer '{methodName}' was not found.");
        var builder = new StringBuilder();
        _ = method.Invoke(null, [builder, .. arguments]);
        return builder.ToString();
    }

    private static string ReadFingerprintField(string payload, string fieldName)
    {
        string prefix = $"{fieldName.Length}:{fieldName}=";
        string line = Assert.Single(
            payload.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));
        int lengthSeparator = line.IndexOf(':', prefix.Length);
        int valueLength = int.Parse(
            line.AsSpan(prefix.Length, lengthSeparator - prefix.Length),
            CultureInfo.InvariantCulture);
        string value = line[(lengthSeparator + 1)..];
        Assert.Equal(valueLength, value.Length);
        return value;
    }
}
