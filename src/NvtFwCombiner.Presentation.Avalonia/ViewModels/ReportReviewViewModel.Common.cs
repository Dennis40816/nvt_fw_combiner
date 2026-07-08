using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static int CountBlockingIssues(IReadOnlyList<ReportLineViewModel> issues)
    {
        return issues.Count(issue => !IsWarning(issue));
    }

    private static int CountWarnings(IReadOnlyList<ReportLineViewModel> issues)
    {
        return issues.Count(IsWarning);
    }

    private static string T(ShellLanguage language, string english, string chineseTraditional)
    {
        return language == ShellLanguage.ChineseTraditional ? chineseTraditional : english;
    }

    private static bool IsWarning(ReportLineViewModel issue)
    {
        return string.Equals(issue.Severity, "warning", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Severity, "info", StringComparison.OrdinalIgnoreCase) ||
            (string.IsNullOrWhiteSpace(issue.Severity) &&
                string.Equals(issue.Title, WorkbenchCompositionIssueCodes.InputAddressSpaceTruncated, StringComparison.Ordinal));
    }

    private static string Shorten(string text, int keep)
    {
        return text.Length <= keep ? text : $"{text[..keep]}...";
    }
}
