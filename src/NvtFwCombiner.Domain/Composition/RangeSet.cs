namespace NvtFwCombiner.Domain.Composition;

/// <summary>Immutable helper for range containment decisions.</summary>
public sealed class RangeSet
{
    private readonly ByteRange[] _ranges;

    /// <summary>Creates a checked range set from non-empty ranges.</summary>
    public RangeSet(IEnumerable<ByteRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        _ranges = [.. ranges.OrderBy(range => range.Start).ThenBy(range => range.Length)];
    }

    /// <summary>Declared ranges sorted by start offset.</summary>
    public IReadOnlyList<ByteRange> Ranges => _ranges;

    /// <summary>Returns true when at least one declared range fully contains <paramref name="candidate"/>.</summary>
    public bool Contains(ByteRange candidate)
    {
        return _ranges.Any(range => range.Contains(candidate));
    }

    /// <summary>Returns true when every range in <paramref name="candidates"/> is fully declared.</summary>
    public bool ContainsAll(IEnumerable<ByteRange> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates.All(Contains);
    }
}
