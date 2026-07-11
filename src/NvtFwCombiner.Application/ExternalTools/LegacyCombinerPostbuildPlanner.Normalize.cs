using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

public static partial class LegacyCombinerPostbuildPlanner
{
    private static IReadOnlyList<LegacyCombinerPostbuildWriteRange> NormalizeCandidateWriteRangeSections(
        List<LegacyCombinerPostbuildWriteRange> candidateRanges,
        IReadOnlyList<ByteRange> stagedTargetRanges)
    {
        if (candidateRanges.Count == 0)
        {
            return [];
        }

        SortedSet<long> splitPoints = [];
        foreach (LegacyCombinerPostbuildWriteRange candidate in candidateRanges)
        {
            ByteRange range = candidate.Range;
            _ = splitPoints.Add(range.Start);
            _ = splitPoints.Add(range.EndExclusive);
            foreach (ByteRange stagedRange in stagedTargetRanges)
            {
                ByteRange? overlap = range.Intersect(stagedRange);
                if (overlap is not null)
                {
                    _ = splitPoints.Add(overlap.Value.Start);
                    _ = splitPoints.Add(overlap.Value.EndExclusive);
                }
            }
        }

        long[] points = [.. splitPoints];
        List<LegacyCombinerPostbuildWriteRange> ranges = [];
        for (int index = 0; index < points.Length - 1; index++)
        {
            var segment = ByteRange.FromStartEndExclusive(points[index], points[index + 1]);
            if (candidateRanges.Any(candidate => candidate.Range.Contains(segment)))
            {
                LegacyCombinerPostbuildWriteRange selected = SelectWriteRangeSection(candidateRanges, segment);
                ranges.Add(new LegacyCombinerPostbuildWriteRange(
                    segment,
                    selected.SectionId,
                    selected.TryMapRangeToSourceRange(segment, out ByteRange sourceRange)
                        ? sourceRange
                        : null));
            }
        }

        return [
            .. ranges
                .GroupBy(section => (section.Range, section.SectionId, section.SourceRange))
                .Select(group => group.First())
                .OrderBy(section => section.Range.Start)
                .ThenBy(section => section.Range.Length)
                .ThenBy(section => section.SectionId, StringComparer.Ordinal),
        ];
    }

    private static LegacyCombinerPostbuildWriteRange SelectWriteRangeSection(
        IReadOnlyList<LegacyCombinerPostbuildWriteRange> candidates,
        ByteRange segment)
    {
        return candidates
            .Where(candidate => candidate.Range.Contains(segment))
            .OrderByDescending(candidate => TpHeaderCatalog.GetPriority(candidate.SectionId))
            .ThenBy(candidate => candidate.Range.Length)
            .ThenBy(candidate => candidate.Range.Start)
            .First();
    }
}
