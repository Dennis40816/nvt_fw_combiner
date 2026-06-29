namespace NvtFwCombiner.Domain.Composition;

/// <summary>Validates that observed byte changes remain inside declared write authority.</summary>
public sealed class ChangedRangePolicy
{
    private readonly RangeSet _allowedWrites;

    /// <summary>Creates a policy from profile-declared allowed write ranges.</summary>
    public ChangedRangePolicy(IEnumerable<ByteRange> allowedWrites)
    {
        _allowedWrites = new RangeSet(allowedWrites);
    }

    /// <summary>Returns a deterministic verdict for observed changed ranges.</summary>
    public ChangedRangeVerdict Evaluate(IEnumerable<ByteRange> changedRanges)
    {
        ArgumentNullException.ThrowIfNull(changedRanges);
        ByteRange[] observed = [.. changedRanges.OrderBy(range => range.Start).ThenBy(range => range.Length)];
        ByteRange[] violations = [.. observed.Where(range => !_allowedWrites.Contains(range))];
        return new ChangedRangeVerdict(violations.Length == 0, observed, violations);
    }
}

/// <summary>Result of validating observed changes against declared write ranges.</summary>
public sealed class ChangedRangeVerdict
{
    /// <summary>Creates a verdict from observed ranges and policy violations.</summary>
    public ChangedRangeVerdict(bool isAllowed, IReadOnlyList<ByteRange> observedRanges, IReadOnlyList<ByteRange> violatingRanges)
    {
        IsAllowed = isAllowed;
        ObservedRanges = observedRanges;
        ViolatingRanges = violatingRanges;
    }

    /// <summary>True when every observed range is fully covered by declared write authority.</summary>
    public bool IsAllowed { get; }

    /// <summary>Observed changed ranges in deterministic order.</summary>
    public IReadOnlyList<ByteRange> ObservedRanges { get; }

    /// <summary>Observed ranges that are outside declared write authority.</summary>
    public IReadOnlyList<ByteRange> ViolatingRanges { get; }
}
