using System.Globalization;
using System.Text.Json;

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
        List<SavedRuleOperationFragment> operationFragments =
            ReadOperationFragments(root, compositionKind, sourceExperience, mappingRows, issues);
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
            operationFragments,
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
                operationFragments,
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
