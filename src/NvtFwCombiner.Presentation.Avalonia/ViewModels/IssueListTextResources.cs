// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Only component-owned text is localized; supplied content is preserved verbatim.</summary>
internal sealed class IssueListTextResources(ShellLanguage language)
{
    internal string Title => Pick("Issues", "問題");
    internal string Selected => Pick("Selected", "已選取");
    internal string NotSelected => Pick("Not selected", "未選取");

    internal string Severity(IssueListSeverity severity)
    {
        return severity switch
        {
            IssueListSeverity.Error => Pick("Error", "錯誤"),
            IssueListSeverity.Warning => Pick("Warning", "警告"),
            _ => throw new ArgumentOutOfRangeException(nameof(severity)),
        };
    }

    internal string Summary(IssueListSeverity severity, long count)
    {
        string label = severity switch
        {
            IssueListSeverity.Error => Pick("Errors", "錯誤"),
            IssueListSeverity.Warning => Pick("Warnings", "警告"),
            _ => throw new ArgumentOutOfRangeException(nameof(severity)),
        };
        return $"{label}: {count.ToString(CultureInfo.InvariantCulture)}";
    }

    internal string IssueName(IssueListItem issue, bool selected)
    {
        string[] multiplicity = issue.OccurrenceCount > 1 ? [Multiplicity(issue.OccurrenceCount)] : [];
        IReadOnlyList<string> details = selected ? issue.DetailLines : [];
        string[] lines = [Severity(issue.Severity), issue.Sentence, .. multiplicity, .. issue.LocationChips, .. details];
        return string.Join(" · ", lines);
    }

    internal static string Multiplicity(int count)
    {
        return $"×{count.ToString(CultureInfo.InvariantCulture)}";
    }

    private string Pick(string english, string chinese)
    {
        return language == ShellLanguage.ChineseTraditional ? chinese : english;
    }
}
