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

    /// <summary>
    /// Gets concise count choices grouped by the identical postbuild branch they select.
    /// Serialized request validation remains owned by <see cref="IsNumberSelectionSupported"/>.
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

}
