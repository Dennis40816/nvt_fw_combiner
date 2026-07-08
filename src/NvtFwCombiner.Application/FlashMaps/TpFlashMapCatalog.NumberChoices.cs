using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.FlashMaps;

public static partial class TpFlashMapCatalog
{
    /// <summary>Gets UI number choices from the postbuild branches available for an IC.</summary>
    public static IReadOnlyList<string> GetNumberChoices(string icId)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = LegacyCombinerPostbuildCatalog.GetProfiles(icId);
        return profiles.Any(profile => profile.BranchRules.Values.Contains(LegacyCombinerPostbuildBranch.CascadeExtended))
            ? GetExtendedNumberChoices(profiles)
            : !PostbuildProfilesByIc.TryGetValue(icId, out LegacyCombinerPostbuildProfile? profile)
            ? ["single"]
            : profile.TwoChipCommands is not null || profile.ThreeChipCommands is not null
            ? ["single", "2", "3"]
            : ["single", "cascade"];
    }

    private static IReadOnlyList<string> GetExtendedNumberChoices(IEnumerable<LegacyCombinerPostbuildProfile> profiles)
    {
        return
        [
            "single",
            .. profiles
                .SelectMany(profile => profile.BranchRules.Keys)
                .Where(token => int.TryParse(token, out int value) && value > 1)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(token => int.Parse(token, CultureInfo.InvariantCulture)),
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
        if (selection is null)
        {
            return true;
        }

        if (selection.Mode == IcNumberInputMode.SingleSelector || count == 1)
        {
            return true;
        }

        string? lastPart = selection.Parts.Count == 0 ? null : selection.Parts[^1];
        return string.Equals(lastPart, "single", StringComparison.OrdinalIgnoreCase);
    }

    private static int? TryGetNumericCount(IcNumberSelection? selection)
    {
        return selection?.Mode != IcNumberInputMode.NumericSelector || selection.Parts.Count == 0
            ? null
            : int.TryParse(selection.Parts[^1], out int count) ? count : null;
    }
}
