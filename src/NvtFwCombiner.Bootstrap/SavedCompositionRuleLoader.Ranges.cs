using System.Text.Json;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedCompositionRuleLoader
{
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
}
