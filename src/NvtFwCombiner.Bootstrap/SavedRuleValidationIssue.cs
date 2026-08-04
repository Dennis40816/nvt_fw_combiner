namespace NvtFwCombiner.Bootstrap;

internal sealed record SavedRuleValidationIssue(
    string Code,
    string Message,
    string Path);
