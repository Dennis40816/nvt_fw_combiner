using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Infrastructure.Composition;

internal sealed partial class BuiltInGeneralAuthoringPlanner
{
    internal static bool IsGeneralReplaceDpV2Route(
        IcNumberSelection selection,
        GeneralMappingDraftState mappingDraft,
        IReadOnlyList<TpFlashMapRegion> regions,
        IReadOnlyList<ExplicitMapping> mappings)
    {
        if (selection.Mode != IcNumberInputMode.SingleSelector ||
            mappingDraft.Rows.Any(static row =>
                row.Source.Kind != GeneralMappingSourceKind.FileArtifact))
        {
            return false;
        }

        TpFlashMapRegion[] dpRegions = [.. regions.Where(static region => region.Kind == TpFlashMapRegionKind.Dp)];
        return mappings.Count > 0 && mappings.All(mapping =>
            dpRegions.Any(region => region.Range.Contains(mapping.TargetRange)));
    }
}
