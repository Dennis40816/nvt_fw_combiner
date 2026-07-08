using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets selectable IC ids from the TP flash-map catalog.</summary>
    public static IReadOnlyList<string> GetSupportedIcIds()
    {
        return IcSupportCatalog.IcIds;
    }

    /// <summary>Gets the catalog-owned initial IC id for shell/workbench surfaces.</summary>
    public static string GetDefaultIcId()
    {
        return IcSupportCatalog.DefaultIcId;
    }

    /// <summary>Gets supported IC-number choices from the TP flash-map/postbuild catalog.</summary>
    public static IReadOnlyList<string> GetNumberChoices(string icId)
    {
        return TpFlashMapCatalog.GetNumberChoices(icId);
    }

    /// <summary>Returns true when the IC uses the DP Perspective family policy.</summary>
    public static bool IsDpPerspectiveIc(string icId)
    {
        return DpPerspectiveCatalog.IsSupportedIc(icId);
    }

    /// <summary>Gets a compact, catalog-backed policy summary for the selected DP Replace IC.</summary>
    public static string GetDpReplacePolicySummary(string icId)
    {
        return DpPerspectiveCatalog.IsSupportedIc(icId)
            ? $"DP replacement follows the selected base BIN length: {DpPerspectiveCatalog.FormatSupportedLengths()}; original TP range {FormatDisplayRange(DpPerspectiveCatalog.TpOverlayRange)} is restored from base."
            : "Build stays gated until this IC has approved DP Replace source mapping evidence.";
    }

    /// <summary>Gets catalog and tool summary data for the Settings page.</summary>
    public static WorkbenchSettingsSnapshot GetSettingsSnapshot()
    {
        IReadOnlyList<string> toolBindingIds =
        [
            .. LegacyCombinerPostbuildCatalog.All
                .Select(profile => profile.ToolBindingId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        return new WorkbenchSettingsSnapshot(
            BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles.Count,
            BuiltInReplaceProfiles.All.Count,
            TpFlashMapCatalog.IcIds.Count,
            LegacyCombinerPostbuildCatalog.All.Select(profile => profile.IcId).Distinct(StringComparer.Ordinal).Count(),
            string.Join(", ", toolBindingIds),
            "external-tools/legacy-combiner/1.13.0/manifest.json");
    }
}
