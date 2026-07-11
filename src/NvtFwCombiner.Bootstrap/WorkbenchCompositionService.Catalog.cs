using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets selectable IC ids from the IC support catalog.</summary>
    public static IReadOnlyList<string> GetSupportedIcIds()
    {
        return IcMetadataFacade.IcIds;
    }

    /// <summary>Gets the catalog-owned initial IC id for shell/workbench surfaces.</summary>
    public static string GetDefaultIcId()
    {
        return IcMetadataFacade.DefaultIcId;
    }

    /// <summary>Gets supported IC-number choices from the TP flash-map/postbuild catalog.</summary>
    public static IReadOnlyList<string> GetNumberChoices(string icId)
    {
        return IcMetadataFacade.GetNumberChoices(icId);
    }

    /// <summary>Gets concise grouped IC-number choices for workbench selection controls.</summary>
    public static IReadOnlyList<IcNumberChoice> GetNumberSelectionChoices(string icId)
    {
        return IcMetadataFacade.GetNumberSelectionChoices(icId);
    }

    /// <summary>Returns true when the IC uses the DP Perspective family policy.</summary>
    public static bool IsDpPerspectiveIc(string icId)
    {
        return IcMetadataFacade.IsDpPerspectiveIc(icId);
    }

    /// <summary>Gets a compact, catalog-backed policy summary for the selected DP Replace IC.</summary>
    public static string GetDpReplacePolicySummary(string icId)
    {
        return IcMetadataFacade.IsDpPerspectiveIc(icId)
            ? $"DP replacement follows the selected base BIN length: {DpPerspectiveCatalog.FormatSupportedLengths()}; original TP range {FormatDisplayRange(DpPerspectiveCatalog.TpOverlayRange)} is restored from base."
            : "Build stays gated until this IC has approved DP Replace source mapping evidence.";
    }

    /// <summary>Gets catalog and tool summary data for the Settings page.</summary>
    public static WorkbenchSettingsSnapshot GetSettingsSnapshot()
    {
        IReadOnlyList<string> toolBindingIds =
        [
            .. IcMetadataFacade.All
                .SelectMany(metadata => IcMetadataFacade.GetPostbuildProfiles(metadata.IcId))
                .Select(profile => profile.ToolBindingId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        return new WorkbenchSettingsSnapshot(
            BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles.Count,
            BuiltInReplaceProfiles.All.Count,
            IcMetadataFacade.All.Count,
            IcMetadataFacade.All.Count(metadata => metadata.HasPostbuild),
            string.Join(", ", toolBindingIds),
            "external-tools/legacy-combiner/1.13.0/manifest.json");
    }
}
