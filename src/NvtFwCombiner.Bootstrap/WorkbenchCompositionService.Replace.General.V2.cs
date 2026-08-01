using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool IsGeneralReplaceDpV2Route(
        GeneralReplaceRunContext context,
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyList<ExplicitMapping> mappings)
    {
        if (context.Selection.Mode != IcNumberInputMode.SingleSelector ||
            context.MappingDraft.Rows.Any(static row =>
                row.Source.Kind != GeneralMappingSourceKind.FileArtifact))
        {
            return false;
        }

        TpFlashMapRegion[] dpRegions = [.. regions.Where(static region => region.Kind == TpFlashMapRegionKind.Dp)];
        return mappings.Count > 0 && mappings.All(mapping =>
            dpRegions.Any(region => region.Range.Contains(mapping.TargetRange)));
    }
}
