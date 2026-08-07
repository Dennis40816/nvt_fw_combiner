namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed scalar kind decoded from one canonical firmware metadata field.</summary>
public enum FirmwareMetadataValueKind
{
    /// <summary>Two's-complement signed integer.</summary>
    SignedInteger,

    /// <summary>Unsigned integer.</summary>
    UnsignedInteger,

    /// <summary>Exact raw bytes.</summary>
    Bytes,

    /// <summary>Exact non-empty text.</summary>
    Text,
}

/// <summary>Immutable raw metadata bytes with structural equality and canonical rendering.</summary>
public sealed class FirmwareMetadataBytes : IEquatable<FirmwareMetadataBytes>
{
    private readonly byte[] _bytes;

    /// <summary>Creates an immutable non-empty byte value.</summary>
    public FirmwareMetadataBytes(ReadOnlySpan<byte> bytes)
    {
        DomainInvariant.Reject(bytes.IsEmpty, "Firmware metadata byte values cannot be empty.", nameof(bytes));

        _bytes = bytes.ToArray();
        Hex = Convert.ToHexString(_bytes).ToLowerInvariant();
    }

    /// <summary>Exact byte length.</summary>
    public int Length => _bytes.Length;

    /// <summary>Canonical lowercase hexadecimal bytes without a prefix.</summary>
    public string Hex { get; }

    internal ReadOnlySpan<byte> Bytes => _bytes;

    /// <inheritdoc />
    public bool Equals(FirmwareMetadataBytes? other)
    {
        return other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is FirmwareMetadataBytes other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (byte value in _bytes)
            {
                hash = (hash * 31) + value;
            }

            return hash;
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Hex;
    }
}

/// <summary>One immutable, non-coercing firmware metadata scalar.</summary>
public sealed record FirmwareMetadataValue
{
    private FirmwareMetadataValue(
        FirmwareMetadataValueKind kind,
        long? signedIntegerValue,
        ulong? unsignedIntegerValue,
        FirmwareMetadataBytes? bytesValue,
        string? textValue)
    {
        Kind = kind;
        SignedIntegerValue = signedIntegerValue;
        UnsignedIntegerValue = unsignedIntegerValue;
        BytesValue = bytesValue;
        TextValue = textValue;
    }

    /// <summary>Scalar kind.</summary>
    public FirmwareMetadataValueKind Kind { get; }

    /// <summary>Signed value when <see cref="Kind"/> is <see cref="FirmwareMetadataValueKind.SignedInteger"/>.</summary>
    public long? SignedIntegerValue { get; }

    /// <summary>Unsigned value when <see cref="Kind"/> is <see cref="FirmwareMetadataValueKind.UnsignedInteger"/>.</summary>
    public ulong? UnsignedIntegerValue { get; }

    /// <summary>Raw byte value when <see cref="Kind"/> is <see cref="FirmwareMetadataValueKind.Bytes"/>.</summary>
    public FirmwareMetadataBytes? BytesValue { get; }

    /// <summary>Text value when <see cref="Kind"/> is <see cref="FirmwareMetadataValueKind.Text"/>.</summary>
    public string? TextValue { get; }

    /// <summary>Creates a signed integer metadata value.</summary>
    public static FirmwareMetadataValue FromSignedInteger(long value)
    {
        return new FirmwareMetadataValue(FirmwareMetadataValueKind.SignedInteger, value, null, null, null);
    }

    /// <summary>Creates an unsigned integer metadata value.</summary>
    public static FirmwareMetadataValue FromUnsignedInteger(ulong value)
    {
        return new FirmwareMetadataValue(FirmwareMetadataValueKind.UnsignedInteger, null, value, null, null);
    }

    /// <summary>Creates an exact non-empty raw-byte metadata value.</summary>
    public static FirmwareMetadataValue FromBytes(ReadOnlySpan<byte> bytes)
    {
        return new FirmwareMetadataValue(
            FirmwareMetadataValueKind.Bytes,
            null,
            null,
            new FirmwareMetadataBytes(bytes),
            null);
    }

    /// <summary>Creates a non-empty text metadata value.</summary>
    public static FirmwareMetadataValue FromText(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        return new FirmwareMetadataValue(FirmwareMetadataValueKind.Text, null, null, null, value);
    }
}
