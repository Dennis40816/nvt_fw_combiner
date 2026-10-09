using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>The postbuild profile and TP flash map that one Common FW version selects for an IC.</summary>
/// <param name="PostbuildProfile">The legacy Combiner postbuild profile for the version.</param>
/// <param name="TpFlashMap">The TP flash map for the version, or null when the IC has no TP flash-map entry.</param>
internal sealed record BuiltInCommonFwSelection(
    LegacyCombinerPostbuildProfile? PostbuildProfile,
    TpFlashMapProfile? TpFlashMap);

/// <summary>
/// Single place that turns an IC and a Common FW version into the postbuild profile and the TP flash map to use.
/// Every flow that needs either one asks here, so the interval rule, the diagnostics and the pending-map refusal
/// cannot differ between flows.
/// </summary>
internal static class BuiltInCommonFwSelector
{
    /// <summary>Selects both entries or returns the issue that explains why no entry applies.</summary>
    internal static bool TrySelect(
        string icId,
        bool hasCommonFwVersion,
        string? commonFwVersion,
        out BuiltInCommonFwSelection? selection,
        out CompositionIssue? issue)
    {
        selection = null;
        string normalizedIcId = IcIdentifier.Normalize(icId);
        if (!hasCommonFwVersion && BuiltInTpFlashMapCatalog.TryGetNotProvidedIssue(normalizedIcId, out string? notProvided))
        {
            // No slot of this IC is a map, so no version could select one. Say that before asking for a version.
            issue = new CompositionIssue(
                CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildCategoryUnknown,
                notProvided!,
                "postbuild");
            return false;
        }

        if (!BuiltInPostbuildProfileResolver.TryResolvePostbuildProfile(
                icId,
                BuiltInPostbuildProfileCatalog.GetProfiles(normalizedIcId),
                hasCommonFwVersion,
                commonFwVersion,
                out LegacyCombinerPostbuildProfile? postbuildProfile,
                out issue))
        {
            return false;
        }

        TpFlashMapProfile? tpFlashMap = null;
        if (BuiltInTpFlashMapCatalog.IcIds.Contains(normalizedIcId, StringComparer.Ordinal) &&
            !BuiltInTpFlashMapCatalog.TrySelect(
                normalizedIcId,
                hasCommonFwVersion ? commonFwVersion : null,
                out tpFlashMap,
                out string? mapIssue))
        {
            issue = new CompositionIssue(
                hasCommonFwVersion
                    ? CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildCategoryUnsupported
                    : CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildCategoryUnknown,
                mapIssue ?? $"No TP flash-map profile is registered for {icId}.",
                "postbuild");
            return false;
        }

        issue = null;
        selection = new BuiltInCommonFwSelection(postbuildProfile, tpFlashMap);
        return true;
    }

    /// <summary>
    /// Tells a flow that plans from map regions to stop: the selection was refused, the IC has TP flash-map slots,
    /// and no map remains to show where the TP regions are. Without a map such a flow cannot tell whether a
    /// mapping touches TP, so it must carry the refusal instead of continuing.
    /// </summary>
    internal static bool MustRefuseWithoutTpRegions(string icId, bool selected, TpFlashMapProfile? tpFlashMap)
    {
        return !selected &&
            tpFlashMap is null &&
            BuiltInTpFlashMapCatalog.IcIds.Contains(IcIdentifier.Normalize(icId), StringComparer.Ordinal);
    }

    /// <summary>
    /// Finds the TP flash map for a display that must stay available while an input is incomplete. A refusal
    /// shows no map instead of an unverified one, except that an IC with exactly one map keeps it.
    /// </summary>
    internal static TpFlashMapProfile? FindTpFlashMap(string icId, string? commonFwVersion)
    {
        string normalizedIcId = IcIdentifier.Normalize(icId);
        // An IC with one map keeps showing it for any version, as before. Other refusals show no map.
        return BuiltInTpFlashMapCatalog.TrySelect(normalizedIcId, commonFwVersion, out TpFlashMapProfile? tpFlashMap, out _)
            ? tpFlashMap
            : BuiltInTpFlashMapCatalog.TryFind(normalizedIcId, out TpFlashMapProfile? sole) ? sole : null;
    }
}
