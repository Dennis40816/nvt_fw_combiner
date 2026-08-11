namespace NvtFwCombiner.Domain.Composition;

/// <summary>Validates that observed byte changes remain inside declared write authority.</summary>
public sealed class ChangedRangePolicy
{
    private readonly ByteRange[] _allowedWrites;

    /// <summary>Creates a policy from the complete declared write authority.</summary>
    public ChangedRangePolicy(IEnumerable<ByteRange> allowedWrites)
    {
        ArgumentNullException.ThrowIfNull(allowedWrites);
        _allowedWrites =
        [
            .. allowedWrites
                .OrderBy(static range => range.Start)
                .ThenBy(static range => range.Length),
        ];
    }

    /// <summary>Returns a deterministic verdict for observed changed ranges.</summary>
    public ChangedRangeVerdict Evaluate(IEnumerable<ByteRange> changedRanges)
    {
        ArgumentNullException.ThrowIfNull(changedRanges);
        ByteRange[] observed = [.. changedRanges.OrderBy(range => range.Start).ThenBy(range => range.Length)];
        ByteRange[] violations = [.. observed.Where(range => !Contains(_allowedWrites, range))];
        return new ChangedRangeVerdict(violations.Length == 0, violations);
    }

    private static bool Contains(IReadOnlyList<ByteRange> ranges, ByteRange candidate)
    {
        long coveredUntil = candidate.Start;
        foreach (ByteRange range in ranges)
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
}

/// <summary>Result of validating observed changes against declared write ranges.</summary>
public sealed class ChangedRangeVerdict(
    bool isAllowed,
    IReadOnlyList<ByteRange> violatingRanges)
{
    /// <summary>True when every observed range is fully covered by declared write authority.</summary>
    public bool IsAllowed { get; } = isAllowed;

    /// <summary>Observed ranges that are outside declared write authority.</summary>
    public IReadOnlyList<ByteRange> ViolatingRanges { get; } = violatingRanges;
}
