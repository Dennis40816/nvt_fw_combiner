using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.FlashMaps;

internal static partial class BuiltInTpFlashMapCatalog
{
    /// <summary>Gets visible CtrlRAM regions that are consumed by a selected postbuild command plan.</summary>
    internal static IReadOnlyList<TpFlashMapRegion> GetPostbuildMappedCtrlRamRegions(
        string icId,
        IcNumberSelection? selection,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        if (!ProfilesByIc.ContainsKey(icId) || postbuildProfile is null)
        {
            return [];
        }

        IReadOnlyList<LegacyCombinerBlockArgument> blocks = LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(
            LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile, selection));
        return [
            .. GetCtrlRamRegions(icId, selection, postbuildProfile)
                .Where(region => blocks.Any(block => IsMappedBlock(region, block)))
        ];
    }

    /// <summary>
    /// Gets physical CtrlRAM source files separately from the logical destination regions they feed.
    /// </summary>
    internal static IReadOnlyList<TpCtrlRamPostbuildSource> GetPostbuildCtrlRamSources(
        string icId,
        IcNumberSelection? selection,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        if (!ProfilesByIc.TryGetValue(icId, out TpFlashMapProfile? flashMapProfile) ||
            postbuildProfile is null)
        {
            return [];
        }

        TpFlashMapRegion[] regions = [.. GetPostbuildMappedCtrlRamRegions(icId, selection, postbuildProfile)];
        return
        [
            .. LegacyCombinerPostbuildPlanner
            .GetStagedFileBlocks(LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile, selection))
            .Where(block => block.SourceKind == LegacyCombinerBlockSourceKind.StagedFile)
            .GroupBy(block => block.SourceFileName, StringComparer.Ordinal)
            .OrderBy(group => group.Min(block => block.FirmwareRange.Start))
            .Select(group =>
            {
                LegacyCombinerBlockArgument[] blocks = [.. group];
                TpFlashMapRegion[] sourceRegions = [.. regions.Where(region =>
                    string.Equals(region.PostbuildFileName, group.Key, StringComparison.Ordinal) &&
                    blocks.Any(block => region.Range.Overlaps(block.FirmwareRange)))];
                string[] physicalRegionIds = [.. flashMapProfile.Regions
                    .Where(region =>
                    region.Kind == TpFlashMapRegionKind.CtrlRam &&
                    string.Equals(region.PostbuildFileName, group.Key, StringComparison.Ordinal))
                    .Select(region => region.RegionId)];
                string sourceId = physicalRegionIds.Length > 1
                    ? physicalRegionIds.Select(id => id.Split('-', 2)[0]).Distinct(StringComparer.Ordinal).Single()
                    : sourceRegions[0].RegionId;
                return new TpCtrlRamPostbuildSource(
                    sourceId,
                    group.Key,
                    blocks.Select(block => block.StagedArtifactId).Distinct(StringComparer.Ordinal).Single()!,
                    blocks.Max(block => checked(block.SourceOffset + block.FirmwareRange.Length)),
                    blocks,
                    sourceRegions);
            }),
        ];
    }

    private static TpFlashMapRegion[] ApplyPostbuildRangeOverrides(
        IEnumerable<TpFlashMapRegion> regions,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        IcNumberSelection? selection)
    {
        TpFlashMapRegion[] visibleRegions = [.. regions];
        if (postbuildProfile is null)
        {
            return visibleRegions;
        }

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile, selection);
        IReadOnlyList<LegacyCombinerBlockArgument> blocks = [.. plan.Commands.SelectMany(command => command.Blocks)];
        return [
            .. visibleRegions.Select(region => TryResolvePostbuildRange(region, blocks, out ByteRange range)
                ? new TpFlashMapRegion(
                    region.RegionId,
                    region.DisplayName,
                    region.Kind,
                    range,
                    region.Visibility,
                    region.PostbuildFileName,
                    region.Tags)
                : region),
        ];
    }

    private static bool TryResolvePostbuildRange(
        TpFlashMapRegion region,
        IReadOnlyList<LegacyCombinerBlockArgument> blocks,
        out ByteRange range)
    {
        LegacyCombinerBlockArgument[] candidates = [.. blocks.Where(block => IsPostbuildRangeOverrideCandidate(region, block))];
        if (candidates.Length == 0)
        {
            range = default;
            return false;
        }

        long start = candidates.Min(block => block.FirmwareRange.Start);
        long endExclusive = candidates.Max(block => block.FirmwareRange.EndExclusive);
        range = ByteRange.FromStartEndExclusive(start, endExclusive);
        return true;
    }

    private static bool IsPostbuildRangeOverrideCandidate(
        TpFlashMapRegion region,
        LegacyCombinerBlockArgument block)
    {
        return region.Range.Overlaps(block.FirmwareRange) &&
            (block.SourceKind == LegacyCombinerBlockSourceKind.StagedFile
            ? string.Equals(region.PostbuildFileName, block.SourceFileName, StringComparison.Ordinal)
            : region.RegionId.Contains("fw-config", StringComparison.OrdinalIgnoreCase) &&
            block.BlockId.Contains("fw-config", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMappedBlock(TpFlashMapRegion region, LegacyCombinerBlockArgument block)
    {
        return string.Equals(region.PostbuildFileName, block.SourceFileName, StringComparison.Ordinal) &&
            region.Range.Overlaps(block.FirmwareRange);
    }
}
