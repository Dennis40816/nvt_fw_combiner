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

    /// <summary>Returns true when declared ranges cover <paramref name="candidate"/> without gaps.</summary>
    public bool Contains(ByteRange candidate)
    {
        long coveredUntil = candidate.Start;
        foreach (ByteRange range in _ranges)
        {
            if (range.EndExclusive <= coveredUntil)
            {
                continue;
            }

            if (range.Start > coveredUntil)
            {
                return false;
            }

            coveredUntil = Math.Max(coveredUntil, range.EndExclusive);
            if (coveredUntil >= candidate.EndExclusive)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns true when every range in <paramref name="candidates"/> is fully declared.</summary>
    public bool ContainsAll(IEnumerable<ByteRange> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates.All(Contains);
    }
}
