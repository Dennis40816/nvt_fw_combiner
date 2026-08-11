using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.FlashMaps;

/// <summary>Hash-pinned TP flash-map facts normalized from TP Overview and owner-approved base shapes.</summary>
internal static partial class BuiltInTpFlashMapCatalog
{
    private static readonly Dictionary<string, TpFlashMapProfile> ProfilesByIc = LoadProfiles()
        .ToDictionary(profile => profile.IcId, StringComparer.Ordinal);

    /// <summary>Supported IC ids in stable order.</summary>
    internal static IReadOnlyList<string> IcIds { get; } =
    [
        .. ProfilesByIc.Keys.Order(StringComparer.Ordinal),
    ];

    /// <summary>Returns true when the catalog has a flash-map profile for <paramref name="icId"/>.</summary>
    internal static bool TryFind(string icId, out TpFlashMapProfile? profile)
    {
        return ProfilesByIc.TryGetValue(icId, out profile);
    }

    /// <summary>Gets TP Overview regions adjusted to the selected postbuild category.</summary>
    internal static IReadOnlyList<TpFlashMapRegion> GetRegions(
        string icId,
        IcNumberSelection? selection,
        LegacyCombinerPostbuildProfile? postbuildProfile,
        TpFlashMapRegionKind? kind = null)
    {
        if (!ProfilesByIc.TryGetValue(icId, out TpFlashMapProfile? profile))
        {
            return [];
        }

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
        ArgumentNullException.ThrowIfNull(postbuildPlan);
        if (!ProfilesByIc.TryGetValue(icId, out TpFlashMapProfile? profile))
        {
            return [];
        }

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
