namespace NvtFwCombiner.Domain.Memory;

/// <summary>Half-open byte range represented as [Start, EndExclusive).</summary>
public readonly record struct ByteRange
{
    /// <summary>Creates a checked half-open byte range.</summary>
    public ByteRange(ByteOffset start, ByteLength length)
    {
        Start = start;
        Length = length;
        _ = checked(start.Value + length.Value);
    }

    /// <summary>Inclusive start offset.</summary>
    public ByteOffset Start { get; }

    /// <summary>Range length in bytes.</summary>
    public ByteLength Length { get; }

    /// <summary>Exclusive end offset.</summary>
    public long EndExclusive => checked(Start.Value + Length.Value);

    /// <summary>True when the range contains zero bytes.</summary>
    public bool IsEmpty => Length.Value == 0;

    /// <summary>Returns true when the offset lies inside the half-open range.</summary>
    public bool Contains(ByteOffset offset)
    {
        return offset.Value >= Start.Value && offset.Value < EndExclusive;
    }

    /// <summary>Returns true when the other range lies fully inside this range.</summary>
    public bool Contains(ByteRange other)
    {
        return other.Start.Value >= Start.Value && other.EndExclusive <= EndExclusive;
    }

    /// <summary>Returns true when two half-open ranges share at least one byte.</summary>
    public bool Overlaps(ByteRange other)
    {
        return Start.Value < other.EndExclusive && other.Start.Value < EndExclusive;
    }

    /// <summary>Returns the shared half-open range, or null when ranges are disjoint.</summary>
    public ByteRange? Intersect(ByteRange other)
    {
        long start = Math.Max(Start.Value, other.Start.Value);
        long end = Math.Min(EndExclusive, other.EndExclusive);
        return end <= start ? null : new ByteRange(new ByteOffset(start), new ByteLength(end - start));
    }

    /// <summary>Returns an invariant hexadecimal half-open range string.</summary>
    public override string ToString()
    {
        return FormattableString.Invariant($"[0x{Start.Value:X}, 0x{EndExclusive:X})");
    }
}
