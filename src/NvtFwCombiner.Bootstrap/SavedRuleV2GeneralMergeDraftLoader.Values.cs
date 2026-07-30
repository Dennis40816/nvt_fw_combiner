using System.Text.Json;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedRuleV2GeneralMergeDraftLoader
{
    private static bool TryReadRange(
        JsonElement parent,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues,
        out ByteRange range)
    {
        range = default;
        if (!parent.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Object ||
            !TryReadNonNegativeLong(value, "start", $"{path}.start", issues, out long start) ||
            !TryReadPositiveLong(value, "length", $"{path}.length", issues, out long length))
        {
            return false;
        }

        try
        {
            range = new ByteRange(start, length);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.RangeOverflow,
                "Saved Rule v2 byte range overflows.",
                path));
            return false;
        }
    }

    private static bool TryReadNonNegativeLong(
        JsonElement parent,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues,
        out long value)
    {
        value = default;
        if (!parent.TryGetProperty(propertyName, out JsonElement element) ||
            !element.TryGetInt64(out value) ||
            value < 0)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.IntegerNegative,
                $"Property '{propertyName}' must be a non-negative integer.",
                path));
            return false;
        }

        return true;
    }

    private static bool TryReadPositiveLong(
        JsonElement parent,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues,
        out long value)
    {
        value = default;
        if (!parent.TryGetProperty(propertyName, out JsonElement element) ||
            !element.TryGetInt64(out value) ||
            value <= 0)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.IntegerPositive,
                $"Property '{propertyName}' must be a positive integer.",
                path));
            return false;
        }

        return true;
    }

    private static string ReadRequiredString(
        JsonElement parent,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(element.GetString()))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.StringRequired,
                $"Property '{propertyName}' must be a non-empty string.",
                path));
            return string.Empty;
        }

        return element.GetString()!;
    }

    private static SavedRuleV2GeneralMergeDraftLoadResult Failed(
        SavedRuleValidationIssue issue)
    {
        return new SavedRuleV2GeneralMergeDraftLoadResult(null, null, [issue]);
    }

    private static SavedRuleValidationIssue Issue(
        string code,
        string message,
        string path)
    {
        return new SavedRuleValidationIssue(code, message, path);
    }
}
