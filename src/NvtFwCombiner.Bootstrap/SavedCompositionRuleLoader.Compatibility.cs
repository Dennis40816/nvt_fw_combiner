using System.Text.Json;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedCompositionRuleLoader
{
    private static SavedRuleCompatibility ReadCompatibility(JsonElement root, List<SavedRuleValidationIssue> issues)
    {
        if (!root.TryGetProperty("compatibility", out JsonElement compatibility) ||
            compatibility.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(SavedRuleIssueCodes.CompatibilityRequired, "Saved rule requires a compatibility object.", "$.compatibility"));
            return new SavedRuleCompatibility([], [], [], []);
        }

        ValidateProperties(compatibility, CompatibilityProperties, "$.compatibility", issues);
        return new SavedRuleCompatibility(
            ReadStringArray(compatibility, "profileIds", "$.compatibility.profileIds", required: true, validateId: true, issues),
            ReadIcArray(compatibility, issues),
            ReadStringArray(compatibility, "modeIds", "$.compatibility.modeIds", required: true, validateId: true, issues),
            ReadStringArray(compatibility, "compatibilityTags", "$.compatibility.compatibilityTags", required: false, validateId: true, issues));
    }

    private static List<string> ReadIcArray(JsonElement compatibility, List<SavedRuleValidationIssue> issues)
    {
        List<string> values = ReadStringArray(compatibility, "icIds", "$.compatibility.icIds", required: true, validateId: false, issues);
        foreach (string value in values.Where(value => !IcIdRegex().IsMatch(value)))
        {
            issues.Add(Issue(SavedRuleIssueCodes.IcIdInvalid, $"IC id '{value}' must use NT-prefixed catalog form.", "$.compatibility.icIds"));
        }

        return values;
    }

    private static void ValidateRuleCompatibility(
        string compositionKind,
        string sourceExperience,
        string supportStatus,
        List<string> evidenceRefs,
        List<SavedRuleMappingRow> mappingRows,
        List<SavedRuleOperationFragment> operationFragments,
        List<string> processorDependencyIds,
        List<SavedRuleValidationIssue> issues)
    {
        if (compositionKind == "merge" && sourceExperience != IcWorkflowIds.GeneralMerge)
        {
            issues.Add(Issue(SavedRuleIssueCodes.ExperienceKindMismatch, "Merge saved rules must use sourceExperience general-merge.", "$.sourceExperience"));
        }

        if (compositionKind == "replace" && sourceExperience != IcWorkflowIds.GeneralReplace)
        {
            issues.Add(Issue(SavedRuleIssueCodes.ExperienceKindMismatch, "Replace saved rules must use sourceExperience general-replace.", "$.sourceExperience"));
        }

        if (mappingRows.Count == 0)
        {
            issues.Add(Issue(SavedRuleIssueCodes.MappingRowsEmpty, "Saved rule must include at least one mapping row.", "$.mappingRows"));
        }

        if (operationFragments.Count == 0)
        {
            issues.Add(Issue(SavedRuleIssueCodes.OperationFragmentsEmpty, "Saved rule must include at least one operation fragment.", "$.operationFragments"));
        }

        if (compositionKind == "merge" && sourceExperience == IcWorkflowIds.GeneralMerge && processorDependencyIds.Count > 0)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.ProcessorDependencyUnsupported,
                "Current General Merge saved-rule CLI consumption does not support root processorDependencyIds.",
                "$.processorDependencyIds"));
        }

        if (compositionKind == "replace" && sourceExperience == IcWorkflowIds.GeneralReplace && processorDependencyIds.Count > 0)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.ProcessorDependencyUnsupported,
                "Current General Replace saved-rule projection does not support root processorDependencyIds.",
                "$.processorDependencyIds"));
        }

        if (supportStatus is "candidate" or "supported" && evidenceRefs.Count == 0)
        {
            issues.Add(Issue(SavedRuleIssueCodes.EvidenceRequired, "Candidate or supported saved rules must include evidenceRefs.", "$.evidenceRefs"));
        }
    }
}
