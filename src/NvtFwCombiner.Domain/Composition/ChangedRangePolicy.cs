namespace NvtFwCombiner.Domain.Composition;

/// <summary>Validates that observed byte changes remain inside declared write authority.</summary>
public sealed class ChangedRangePolicy(IEnumerable<ByteRange> allowedWrites)
{
    private readonly RangeSet _allowedWrites = new(allowedWrites);

    /// <summary>Returns a deterministic verdict for observed changed ranges.</summary>
    public ChangedRangeVerdict Evaluate(IEnumerable<ByteRange> changedRanges)
    {
        ArgumentNullException.ThrowIfNull(changedRanges);
        ByteRange[] observed = [.. changedRanges.OrderBy(range => range.Start).ThenBy(range => range.Length)];
        ByteRange[] violations = [.. observed.Where(range => !_allowedWrites.Contains(range))];
        return new ChangedRangeVerdict(violations.Length == 0, violations);
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
