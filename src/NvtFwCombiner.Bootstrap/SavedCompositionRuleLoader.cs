using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedCompositionRuleLoader
{
    private static readonly HashSet<string> TopLevelProperties =
    [
        "schemaVersion",
        "ruleId",
        "ruleVersion",
        "displayName",
        "description",
        "compositionKind",
        "sourceExperience",
        "supportStatus",
        "compatibility",
        "inputSlotTemplates",
        "mappingRows",
        "operationFragments",
        "processorDependencyIds",
        "validationRuleIds",
        "protectedRangePolicy",
        "owner",
        "reviewers",
        "evidenceRefs",
    ];

    private static readonly HashSet<string> CompatibilityProperties =
    [
        "profileIds",
        "icIds",
        "modeIds",
        "compatibilityTags",
    ];

    private static readonly HashSet<string> MappingRowProperties =
    [
        "rowId",
        "sourceBindingId",
        "sourceSlotTemplateId",
        "sourceRange",
        "targetAddressSpaceId",
        "targetRegionId",
        "targetRange",
        "overlapPolicy",
        "alignment",
        "reason",
    ];

    private static readonly HashSet<string> ByteRangeProperties = ["start", "length"];

    private static readonly HashSet<string> OperationFragmentProperties =
    [
        "operationId",
        "kind",
        "reason",
        "mappingRowIds",
        "processorDependencyIds",
    ];

    private static readonly HashSet<string> InputSlotTemplateProperties =
    [
        "slotTemplateId",
        "role",
        "cardinality",
        "acceptedExtensions",
    ];

    public static SavedCompositionRuleLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return new SavedCompositionRuleLoadResult(
                null,
                [Issue("saved-rule.file-not-found", $"Saved rule JSON was not found: {fullPath}", "$")]);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
            return Parse(document.RootElement);
        }
        catch (JsonException exception)
        {
            return new SavedCompositionRuleLoadResult(
                null,
                [Issue("saved-rule.json.invalid", $"Saved rule JSON is invalid: {exception.Message}", "$")]);
        }
        catch (IOException exception)
        {
            return new SavedCompositionRuleLoadResult(
                null,
                [Issue("saved-rule.file-read-failed", $"Saved rule JSON could not be read: {exception.Message}", "$")]);
        }
        catch (UnauthorizedAccessException exception)
        {
            return new SavedCompositionRuleLoadResult(
                null,
                [Issue("saved-rule.file-read-failed", $"Saved rule JSON could not be read: {exception.Message}", "$")]);
        }
    }

    private static SavedCompositionRuleLoadResult Parse(JsonElement root)
    {
        List<SavedRuleValidationIssue> issues = [];
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue("saved-rule.root.invalid", "Saved rule JSON root must be an object.", "$"));
            return new SavedCompositionRuleLoadResult(null, issues);
        }

        ValidateProperties(root, TopLevelProperties, "$", issues);
        string schemaVersion = RequiredString(root, "schemaVersion", "$.schemaVersion", issues);
        if (!string.Equals(schemaVersion, "1.0", StringComparison.Ordinal))
        {
            issues.Add(Issue("saved-rule.schema-version.unsupported", "Saved rule schemaVersion must be '1.0'.", "$.schemaVersion"));
        }

        string ruleId = RequiredId(root, "ruleId", "$.ruleId", issues);
        string ruleVersion = RequiredSemver(root, "ruleVersion", "$.ruleVersion", issues);
        string displayName = RequiredString(root, "displayName", "$.displayName", issues);
        string compositionKind = RequiredEnum(root, "compositionKind", "$.compositionKind", ["merge", "replace"], issues);
        string sourceExperience = RequiredEnum(root, "sourceExperience", "$.sourceExperience", ["general-merge", "general-replace"], issues);
        string supportStatus = RequiredEnum(root, "supportStatus", "$.supportStatus", ["draft", "candidate", "supported", "deprecated"], issues);
        _ = OptionalString(root, "description", "$.description", issues);
        _ = OptionalEnum(root, "protectedRangePolicy", "$.protectedRangePolicy", ["deny-crossing", "deny-touch", "profile-defined"], issues);
        SavedRuleCompatibility compatibility = ReadCompatibility(root, issues);
        HashSet<string> inputSlotTemplateIds = ReadInputSlotTemplateIds(root, issues);
        List<SavedRuleMappingRow> mappingRows = ReadMappingRows(root, compositionKind, sourceExperience, inputSlotTemplateIds, issues);
        List<string> operationFragmentIds = ReadOperationFragments(root, compositionKind, sourceExperience, mappingRows, issues);
        List<string> processorDependencyIds = ReadStringArray(root, "processorDependencyIds", "$.processorDependencyIds", required: false, validateId: true, issues);
        List<string> validationRuleIds = ReadStringArray(root, "validationRuleIds", "$.validationRuleIds", required: true, validateId: true, issues);
        string owner = RequiredString(root, "owner", "$.owner", issues);
        _ = ReadStringArray(root, "reviewers", "$.reviewers", required: false, validateId: false, issues);
        List<string> evidenceRefs = ReadStringArray(root, "evidenceRefs", "$.evidenceRefs", required: true, validateId: false, issues);

        ValidateRuleCompatibility(
            compositionKind,
            sourceExperience,
            supportStatus,
            evidenceRefs,
            mappingRows,
            operationFragmentIds,
            processorDependencyIds,
            issues);

        return issues.Count > 0
            ? new SavedCompositionRuleLoadResult(null, issues)
            : new SavedCompositionRuleLoadResult(
            new SavedCompositionRule(
                schemaVersion,
                ruleId,
                ruleVersion,
                displayName,
                compositionKind,
                sourceExperience,
                supportStatus,
                compatibility,
                mappingRows,
                operationFragmentIds,
                processorDependencyIds,
                validationRuleIds,
                owner,
                evidenceRefs),
            []);
    }

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
            string overlapPolicy = RequiredEnum(row, "overlapPolicy", $"{rowPath}.overlapPolicy", ["reject", "allow-declared", "replace-existing"], issues);
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

                if (!string.IsNullOrWhiteSpace(overlapPolicy) &&
                    !string.Equals(overlapPolicy, "reject", StringComparison.Ordinal))
                {
                    issues.Add(Issue(
                        "saved-rule.mapping-row.overlap-policy-unsupported",
                        "Current General Merge saved-rule CLI consumption supports only reject overlap policy.",
                        $"{rowPath}.overlapPolicy"));
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

    private static List<string> ReadOperationFragments(
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

            foreach (string mappingRowId in ReadStringArray(fragment, "mappingRowIds", $"{path}.mappingRowIds", required: false, validateId: true, issues))
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

        return operationIds;
    }

    private static HashSet<string> ReadInputSlotTemplateIds(JsonElement root, List<SavedRuleValidationIssue> issues)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        if (!root.TryGetProperty("inputSlotTemplates", out JsonElement templates))
        {
            return ids;
        }

        if (templates.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Issue("saved-rule.input-slot-templates.invalid", "inputSlotTemplates must be an array.", "$.inputSlotTemplates"));
            return ids;
        }

        List<string> values = [];
        int index = 0;
        foreach (JsonElement template in templates.EnumerateArray())
        {
            string path = string.Create(CultureInfo.InvariantCulture, $"$.inputSlotTemplates[{index++}]");
            if (template.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue("saved-rule.input-slot-template.invalid", "Input slot template must be an object.", path));
                continue;
            }

            ValidateProperties(template, InputSlotTemplateProperties, path, issues);
            string slotTemplateId = RequiredId(template, "slotTemplateId", $"{path}.slotTemplateId", issues);
            if (!string.IsNullOrWhiteSpace(slotTemplateId))
            {
                values.Add(slotTemplateId);
                _ = ids.Add(slotTemplateId);
            }

            _ = RequiredString(template, "role", $"{path}.role", issues);
            _ = RequiredEnum(template, "cardinality", $"{path}.cardinality", ["one", "many"], issues);
            _ = ReadExtensionArray(template, "acceptedExtensions", $"{path}.acceptedExtensions", issues);
        }

        AddDuplicateIssues(
            values,
            "saved-rule.input-slot-template.duplicate",
            "Input slot template id is duplicated.",
            "$.inputSlotTemplates",
            issues);
        return ids;
    }

    private static void ValidateRuleCompatibility(
        string compositionKind,
        string sourceExperience,
        string supportStatus,
        List<string> evidenceRefs,
        List<SavedRuleMappingRow> mappingRows,
        List<string> operationFragmentIds,
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

        if (operationFragmentIds.Count == 0)
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

        if (supportStatus is "candidate" or "supported" && evidenceRefs.Count == 0)
        {
            issues.Add(Issue("saved-rule.evidence.required", "Candidate or supported saved rules must include evidenceRefs.", "$.evidenceRefs"));
        }
    }

}
