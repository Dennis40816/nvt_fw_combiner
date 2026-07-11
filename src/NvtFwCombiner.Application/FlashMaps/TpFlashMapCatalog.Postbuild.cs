using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.FlashMaps;

public static partial class TpFlashMapCatalog
{
    /// <summary>Gets visible CtrlRAM regions that are consumed by the selected postbuild command plan.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetPostbuildMappedCtrlRamRegions(
        string icId,
        IcNumberSelection? selection)
    {
        return !PostbuildProfilesByIc.TryGetValue(icId, out LegacyCombinerPostbuildProfile? postbuildProfile)
            ? []
            : GetPostbuildMappedCtrlRamRegions(icId, selection, postbuildProfile);
    }

    /// <summary>Gets visible CtrlRAM regions that are consumed by a selected postbuild command plan.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetPostbuildMappedCtrlRamRegions(
        string icId,
        IcNumberSelection? selection,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        if (!ProfilesByIc.ContainsKey(icId) || postbuildProfile is null)
        {
            return [];
        }

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            postbuildProfile,
            selection);
        IReadOnlyList<LegacyCombinerBlockArgument> blocks = LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan);
        return [
            .. GetCtrlRamRegions(icId, selection, postbuildProfile)
                .Where(region => blocks.Any(block => IsMappedBlock(region, block)))
        ];
    }

    private static TpFlashMapRegion[] ApplyPostbuildRangeOverrides(
        IEnumerable<TpFlashMapRegion> regions,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        IcNumberSelection? selection)
    {
        TpFlashMapRegion[] visibleRegions =
        [
            .. regions,
        ];
        if (postbuildProfile is null)
        {
            return visibleRegions;
        }

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            postbuildProfile,
            selection);
        IReadOnlyList<LegacyCombinerBlockArgument> blocks =
        [
            .. plan.Commands.SelectMany(command => command.Blocks),
        ];
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
        LegacyCombinerBlockArgument[] candidates =
        [
            .. blocks.Where(block => IsPostbuildRangeOverrideCandidate(region, block)),
        ];
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
