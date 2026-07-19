using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private const int OutputDifferenceHexPreviewBytes = 32;

    private static IEnumerable<ByteRange> SplitRangeByExpectations(
        ByteRange changedRange,
        IReadOnlyList<OutputDifferenceExpectation> expectations)
    {
        SortedSet<long> points = [changedRange.Start, changedRange.EndExclusive];
        foreach (OutputDifferenceExpectation expectation in expectations)
        {
            ByteRange? overlap = changedRange.Intersect(expectation.Range);
            if (overlap is null)
            {
                continue;
            }

            _ = points.Add(overlap.Value.Start);
            _ = points.Add(overlap.Value.EndExclusive);
        }

        long[] ordered = [.. points];
        for (int index = 0; index < ordered.Length - 1; index++)
        {
            yield return ByteRange.FromStartEndExclusive(ordered[index], ordered[index + 1]);
        }
    }

    private static IEnumerable<ByteRange> SplitRangeByWriteSectionBoundaries(
        ByteRange source,
        IReadOnlyList<ExternalProcessorWriteRangeSection> sections)
    {
        SortedSet<long> points = [source.Start, source.EndExclusive];
        foreach (ExternalProcessorWriteRangeSection section in sections)
        {
            ByteRange? overlap = source.Intersect(section.Range);
            if (overlap is not null)
            {
                _ = points.Add(overlap.Value.Start);
                _ = points.Add(overlap.Value.EndExclusive);
            }
        }

        long[] ordered = [.. points];
        for (int index = 0; index < ordered.Length - 1; index++)
        {
            yield return ByteRange.FromStartEndExclusive(ordered[index], ordered[index + 1]);
        }
    }

    private static string ToSliceSha256Hex(byte[] bytes, ByteRange range)
    {
        return ToSha256Hex(bytes.AsSpan(checked((int)range.Start), checked((int)range.Length)));
    }

    private static string ToSliceHexPreview(byte[] bytes, ByteRange range)
    {
        int length = checked((int)Math.Min(range.Length, OutputDifferenceHexPreviewBytes));
        return ToHex(bytes.AsSpan(checked((int)range.Start), length));
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
