using System.Numerics;
using System.Text.Json;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests DTO normalization for v2 closed composition operations.</summary>
public sealed class CompositionProfileV2OperationNormalizerTests
{
    /// <summary>Verifies all six operation kinds preserve typed logical references and values.</summary>
    [Fact]
    public void OperationMapsEveryKind()
    {
        CompositionOperationDefinition copy = Normalize(Operation(
            "copy-range",
            sourceViewId: "source",
            targetViewId: "target"));
        CompositionOperationDefinition replace = Normalize(Operation(
            "replace-range",
            sourceViewId: "replacement",
            targetViewId: "target"));
        CompositionOperationDefinition fill = Normalize(Operation(
            "fill-range",
            targetViewId: "target",
            fillByte: Number("255")));
        CompositionOperationDefinition patch = Normalize(Operation(
            "patch-scalar",
            targetViewId: "target",
            valueHex: "aa01"));
        CompositionOperationDefinition transform = Normalize(Transform(
            Number("18446744073709551616"),
            Number("4"),
            "little",
            Number("-18446744073709551617"),
            Number("32")));
        CompositionOperationDefinition processor = Normalize(Operation(
            "run-processor",
            processorStageId: "legacy-postbuild"));

        Assert.Equal(CompositionOperationKind.CopyRange, copy.Kind);
        Assert.Equal(CompositionOperationKind.ReplaceRange, replace.Kind);
        Assert.Equal(0xFF, fill.FillByte);
        Assert.Equal("aa01", patch.PatchBytes.Hex);
        Assert.Equal(
            BigInteger.Parse("18446744073709551616", System.Globalization.CultureInfo.InvariantCulture),
            transform.Sequence);
        Assert.Equal(
            BigInteger.Parse("-18446744073709551617", System.Globalization.CultureInfo.InvariantCulture),
            transform.Addend);
        Assert.Equal(ScalarTransformByteOrder.LittleEndian, transform.TransformByteOrder);
        Assert.Equal("legacy-postbuild", processor.ProcessorStageId);
    }

    /// <summary>Verifies all overlap policies map without fallback.</summary>
    [Fact]
    public void OperationMapsEveryOverlapPolicy()
    {
        (string Token, OverlapPolicy Expected)[] cases =
        [
            ("reject", OverlapPolicy.Reject),
            ("allow-declared", OverlapPolicy.AllowDeclared),
            ("replace-existing", OverlapPolicy.ReplaceExisting),
        ];

        foreach ((string token, OverlapPolicy expected) in cases)
        {
            CompositionOperationDefinition operation = Normalize(Operation(
                "fill-range",
                overlapPolicy: token,
                targetViewId: "target",
                fillByte: Number("0")));
            Assert.Equal(expected, operation.OverlapPolicy);
        }
    }

    /// <summary>Verifies every scalar width accepts its exact unsigned boundary.</summary>
    [Theory]
    [InlineData("1", "255")]
    [InlineData("2", "65535")]
    [InlineData("4", "4294967295")]
    [InlineData("8", "18446744073709551615")]
    public void TransformMapsEveryWidthBoundary(string width, string expectedBefore)
    {
        CompositionOperationDefinition transform = Normalize(Transform(
            Number("0"),
            Number(width),
            "big",
            Number("0"),
            Number(expectedBefore)));

        Assert.Equal(int.Parse(width, System.Globalization.CultureInfo.InvariantCulture), (int)transform.TransformWidth);
        Assert.Equal(ScalarTransformByteOrder.BigEndian, transform.TransformByteOrder);
        Assert.Equal(ulong.Parse(expectedBefore, System.Globalization.CultureInfo.InvariantCulture), transform.ExpectedBefore);
    }

    /// <summary>Verifies a region-instance delta retains both explicit placement identities.</summary>
    [Fact]
    public void TransformMapsRegionInstanceDeltaAddend()
    {
        CompositionOperationDefinition transform = Normalize(Transform(
            Number("0"),
            Number("4"),
            "little",
            RegionInstanceDeltaAddend(
                "region-instance-delta",
                sourceRegionInstanceId: "a-bank",
                targetRegionInstanceId: "b-bank")));

        ScalarTransformAddendSource addend = transform.AddendSource;
        Assert.Equal(ScalarTransformAddendSourceKind.RegionInstanceDelta, addend.Kind);
        Assert.Equal("a-bank", addend.SourceRegionInstanceId);
        Assert.Equal("b-bank", addend.TargetRegionInstanceId);
    }

    /// <summary>Verifies unknown discriminators retain exact paths after schema admission.</summary>
    [Fact]
    public void OperationRejectsUnknownPolicyTokensWithPaths()
    {
        CompositionProfileNormalizationException kind = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Operation("future")));
        CompositionProfileNormalizationException overlap = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Operation(
                "fill-range",
                overlapPolicy: "future",
                targetViewId: "target",
                fillByte: Number("0"))));
        CompositionProfileNormalizationException byteOrder = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(Number("0"), Number("4"), "middle", Number("0"))));
        Assert.Equal("operations[0].kind", kind.Path);
        Assert.Equal("operations[0].overlapPolicy", overlap.Path);
        Assert.Equal("operations[0].byteOrder", byteOrder.Path);
    }

    /// <summary>Verifies numeric and byte values fail closed without lossy coercion.</summary>
    [Fact]
    public void OperationRejectsInvalidScalarValuesWithPaths()
    {
        CompositionProfileNormalizationException sequence = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Operation(
                "fill-range",
                sequence: Number("-1"),
                targetViewId: "target",
                fillByte: Number("0"))));
        CompositionProfileNormalizationException fill = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Operation("fill-range", targetViewId: "target", fillByte: Number("256"))));
        CompositionProfileNormalizationException patch = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Operation("patch-scalar", targetViewId: "target", valueHex: "AA")));
        CompositionProfileNormalizationException width = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(Number("0"), Number("3"), "little", Number("0"))));
        CompositionProfileNormalizationException addend = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(Number("0"), Number("4"), "little", Number("1.5"))));
        CompositionProfileNormalizationException expected = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(
                Number("0"),
                Number("8"),
                "little",
                Number("0"),
                Number("18446744073709551616"))));

        Assert.Equal("operations[0]", sequence.Path);
        Assert.Equal("operations[0].fillByte", fill.Path);
        Assert.Equal("operations[0].valueHex", patch.Path);
        Assert.Equal("operations[0].widthBytes", width.Path);
        Assert.Equal("operations[0].addend", addend.Path);
        Assert.Equal("operations[0].expectedBefore", expected.Path);
    }

    /// <summary>Verifies region-instance identities retain canonical Domain invariants.</summary>
    [Fact]
    public void TransformRejectsInvalidRegionInstanceDeltaWithPaths()
    {
        CompositionProfileNormalizationException source = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(
                Number("0"),
                Number("4"),
                "little",
                RegionInstanceDeltaAddend(
                    "region-instance-delta",
                    targetRegionInstanceId: "b-bank"))));
        CompositionProfileNormalizationException target = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(
                Number("0"),
                Number("4"),
                "little",
                RegionInstanceDeltaAddend(
                    "region-instance-delta",
                    sourceRegionInstanceId: "a-bank"))));
        CompositionProfileNormalizationException nonCanonical = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(
                Number("0"),
                Number("4"),
                "little",
                RegionInstanceDeltaAddend(
                    "region-instance-delta",
                    sourceRegionInstanceId: "A-bank",
                    targetRegionInstanceId: "b-bank"))));

        Assert.Equal("operations[0].addend", source.Path);
        Assert.Equal("operations[0].addend", target.Path);
        Assert.Equal("operations[0]", nonCanonical.Path);
    }

    /// <summary>Verifies expected-before must fit the selected scalar width.</summary>
    [Fact]
    public void TransformRejectsCrossFieldWidthOverflowAtOperationPath()
    {
        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(Number("0"), Number("1"), "little", Number("0"), Number("256"))));

        Assert.Equal("operations[0]", exception.Path);
        _ = Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException, exactMatch: false);
    }

    private static CompositionOperationDefinition Normalize(CompositionProfileOperationDocument document)
    {
        return CompositionProfileNormalizer.NormalizeOperation(document);
    }

    private static CompositionProfileOperationDocument Operation(
        string kind,
        JsonElement? sequence = null,
        string overlapPolicy = "reject",
        string? sourceViewId = null,
        string? targetViewId = null,
        JsonElement? fillByte = null,
        string? valueHex = null,
        string? processorStageId = null)
    {
        return new CompositionProfileOperationDocument(
            "operation",
            sequence ?? Number("0"),
            overlapPolicy,
            "Owner-approved operation.",
            kind,
            SourceViewId: sourceViewId,
            TargetViewId: targetViewId,
            FillByte: fillByte,
            ValueHex: valueHex,
            ProcessorStageId: processorStageId);
    }

    private static CompositionProfileOperationDocument Transform(
        JsonElement sequence,
        JsonElement? width,
        string byteOrder,
        JsonElement? addend,
        JsonElement? expectedBefore = null,
        string valueInterpretation = "unsigned",
        string overflowPolicy = "reject")
    {
        return new CompositionProfileOperationDocument(
            "transform",
            sequence,
            "reject",
            "Owner-approved scalar transform.",
            "transform-scalar",
            SourceViewId: "source",
            TargetViewId: "target",
            WidthBytes: width,
            ByteOrder: byteOrder,
            ValueInterpretation: valueInterpretation,
            Addend: addend,
            ExpectedBefore: expectedBefore,
            OverflowPolicy: overflowPolicy);
    }

    private static JsonElement Number(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static JsonElement RegionInstanceDeltaAddend(
        string kind,
        string? sourceRegionInstanceId = null,
        string? targetRegionInstanceId = null)
    {
        var properties = new Dictionary<string, string>
        {
            ["kind"] = kind,
        };
        if (sourceRegionInstanceId is not null)
        {
            properties["sourceRegionInstanceId"] = sourceRegionInstanceId;
        }

        if (targetRegionInstanceId is not null)
        {
            properties["targetRegionInstanceId"] = targetRegionInstanceId;
        }

        return JsonSerializer.SerializeToElement(properties);
    }
}
