using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Cli;

internal static partial class MergeCliCommandHandler
{
    private static bool TryCreateDraftFromSavedRule(
        ISavedRuleAuthoring authoring,
        string rulePath,
        IReadOnlyList<string> slotValues,
        string icId,
        TextWriter error,
        [NotNullWhen(true)] out GeneralMergeDraftState? draft,
        [NotNullWhen(true)] out GeneralSavedRuleResourcePolicy? savedRulePolicy)
    {
        draft = null;
        savedRulePolicy = null;
        if (!SavedRuleCliSupport.TryCreateSlotBindings(
                slotValues,
                error,
                out Dictionary<string, string>? slotsById))
        {
            return false;
        }

        SavedRuleV2DraftLoadResult<GeneralMergeDraftState> load =
            authoring.LoadGeneralMergeSavedRule(
                icId,
                rulePath,
                slotsById);
        if (!load.IsValid)
        {
            SavedRuleCliSupport.PrintIssues(load.Issues, error);
            return false;
        }

        draft = load.Draft!;
        savedRulePolicy = load.ResourcePolicy!;
        return true;
    }
}
