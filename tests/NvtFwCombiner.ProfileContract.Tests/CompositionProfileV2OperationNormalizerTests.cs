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
        CopyOrReplaceProfileOperation copy = Assert.IsType<CopyOrReplaceProfileOperation>(Normalize(Operation(
            "copy-range",
            sourceViewId: "source",
            targetViewId: "target")));
        CopyOrReplaceProfileOperation replace = Assert.IsType<CopyOrReplaceProfileOperation>(Normalize(Operation(
            "replace-range",
            sourceViewId: "replacement",
            targetViewId: "target")));
        FillRangeProfileOperation fill = Assert.IsType<FillRangeProfileOperation>(Normalize(Operation(
            "fill-range",
            targetViewId: "target",
            fillByte: Number("255"))));
        PatchScalarProfileOperation patch = Assert.IsType<PatchScalarProfileOperation>(Normalize(Operation(
            "patch-scalar",
            targetViewId: "target",
            valueHex: "aa01")));
        TransformScalarProfileOperation transform = Assert.IsType<TransformScalarProfileOperation>(Normalize(Transform(
            Number("18446744073709551616"),
            Number("4"),
            "little",
            Number("-18446744073709551617"),
            Number("32"))));
        RunProcessorProfileOperation processor = Assert.IsType<RunProcessorProfileOperation>(Normalize(Operation(
            "run-processor",
            processorStageId: "legacy-postbuild")));

        Assert.Equal(CompositionOperationKind.CopyRange, copy.Kind);
        Assert.Equal(CompositionOperationKind.ReplaceRange, replace.Kind);
        Assert.Equal(0xFF, fill.FillByte);
        Assert.Equal("aa01", patch.Value.Hex);
        Assert.Equal(
            BigInteger.Parse("18446744073709551616", System.Globalization.CultureInfo.InvariantCulture),
            transform.Sequence);
        Assert.Equal(
            BigInteger.Parse("-18446744073709551617", System.Globalization.CultureInfo.InvariantCulture),
            transform.Addend);
        Assert.Equal(ScalarTransformByteOrder.LittleEndian, transform.ByteOrder);
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
            CompositionProfileOperation operation = Normalize(Operation(
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
        TransformScalarProfileOperation transform = Assert.IsType<TransformScalarProfileOperation>(Normalize(Transform(
            Number("0"),
            Number(width),
            "big",
            Number("0"),
            Number(expectedBefore))));

        Assert.Equal(int.Parse(width, System.Globalization.CultureInfo.InvariantCulture), (int)transform.Width);
        Assert.Equal(ScalarTransformByteOrder.BigEndian, transform.ByteOrder);
        Assert.Equal(ulong.Parse(expectedBefore, System.Globalization.CultureInfo.InvariantCulture), transform.ExpectedBefore);
    }

    /// <summary>Verifies a region-instance delta retains both explicit placement identities.</summary>
    [Fact]
    public void TransformMapsRegionInstanceDeltaAddend()
    {
        TransformScalarProfileOperation transform = Assert.IsType<TransformScalarProfileOperation>(Normalize(Transform(
            Number("0"),
            Number("4"),
            "little",
            RegionInstanceDeltaAddend(
                "region-instance-delta",
                sourceRegionInstanceId: "a-bank",
                targetRegionInstanceId: "b-bank")),
            "2.14"));

        ScalarTransformAddendSource addend = transform.AddendSource;
        Assert.Equal(ScalarTransformAddendSourceKind.RegionInstanceDelta, addend.Kind);
        Assert.Equal("a-bank", addend.SourceRegionInstanceId);
        Assert.Equal("b-bank", addend.TargetRegionInstanceId);
    }

    /// <summary>Verifies unknown discriminators and fixed transform policies retain exact paths.</summary>
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
        CompositionProfileNormalizationException interpretation = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(
                Number("0"),
                Number("4"),
                "little",
                Number("0"),
                valueInterpretation: "signed")));
        CompositionProfileNormalizationException overflow = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(
                Number("0"),
                Number("4"),
                "little",
                Number("0"),
                overflowPolicy: "wrap")));

        Assert.Equal("operations[0].kind", kind.Path);
        Assert.Equal("operations[0].overlapPolicy", overlap.Path);
        Assert.Equal("operations[0].byteOrder", byteOrder.Path);
        Assert.Equal("operations[0].valueInterpretation", interpretation.Path);
        Assert.Equal("operations[0].overflowPolicy", overflow.Path);
    }

    /// <summary>Verifies required union fields fail at their exact source paths.</summary>
    [Fact]
    public void OperationRejectsMissingUnionMembersWithPaths()
    {
        CompositionProfileNormalizationException source = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Operation("copy-range", targetViewId: "target")));
        CompositionProfileNormalizationException target = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Operation("fill-range", fillByte: Number("0"))));
        CompositionProfileNormalizationException fill = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Operation("fill-range", targetViewId: "target")));
        CompositionProfileNormalizationException value = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Operation("patch-scalar", targetViewId: "target")));
        CompositionProfileNormalizationException width = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(Number("0"), null, "little", Number("0"))));
        CompositionProfileNormalizationException addend = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(Number("0"), Number("4"), "little", null)));
        CompositionProfileNormalizationException processor = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Operation("run-processor")));

        Assert.Equal("operations[0].sourceViewId", source.Path);
        Assert.Equal("operations[0].targetViewId", target.Path);
        Assert.Equal("operations[0].fillByte", fill.Path);
        Assert.Equal("operations[0].valueHex", value.Path);
        Assert.Equal("operations[0].widthBytes", width.Path);
        Assert.Equal("operations[0].addend", addend.Path);
        Assert.Equal("operations[0].processorStageId", processor.Path);
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

        Assert.Equal("operations[0].sequence", sequence.Path);
        Assert.Equal("operations[0].fillByte", fill.Path);
        Assert.Equal("operations[0].valueHex", patch.Path);
        Assert.Equal("operations[0].widthBytes", width.Path);
        Assert.Equal("operations[0].addend", addend.Path);
        Assert.Equal("operations[0].expectedBefore", expected.Path);
    }

    /// <summary>Verifies region-instance addend objects reject unknown and incomplete identities.</summary>
    [Fact]
    public void TransformRejectsInvalidRegionInstanceDeltaWithPaths()
    {
        CompositionProfileNormalizationException kind = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(
                Number("0"),
                Number("4"),
                "little",
                RegionInstanceDeltaAddend(
                    "future",
                    sourceRegionInstanceId: "a-bank",
                    targetRegionInstanceId: "b-bank")),
                "2.14"));
        CompositionProfileNormalizationException source = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(
                Number("0"),
                Number("4"),
                "little",
                RegionInstanceDeltaAddend(
                    "region-instance-delta",
                    targetRegionInstanceId: "b-bank")),
                "2.14"));
        CompositionProfileNormalizationException target = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(
                Number("0"),
                Number("4"),
                "little",
                RegionInstanceDeltaAddend(
                    "region-instance-delta",
                    sourceRegionInstanceId: "a-bank")),
                "2.14"));
        CompositionProfileNormalizationException nonCanonical = Assert.Throws<CompositionProfileNormalizationException>(() =>
            Normalize(Transform(
                Number("0"),
                Number("4"),
                "little",
                RegionInstanceDeltaAddend(
                    "region-instance-delta",
                    sourceRegionInstanceId: "A-bank",
                    targetRegionInstanceId: "b-bank")),
                "2.14"));

        Assert.Equal("operations[0].addend.kind", kind.Path);
        Assert.Equal("operations[0].addend.sourceRegionInstanceId", source.Path);
        Assert.Equal("operations[0].addend.targetRegionInstanceId", target.Path);
        Assert.Equal("operations[0].addend", nonCanonical.Path);
    }

    /// <summary>Verifies older profile schemas cannot gain geometry-derived addend authority.</summary>
    [Fact]
    public void TransformRejectsRegionInstanceDeltaBeforeSchemaV214()
    {
        CompositionProfileNormalizationException exception =
            Assert.Throws<CompositionProfileNormalizationException>(() =>
                Normalize(
                    Transform(
                        Number("0"),
                        Number("4"),
                        "little",
                        RegionInstanceDeltaAddend(
                            "region-instance-delta",
                            sourceRegionInstanceId: "a-bank",
                            targetRegionInstanceId: "b-bank")),
                    "2.13"));

        Assert.Equal("operations[0].addend", exception.Path);
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

    private static CompositionProfileOperation Normalize(
        CompositionProfileOperationDocument document,
        string schemaVersion = "2.0")
    {
        return CompositionProfileNormalizer.NormalizeOperation(
            document,
            schemaVersion: schemaVersion);
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
