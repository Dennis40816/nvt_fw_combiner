using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets TP Overview CtrlRAM regions visible for a selected IC and IC-number context.</summary>
    public static IReadOnlyList<WorkbenchCtrlRamRegion> GetCtrlRamRegions(
        string icId,
        string number,
        string? basePath = null)
    {
        LegacyCombinerPostbuildProfile? postbuildProfile = TryResolvePostbuildProfileForDisplay(
            icId,
            basePath,
            out LegacyCombinerPostbuildProfile? profile)
                ? profile
                : null;
        return
        [
            .. TpFlashMapCatalog.GetCtrlRamRegions(icId, ToIcNumberSelection(number), postbuildProfile)
                .Select(region => new WorkbenchCtrlRamRegion(
                    region.DisplayName,
                    region.Range.Start,
                    region.Range.Length,
                    region.Tags.Any(tag =>
                        string.Equals(tag, "diff", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "dlm", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "slave", StringComparison.OrdinalIgnoreCase)))),
        ];
    }
}
