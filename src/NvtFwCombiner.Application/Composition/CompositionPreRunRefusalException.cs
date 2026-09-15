using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>A typed refusal raised before composition execution or report creation begins.</summary>
public sealed class CompositionPreRunRefusalException : InvalidOperationException
{
    /// <summary>Creates a refusal from one or more immutable issue snapshots.</summary>
    public CompositionPreRunRefusalException(IEnumerable<CompositionIssue> issues)
        : this(CopyIssues(issues))
    {
    }

    private CompositionPreRunRefusalException(ReadOnlyCollection<CompositionIssue> issues)
        : base(string.Join(" | ", issues.Select(static issue => $"{issue.Code}: {issue.Message}")))
    {
        Issues = issues;
    }

    /// <summary>Gets the issues that prevented execution from starting.</summary>
    public IReadOnlyList<CompositionIssue> Issues { get; }

    private static ReadOnlyCollection<CompositionIssue> CopyIssues(IEnumerable<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        CompositionIssue[] copy = [.. issues];
        return copy.Length == 0
            ? throw new ArgumentException("At least one pre-run issue is required.", nameof(issues))
            : Array.AsReadOnly(copy);
    }
}
