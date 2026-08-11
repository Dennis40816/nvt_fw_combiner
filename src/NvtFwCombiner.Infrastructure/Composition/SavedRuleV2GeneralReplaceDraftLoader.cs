using System.Text.Json;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Infrastructure.Composition;

internal static partial class SavedRuleV2GeneralMergeDraftLoader
{
    internal static SavedRuleV2DraftLoadResult<GeneralMappingDraftState>
        LoadGeneralReplace(
        string path,
        IReadOnlyDictionary<string, string> slotsById,
        SavedRuleV2GeneralReplaceAdmissionContext admissionContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(slotsById);
        ArgumentNullException.ThrowIfNull(admissionContext);
        return LoadFile(
            path,
            root => ParseGeneralReplace(
                root,
                slotsById,
                admissionContext),
            GeneralReplaceFailed);
    }

    private static SavedRuleV2DraftLoadResult<GeneralMappingDraftState>
        ParseGeneralReplace(
        JsonElement root,
        IReadOnlyDictionary<string, string> slotsById,
        SavedRuleV2GeneralReplaceAdmissionContext admissionContext)
    {
        SavedCompositionRuleV2AdmissionResult admission =
            SavedCompositionRuleV2Admission.ValidateGeneralReplace(
                root,
                admissionContext);
        if (!admission.IsValid)
        {
            return new SavedRuleV2DraftLoadResult<GeneralMappingDraftState>(
                null,
                admission.ParentBinding,
                null,
                null,
                admission.Issues);
        }

        SavedRuleV2MappingProjection? projection = ProjectMappings(
            root,
            slotsById,
            admissionContext.TargetRegions,
            admission.ParentBinding!,
            out IReadOnlyList<SavedRuleValidationIssue> issues);
        return projection is null
            ? new SavedRuleV2DraftLoadResult<GeneralMappingDraftState>(
                null,
                admission.ParentBinding,
                null,
                null,
                issues)
            : new SavedRuleV2DraftLoadResult<GeneralMappingDraftState>(
                projection.Draft,
                admission.ParentBinding,
                projection.ExecutionIdentity,
                projection.ResourcePolicy,
                []);
    }

    private static SavedRuleV2DraftLoadResult<GeneralMappingDraftState>
        GeneralReplaceFailed(SavedRuleValidationIssue issue)
    {
        return new SavedRuleV2DraftLoadResult<GeneralMappingDraftState>(
            null,
            null,
            null,
            null,
            [issue]);
    }
}
