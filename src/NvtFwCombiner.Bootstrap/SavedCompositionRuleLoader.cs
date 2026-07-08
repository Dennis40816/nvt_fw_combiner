using System.Text.Json;

namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedCompositionRuleLoader
{
    public static SavedCompositionRuleLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return new SavedCompositionRuleLoadResult(
                null,
                [Issue(SavedRuleIssueCodes.FileNotFound, $"Saved rule JSON was not found: {fullPath}", "$")]);
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
                [Issue(SavedRuleIssueCodes.JsonInvalid, $"Saved rule JSON is invalid: {exception.Message}", "$")]);
        }
        catch (IOException exception)
        {
            return new SavedCompositionRuleLoadResult(
                null,
                [Issue(SavedRuleIssueCodes.FileReadFailed, $"Saved rule JSON could not be read: {exception.Message}", "$")]);
        }
        catch (UnauthorizedAccessException exception)
        {
            return new SavedCompositionRuleLoadResult(
                null,
                [Issue(SavedRuleIssueCodes.FileReadFailed, $"Saved rule JSON could not be read: {exception.Message}", "$")]);
        }
    }

    private static SavedCompositionRuleLoadResult Parse(JsonElement root)
    {
        List<SavedRuleValidationIssue> issues = [];
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(SavedRuleIssueCodes.RootInvalid, "Saved rule JSON root must be an object.", "$"));
            return new SavedCompositionRuleLoadResult(null, issues);
        }

        ValidateProperties(root, TopLevelProperties, "$", issues);
        string schemaVersion = RequiredString(root, "schemaVersion", "$.schemaVersion", issues);
        if (!string.Equals(schemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            issues.Add(Issue(SavedRuleIssueCodes.SchemaVersionUnsupported, "Saved rule schemaVersion must be '1.0'.", "$.schemaVersion"));
        }

        string ruleId = RequiredId(root, "ruleId", "$.ruleId", issues);
        string ruleVersion = RequiredSemver(root, "ruleVersion", "$.ruleVersion", issues);
        string displayName = RequiredString(root, "displayName", "$.displayName", issues);
        string compositionKind = RequiredEnum(root, "compositionKind", "$.compositionKind", CompositionKindValues, issues);
        string sourceExperience = RequiredEnum(root, "sourceExperience", "$.sourceExperience", SourceExperienceValues, issues);
        string supportStatus = RequiredEnum(root, "supportStatus", "$.supportStatus", SupportStatusValues, issues);
        _ = OptionalString(root, "description", "$.description", issues);
        _ = OptionalEnum(root, "protectedRangePolicy", "$.protectedRangePolicy", ProtectedRangePolicyValues, issues);
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

}
