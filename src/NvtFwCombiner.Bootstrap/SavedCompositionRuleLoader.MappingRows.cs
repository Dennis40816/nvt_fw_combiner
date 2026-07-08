using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedCompositionRuleLoader
{
    private static List<SavedRuleMappingRow> ReadMappingRows(
        JsonElement root,
        string compositionKind,
        string sourceExperience,
        HashSet<string> inputSlotTemplateIds,
        List<SavedRuleValidationIssue> issues)
    {
        if (!root.TryGetProperty("mappingRows", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Issue("saved-rule.mapping-rows.required", "Saved rule requires mappingRows array.", "$.mappingRows"));
            return [];
        }

        List<SavedRuleMappingRow> result = [];
        int index = 0;
        foreach (JsonElement row in rows.EnumerateArray())
        {
            string rowPath = string.Create(CultureInfo.InvariantCulture, $"$.mappingRows[{index++}]");
            if (row.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue("saved-rule.mapping-row.invalid", "Mapping row must be an object.", rowPath));
                continue;
            }

            ValidateProperties(row, MappingRowProperties, rowPath, issues);
            string rowId = RequiredId(row, "rowId", $"{rowPath}.rowId", issues);
            string? sourceBindingId = OptionalId(row, "sourceBindingId", $"{rowPath}.sourceBindingId", issues);
            string? sourceSlotTemplateId = OptionalId(row, "sourceSlotTemplateId", $"{rowPath}.sourceSlotTemplateId", issues);
            if (string.IsNullOrWhiteSpace(sourceBindingId) == string.IsNullOrWhiteSpace(sourceSlotTemplateId))
            {
                issues.Add(Issue(
                    "saved-rule.mapping-row.source-reference",
                    "Mapping row must declare exactly one of sourceBindingId or sourceSlotTemplateId.",
                    rowPath));
            }
            else if (!string.IsNullOrWhiteSpace(sourceSlotTemplateId) &&
                !inputSlotTemplateIds.Contains(sourceSlotTemplateId))
            {
                issues.Add(Issue(
                    "saved-rule.mapping-row.source-slot-template-unknown",
                    $"Mapping row references undeclared sourceSlotTemplateId '{sourceSlotTemplateId}'.",
                    $"{rowPath}.sourceSlotTemplateId"));
            }

            ByteRange? sourceRange = OptionalByteRange(row, "sourceRange", $"{rowPath}.sourceRange", issues);
            string targetAddressSpaceId = RequiredId(row, "targetAddressSpaceId", $"{rowPath}.targetAddressSpaceId", issues);
            string? targetRegionId = OptionalId(row, "targetRegionId", $"{rowPath}.targetRegionId", issues);
            ByteRange? targetRange = RequiredByteRange(row, "targetRange", $"{rowPath}.targetRange", issues);
            string overlapPolicy = RequiredEnum(row, "overlapPolicy", $"{rowPath}.overlapPolicy", MappingOverlapPolicyValues, issues);
            int alignment = OptionalPositiveInt(row, "alignment", $"{rowPath}.alignment", issues) ?? 1;
            string reason = RequiredString(row, "reason", $"{rowPath}.reason", issues);

            if (compositionKind == "merge" && sourceRange is null)
            {
                issues.Add(Issue(
                    "saved-rule.mapping-row.source-range-required",
                    "General Merge saved-rule mapping rows must declare sourceRange.",
                    $"{rowPath}.sourceRange"));
            }

            if (sourceRange is not null && targetRange is not null && sourceRange.Value.Length != targetRange.Value.Length)
            {
                issues.Add(Issue(
                    "saved-rule.mapping-row.length-mismatch",
                    "Mapping row sourceRange length must match targetRange length.",
                    rowPath));
            }

            if (sourceExperience == "general-replace" && sourceRange is not null && sourceRange.Value.Start != 0)
            {
                issues.Add(Issue(
                    "saved-rule.mapping-row.replace-source-offset-unsupported",
                    "Current General Replace CLI materialization supports replacement files from source offset 0 only.",
                    $"{rowPath}.sourceRange"));
            }

            if (compositionKind == "merge" && sourceExperience == "general-merge")
            {
                if (!string.IsNullOrWhiteSpace(targetAddressSpaceId) &&
                    !string.Equals(targetAddressSpaceId, "output-image", StringComparison.Ordinal))
                {
                    issues.Add(Issue(
                        "saved-rule.mapping-row.target-address-space-unsupported",
                        "Current General Merge saved-rule CLI consumption supports only output-image target rows.",
                        $"{rowPath}.targetAddressSpaceId"));
                }

                if (!string.IsNullOrWhiteSpace(targetRegionId) &&
                    !string.Equals(targetRegionId, "general-output", StringComparison.Ordinal))
                {
                    issues.Add(Issue(
                        "saved-rule.mapping-row.target-region-unsupported",
                        "Current General Merge saved-rule CLI consumption supports only the general-output target region.",
                        $"{rowPath}.targetRegionId"));
                }

                if (!string.IsNullOrWhiteSpace(overlapPolicy) &&
                    !string.Equals(overlapPolicy, "reject", StringComparison.Ordinal))
                {
                    issues.Add(Issue(
                        "saved-rule.mapping-row.overlap-policy-unsupported",
                        "Current General Merge saved-rule CLI consumption supports only reject overlap policy.",
                        $"{rowPath}.overlapPolicy"));
                }

                if (sourceRange is not null &&
                    targetRange is not null &&
                    (!IsAligned(sourceRange.Value, alignment) || !IsAligned(targetRange.Value, alignment)))
                {
                    issues.Add(Issue(
                        "saved-rule.mapping-row.alignment",
                        $"General Merge saved-rule row '{rowId}' source and target ranges must satisfy alignment {alignment}.",
                        $"{rowPath}.alignment"));
                }
            }

            if (targetRange is not null)
            {
                result.Add(new SavedRuleMappingRow(
                    rowId,
                    string.IsNullOrWhiteSpace(sourceBindingId) ? sourceSlotTemplateId! : sourceBindingId,
                    sourceRange,
                    targetAddressSpaceId,
                    targetRegionId,
                    targetRange.Value,
                    overlapPolicy,
                    alignment,
                    reason));
            }
        }

        AddDuplicateIssues(
            [.. result.Select(row => row.RowId).Where(rowId => !string.IsNullOrWhiteSpace(rowId))],
            "saved-rule.mapping-row.duplicate",
            "Mapping row id is duplicated.",
            "$.mappingRows",
            issues);
        return result;
    }

    private static bool IsAligned(ByteRange range, int alignment)
    {
        return range.Start % alignment == 0 &&
            range.Length % alignment == 0;
    }
}
