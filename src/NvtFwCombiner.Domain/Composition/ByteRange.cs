namespace NvtFwCombiner.Domain.Composition;

/// <summary>Represents a checked half-open byte range [Start, EndExclusive).</summary>
public readonly record struct ByteRange
{
    /// <summary>Creates a new byte range from a start offset and positive byte length.</summary>
    public ByteRange(long start, long length)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Range start must be non-negative.");
        }

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Range length must be positive.");
        }

        checked
        {
            _ = start + length;
        }

        Start = start;
        Length = length;
    }

    /// <summary>Inclusive start offset.</summary>
    public long Start { get; }

    /// <summary>Positive range length in bytes.</summary>
    public long Length { get; }

    /// <summary>Exclusive end offset.</summary>
    public long EndExclusive
    {
        get
        {
            return checked(Start + Length);
        }
    }

    /// <summary>Creates a range from an inclusive start and exclusive end.</summary>
    public static ByteRange FromStartEndExclusive(long start, long endExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(endExclusive, start);
        return new ByteRange(start, checked(endExclusive - start));
    }

    /// <summary>Returns true when this range contains the supplied absolute offset.</summary>
    public bool Contains(long offset)
    {
        return offset >= Start && offset < EndExclusive;
    }

    /// <summary>Returns true when this range fully contains <paramref name="other"/>.</summary>
    public bool Contains(ByteRange other)
    {
        return other.Start >= Start && other.EndExclusive <= EndExclusive;
    }

    /// <summary>Returns true when this range shares at least one byte with <paramref name="other"/>.</summary>
    public bool Overlaps(ByteRange other)
    {
        return Start < other.EndExclusive && other.Start < EndExclusive;
    }

    /// <summary>Returns the intersection of two ranges, or null when they do not overlap.</summary>
    public ByteRange? Intersect(ByteRange other)
    {
        long start = Math.Max(Start, other.Start);
        long end = Math.Min(EndExclusive, other.EndExclusive);
        return start < end ? FromStartEndExclusive(start, end) : null;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"[{Start}, {EndExclusive})/{Length}";
    }
}
