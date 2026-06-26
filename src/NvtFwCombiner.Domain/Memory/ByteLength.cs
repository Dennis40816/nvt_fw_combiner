namespace NvtFwCombiner.Domain.Memory;

/// <summary>Non-negative byte count.</summary>
public readonly record struct ByteLength
{
    /// <summary>Creates a byte length from a non-negative integer value.</summary>
    public ByteLength(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Length value in bytes.</summary>
    public long Value { get; }

    /// <summary>Returns the underlying byte count.</summary>
    public static implicit operator long(ByteLength length)
    {
        return length.Value;
    }
}
