using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Production flash-map catalog normalized from TP Overview and postbuild naming.</summary>
public static partial class TpFlashMapCatalog
{
    private static readonly Dictionary<string, TpFlashMapProfile> ProfilesByIc = BuildProfiles()
        .ToDictionary(profile => profile.IcId, StringComparer.Ordinal);

    /// <summary>Supported IC ids in stable order.</summary>
    public static IReadOnlyList<string> IcIds { get; } =
    [
        .. ProfilesByIc.Keys.Order(StringComparer.Ordinal),
    ];

    /// <summary>Returns true when the catalog has a flash-map profile for <paramref name="icId"/>.</summary>
    public static bool TryFind(string icId, out TpFlashMapProfile? profile)
    {
        return ProfilesByIc.TryGetValue(icId, out profile);
    }

    /// <summary>Gets TP Overview CtrlRAM regions visible for the selected IC and IC-count context.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetCtrlRamRegions(
        string icId,
        IcNumberSelection? selection)
    {
        return GetRegions(icId, selection, TpFlashMapRegionKind.CtrlRam);
    }

    /// <summary>Gets TP Overview CtrlRAM regions adjusted to the selected postbuild category.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetCtrlRamRegions(
        string icId,
        IcNumberSelection? selection,
        LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        return GetRegions(icId, selection, postbuildProfile, TpFlashMapRegionKind.CtrlRam);
    }

    /// <summary>Gets TP Overview regions visible for the selected IC, IC-count context, and optional kind.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetRegions(
        string icId,
        IcNumberSelection? selection,
        TpFlashMapRegionKind? kind = null)
    {
        return GetRegions(icId, selection, postbuildProfile: null, kind);
    }

    /// <summary>Gets TP Overview regions adjusted to the selected postbuild category.</summary>
    public static IReadOnlyList<TpFlashMapRegion> GetRegions(
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
                .Where(region => kind is null || region.Kind == kind)
        ];
    }
}
