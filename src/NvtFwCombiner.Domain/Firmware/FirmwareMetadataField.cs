using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed storage encoding for a canonical firmware metadata field.</summary>
public enum FirmwareMetadataEncoding
{
    /// <summary>Exact raw bytes.</summary>
    Bytes,

    /// <summary>Exact fixed-width printable ASCII.</summary>
    PrintableAscii,

    /// <summary>Unsigned integer with optional bit slice.</summary>
    UnsignedInteger,

    /// <summary>Full-width two's-complement signed integer.</summary>
    SignedInteger,
}

/// <summary>Storage byte order for integer metadata carriers.</summary>
public enum FirmwareMetadataByteOrder
{
    /// <summary>Least-significant byte first.</summary>
    LittleEndian,

    /// <summary>Most-significant byte first.</summary>
    BigEndian,
}

/// <summary>Checked bit projection from an unsigned integer carrier.</summary>
public sealed record FirmwareMetadataBitSlice
{
    /// <summary>Creates one positive slice inside the maximum 32-bit carrier.</summary>
    public FirmwareMetadataBitSlice(int leastSignificantBit, int bitCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(leastSignificantBit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(leastSignificantBit, 31);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bitCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitCount, 32);

        LeastSignificantBit = leastSignificantBit;
        BitCount = bitCount;
    }

    /// <summary>First selected bit after byte-order normalization.</summary>
    public int LeastSignificantBit { get; }

    /// <summary>Positive selected bit count.</summary>
    public int BitCount { get; }

    /// <summary>Exclusive end bit.</summary>
    public int EndExclusive => checked(LeastSignificantBit + BitCount);
}

/// <summary>Immutable structure-relative declaration of one metadata field.</summary>
public sealed class FirmwareMetadataField
{
    private const int MaximumIntegerWidthBytes = 4;

    /// <summary>Creates a checked field declaration without assigning a production structure.</summary>
    public FirmwareMetadataField(
        string fieldId,
        long offset,
        int widthBytes,
        FirmwareMetadataEncoding encoding,
        FirmwareMetadataByteOrder? byteOrder = null,
        FirmwareMetadataBitSlice? bitSlice = null,
        string? sourceName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        if (sourceName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        }
        if (!Enum.IsDefined(encoding))
        {
            throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Unknown metadata encoding.");
        }

        if (byteOrder is { } selectedByteOrder && !Enum.IsDefined(selectedByteOrder))
        {
            throw new ArgumentOutOfRangeException(nameof(byteOrder), byteOrder, "Unknown metadata byte order.");
        }

        Range = new ByteRange(offset, widthBytes);
        ValidateEncodingOptions(encoding, widthBytes, byteOrder, bitSlice);

        FieldId = fieldId;
        WidthBytes = widthBytes;
        Encoding = encoding;
        ByteOrder = byteOrder;
        BitSlice = bitSlice;
        SourceName = sourceName;
        ValueKind = ToValueKind(encoding);
        EffectiveBitCount = encoding is FirmwareMetadataEncoding.UnsignedInteger or FirmwareMetadataEncoding.SignedInteger
            ? bitSlice?.BitCount ?? checked(widthBytes * 8)
            : null;
    }

    /// <summary>Stable field identifier unique inside one metadata structure.</summary>
    public string FieldId { get; }

    /// <summary>Checked structure-relative half-open byte range.</summary>
    public ByteRange Range { get; }

    /// <summary>Declared byte width.</summary>
    public int WidthBytes { get; }

    /// <summary>Closed storage encoding.</summary>
    public FirmwareMetadataEncoding Encoding { get; }

    /// <summary>Required integer byte order; null for bytes and printable text.</summary>
    public FirmwareMetadataByteOrder? ByteOrder { get; }

    /// <summary>Optional unsigned integer bit projection.</summary>
    public FirmwareMetadataBitSlice? BitSlice { get; }

    /// <summary>Optional retained source declaration name used for provenance and diagnostics.</summary>
    public string? SourceName { get; }

    /// <summary>Domain scalar kind produced by this declaration.</summary>
    public FirmwareMetadataValueKind ValueKind { get; }

    /// <summary>Effective integer width after slicing; null for nonnumeric fields.</summary>
    public int? EffectiveBitCount { get; }

    /// <summary>Returns whether a typed predicate value is exactly representable by this field.</summary>
    public bool CanRepresent(FirmwareMetadataValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Encoding switch
        {
            FirmwareMetadataEncoding.Bytes =>
                value.Kind == FirmwareMetadataValueKind.Bytes &&
                value.BytesValue?.Length == WidthBytes,
            FirmwareMetadataEncoding.PrintableAscii =>
                value.Kind == FirmwareMetadataValueKind.Text &&
                IsExactPrintableAscii(value.TextValue, WidthBytes),
            FirmwareMetadataEncoding.UnsignedInteger =>
                value.Kind == FirmwareMetadataValueKind.UnsignedInteger &&
                value.UnsignedIntegerValue is { } unsignedValue &&
                FitsUnsigned(unsignedValue, EffectiveBitCount!.Value),
            FirmwareMetadataEncoding.SignedInteger =>
                value.Kind == FirmwareMetadataValueKind.SignedInteger &&
                value.SignedIntegerValue is { } signedValue &&
                FitsSigned(signedValue, EffectiveBitCount!.Value),
            _ => throw new InvalidOperationException("Unknown metadata encoding."),
        };
    }

    /// <summary>Decodes an exact-width carrier without coercion or replacement characters.</summary>
    public bool TryDecode(
        ReadOnlySpan<byte> bytes,
        [NotNullWhen(true)] out FirmwareMetadataValue? value)
    {
        value = null;
        if (bytes.Length != WidthBytes)
        {
            return false;
        }

        switch (Encoding)
        {
            case FirmwareMetadataEncoding.Bytes:
                value = FirmwareMetadataValue.FromBytes(bytes);
                return true;
            case FirmwareMetadataEncoding.PrintableAscii:
                return TryDecodePrintableAscii(bytes, out value);
            case FirmwareMetadataEncoding.UnsignedInteger:
                ulong unsignedValue = DecodeUnsigned(bytes, ByteOrder!.Value);
                if (BitSlice is { } bitSlice)
                {
                    ulong mask = (1UL << bitSlice.BitCount) - 1UL;
                    unsignedValue = (unsignedValue >> bitSlice.LeastSignificantBit) & mask;
                }

                value = FirmwareMetadataValue.FromUnsignedInteger(unsignedValue);
                return true;
            case FirmwareMetadataEncoding.SignedInteger:
                ulong carrier = DecodeUnsigned(bytes, ByteOrder!.Value);
                int bitCount = checked(WidthBytes * 8);
                ulong signBit = 1UL << (bitCount - 1);
                long signedValue = (carrier & signBit) == 0
                    ? (long)carrier
                    : checked((long)carrier - (1L << bitCount));
                value = FirmwareMetadataValue.FromSignedInteger(signedValue);
                return true;
            default:
                throw new InvalidOperationException("Unknown metadata encoding.");
        }
    }

    private static void ValidateEncodingOptions(
        FirmwareMetadataEncoding encoding,
        int widthBytes,
        FirmwareMetadataByteOrder? byteOrder,
        FirmwareMetadataBitSlice? bitSlice)
    {
        bool isInteger = encoding is FirmwareMetadataEncoding.UnsignedInteger or FirmwareMetadataEncoding.SignedInteger;
        if (!isInteger)
        {
            if (byteOrder is not null || bitSlice is not null)
            {
                throw new ArgumentException("Byte and text metadata cannot declare numeric options.", nameof(encoding));
            }

            return;
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(widthBytes, MaximumIntegerWidthBytes);
        if (byteOrder is null)
        {
            throw new ArgumentException("Integer metadata requires explicit byte order.", nameof(byteOrder));
        }

        if (encoding == FirmwareMetadataEncoding.SignedInteger && bitSlice is not null)
        {
            throw new ArgumentException("Signed integer metadata cannot declare a bit slice.", nameof(bitSlice));
        }

        if (bitSlice is not null && bitSlice.EndExclusive > checked(widthBytes * 8))
        {
            throw new ArgumentException("Metadata bit slice exceeds its unsigned carrier.", nameof(bitSlice));
        }
    }

    private static FirmwareMetadataValueKind ToValueKind(FirmwareMetadataEncoding encoding)
    {
        return encoding switch
        {
            FirmwareMetadataEncoding.Bytes => FirmwareMetadataValueKind.Bytes,
            FirmwareMetadataEncoding.PrintableAscii => FirmwareMetadataValueKind.Text,
            FirmwareMetadataEncoding.UnsignedInteger => FirmwareMetadataValueKind.UnsignedInteger,
            FirmwareMetadataEncoding.SignedInteger => FirmwareMetadataValueKind.SignedInteger,
            _ => throw new InvalidOperationException("Unknown metadata encoding."),
        };
    }

    private static bool TryDecodePrintableAscii(
        ReadOnlySpan<byte> bytes,
        [NotNullWhen(true)] out FirmwareMetadataValue? value)
    {
        value = null;
        char[] characters = new char[bytes.Length];
        for (int index = 0; index < bytes.Length; index++)
        {
            byte current = bytes[index];
            if (current is < 0x20 or > 0x7E)
            {
                return false;
            }

            characters[index] = (char)current;
        }

        value = FirmwareMetadataValue.FromText(new string(characters));
        return true;
    }

    private static ulong DecodeUnsigned(ReadOnlySpan<byte> bytes, FirmwareMetadataByteOrder byteOrder)
    {
        ulong value = 0;
        if (byteOrder == FirmwareMetadataByteOrder.BigEndian)
        {
            foreach (byte current in bytes)
            {
                value = (value << 8) | current;
            }

            return value;
        }

        for (int index = bytes.Length - 1; index >= 0; index--)
        {
            value = (value << 8) | bytes[index];
        }

        return value;
    }

    private static bool IsExactPrintableAscii(string? value, int widthBytes)
    {
        return value is not null &&
            value.Length == widthBytes &&
            value.All(static character => character is >= '\u0020' and <= '\u007e');
    }

    private static bool FitsUnsigned(ulong value, int bitCount)
    {
        ulong maximum = (1UL << bitCount) - 1UL;
        return value <= maximum;
    }

    private static bool FitsSigned(long value, int bitCount)
    {
        long magnitude = 1L << (bitCount - 1);
        return value >= -magnitude && value <= magnitude - 1;
    }
}
