using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests closed metadata field declarations and value representability.</summary>
public sealed class FirmwareMetadataFieldTests
{
    /// <summary>Verifies every closed encoding retains its canonical declaration shape.</summary>
    [Fact]
    public void ConstructorCreatesClosedFieldShapes()
    {
        var bytes = new FirmwareMetadataField("pid-bytes", 0, 2, FirmwareMetadataEncoding.Bytes);
        var text = new FirmwareMetadataField("label", 2, 4, FirmwareMetadataEncoding.PrintableAscii);
        var unsigned = new FirmwareMetadataField(
            "minor-version",
            6,
            1,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian,
            new FirmwareMetadataBitSlice(4, 4));
        var signed = new FirmwareMetadataField(
            "signed-offset",
            7,
            1,
            FirmwareMetadataEncoding.SignedInteger,
            FirmwareMetadataByteOrder.LittleEndian);

        Assert.Equal(new ByteRange(0, 2), bytes.Range);
        Assert.Equal(FirmwareMetadataValueKind.Bytes, bytes.ValueKind);
        Assert.Null(bytes.EffectiveBitCount);
        Assert.Equal(FirmwareMetadataValueKind.Text, text.ValueKind);
        Assert.Equal(FirmwareMetadataValueKind.UnsignedInteger, unsigned.ValueKind);
        Assert.Equal(4, unsigned.EffectiveBitCount);
        Assert.Equal(4, unsigned.BitSlice?.LeastSignificantBit);
        Assert.Equal(FirmwareMetadataValueKind.SignedInteger, signed.ValueKind);
        Assert.Equal(8, signed.EffectiveBitCount);
    }

    /// <summary>Verifies typed values must fit exact byte, text, and integer boundaries.</summary>
    [Fact]
    public void CanRepresentRequiresExactFieldContext()
    {
        var bytes = new FirmwareMetadataField("pid-bytes", 0, 2, FirmwareMetadataEncoding.Bytes);
        var text = new FirmwareMetadataField("label", 0, 3, FirmwareMetadataEncoding.PrintableAscii);
        var printableCharacter = new FirmwareMetadataField(
            "printable-character",
            0,
            1,
            FirmwareMetadataEncoding.PrintableAscii);
        var nibble = new FirmwareMetadataField(
            "nibble",
            0,
            1,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.BigEndian,
            new FirmwareMetadataBitSlice(4, 4));
        var signedByte = new FirmwareMetadataField(
            "signed-byte",
            0,
            1,
            FirmwareMetadataEncoding.SignedInteger,
            FirmwareMetadataByteOrder.LittleEndian);
        var unsignedWord = new FirmwareMetadataField(
            "unsigned-word",
            0,
            4,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian);
        var signedWord = new FirmwareMetadataField(
            "signed-word",
            0,
            4,
            FirmwareMetadataEncoding.SignedInteger,
            FirmwareMetadataByteOrder.BigEndian);

        Assert.True(bytes.CanRepresent(FirmwareMetadataValue.FromBytes([0, 1])));
        Assert.False(bytes.CanRepresent(FirmwareMetadataValue.FromBytes([1])));
        Assert.True(text.CanRepresent(FirmwareMetadataValue.FromText(" A ")));
        Assert.False(text.CanRepresent(FirmwareMetadataValue.FromText("AB")));
        Assert.False(text.CanRepresent(FirmwareMetadataValue.FromText("A\u007fB")));
        Assert.True(printableCharacter.CanRepresent(FirmwareMetadataValue.FromText("~")));
        Assert.False(printableCharacter.CanRepresent(FirmwareMetadataValue.FromText("\u001f")));
        Assert.True(nibble.CanRepresent(FirmwareMetadataValue.FromUnsignedInteger(15)));
        Assert.False(nibble.CanRepresent(FirmwareMetadataValue.FromUnsignedInteger(16)));
        Assert.False(nibble.CanRepresent(FirmwareMetadataValue.FromSignedInteger(15)));
        Assert.True(signedByte.CanRepresent(FirmwareMetadataValue.FromSignedInteger(-128)));
        Assert.True(signedByte.CanRepresent(FirmwareMetadataValue.FromSignedInteger(127)));
        Assert.False(signedByte.CanRepresent(FirmwareMetadataValue.FromSignedInteger(-129)));
        Assert.False(signedByte.CanRepresent(FirmwareMetadataValue.FromSignedInteger(128)));
        Assert.True(unsignedWord.CanRepresent(FirmwareMetadataValue.FromUnsignedInteger(uint.MaxValue)));
        Assert.False(unsignedWord.CanRepresent(FirmwareMetadataValue.FromUnsignedInteger((ulong)uint.MaxValue + 1)));
        Assert.True(signedWord.CanRepresent(FirmwareMetadataValue.FromSignedInteger(int.MinValue)));
        Assert.True(signedWord.CanRepresent(FirmwareMetadataValue.FromSignedInteger(int.MaxValue)));
        Assert.False(signedWord.CanRepresent(FirmwareMetadataValue.FromSignedInteger((long)int.MinValue - 1)));
        Assert.False(signedWord.CanRepresent(FirmwareMetadataValue.FromSignedInteger((long)int.MaxValue + 1)));
    }

    /// <summary>Verifies numeric declarations require one-to-four-byte carriers and explicit order.</summary>
    [Fact]
    public void ConstructorRejectsInvalidNumericOptions()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataField(
            "missing-order",
            0,
            1,
            FirmwareMetadataEncoding.UnsignedInteger));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareMetadataField(
            "too-wide",
            0,
            5,
            FirmwareMetadataEncoding.SignedInteger,
            FirmwareMetadataByteOrder.LittleEndian));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataField(
            "signed-slice",
            0,
            1,
            FirmwareMetadataEncoding.SignedInteger,
            FirmwareMetadataByteOrder.LittleEndian,
            new FirmwareMetadataBitSlice(0, 1)));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataField(
            "bytes-order",
            0,
            1,
            FirmwareMetadataEncoding.Bytes,
            FirmwareMetadataByteOrder.LittleEndian));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataField(
            "text-slice",
            0,
            1,
            FirmwareMetadataEncoding.PrintableAscii,
            bitSlice: new FirmwareMetadataBitSlice(0, 1)));
    }

    /// <summary>Verifies bit slices may end at, but never pass, their unsigned carrier boundary.</summary>
    [Fact]
    public void ConstructorChecksUnsignedBitSliceBounds()
    {
        var exact = new FirmwareMetadataField(
            "whole-word",
            0,
            4,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian,
            new FirmwareMetadataBitSlice(0, 32));
        var lastBit = new FirmwareMetadataField(
            "last-bit",
            0,
            1,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.BigEndian,
            new FirmwareMetadataBitSlice(7, 1));
        var maximumBit = new FirmwareMetadataField(
            "maximum-bit",
            0,
            4,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.BigEndian,
            new FirmwareMetadataBitSlice(31, 1));

        Assert.Equal(32, exact.EffectiveBitCount);
        Assert.Equal(8, lastBit.BitSlice?.EndExclusive);
        Assert.Equal(32, maximumBit.BitSlice?.EndExclusive);
        Assert.Equal(1, maximumBit.EffectiveBitCount);
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataField(
            "past-end",
            0,
            1,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian,
            new FirmwareMetadataBitSlice(7, 2)));
    }

    /// <summary>Verifies standalone bit slices enforce the closed 32-bit contract.</summary>
    [Fact]
    public void BitSliceRejectsInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareMetadataBitSlice(-1, 1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareMetadataBitSlice(32, 1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareMetadataBitSlice(0, 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareMetadataBitSlice(0, 33));
    }

    /// <summary>Verifies identity, enum, range, and null-value boundaries fail closed.</summary>
    [Fact]
    public void ConstructorRejectsInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataField(
            " ",
            0,
            1,
            FirmwareMetadataEncoding.Bytes));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareMetadataField(
            "bad-encoding",
            0,
            1,
            (FirmwareMetadataEncoding)int.MaxValue));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareMetadataField(
            "bad-order",
            0,
            1,
            FirmwareMetadataEncoding.UnsignedInteger,
            (FirmwareMetadataByteOrder)int.MaxValue));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareMetadataField(
            "negative-offset",
            -1,
            1,
            FirmwareMetadataEncoding.Bytes));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareMetadataField(
            "zero-width",
            0,
            0,
            FirmwareMetadataEncoding.Bytes));
        _ = Assert.Throws<OverflowException>(() => new FirmwareMetadataField(
            "overflow",
            long.MaxValue,
            1,
            FirmwareMetadataEncoding.Bytes));

        FirmwareMetadataField field = new("bytes", 0, 1, FirmwareMetadataEncoding.Bytes);
        _ = Assert.Throws<ArgumentNullException>(() => field.CanRepresent(null!));
    }
}
