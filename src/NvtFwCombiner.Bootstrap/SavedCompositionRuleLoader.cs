using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        SavedRuleCompatibility compatibility = ReadCompatibility(root, issues);
        List<SavedRuleMappingRow> mappingRows = ReadMappingRows(root, compositionKind, sourceExperience, issues);
        List<string> operationFragmentIds = ReadOperationFragments(root, mappingRows, issues);
        List<string> processorDependencyIds = ReadStringArray(root, "processorDependencyIds", "$.processorDependencyIds", required: false, validateId: true, issues);
        List<string> validationRuleIds = ReadStringArray(root, "validationRuleIds", "$.validationRuleIds", required: true, validateId: true, issues);
        string owner = RequiredString(root, "owner", "$.owner", issues);
        List<string> evidenceRefs = ReadStringArray(root, "evidenceRefs", "$.evidenceRefs", required: true, validateId: false, issues);

        ValidateInputSlotTemplates(root, issues);
        ValidateRuleCompatibility(compositionKind, sourceExperience, supportStatus, evidenceRefs, mappingRows, operationFragmentIds, issues);

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

        return result;
    }

    private static List<string> ReadOperationFragments(
        JsonElement root,
        List<SavedRuleMappingRow> mappingRows,
        List<SavedRuleValidationIssue> issues)
    {
        if (!root.TryGetProperty("operationFragments", out JsonElement fragments) ||
            fragments.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Issue("saved-rule.operation-fragments.required", "Saved rule requires operationFragments array.", "$.operationFragments"));
            return [];
        }

        HashSet<string> mappingRowIds = [.. mappingRows.Select(row => row.RowId)];
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
            _ = RequiredEnum(
                fragment,
                "kind",
                $"{path}.kind",
                ["copy-range", "fill-range", "patch-scalar", "replace-range", "run-external-processor", "assert-range", "validate-checksum"],
                issues);
            _ = RequiredString(fragment, "reason", $"{path}.reason", issues);
            foreach (string mappingRowId in ReadStringArray(fragment, "mappingRowIds", $"{path}.mappingRowIds", required: false, validateId: true, issues))
            {
                if (!mappingRowIds.Contains(mappingRowId))
                {
                    issues.Add(Issue(
                        "saved-rule.operation-fragment.mapping-row-unknown",
                        $"Operation fragment references unknown mapping row '{mappingRowId}'.",
                        $"{path}.mappingRowIds"));
                }
            }

            if (!string.IsNullOrWhiteSpace(operationId))
            {
                operationIds.Add(operationId);
            }
        }

        AddDuplicateIssues(operationIds, "saved-rule.operation-fragment.duplicate", "Operation fragment id is duplicated.", "$.operationFragments", issues);
        return operationIds;
    }

    private static void ValidateInputSlotTemplates(JsonElement root, List<SavedRuleValidationIssue> issues)
    {
        if (!root.TryGetProperty("inputSlotTemplates", out JsonElement templates))
        {
            return;
        }

        if (templates.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Issue("saved-rule.input-slot-templates.invalid", "inputSlotTemplates must be an array.", "$.inputSlotTemplates"));
            return;
        }

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
            _ = RequiredId(template, "slotTemplateId", $"{path}.slotTemplateId", issues);
            _ = RequiredString(template, "role", $"{path}.role", issues);
            _ = RequiredEnum(template, "cardinality", $"{path}.cardinality", ["one", "many"], issues);
        }
    }

    private static void ValidateRuleCompatibility(
        string compositionKind,
        string sourceExperience,
        string supportStatus,
        List<string> evidenceRefs,
        List<SavedRuleMappingRow> mappingRows,
        List<string> operationFragmentIds,
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

        if (supportStatus is "candidate" or "supported" && evidenceRefs.Count == 0)
        {
            issues.Add(Issue("saved-rule.evidence.required", "Candidate or supported saved rules must include evidenceRefs.", "$.evidenceRefs"));
        }
    }

    private static void ValidateProperties(
        JsonElement element,
        HashSet<string> allowed,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        HashSet<string> seen = [];
        foreach (JsonProperty property in element.EnumerateObject())
        {
            string propertyPath = $"{path}.{property.Name}";
            if (!seen.Add(property.Name))
            {
                issues.Add(Issue("saved-rule.property.duplicate", $"Property '{property.Name}' is duplicated.", propertyPath));
            }

            if (!allowed.Contains(property.Name))
            {
                issues.Add(Issue("saved-rule.property.unknown", $"Property '{property.Name}' is not allowed in a saved rule.", propertyPath));
            }
        }
    }

    private static List<string> ReadStringArray(
        JsonElement element,
        string propertyName,
        string path,
        bool required,
        bool validateId,
        List<SavedRuleValidationIssue> issues)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement array))
        {
            if (required)
            {
                issues.Add(Issue("saved-rule.array.required", $"Property '{propertyName}' is required.", path));
            }

            return [];
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Issue("saved-rule.array.invalid", $"Property '{propertyName}' must be an array.", path));
            return [];
        }

        List<string> values = [];
        int index = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            string itemPath = string.Create(CultureInfo.InvariantCulture, $"{path}[{index++}]");
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(Issue("saved-rule.array-item.invalid", $"Property '{propertyName}' entries must be non-empty strings.", itemPath));
                continue;
            }

            string value = item.GetString()!;
            if (validateId && !IdRegex().IsMatch(value))
            {
                issues.Add(Issue("saved-rule.id.invalid", $"Identifier '{value}' does not match the saved-rule id grammar.", itemPath));
            }

            values.Add(value);
        }

        AddDuplicateIssues(values, "saved-rule.array.duplicate", $"Property '{propertyName}' contains duplicate values.", path, issues);
        return values;
    }

    private static ByteRange? RequiredByteRange(
        JsonElement element,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement range))
        {
            issues.Add(Issue("saved-rule.range.required", $"Property '{propertyName}' is required.", path));
            return null;
        }

        return ParseByteRange(range, path, issues);
    }

    private static ByteRange? OptionalByteRange(
        JsonElement element,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        return element.TryGetProperty(propertyName, out JsonElement range)
            ? ParseByteRange(range, path, issues)
            : null;
    }

    private static ByteRange? ParseByteRange(JsonElement range, string path, List<SavedRuleValidationIssue> issues)
    {
        if (range.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue("saved-rule.range.invalid", "Range must be an object.", path));
            return null;
        }

        ValidateProperties(range, ByteRangeProperties, path, issues);
        if (!TryReadNonNegativeLong(range, "start", $"{path}.start", out long start, issues) ||
            !TryReadNonNegativeLong(range, "length", $"{path}.length", out long length, issues))
        {
            return null;
        }

        if (length <= 0)
        {
            issues.Add(Issue("saved-rule.range.length", "Range length must be positive.", $"{path}.length"));
            return null;
        }

        try
        {
            return new ByteRange(start, length);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or OverflowException)
        {
            issues.Add(Issue("saved-rule.range.overflow", "Range exceeds the supported address size.", path));
            return null;
        }
    }

    private static bool TryReadNonNegativeLong(
        JsonElement element,
        string propertyName,
        string path,
        out long value,
        List<SavedRuleValidationIssue> issues)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out JsonElement property) || !property.TryGetInt64(out value))
        {
            issues.Add(Issue("saved-rule.integer.required", $"Property '{propertyName}' must be an integer.", path));
            return false;
        }

        if (value < 0)
        {
            issues.Add(Issue("saved-rule.integer.negative", $"Property '{propertyName}' must be non-negative.", path));
            return false;
        }

        return true;
    }

    private static string RequiredString(
        JsonElement element,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            issues.Add(Issue("saved-rule.string.required", $"Property '{propertyName}' must be a non-empty string.", path));
            return string.Empty;
        }

        return property.GetString()!;
    }

    private static string RequiredEnum(
        JsonElement element,
        string propertyName,
        string path,
        IReadOnlyList<string> allowed,
        List<SavedRuleValidationIssue> issues)
    {
        string value = RequiredString(element, propertyName, path, issues);
        if (!string.IsNullOrWhiteSpace(value) && !allowed.Contains(value, StringComparer.Ordinal))
        {
            issues.Add(Issue(
                "saved-rule.enum.invalid",
                $"Property '{propertyName}' must be one of: {string.Join(", ", allowed)}.",
                path));
        }

        return value;
    }

    private static string RequiredId(
        JsonElement element,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        string value = RequiredString(element, propertyName, path, issues);
        if (!string.IsNullOrWhiteSpace(value) && !IdRegex().IsMatch(value))
        {
            issues.Add(Issue("saved-rule.id.invalid", $"Identifier '{value}' does not match the saved-rule id grammar.", path));
        }

        return value;
    }

    private static string? OptionalId(
        JsonElement element,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            issues.Add(Issue("saved-rule.id.invalid", $"Property '{propertyName}' must be a non-empty id.", path));
            return null;
        }

        string value = property.GetString()!;
        if (!IdRegex().IsMatch(value))
        {
            issues.Add(Issue("saved-rule.id.invalid", $"Identifier '{value}' does not match the saved-rule id grammar.", path));
        }

        return value;
    }

    private static string RequiredSemver(
        JsonElement element,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        string value = RequiredString(element, propertyName, path, issues);
        if (!string.IsNullOrWhiteSpace(value) && !SemverRegex().IsMatch(value))
        {
            issues.Add(Issue("saved-rule.semver.invalid", $"Property '{propertyName}' must be semantic version text.", path));
        }

        return value;
    }

    private static int? OptionalPositiveInt(
        JsonElement element,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        if (!property.TryGetInt32(out int value) || value <= 0)
        {
            issues.Add(Issue("saved-rule.integer.positive", $"Property '{propertyName}' must be a positive integer.", path));
            return null;
        }

        return value;
    }

    private static void AddDuplicateIssues(
        IReadOnlyList<string> values,
        string code,
        string message,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        foreach (string duplicate in values
                     .GroupBy(value => value, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            issues.Add(Issue(code, $"{message} Duplicate: '{duplicate}'.", path));
        }
    }

    private static SavedRuleValidationIssue Issue(string code, string message, string path)
    {
        return new SavedRuleValidationIssue(code, message, path);
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();

    [GeneratedRegex("^NT[0-9A-Za-z-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex IcIdRegex();

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex SemverRegex();
}
