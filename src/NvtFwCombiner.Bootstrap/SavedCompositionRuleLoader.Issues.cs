namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedCompositionRuleLoader
{
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
}
