using System.Text.Json;

namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedCompositionRuleLoader
{
    private static SavedRuleCompatibility ReadCompatibility(JsonElement root, List<SavedRuleValidationIssue> issues)
    {
        if (!root.TryGetProperty("compatibility", out JsonElement compatibility) ||
            compatibility.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue("saved-rule.compatibility.required", "Saved rule requires a compatibility object.", "$.compatibility"));
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
            issues.Add(Issue("saved-rule.ic-id.invalid", $"IC id '{value}' must use NT-prefixed catalog form.", "$.compatibility.icIds"));
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
        if (compositionKind == "merge" && sourceExperience != "general-merge")
        {
            issues.Add(Issue("saved-rule.experience-kind.mismatch", "Merge saved rules must use sourceExperience general-merge.", "$.sourceExperience"));
        }

        if (compositionKind == "replace" && sourceExperience != "general-replace")
        {
            issues.Add(Issue("saved-rule.experience-kind.mismatch", "Replace saved rules must use sourceExperience general-replace.", "$.sourceExperience"));
        }

        if (mappingRows.Count == 0)
        {
            issues.Add(Issue("saved-rule.mapping-rows.empty", "Saved rule must include at least one mapping row.", "$.mappingRows"));
        }

        if (operationFragments.Count == 0)
        {
            issues.Add(Issue("saved-rule.operation-fragments.empty", "Saved rule must include at least one operation fragment.", "$.operationFragments"));
        }

        if (compositionKind == "merge" && sourceExperience == "general-merge" && processorDependencyIds.Count > 0)
        {
            issues.Add(Issue(
                "saved-rule.processor-dependency.unsupported",
                "Current General Merge saved-rule CLI consumption does not support root processorDependencyIds.",
                "$.processorDependencyIds"));
        }

        if (compositionKind == "replace" && sourceExperience == "general-replace" && processorDependencyIds.Count > 0)
        {
            issues.Add(Issue(
                "saved-rule.processor-dependency.unsupported",
                "Current General Replace saved-rule projection does not support root processorDependencyIds.",
                "$.processorDependencyIds"));
        }

        if (supportStatus is "candidate" or "supported" && evidenceRefs.Count == 0)
        {
            issues.Add(Issue("saved-rule.evidence.required", "Candidate or supported saved rules must include evidenceRefs.", "$.evidenceRefs"));
        }
    }
}
