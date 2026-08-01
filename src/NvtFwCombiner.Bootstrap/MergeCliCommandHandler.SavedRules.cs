using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Bootstrap;

internal static partial class MergeCliCommandHandler
{
    private static bool TryCreateDraftFromSavedRule(
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

        if (!BuiltInV2RegistrationRegistry.GeneralMergeByIc.TryGetValue(
                icId,
                out GeneralMergeV2CandidateRegistration? registration))
        {
            error.WriteLine(
                $"error: no exact trusted {icId} / General Merge parent is registered");
            return false;
        }

        SavedRuleV2DraftLoadResult<GeneralMergeDraftState> load =
            SavedRuleV2GeneralMergeDraftLoader.Load(
                rulePath,
                slotsById,
                registration.Bundle.GetGeneralMergeSavedRuleAdmissionContext(
                    registration.ProfileId));
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
