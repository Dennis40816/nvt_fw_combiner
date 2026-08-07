namespace NvtFwCombiner.Domain.Composition;

/// <summary>Calculates compact changed byte ranges between two equally sized buffers.</summary>
public static class ByteDiff
{
    /// <summary>Returns contiguous changed ranges from <paramref name="before"/> to <paramref name="after"/>.</summary>
    public static IReadOnlyList<ByteRange> FindChangedRanges(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        DomainInvariant.Reject(before.Length != after.Length, "Buffers must have the same length.", nameof(after));

        List<ByteRange> ranges = [];
        int index = 0;
        while (index < before.Length)
        {
            if (before[index] == after[index])
            {
                index++;
                continue;
            }

            int start = index;
            do
            {
                index++;
            }
            while (index < before.Length && before[index] != after[index]);

            ranges.Add(new ByteRange(start, index - start));
        }

        return ranges;
    }
}
