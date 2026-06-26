namespace NvtFwCombiner.Domain.Memory;

/// <summary>Non-negative byte offset in an address space.</summary>
public readonly record struct ByteOffset
{
    /// <summary>Creates a byte offset from a non-negative integer value.</summary>
    public ByteOffset(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Offset value in bytes.</summary>
    public long Value { get; }

    /// <summary>Returns the underlying byte offset.</summary>
    public static implicit operator long(ByteOffset offset)
    {
        return offset.Value;
    }
}
