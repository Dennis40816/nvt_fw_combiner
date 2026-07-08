using System.Globalization;
using System.Text.Json;

namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedCompositionRuleLoader
{
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

    private static List<string> ReadExtensionArray(
        JsonElement element,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement array))
        {
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
            if (item.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(item.GetString()) ||
                !ExtensionRegex().IsMatch(item.GetString()!))
            {
                issues.Add(Issue(
                    "saved-rule.extension.invalid",
                    $"Property '{propertyName}' entries must be file extensions like .bin.",
                    itemPath));
                continue;
            }

            values.Add(item.GetString()!);
        }

        AddDuplicateIssues(values, "saved-rule.array.duplicate", $"Property '{propertyName}' contains duplicate values.", path, issues);
        return values;
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

    private static string? OptionalString(
        JsonElement element,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        return element.TryGetProperty(propertyName, out _)
            ? RequiredString(element, propertyName, path, issues)
            : null;
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

    private static string? OptionalEnum(
        JsonElement element,
        string propertyName,
        string path,
        IReadOnlyList<string> allowed,
        List<SavedRuleValidationIssue> issues)
    {
        return element.TryGetProperty(propertyName, out _)
            ? RequiredEnum(element, propertyName, path, allowed, issues)
            : null;
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
}
