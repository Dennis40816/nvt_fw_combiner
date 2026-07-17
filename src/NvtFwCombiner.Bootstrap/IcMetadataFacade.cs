using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Read-only boundary over the canonical IC onboarding, TP flash-map, and postbuild catalogs.
/// This type owns no firmware facts; it prevents UI and CLI callers from joining catalog categories themselves.
/// </summary>
internal static class IcMetadataFacade
{
    /// <summary>Gets grouped UI choices without exposing every legacy branch alias.</summary>
    public static IReadOnlyList<IcNumberChoice> GetNumberSelectionChoices(string icId)
    {
        return IcSupportCatalog.TryFind(icId, out _)
            ? IcNumberChoicePolicy.GetNumberSelectionChoices(GetPostbuildProfiles(icId))
            : [];
    }

    /// <summary>Gets the approved postbuild category variants for a selectable IC.</summary>
    public static IReadOnlyList<LegacyCombinerPostbuildProfile> GetPostbuildProfiles(string icId)
    {
        return IcSupportCatalog.TryFind(icId, out _)
            ? BuiltInPostbuildProfileCatalog.GetProfiles(IcSupportCatalog.NormalizeIcId(icId))
            : [];
    }

    /// <summary>Returns whether a profile-backed IC-number token selects an approved postbuild branch.</summary>
    public static bool IsNumberSelectionSupported(string icId, IcNumberSelection selection)
    {
        return IcSupportCatalog.TryFind(icId, out _) &&
            IcNumberChoicePolicy.IsNumberSelectionSupported(selection, GetPostbuildProfiles(icId));
    }

    /// <summary>Selects the approved postbuild category for a base image Common FW version.</summary>
    public static bool TrySelectPostbuildProfile(
        string icId,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? postbuildProfile,
        out string? issue)
    {
        if (!IcSupportCatalog.TryFind(icId, out _))
        {
            postbuildProfile = null;
            issue = $"No IC metadata is registered for {icId}.";
            return false;
        }

        return BuiltInPostbuildProfileCatalog.TrySelectProfileForCommonFwVersion(
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
        return IcSupportCatalog.TryFind(icId, out _) && BuiltInPostbuildProfileCatalog.TryGetDefaultProfile(
            IcSupportCatalog.NormalizeIcId(icId),
            out postbuildProfile);
    }
}
