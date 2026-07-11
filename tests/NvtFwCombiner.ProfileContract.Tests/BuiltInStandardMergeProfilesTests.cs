using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests built-in standard merge profile evidence.</summary>
public sealed partial class BuiltInStandardMergeProfilesTests
{
    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }
}
