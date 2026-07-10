using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>
    /// Gets profile-authorized General Replace ranges for a selected base BIN.
    /// The returned rows are display aids; Build remains the compiler authority.
    /// </summary>
    public static IReadOnlyList<WorkbenchGeneralReplaceEditableRange> GetGeneralReplaceEditableRanges(
        string icId,
        string number,
        string? basePath,
        WorkbenchGeneralReplaceBaseSnapshot? baseSnapshot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);

        long fileCapacity = 0;
        if (string.IsNullOrWhiteSpace(basePath) ||
            (baseSnapshot is not null && !baseSnapshot.IsForSourcePath(basePath)) ||
            (baseSnapshot is null && !TryGetGeneralReplaceBaseCapacity(basePath, out fileCapacity)))
        {
            return [];
        }

        long capacity = baseSnapshot?.Length ?? fileCapacity;

        IcNumberSelection selection;
        try
        {
            selection = ToIcNumberSelection(number);
        }
        catch (ArgumentException)
        {
            return [];
        }

        LegacyCombinerPostbuildProfile? postbuildProfile;
        if (baseSnapshot is null)
        {
            postbuildProfile = TryGetPostbuildProfile(
                icId,
                basePath,
                out LegacyCombinerPostbuildProfile? resolvedPostbuild,
                out _)
                    ? resolvedPostbuild
                    : null;
        }
        else
        {
            postbuildProfile = TryGetPostbuildProfile(
                icId,
                baseSnapshot,
                out LegacyCombinerPostbuildProfile? resolvedPostbuild,
                out _)
                    ? resolvedPostbuild
                    : null;
        }
        LegacyCombinerPostbuildCommandPlan? commandPlan = TryCreateGeneralReplacePostbuildPlan(postbuildProfile, selection);
        IReadOnlyList<LegacyCombinerPostbuildWriteRange> writeRangeSections = commandPlan is null
            ? []
            : LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForInPlaceRefresh(commandPlan, capacity);
        CompositionProfileDefinition profile = CreateGeneralReplaceProfile(
            icId,
            selection,
            capacity,
            postbuildProfile,
            commandPlan,
            writeRangeSections);
        IReadOnlyList<TpFlashMapRegion> flashMapRegions = TpFlashMapCatalog.GetRegions(
            icId,
            selection,
            postbuildProfile);

        return
        [
            .. profile.Regions
                .Where(region =>
                    region.WritePolicy == RegionWritePolicy.GeneralExplicit &&
                    (commandPlan is not null || !RequiresGeneralReplacePostbuild(region)))
                .OrderBy(region => region.Range.Start)
                .Select(region => CreateGeneralReplaceEditableRange(region, flashMapRegions)),
        ];
    }

    private static bool TryGetGeneralReplaceBaseCapacity(string basePath, out long capacity)
    {
        capacity = 0;
        try
        {
            string fullPath = Path.GetFullPath(basePath);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            capacity = new FileInfo(fullPath).Length;
            return capacity > 0;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static LegacyCombinerPostbuildCommandPlan? TryCreateGeneralReplacePostbuildPlan(
        LegacyCombinerPostbuildProfile? postbuildProfile,
        IcNumberSelection selection)
    {
        if (postbuildProfile is null)
        {
            return null;
        }

        try
        {
            return LegacyCombinerPostbuildPlanner.CreatePlan(postbuildProfile, selection);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static WorkbenchGeneralReplaceEditableRange CreateGeneralReplaceEditableRange(
        ProfileRegion region,
        IReadOnlyList<TpFlashMapRegion> flashMapRegions)
    {
        TpFlashMapRegion? source = flashMapRegions.FirstOrDefault(candidate =>
            string.Equals(candidate.RegionId, region.RegionId, StringComparison.Ordinal) ||
            region.RegionId.StartsWith($"{candidate.RegionId}-", StringComparison.Ordinal));
        string displayName = source?.DisplayName ?? region.RegionId.Replace('-', ' ');
        bool requiresPostbuild = RequiresGeneralReplacePostbuild(region);
        string detail = requiresPostbuild
            ? "TP region: Build runs the approved CRC/header postbuild refresh."
            : "DP region: Build applies the selected bytes without CRC postbuild.";
        return new WorkbenchGeneralReplaceEditableRange(
            region.RegionId,
            displayName,
            region.Range.Start,
            region.Range.EndExclusive - 1,
            requiresPostbuild,
            detail);
    }

    private static bool RequiresGeneralReplacePostbuild(ProfileRegion region)
    {
        return region.ClassificationTags.Any(tag =>
            string.Equals(tag, "tp", StringComparison.OrdinalIgnoreCase) ||
            tag.StartsWith("tp-", StringComparison.OrdinalIgnoreCase));
    }
}
