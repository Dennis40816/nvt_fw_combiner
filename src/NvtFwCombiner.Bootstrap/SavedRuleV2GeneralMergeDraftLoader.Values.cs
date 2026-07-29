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

    private static string ReadSha256(
        JsonElement parent,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        string value = ReadRequiredString(
            parent,
            propertyName,
            path,
            issues);
        if (value.Length != 64 ||
            value.Any(static character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.StringRequired,
                $"Property '{propertyName}' must be a lowercase SHA-256 value.",
                path));
            return string.Empty;
        }

        return value;
    }

    private static HashSet<string> ReadRequiredStringArray(
        JsonElement parent,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        HashSet<string> values = new(StringComparer.Ordinal);
        if (!parent.TryGetProperty(propertyName, out JsonElement array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.ArrayRequired,
                $"Property '{propertyName}' must be an array.",
                path));
            return values;
        }

        int index = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            string itemPath = $"{path}[{index++}]";
            if (item.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.ArrayItemInvalid,
                    $"Property '{propertyName}' must contain non-empty strings.",
                    itemPath));
            }
            else if (!values.Add(item.GetString()!))
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.ArrayDuplicate,
                    $"Property '{propertyName}' contains duplicate values.",
                    itemPath));
            }
        }

        if (values.Count == 0)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.ArrayItemInvalid,
                $"Property '{propertyName}' must not be empty.",
                path));
        }

        return values;
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
