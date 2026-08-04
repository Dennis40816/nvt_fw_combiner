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
        return postbuildProfile is null
            ? []
            : GetPostbuildMappedCtrlRamRegions(
                icId,
                LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile, selection));
    }

    /// <summary>Gets mapped CtrlRAM regions from one exact topology-resolved postbuild plan.</summary>
    internal static IReadOnlyList<TpFlashMapRegion> GetPostbuildMappedCtrlRamRegions(
        string icId,
        LegacyCombinerPostbuildCommandPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        IReadOnlyList<LegacyCombinerBlockArgument> blocks =
            GetSelectableStagedFileBlocks(plan);
        return [
            .. GetRegionsForPlan(icId, plan, TpFlashMapRegionKind.CtrlRam)
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
        return ProfilesByIc.TryGetValue(icId, out TpFlashMapProfile? flashMapProfile) &&
               postbuildProfile is not null
            ? GetPostbuildCtrlRamSources(
                icId,
                flashMapProfile,
                LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile, selection))
            : [];
    }

    /// <summary>Gets selectable CtrlRAM sources from one exact topology-resolved postbuild plan.</summary>
    internal static IReadOnlyList<TpCtrlRamPostbuildSource> GetPostbuildCtrlRamSources(
        string icId,
        LegacyCombinerPostbuildCommandPlan plan)
    {
        return ProfilesByIc.TryGetValue(icId, out TpFlashMapProfile? flashMapProfile)
            ? GetPostbuildCtrlRamSources(icId, flashMapProfile, plan)
            : [];
    }

    private static IReadOnlyList<TpCtrlRamPostbuildSource> GetPostbuildCtrlRamSources(
        string icId,
        TpFlashMapProfile flashMapProfile,
        LegacyCombinerPostbuildCommandPlan plan)
    {
        TpFlashMapRegion[] regions =
        [
            .. GetPostbuildMappedCtrlRamRegions(icId, plan),
        ];
        return
        [
            .. GetSelectableStagedFileBlocks(plan)
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
                long mappedRequiredLength = blocks.Max(block =>
                    checked(block.SourceOffset + block.FirmwareRange.Length));
                LegacyCombinerDiffDlmPolicy? diffDlmPolicy =
                    plan.Branch == LegacyCombinerPostbuildBranch.Cascade &&
                    plan.Profile.DiffDlmPolicy is { } candidate &&
                    StringComparer.Ordinal.Equals(candidate.SourceFileName, group.Key)
                        ? candidate
                        : null;
                long requiredLength = diffDlmPolicy is null
                    ? mappedRequiredLength
                    : Math.Max(
                        mappedRequiredLength,
                        diffDlmPolicy.GetRequiredSourceLength(plan.TopologyCount));
                return new TpCtrlRamPostbuildSource(
                    sourceId,
                    group.Key,
                    blocks.Select(block => block.StagedArtifactId).Distinct(StringComparer.Ordinal).Single()!,
                    requiredLength,
                    blocks,
                    sourceRegions,
                    sourceRegions.Any(static region =>
                        region.Tags.Contains("diff", StringComparer.Ordinal))
                            ? TpCtrlRamPostbuildArtifactRole.DiffDlm
                            : TpCtrlRamPostbuildArtifactRole.CtrlRam);
            }),
        ];
    }

    private static IReadOnlyList<LegacyCombinerBlockArgument> GetSelectableStagedFileBlocks(
        LegacyCombinerPostbuildCommandPlan plan)
    {
        IReadOnlyList<LegacyCombinerBlockArgument> blocks =
            LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan);
        LegacyCombinerDiffDlmPolicy? policy =
            plan.Branch == LegacyCombinerPostbuildBranch.Cascade
                ? plan.Profile.DiffDlmPolicy
                : null;
        return policy is null
            ? blocks
            :
            [
                .. blocks.Where(block => !policy.IsIndependentNfBlock(block)),
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
        return ApplyPostbuildRangeOverrides(visibleRegions, plan);
    }

    private static TpFlashMapRegion[] ApplyPostbuildRangeOverrides(
        IEnumerable<TpFlashMapRegion> regions,
        LegacyCombinerPostbuildCommandPlan plan)
    {
        TpFlashMapRegion[] visibleRegions = [.. regions];
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
