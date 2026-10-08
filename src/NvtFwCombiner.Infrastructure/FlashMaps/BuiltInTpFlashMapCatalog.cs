using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.FlashMaps;

/// <summary>Hash-pinned TP flash-map facts normalized from TP Overview and owner-approved base shapes.</summary>
internal static partial class BuiltInTpFlashMapCatalog
{
    private static readonly Catalog CatalogInstance = new(LoadProfiles());

    /// <summary>Supported IC ids in stable order.</summary>
    internal static IReadOnlyList<string> IcIds { get; } = CatalogInstance.IcIds;

    /// <summary>Returns true only when the IC has exactly one map entry and needs no version selection.</summary>
    internal static bool TryFind(string icId, out TpFlashMapProfile? profile)
    {
        return CatalogInstance.TryFind(icId, out profile);
    }

    /// <summary>Gets TP Overview regions adjusted to the selected postbuild category.</summary>
    internal static IReadOnlyList<TpFlashMapRegion> GetRegions(
        string icId,
        IcNumberSelection? selection,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        TpFlashMapRegionKind? kind = null)
    {
        return CatalogInstance.GetRegions(icId, selection, postbuildProfile, kind);
    }

    private static IReadOnlyList<TpFlashMapRegion> GetRegions(
        TpFlashMapProfile profile,
        IcNumberSelection? selection,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        TpFlashMapRegionKind? kind)
    {
        int? count = TryGetNumericCount(selection);
        bool isSingle = IsSingle(selection, count);
        return [
            .. ApplyPostbuildRangeOverrides(
                    profile.Regions
                        .Where(region => kind is null || region.Kind == kind)
                        .Where(region => IsVisible(region.Visibility, isSingle, count)),
                    postbuildProfile,
                    selection)
        ];
    }

    /// <summary>Gets TP Overview regions adjusted by one exact topology-resolved postbuild plan.</summary>
    internal static IReadOnlyList<TpFlashMapRegion> GetRegionsForPlan(
        string icId,
        LegacyCombinerPostbuildCommandPlan postbuildPlan,
        TpFlashMapRegionKind? kind = null)
    {
        return CatalogInstance.GetRegionsForPlan(icId, postbuildPlan, kind);
    }

    private static IReadOnlyList<TpFlashMapRegion> GetRegionsForPlan(
        TpFlashMapProfile profile,
        LegacyCombinerPostbuildCommandPlan postbuildPlan,
        TpFlashMapRegionKind? kind)
    {
        int count = postbuildPlan.TopologyCount;
        bool isSingle = count == 1;
        return [
            .. ApplyPostbuildRangeOverrides(
                profile.Regions
                    .Where(region => kind is null || region.Kind == kind)
                    .Where(region => IsVisible(region.Visibility, isSingle, count)),
                postbuildPlan)
        ];
    }

    private static bool IsVisible(TpFlashMapRegionVisibility visibility, bool isSingle, int? count)
    {
        return visibility switch
        {
            TpFlashMapRegionVisibility.Always => true,
            TpFlashMapRegionVisibility.MultiChipOnly => !isSingle,
            TpFlashMapRegionVisibility.TwoChipAndAbove => !isSingle && (count is null || count >= 2),
            TpFlashMapRegionVisibility.ThreeChipAndAbove => !isSingle && (count is null || count >= 3),
            _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unsupported visibility."),
        };
    }

    private static bool IsSingle(IcNumberSelection? selection, int? count)
    {
        if (selection is null || selection.Mode == IcNumberInputMode.SingleSelector || count == 1)
        {
            return true;
        }

        string? lastPart = selection.Parts.Count == 0 ? null : selection.Parts[^1];
        return IcNumberSelectionTokens.IsSingle(lastPart);
    }

    private static int? TryGetNumericCount(IcNumberSelection? selection)
    {
        return selection?.Mode != IcNumberInputMode.NumericSelector || selection.Parts.Count == 0
            ? null
            : int.TryParse(selection.Parts[^1], out int count) ? count : null;
    }
}
