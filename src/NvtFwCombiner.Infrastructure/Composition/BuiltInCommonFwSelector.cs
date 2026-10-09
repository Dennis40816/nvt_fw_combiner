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
    /// Finds the TP flash map for a display that must stay available while an input is incomplete. Any refusal
    /// shows no map instead of an unverified one.
    /// </summary>
    internal static TpFlashMapProfile? FindTpFlashMap(string icId, string? commonFwVersion)
    {
        return BuiltInTpFlashMapCatalog.TrySelect(
            IcIdentifier.Normalize(icId),
            commonFwVersion,
            out TpFlashMapProfile? tpFlashMap,
            out _)
                ? tpFlashMap
                : null;
    }
}
