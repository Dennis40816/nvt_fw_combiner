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
        return profile.PlanSelectors.Select(ToPlanSelection);
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
            string value when value.StartsWith("cascade_", StringComparison.Ordinal) =>
                new IcNumberSelection(IcNumberInputMode.CascadeSelector, [value]),
            _ => throw new ArgumentException($"Unsupported IC number token '{token}'.", nameof(token)),
        };
    }

    private static IcNumberSelection ToPlanSelection(LegacyCombinerPostbuildPlanSelector selector)
    {
        return selector.Kind switch
        {
            LegacyCombinerPostbuildPlanSelectorKind.SingleChip =>
                new IcNumberSelection(IcNumberInputMode.SingleSelector, [selector.Token]),
            LegacyCombinerPostbuildPlanSelectorKind.GenericCascade or
                LegacyCombinerPostbuildPlanSelectorKind.CountRange =>
                new IcNumberSelection(IcNumberInputMode.CascadeSelector, [selector.Token]),
            LegacyCombinerPostbuildPlanSelectorKind.ExactCount =>
                new IcNumberSelection(IcNumberInputMode.NumericSelector, [selector.Token]),
            _ => throw new ArgumentOutOfRangeException(nameof(selector), selector.Kind, "Unsupported postbuild selector."),
        };
    }
}
