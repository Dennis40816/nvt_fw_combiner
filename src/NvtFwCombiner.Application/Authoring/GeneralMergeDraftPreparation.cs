// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Initializer resolution retaining the canonical parser's typed refusal.</summary>
public sealed record GeneralMergeInitializerResolution(
    GeneralMergeInitializer? Initializer,
    CompositionIssue? Issue)
{
    /// <summary>True only when the canonical initializer was resolved without a refusal.</summary>
    public bool Succeeded => Initializer is not null && Issue is null;
}

/// <summary>Immutable text-to-draft preparation; it does not grant execution admission.</summary>
public sealed class GeneralMergeDraftPreparation
{
    internal GeneralMergeDraftPreparation(
        GeneralMergeInitializerResolution initializer,
        GeneralMergeDraftState? draft,
        IEnumerable<CompositionIssue> draftIssues)
    {
        Initializer = initializer;
        Draft = draft;
        DraftIssues = Array.AsReadOnly([.. draftIssues]);
        Issues = Array.AsReadOnly<CompositionIssue>(initializer.Issue is { } issue
            ? [issue, .. DraftIssues]
            : [.. DraftIssues]);
    }

    /// <summary>The initializer or its original typed refusal.</summary>
    public GeneralMergeInitializerResolution Initializer { get; }

    /// <summary>The complete typed draft, absent when either parser refused the input.</summary>
    public GeneralMergeDraftState? Draft { get; }

    /// <summary>All issues returned by the canonical mapping draft owner, in its order.</summary>
    public IReadOnlyList<CompositionIssue> DraftIssues { get; }

    /// <summary>Initializer refusal followed by mapping draft issues, without rewriting severity.</summary>
    public IReadOnlyList<CompositionIssue> Issues { get; }

    /// <summary>True only when the same initializer and mapping parsers both accepted.</summary>
    public bool Succeeded => Draft is not null && Issues.Count == 0;
}
