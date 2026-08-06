namespace NvtFwCombiner.Domain.Composition;

/// <summary>Bounds profile-declared expected immutable input-length sets.</summary>
public static class InputLengthPolicyLimits
{
    /// <summary>Maximum number of non-blocking expected input lengths carried by one contract.</summary>
    public const int MaximumExpectedInputLengths = 8;

    internal static long[] SnapshotExpectedOuterLengths(
        IReadOnlyList<long> expectedOuterLengths,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(expectedOuterLengths, parameterName);
        if (expectedOuterLengths.Count is 0 or > MaximumExpectedInputLengths)
        {
            throw new ArgumentException(
                $"Expected outer lengths must contain between 1 and {MaximumExpectedInputLengths} values.",
                parameterName);
        }

        long[] snapshot = [.. expectedOuterLengths];
        return snapshot.Where((value, index) => value <= 0 ||
                (index > 0 && value <= snapshot[index - 1])).Any()
            ? throw new ArgumentException(
                "Expected outer lengths must be positive and strictly ascending.",
                parameterName)
            : snapshot;
    }
}
