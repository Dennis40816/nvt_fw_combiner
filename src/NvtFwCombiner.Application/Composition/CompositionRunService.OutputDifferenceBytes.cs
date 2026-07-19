using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private const int OutputDifferenceHexPreviewBytes = 32;
    private static readonly string?[] SingleByteSha256Hex = new string[byte.MaxValue + 1];

    private static IEnumerable<ByteRange> SplitRangeByExpectations(
        ByteRange changedRange,
        IReadOnlyList<OutputDifferenceExpectation> expectations)
    {
        List<long>? interiorPoints = null;
        foreach (OutputDifferenceExpectation expectation in expectations)
        {
            ByteRange? overlap = changedRange.Intersect(expectation.Range);
            if (overlap is null)
            {
                continue;
            }

            if (overlap.Value.Start > changedRange.Start)
            {
                (interiorPoints ??= []).Add(overlap.Value.Start);
            }

            if (overlap.Value.EndExclusive < changedRange.EndExclusive)
            {
                (interiorPoints ??= []).Add(overlap.Value.EndExclusive);
            }
        }

        if (interiorPoints is null)
        {
            yield return changedRange;
            yield break;
        }

        interiorPoints.Sort();
        long segmentStart = changedRange.Start;
        foreach (long point in interiorPoints)
        {
            if (point == segmentStart)
            {
                continue;
            }

            yield return ByteRange.FromStartEndExclusive(segmentStart, point);
            segmentStart = point;
        }

        yield return ByteRange.FromStartEndExclusive(segmentStart, changedRange.EndExclusive);
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

    private static string ToSliceSha256Hex(ReadOnlySpan<byte> bytes, ByteRange range)
    {
        ReadOnlySpan<byte> slice = bytes.Slice(checked((int)range.Start), checked((int)range.Length));
        if (slice.Length != 1)
        {
            return ToSha256Hex(slice);
        }

        byte value = slice[0];
        string? cached = Volatile.Read(ref SingleByteSha256Hex[value]);
        if (cached is not null)
        {
            return cached;
        }

        string computed = ToSha256Hex(slice);
        return Interlocked.CompareExchange(ref SingleByteSha256Hex[value], computed, null) ?? computed;
    }

    private static string ToSliceHexPreview(ReadOnlySpan<byte> bytes, ByteRange range)
    {
        int length = checked((int)Math.Min(range.Length, OutputDifferenceHexPreviewBytes));
        return ToHex(bytes.Slice(checked((int)range.Start), length));
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(bytes);
    }
}
