using System.Numerics;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests immutable map-independent v2 operation algebra values.</summary>
public sealed class CompositionProfileV2OperationTests
{
    /// <summary>Verifies all six operation kinds retain logical references and closed policy.</summary>
    [Fact]
    public void OperationKindsKeepTypedLogicalReferences()
    {
        var copy = new CopyOrReplaceProfileOperation(
            "copy-code", 0, OverlapPolicy.Reject, "copy", CompositionProfileOperationKind.CopyRange,
            "source", "target");
        var replace = new CopyOrReplaceProfileOperation(
            "replace-code", 1, OverlapPolicy.ReplaceExisting, "replace",
            CompositionProfileOperationKind.ReplaceRange, "replacement", "target");
        var fill = new FillRangeProfileOperation(
            "fill-gap", 2, OverlapPolicy.Reject, "fill", "gap", 0xFF);
        var patch = new PatchScalarProfileOperation(
            "patch-header", 3, OverlapPolicy.AllowDeclared, "patch", "header-field",
            new CompositionProfileByteValue([0x01, 0x02]));
        var transform = new TransformScalarProfileOperation(
            "relocate", 4, OverlapPolicy.ReplaceExisting, "relocate", "source-scalar", "target-scalar",
            CompositionProfileScalarWidth.FourBytes, CompositionProfileScalarByteOrder.LittleEndian,
            -16, 32);
        var instanceDelta = new TransformScalarProfileOperation(
            "relocate-instance", 5, OverlapPolicy.Reject, "relocate", "source-scalar", "target-scalar",
            CompositionProfileScalarWidth.FourBytes, CompositionProfileScalarByteOrder.LittleEndian,
            new RegionInstanceDeltaTransformAddendSource("a-bank", "b-bank"), null);
        var processor = new RunProcessorProfileOperation(
            "postbuild", 6, OverlapPolicy.ReplaceExisting, "postbuild", "legacy-postbuild");

        Assert.Equal(CompositionProfileOperationKind.CopyRange, copy.Kind);
        Assert.Equal("target", replace.TargetViewId);
        Assert.Equal(0xFF, fill.FillByte);
        Assert.Equal("0102", patch.Value.Hex);
        Assert.Equal(CompositionProfileScalarWidth.FourBytes, transform.Width);
        Assert.Equal(-16, transform.Addend);
        RegionInstanceDeltaTransformAddendSource delta =
            Assert.IsType<RegionInstanceDeltaTransformAddendSource>(instanceDelta.AddendSource);
        Assert.Equal("a-bank", delta.SourceRegionInstanceId);
        Assert.Equal("b-bank", delta.TargetRegionInstanceId);
        Assert.Equal("legacy-postbuild", processor.ProcessorStageId);
    }

    /// <summary>Verifies exact patch bytes are snapshotted and compare structurally.</summary>
    [Fact]
    public void ByteValuesAreImmutableAndStructural()
    {
        byte[] bytes = [0xAA, 0x55];
        var value = new CompositionProfileByteValue(bytes);
        bytes[0] = 0;
        var equal = new CompositionProfileByteValue([0xAA, 0x55]);
        var different = new CompositionProfileByteValue([0xAA, 0x56]);

        Assert.Equal("aa55", value.Hex);
        Assert.Equal(2, value.Length);
        Assert.Equal(value, equal);
        Assert.Equal(value.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(value, different);
        _ = Assert.Throws<ArgumentException>(() => new CompositionProfileByteValue([]));
    }

    /// <summary>Verifies schema-sized sequence and signed addend values stay lossless.</summary>
    [Fact]
    public void OperationSequenceAndTransformAddendUseArbitraryPrecision()
    {
        var sequence = BigInteger.Parse("18446744073709551616", System.Globalization.CultureInfo.InvariantCulture);
        var addend = BigInteger.Parse("-18446744073709551617", System.Globalization.CultureInfo.InvariantCulture);
        var transform = new TransformScalarProfileOperation(
            "relocate", sequence, OverlapPolicy.Reject, "relocate", "source", "target",
            CompositionProfileScalarWidth.EightBytes, CompositionProfileScalarByteOrder.BigEndian,
            addend, ulong.MaxValue);

        Assert.Equal(sequence, transform.Sequence);
        Assert.Equal(addend, transform.Addend);
        Assert.Equal(ulong.MaxValue, transform.ExpectedBefore);
    }

    /// <summary>Verifies expected-before values fit the exact unsigned scalar width.</summary>
    [Theory]
    [InlineData(1, 255UL)]
    [InlineData(2, 65535UL)]
    [InlineData(4, 4294967295UL)]
    [InlineData(8, ulong.MaxValue)]
    public void TransformAcceptsWidthBoundary(
        int widthBytes,
        ulong expectedBefore)
    {
        var width = (CompositionProfileScalarWidth)widthBytes;
        TransformScalarProfileOperation operation = Transform(width, expectedBefore);
        Assert.Equal(expectedBefore, operation.ExpectedBefore);
    }

    /// <summary>Verifies scalar width and byte-order carriers fail closed.</summary>
    [Fact]
    public void TransformRejectsInvalidWidthOrderAndExpectedValue()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Transform(
            CompositionProfileScalarWidth.OneByte,
            256));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Transform(
            (CompositionProfileScalarWidth)3,
            null));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new TransformScalarProfileOperation(
            "relocate", 0, OverlapPolicy.Reject, "relocate", "source", "target",
            CompositionProfileScalarWidth.OneByte, (CompositionProfileScalarByteOrder)99, 0, null));
    }

    /// <summary>Verifies common operation identity, sequence, and copy-kind invariants.</summary>
    [Fact]
    public void OperationsRejectInvalidCommonAndUnionValues()
    {
        _ = Assert.Throws<ArgumentException>(() => new FillRangeProfileOperation(
            "Fill", 0, OverlapPolicy.Reject, "fill", "target", 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FillRangeProfileOperation(
            "fill", -1, OverlapPolicy.Reject, "fill", "target", 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FillRangeProfileOperation(
            "fill", 0, (OverlapPolicy)99, "fill", "target", 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CopyOrReplaceProfileOperation(
            "copy", 0, OverlapPolicy.Reject, "copy", CompositionProfileOperationKind.FillRange,
            "source", "target"));
        _ = Assert.Throws<ArgumentNullException>(() => new PatchScalarProfileOperation(
            "patch", 0, OverlapPolicy.Reject, "patch", "target", null!));
        _ = Assert.Throws<ArgumentException>(() => new RunProcessorProfileOperation(
            "processor", 0, OverlapPolicy.Reject, "run", "Processor"));
    }

    private static TransformScalarProfileOperation Transform(
        CompositionProfileScalarWidth width,
        ulong? expectedBefore)
    {
        return new TransformScalarProfileOperation(
            "relocate",
            0,
            OverlapPolicy.Reject,
            "relocate",
            "source",
            "target",
            width,
            CompositionProfileScalarByteOrder.LittleEndian,
            0,
            expectedBefore);
    }
}
