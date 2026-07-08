using System.Globalization;
using System.Text.Json;

namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedCompositionRuleLoader
{
    private static List<SavedRuleOperationFragment> ReadOperationFragments(
        JsonElement root,
        string compositionKind,
        string sourceExperience,
        List<SavedRuleMappingRow> mappingRows,
        List<SavedRuleValidationIssue> issues)
    {
        if (!root.TryGetProperty("operationFragments", out JsonElement fragments) ||
            fragments.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Issue("saved-rule.operation-fragments.required", "Saved rule requires operationFragments array.", "$.operationFragments"));
            return [];
        }

        HashSet<string> mappingRowIds = new(mappingRows.Select(row => row.RowId), StringComparer.Ordinal);
        HashSet<string> referencedMappingRowIds = new(StringComparer.Ordinal);
        List<string> operationIds = [];
        List<SavedRuleOperationFragment> result = [];
        int index = 0;
        foreach (JsonElement fragment in fragments.EnumerateArray())
        {
            string path = string.Create(CultureInfo.InvariantCulture, $"$.operationFragments[{index++}]");
            if (fragment.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue("saved-rule.operation-fragment.invalid", "Operation fragment must be an object.", path));
                continue;
            }

            ValidateProperties(fragment, OperationFragmentProperties, path, issues);
            string operationId = RequiredId(fragment, "operationId", $"{path}.operationId", issues);
            string kind = RequiredEnum(
                fragment,
                "kind",
                $"{path}.kind",
                ["copy-range", "fill-range", "patch-scalar", "replace-range", "run-external-processor", "assert-range", "validate-checksum"],
                issues);
            if (compositionKind == "merge" &&
                sourceExperience == "general-merge" &&
                !string.IsNullOrWhiteSpace(kind) &&
                !string.Equals(kind, "copy-range", StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    "saved-rule.operation-fragment.kind-unsupported",
                    "Current General Merge saved-rule CLI consumption supports only copy-range operation fragments.",
                    $"{path}.kind"));
            }

            _ = RequiredString(fragment, "reason", $"{path}.reason", issues);
            List<string> fragmentProcessorDependencyIds = ReadStringArray(
                fragment,
                "processorDependencyIds",
                $"{path}.processorDependencyIds",
                required: false,
                validateId: true,
                issues);
            if (compositionKind == "merge" &&
                sourceExperience == "general-merge" &&
                fragmentProcessorDependencyIds.Count > 0)
            {
                issues.Add(Issue(
                    "saved-rule.operation-fragment.processor-dependency.unsupported",
                    "Current General Merge saved-rule CLI consumption does not support processor-dependent operation fragments.",
                    $"{path}.processorDependencyIds"));
            }

            if (compositionKind == "replace" &&
                sourceExperience == "general-replace" &&
                fragmentProcessorDependencyIds.Count > 0)
            {
                issues.Add(Issue(
                    "saved-rule.operation-fragment.processor-dependency.unsupported",
                    "Current General Replace saved-rule projection does not support processor-dependent operation fragments.",
                    $"{path}.processorDependencyIds"));
            }

            List<string> fragmentMappingRowIds = ReadStringArray(fragment, "mappingRowIds", $"{path}.mappingRowIds", required: false, validateId: true, issues);
            if (compositionKind == "merge" &&
                sourceExperience == "general-merge" &&
                fragmentMappingRowIds.Count != 1)
            {
                issues.Add(Issue(
                    "saved-rule.operation-fragment.mapping-row-count",
                    "Current General Merge saved-rule CLI consumption requires each operation fragment to reference exactly one mapping row.",
                    $"{path}.mappingRowIds"));
            }

            foreach (string mappingRowId in fragmentMappingRowIds)
            {
                if (!mappingRowIds.Contains(mappingRowId))
                {
                    issues.Add(Issue(
                        "saved-rule.operation-fragment.mapping-row-unknown",
                        $"Operation fragment references unknown mapping row '{mappingRowId}'.",
                        $"{path}.mappingRowIds"));
                    continue;
                }

                if (!referencedMappingRowIds.Add(mappingRowId))
                {
                    issues.Add(Issue(
                        "saved-rule.operation-fragment.mapping-row-duplicate-reference",
                        $"General Merge saved-rule mapping row '{mappingRowId}' is referenced by more than one operation fragment.",
                        $"{path}.mappingRowIds"));
                }
            }

            if (!string.IsNullOrWhiteSpace(operationId))
            {
                operationIds.Add(operationId);
                result.Add(new SavedRuleOperationFragment(operationId, fragmentMappingRowIds));
            }
        }

        AddDuplicateIssues(operationIds, "saved-rule.operation-fragment.duplicate", "Operation fragment id is duplicated.", "$.operationFragments", issues);
        if (compositionKind == "merge" && sourceExperience == "general-merge")
        {
            foreach (SavedRuleMappingRow row in mappingRows.Where(row =>
                         !string.IsNullOrWhiteSpace(row.RowId) &&
                         !referencedMappingRowIds.Contains(row.RowId)))
            {
                issues.Add(Issue(
                    "saved-rule.mapping-row.unreferenced",
                    $"General Merge saved-rule mapping row '{row.RowId}' is not referenced by any supported operation fragment.",
                    "$.mappingRows"));
            }
        }

        return result;
    }
}
