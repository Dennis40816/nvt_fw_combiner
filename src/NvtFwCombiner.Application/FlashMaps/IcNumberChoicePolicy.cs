using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Projects typed postbuild plan selectors into validated request and UI choices.</summary>
public static class IcNumberChoicePolicy
{
    /// <summary>Returns true when at least one approved postbuild profile accepts the selection.</summary>
    public static bool IsNumberSelectionSupported(
        IcNumberSelection selection,
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(profiles);
        return selection.Parts.Count != 0 && (profiles.Count == 0
            ? IcNumberSelectionTokens.IsSingle(selection.Parts[^1])
            : profiles.All(profile => profile.PlanSelectors.Any(selector => selector.Matches(selection))));
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

        LegacyCombinerPostbuildPlanSelector[] selectors =
        [
            .. profiles[0].PlanSelectors
                .Where(selector => profiles.All(profile => profile.PlanSelectors.Any(candidate =>
                    string.Equals(candidate.Token, selector.Token, StringComparison.Ordinal) &&
                    candidate.Branch == selector.Branch)))
                .OrderBy(static selector => selector.MinimumCount),
        ];
        return [.. selectors.Select(static selector => new IcNumberChoice(selector.Token, selector.DisplayLabel))];
    }

}
