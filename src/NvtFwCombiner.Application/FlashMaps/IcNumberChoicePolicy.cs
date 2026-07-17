using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Projects profile-declared postbuild branch tokens into validated request and UI choices.</summary>
public static class IcNumberChoicePolicy
{
    /// <summary>Returns true when at least one approved postbuild profile accepts the selection.</summary>
    public static bool IsNumberSelectionSupported(
        IcNumberSelection selection,
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(profiles);
        if (selection.Parts.Count == 0)
        {
            return false;
        }

        if (profiles.Count == 0)
        {
            return IcNumberSelectionTokens.IsSingle(selection.Parts[^1]);
        }

        string token = LegacyCombinerPostbuildBranchRule.NormalizeToken(selection.Parts[^1]);
        return profiles.Any(profile => profile.BranchRules.ContainsKey(token));
    }

    /// <summary>Gets UI number choices from the postbuild branches available for an IC.</summary>
    public static IReadOnlyList<string> GetNumberChoices(IReadOnlyList<LegacyCombinerPostbuildProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        IReadOnlyList<string> numericChoices = GetNumericNumberChoices(profiles);
        return numericChoices.Count > 0
            ? [IcNumberSelectionTokens.SingleChip, .. numericChoices]
            : profiles.Count == 0
            ? [IcNumberSelectionTokens.SingleChip]
            : profiles[0].TwoChipCommands is not null || profiles[0].ThreeChipCommands is not null
            ? [IcNumberSelectionTokens.SingleChip, "2", "3"]
            : [IcNumberSelectionTokens.SingleChip, IcNumberSelectionTokens.Cascade];
    }

    /// <summary>
    /// Gets concise count choices grouped by the identical postbuild branch they select.
    /// Raw branch aliases remain available through <see cref="GetNumberChoices"/> for callers that
    /// need to validate a serialized request; the workbench should render these options instead.
    /// </summary>
    public static IReadOnlyList<IcNumberChoice> GetNumberSelectionChoices(
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
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

}
