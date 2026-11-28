// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.ObjectModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>A host-supplied display snapshot. It neither classifies nor merges issues.</summary>
internal sealed class IssueListModel
{
    public IssueListModel(IEnumerable<IssueListItem> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        IssueListItem[] snapshot = [.. issues];
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (IssueListItem issue in snapshot)
        {
            ArgumentNullException.ThrowIfNull(issue);
            if (!keys.Add(issue.Key))
            {
                throw new ArgumentException("Issue keys must be unique.", nameof(issues));
            }
        }
        Issues = Array.AsReadOnly(snapshot);
    }

    public IReadOnlyList<IssueListItem> Issues { get; }

    public long ErrorCount => Count(IssueListSeverity.Error);

    public long WarningCount => Count(IssueListSeverity.Warning);

    private long Count(IssueListSeverity severity)
    {
        return Issues.Where(issue => issue.Severity == severity).Sum(issue => (long)issue.OccurrenceCount);
    }
}

/// <summary>Already worded by the host; chips and detail lines are opaque text.</summary>
internal sealed class IssueListItem
{
    public IssueListItem(string key, IssueListSeverity severity, string sentence,
        IEnumerable<string> locationChips, int occurrenceCount, IEnumerable<string> detailLines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(sentence);
        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity));
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(occurrenceCount, 1);
        Key = key;
        Severity = severity;
        Sentence = sentence;
        LocationChips = CopyLines(locationChips);
        OccurrenceCount = occurrenceCount;
        DetailLines = CopyLines(detailLines);
    }

    public string Key { get; }
    public IssueListSeverity Severity { get; }
    public string Sentence { get; }
    public IReadOnlyList<string> LocationChips { get; }
    public int OccurrenceCount { get; }
    public IReadOnlyList<string> DetailLines { get; }

    private static ReadOnlyCollection<string> CopyLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        string[] snapshot = [.. lines];
        foreach (string line in snapshot)
        {
            ArgumentNullException.ThrowIfNull(line);
        }
        return Array.AsReadOnly(snapshot);
    }
}

internal enum IssueListSeverity
{
    Error,
    Warning,
}
