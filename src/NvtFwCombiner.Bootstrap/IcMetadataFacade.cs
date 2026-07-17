using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Read-only boundary over the canonical IC onboarding, TP flash-map, and postbuild catalogs.
/// This type owns no firmware facts; it prevents UI and CLI callers from joining catalog categories themselves.
/// </summary>
internal static class IcMetadataFacade
{
    /// <summary>All selectable IC identifiers in stable display order.</summary>
    public static IReadOnlyList<string> IcIds { get; } = IcSupportCatalog.IcIds;

    /// <summary>Catalog-owned initial IC identifier for workbench surfaces.</summary>
    public static string DefaultIcId => IcSupportCatalog.DefaultIcId;

    /// <summary>Returns true when the normalized IC is selectable.</summary>
    public static bool IsKnown(string icId)
    {
        return IcSupportCatalog.TryFind(icId, out _);
    }

    /// <summary>Gets the profile-declared IC-number choices for a selectable IC.</summary>
    public static IReadOnlyList<string> GetNumberChoices(string icId)
    {
        return IsKnown(icId)
            ? TpFlashMapCatalog.GetNumberChoices(IcSupportCatalog.NormalizeIcId(icId))
            : [];
    }

    /// <summary>Gets grouped UI choices without exposing every legacy branch alias.</summary>
    public static IReadOnlyList<IcNumberChoice> GetNumberSelectionChoices(string icId)
    {
        return IsKnown(icId)
            ? TpFlashMapCatalog.GetNumberSelectionChoices(IcSupportCatalog.NormalizeIcId(icId))
            : [];
    }

    /// <summary>Gets the approved postbuild category variants for a selectable IC.</summary>
    public static IReadOnlyList<LegacyCombinerPostbuildProfile> GetPostbuildProfiles(string icId)
    {
        return IsKnown(icId) ? LegacyCombinerPostbuildCatalog.GetProfiles(IcSupportCatalog.NormalizeIcId(icId)) : [];
    }

    /// <summary>Returns true when support policy exposes CtrlRAM Replace for the selected IC.</summary>
    public static bool SupportsCtrlRamReplace(string icId)
    {
        return IcSupportCatalog.SupportsWorkflow(icId, IcWorkflowIds.CtrlRamReplace);
    }

    /// <summary>Selects the approved postbuild category for a base image Common FW version.</summary>
    public static bool TrySelectPostbuildProfile(
        string icId,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? postbuildProfile,
        out string? issue)
    {
        if (!IsKnown(icId))
        {
            postbuildProfile = null;
            issue = $"No IC metadata is registered for {icId}.";
            return false;
        }

        return LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            IcSupportCatalog.NormalizeIcId(icId),
            commonFwVersion,
            out postbuildProfile,
            out issue);
    }

    /// <summary>Gets the catalog display fallback when a base image has not been selected yet.</summary>
    public static bool TryGetDefaultPostbuildProfile(
        string icId,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        postbuildProfile = null;
        return IsKnown(icId) && LegacyCombinerPostbuildCatalog.TryGetDefaultProfile(
            IcSupportCatalog.NormalizeIcId(icId),
            out postbuildProfile);
    }
}
