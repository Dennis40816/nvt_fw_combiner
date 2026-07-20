using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Shared postbuild branch selection cases used by catalog dependency tests.</summary>
internal static class PostbuildSelectionTestCases
{
    public static IEnumerable<(LegacyCombinerPostbuildProfile Profile, IcNumberSelection Selection)> AllProfileBranchSelections()
    {
        foreach (LegacyCombinerPostbuildProfile profile in LegacyCombinerPostbuildCatalog.All)
        {
            foreach (IcNumberSelection selection in GetBranchSelections(profile))
            {
                yield return (profile, selection);
            }
        }
    }

    public static IEnumerable<IcNumberSelection> GetBranchSelections(LegacyCombinerPostbuildProfile profile)
    {
        return profile.BranchRules
            .Select(rule => ToBranchSelection(rule.Key, rule.Value))
            .DistinctBy(GetSelectionKey);
    }

    public static IcNumberSelection ToNumberChoiceSelection(string token)
    {
        return token switch
        {
            string value when IcNumberSelectionTokens.IsSingle(value) =>
                new IcNumberSelection(IcNumberInputMode.SingleSelector, [value]),
            string value when string.Equals(value, IcNumberSelectionTokens.Cascade, StringComparison.Ordinal) =>
                new IcNumberSelection(IcNumberInputMode.CascadeSelector, [value]),
            string value when int.TryParse(value, out _) =>
                new IcNumberSelection(IcNumberInputMode.NumericSelector, [value]),
            _ => throw new ArgumentException($"Unsupported IC number token '{token}'.", nameof(token)),
        };
    }

    private static IcNumberSelection ToBranchSelection(string token, LegacyCombinerPostbuildBranch branch)
    {
        return branch switch
        {
            LegacyCombinerPostbuildBranch.SingleChip =>
                new IcNumberSelection(IcNumberInputMode.SingleSelector, [IcNumberSelectionTokens.SingleChip]),
            LegacyCombinerPostbuildBranch.Cascade when int.TryParse(token, out int count) && count > 1 =>
                new IcNumberSelection(IcNumberInputMode.NumericSelector, [token]),
            LegacyCombinerPostbuildBranch.Cascade =>
                new IcNumberSelection(IcNumberInputMode.CascadeSelector, [IcNumberSelectionTokens.Cascade]),
            LegacyCombinerPostbuildBranch.TwoChip or
                LegacyCombinerPostbuildBranch.ThreeChip =>
                new IcNumberSelection(IcNumberInputMode.NumericSelector, [token]),
            _ => throw new ArgumentOutOfRangeException(nameof(branch), branch, "Unsupported postbuild branch."),
        };
    }

    private static string GetSelectionKey(IcNumberSelection selection)
    {
        return $"{selection.Mode}:{string.Join("|", selection.Parts)}";
    }
}
