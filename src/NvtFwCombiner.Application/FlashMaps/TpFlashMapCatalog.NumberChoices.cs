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
        IReadOnlyList<string> numericChoices = GetNumericNumberChoices(profiles);
        return numericChoices.Count > 0
            ? [IcNumberSelectionTokens.SingleChip, .. numericChoices]
            : !PostbuildProfilesByIc.TryGetValue(icId, out LegacyCombinerPostbuildProfile? profile)
            ? [IcNumberSelectionTokens.SingleChip]
            : profile.TwoChipCommands is not null || profile.ThreeChipCommands is not null
            ? [IcNumberSelectionTokens.SingleChip, "2", "3"]
            : [IcNumberSelectionTokens.SingleChip, IcNumberSelectionTokens.Cascade];
    }

    /// <summary>
    /// Gets concise count choices grouped by the identical postbuild branch they select.
    /// Raw legacy aliases remain available through <see cref="GetNumberChoices"/> for callers that
    /// need to validate a serialized request; the workbench should render these options instead.
    /// </summary>
    public static IReadOnlyList<IcNumberChoice> GetNumberSelectionChoices(string icId)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = LegacyCombinerPostbuildCatalog.GetProfiles(icId);
        if (profiles.Count == 0)
        {
            return [new IcNumberChoice(IcNumberSelectionTokens.SingleChip, "1 IC")];
        }

        IReadOnlyList<(int Count, LegacyCombinerPostbuildBranch Branch)> numericBranches =
        [
            .. profiles
                .SelectMany(profile => profile.BranchRules)
                .Where(pair => int.TryParse(pair.Key, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                .Select(pair => (
                    int.Parse(pair.Key, CultureInfo.InvariantCulture),
                    pair.Value))
                .Distinct()
                .OrderBy(pair => pair.Item1),
        ];

        bool hasExplicitCountBranches = numericBranches.Any(pair =>
            pair.Branch is LegacyCombinerPostbuildBranch.TwoChip or LegacyCombinerPostbuildBranch.ThreeChip);
        return hasExplicitCountBranches
            ?
            [
                new IcNumberChoice(IcNumberSelectionTokens.SingleChip, "1 IC"),
                .. numericBranches
                    .Where(pair => pair.Branch is LegacyCombinerPostbuildBranch.TwoChip or LegacyCombinerPostbuildBranch.ThreeChip)
                    .Select(pair => new IcNumberChoice(
                        pair.Count.ToString(CultureInfo.InvariantCulture),
                        $"{pair.Count} IC")),
            ]
            :
            [
                new IcNumberChoice(IcNumberSelectionTokens.SingleChip, "1 IC"),
                new IcNumberChoice(IcNumberSelectionTokens.Cascade, "Cascade"),
            ];
    }

    private static IReadOnlyList<string> GetNumericNumberChoices(IEnumerable<LegacyCombinerPostbuildProfile> profiles)
    {
        return
        [
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
        return IcNumberSelectionTokens.IsSingle(lastPart);
    }

    private static int? TryGetNumericCount(IcNumberSelection? selection)
    {
        return selection?.Mode != IcNumberInputMode.NumericSelector || selection.Parts.Count == 0
            ? null
            : int.TryParse(selection.Parts[^1], out int count) ? count : null;
    }
}
