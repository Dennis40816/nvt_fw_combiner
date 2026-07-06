using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using NvtFwCombiner.Domain.Composition;

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

    [GeneratedRegex("^\\.[A-Za-z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ExtensionRegex();

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex SemverRegex();
}
