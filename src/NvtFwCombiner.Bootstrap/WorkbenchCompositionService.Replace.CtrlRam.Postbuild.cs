using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static List<ByteRange> CreatePostbuildAllowedWriteRanges(
        LegacyCombinerPostbuildCommandPlan commandPlan,
        long capacity,
        IReadOnlyList<TpFlashMapRegion> selectedCtrlRamRegions,
        IReadOnlyList<TpFlashMapRegion> ctrlRamRegions)
    {
        List<ByteRange> candidateRanges = [];
        foreach (LegacyCombinerPostbuildCommand command in commandPlan.Commands)
        {
            foreach (LegacyCombinerBlockArgument block in command.Blocks)
            {
                if (block.FirmwareRange.EndExclusive > capacity)
                {
                    continue;
                }

                if (block.SourceKind == LegacyCombinerBlockSourceKind.StagedFile)
                {
                    foreach (TpFlashMapRegion selectedRegion in selectedCtrlRamRegions)
                    {
                        ByteRange? overlap = block.FirmwareRange.Intersect(selectedRegion.Range);
                        if (overlap is not null)
                        {
                            candidateRanges.Add(overlap.Value);
                        }
                    }

                    continue;
                }

                if (block.SourceOffset != block.FirmwareRange.Start)
                {
                    candidateRanges.Add(block.FirmwareRange);
                }
            }
        }
        candidateRanges.AddRange(LegacyCombinerPostbuildPlanner.GetKnownIntegrityWriteRanges(commandPlan, capacity));

        return NormalizeCandidateWriteRanges(candidateRanges, ctrlRamRegions);
    }

    private static List<ByteRange> NormalizeCandidateWriteRanges(
        List<ByteRange> candidateRanges,
        IReadOnlyList<TpFlashMapRegion> ctrlRamRegions)
    {
        if (candidateRanges.Count == 0)
        {
            return [];
        }

        SortedSet<long> splitPoints = [];
        foreach (ByteRange range in candidateRanges)
        {
            _ = splitPoints.Add(range.Start);
            _ = splitPoints.Add(range.EndExclusive);
            foreach (TpFlashMapRegion region in ctrlRamRegions)
            {
                ByteRange? overlap = range.Intersect(region.Range);
                if (overlap is not null)
                {
                    _ = splitPoints.Add(overlap.Value.Start);
                    _ = splitPoints.Add(overlap.Value.EndExclusive);
                }
            }
        }

        long[] points = [.. splitPoints];
        List<ByteRange> ranges = [];
        for (int index = 0; index < points.Length - 1; index++)
        {
            var segment = ByteRange.FromStartEndExclusive(points[index], points[index + 1]);
            if (candidateRanges.Any(range => range.Contains(segment)))
            {
                ranges.Add(segment);
            }
        }

        return [
            .. ranges
                .Distinct()
                .OrderBy(range => range.Start)
                .ThenBy(range => range.Length),
        ];
    }

    private static string FormatPostbuildCommandBlock(LegacyCombinerPostbuildCommandPlan commandPlan)
    {
        string firmwarePath = Path.Combine("output", commandPlan.Profile.FirmwareFileName);
        const string binDirectory = "BIN";
        return string.Join(
            Environment.NewLine,
            commandPlan.Commands.Select(command =>
                $"Combiner.exe {string.Join(' ', LegacyCombinerPostbuildCommandLineBuilder.CreateArguments(command, firmwarePath, binDirectory))}"));
    }
}
