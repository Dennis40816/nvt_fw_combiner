using NvtFwCombiner.Application.Authoring;
namespace NvtFwCombiner.Infrastructure.Composition;

internal sealed class BuiltInSavedRuleAuthoring : ISavedRuleAuthoring
{
    public SavedRuleV2DraftLoadResult<GeneralMergeDraftState> LoadGeneralMergeSavedRule(
        string icId,
        string path,
        IReadOnlyDictionary<string, string> slotsById)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return BuiltInV2RegistrationRegistry.GeneralMergeByIc.TryGetValue(
                icId,
                out GeneralMergeV2CandidateRegistration? registration)
            ? SavedRuleV2GeneralMergeDraftLoader.Load(
                path,
                slotsById,
                registration.Bundle.GetGeneralMergeSavedRuleAdmissionContext(
                    registration.ProfileId))
            : SavedRuleLoadFailure<GeneralMergeDraftState>(
                $"No exact trusted {icId} / General Merge parent is registered.");
    }

    public string? GetGeneralReplaceSavedRuleReferenceSlotId(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return BuiltInV2RegistrationRegistry.GeneralReplaceByIc.TryGetValue(
                icId,
                out GeneralReplaceV2Registration? registration)
            ? registration.ReferenceSlotId
            : null;
    }

    public SavedRuleV2DraftLoadResult<GeneralMappingDraftState> LoadGeneralReplaceSavedRule(
        string icId,
        string path,
        IReadOnlyDictionary<string, string> slotsById)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return BuiltInV2RegistrationRegistry.GeneralReplaceByIc.TryGetValue(
                icId,
                out GeneralReplaceV2Registration? registration)
            ? SavedRuleV2GeneralMergeDraftLoader.LoadGeneralReplace(
                path,
                slotsById,
                registration.SavedRuleAdmissionContext)
            : SavedRuleLoadFailure<GeneralMappingDraftState>(
                $"No exact trusted {icId} / General Replace Saved Rule parent is registered.");
    }

    public SavedRuleV2InspectionResult InspectSavedRuleV2(string path)
    {
        return SavedRuleV2Inspector.Inspect(path);
    }

    private static SavedRuleV2DraftLoadResult<TDraft> SavedRuleLoadFailure<TDraft>(
        string message)
        where TDraft : class
    {
        return new SavedRuleV2DraftLoadResult<TDraft>(
            null,
            null,
            null,
            null,
            [new SavedRuleValidationIssue(
                SavedRuleIssueCodes.ParentUnavailable,
                message,
                "$.parent")]);
    }
}
