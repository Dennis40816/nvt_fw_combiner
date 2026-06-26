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
        ByteRange[] observed = changedRanges.OrderBy(range => range.Start).ThenBy(range => range.Length).ToArray();
        ByteRange[] violations = observed.Where(range => !_allowedWrites.Contains(range)).ToArray();
        return new ChangedRangeVerdict(violations.Length == 0, observed, violations);
    }
}

/// <summary>Result of validating observed changes against declared write ranges.</summary>
public sealed record ChangedRangeVerdict(
    bool IsAllowed,
    IReadOnlyList<ByteRange> ObservedRanges,
    IReadOnlyList<ByteRange> ViolatingRanges);
