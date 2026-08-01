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
        var bytes = new FirmwareMetadataField(
            "pid-bytes",
            0,
            2,
            FirmwareMetadataEncoding.Bytes,
            sourceName: "PID");
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
        Assert.Equal("PID", bytes.SourceName);
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

    /// <summary>Verifies bytes are snapshotted and printable ASCII is decoded without normalization.</summary>
    [Fact]
    public void TryDecodePreservesBytesAndStrictPrintableAscii()
    {
        var bytesField = new FirmwareMetadataField("pid", 0, 2, FirmwareMetadataEncoding.Bytes);
        var textField = new FirmwareMetadataField("label", 0, 3, FirmwareMetadataEncoding.PrintableAscii);
        byte[] source = [0x00, 0x01];

        Assert.True(bytesField.TryDecode(source, out FirmwareMetadataValue? bytes));
        source[0] = 0xFF;
        Assert.Equal("0001", bytes.BytesValue?.Hex);
        Assert.True(textField.TryDecode([(byte)' ', (byte)'A', (byte)'~'], out FirmwareMetadataValue? text));
        Assert.Equal(" A~", text.TextValue);

        Assert.False(textField.TryDecode([0x1F, (byte)'A', (byte)'B'], out FirmwareMetadataValue? control));
        Assert.Null(control);
        Assert.False(textField.TryDecode([(byte)'A', 0x7F, (byte)'B'], out FirmwareMetadataValue? delete));
        Assert.Null(delete);
        Assert.False(textField.TryDecode([(byte)'A', 0x80, (byte)'B'], out FirmwareMetadataValue? high));
        Assert.Null(high);
    }

    /// <summary>Verifies unsigned carriers normalize byte order before applying a bit slice.</summary>
    [Fact]
    public void TryDecodeUnsignedUsesDeclaredByteOrderAndBitSlice()
    {
        FirmwareMetadataField little = UnsignedField(4, FirmwareMetadataByteOrder.LittleEndian);
        FirmwareMetadataField big = UnsignedField(4, FirmwareMetadataByteOrder.BigEndian);
        var slice = new FirmwareMetadataField(
            "slice",
            0,
            2,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.BigEndian,
            new FirmwareMetadataBitSlice(4, 8));
        var wholeCarrierSlice = new FirmwareMetadataField(
            "whole-carrier-slice",
            0,
            4,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.BigEndian,
            new FirmwareMetadataBitSlice(0, 32));
        var highestBitSlice = new FirmwareMetadataField(
            "highest-bit-slice",
            0,
            4,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.BigEndian,
            new FirmwareMetadataBitSlice(31, 1));

        Assert.True(little.TryDecode([0x78, 0x56, 0x34, 0x12], out FirmwareMetadataValue? littleValue));
        Assert.True(big.TryDecode([0x12, 0x34, 0x56, 0x78], out FirmwareMetadataValue? bigValue));
        Assert.True(slice.TryDecode([0x12, 0x34], out FirmwareMetadataValue? sliceValue));
        Assert.True(wholeCarrierSlice.TryDecode(
            [0xFF, 0xFF, 0xFF, 0xFF],
            out FirmwareMetadataValue? wholeCarrierValue));
        Assert.True(highestBitSlice.TryDecode(
            [0x80, 0x00, 0x00, 0x00],
            out FirmwareMetadataValue? highestBitSet));
        Assert.True(highestBitSlice.TryDecode(
            [0x7F, 0xFF, 0xFF, 0xFF],
            out FirmwareMetadataValue? highestBitClear));

        Assert.Equal(0x12345678UL, littleValue.UnsignedIntegerValue);
        Assert.Equal(littleValue, bigValue);
        Assert.Equal(0x23UL, sliceValue.UnsignedIntegerValue);
        Assert.Equal(uint.MaxValue, wholeCarrierValue.UnsignedIntegerValue);
        Assert.Equal(1UL, highestBitSet.UnsignedIntegerValue);
        Assert.Equal(0UL, highestBitClear.UnsignedIntegerValue);
        Assert.True(little.CanRepresent(littleValue));
        Assert.True(slice.CanRepresent(sliceValue));
    }

    /// <summary>Verifies signed carriers use full-width two's-complement for every supported width.</summary>
    [Fact]
    public void TryDecodeSignedUsesFullWidthTwosComplement()
    {
        (FirmwareMetadataField Field, byte[] Bytes, long Expected)[] cases =
        [
            (SignedField(1, FirmwareMetadataByteOrder.LittleEndian), [0x80], -128),
            (SignedField(1, FirmwareMetadataByteOrder.BigEndian), [0x7F], 127),
            (SignedField(2, FirmwareMetadataByteOrder.LittleEndian), [0x00, 0x80], short.MinValue),
            (SignedField(2, FirmwareMetadataByteOrder.BigEndian), [0x7F, 0xFF], short.MaxValue),
            (SignedField(3, FirmwareMetadataByteOrder.LittleEndian), [0xFE, 0xFF, 0xFF], -2),
            (SignedField(3, FirmwareMetadataByteOrder.BigEndian), [0x7F, 0xFF, 0xFF], 0x7FFFFF),
            (SignedField(4, FirmwareMetadataByteOrder.LittleEndian), [0x00, 0x00, 0x00, 0x80], int.MinValue),
            (SignedField(4, FirmwareMetadataByteOrder.BigEndian), [0x7F, 0xFF, 0xFF, 0xFF], int.MaxValue),
        ];

        foreach ((FirmwareMetadataField field, byte[] bytes, long expected) in cases)
        {
            Assert.True(field.TryDecode(bytes, out FirmwareMetadataValue? decoded));
            Assert.Equal(expected, decoded.SignedIntegerValue);
            Assert.True(field.CanRepresent(decoded));
        }
    }

    /// <summary>Verifies every encoding rejects carriers whose byte width is not exact.</summary>
    [Fact]
    public void TryDecodeRejectsWrongCarrierWidthWithoutValue()
    {
        FirmwareMetadataField[] fields =
        [
            new FirmwareMetadataField("bytes", 0, 2, FirmwareMetadataEncoding.Bytes),
            new FirmwareMetadataField("text", 0, 2, FirmwareMetadataEncoding.PrintableAscii),
            UnsignedField(2, FirmwareMetadataByteOrder.LittleEndian),
            SignedField(2, FirmwareMetadataByteOrder.BigEndian),
        ];

        foreach (FirmwareMetadataField field in fields)
        {
            Assert.False(field.TryDecode([0x01], out FirmwareMetadataValue? shortValue));
            Assert.Null(shortValue);
            Assert.False(field.TryDecode([0x01, 0x02, 0x03], out FirmwareMetadataValue? longValue));
            Assert.Null(longValue);
            Assert.False(field.TryDecode([], out FirmwareMetadataValue? emptyValue));
            Assert.Null(emptyValue);
        }
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
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataField(
            "source-name",
            0,
            1,
            FirmwareMetadataEncoding.Bytes,
            sourceName: " "));
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

    private static FirmwareMetadataField UnsignedField(
        int widthBytes,
        FirmwareMetadataByteOrder byteOrder)
    {
        return new FirmwareMetadataField(
            "unsigned",
            0,
            widthBytes,
            FirmwareMetadataEncoding.UnsignedInteger,
            byteOrder);
    }

    private static FirmwareMetadataField SignedField(
        int widthBytes,
        FirmwareMetadataByteOrder byteOrder)
    {
        return new FirmwareMetadataField(
            "signed",
            0,
            widthBytes,
            FirmwareMetadataEncoding.SignedInteger,
            byteOrder);
    }
}
