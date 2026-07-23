using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Report-safe provenance for an output name rendered from compiled execution facts.</summary>
public sealed class OutputNamingSummary
{
    /// <summary>Creates naming provenance without paths or presentation-derived values.</summary>
    public OutputNamingSummary(
        string rendererKind,
        string template,
        string automaticFileName,
        string actualFileName,
        bool isExplicitOverride,
        string dateSource,
        DateTimeOffset resolvedAtUtc,
        IReadOnlyList<OutputNamingTokenSummary> tokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rendererKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(automaticFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(dateSource);
        ArgumentNullException.ThrowIfNull(tokens);

        RendererKind = rendererKind;
        Template = template;
        AutomaticFileName = automaticFileName;
        ActualFileName = actualFileName;
        IsExplicitOverride = isExplicitOverride;
        DateSource = dateSource;
        ResolvedAtUtc = resolvedAtUtc;
        Tokens = Array.AsReadOnly([.. tokens]);
    }

    /// <summary>Closed compiled renderer identifier.</summary>
    public string RendererKind { get; }

    /// <summary>Compiled profile template, never a presentation string.</summary>
    public string Template { get; }

    /// <summary>Automatic filename candidate rendered from execution snapshots.</summary>
    public string AutomaticFileName { get; }

    /// <summary>Actual requested or committed filename.</summary>
    public string ActualFileName { get; }

    /// <summary>Whether the actual filename came from an explicit UI/CLI override.</summary>
    public bool IsExplicitOverride { get; }

    /// <summary>Clock source used to render the date token.</summary>
    public string DateSource { get; }

    /// <summary>UTC run instant captured once before the execution snapshots were read.</summary>
    public DateTimeOffset ResolvedAtUtc { get; }

    /// <summary>Stable token values and their immutable parsing provenance.</summary>
    public IReadOnlyList<OutputNamingTokenSummary> Tokens { get; }
}

/// <summary>One report-safe output-name token and its parsing provenance.</summary>
public sealed record OutputNamingTokenSummary(
    string TokenId,
    string Value,
    bool IsKnown,
    string? SourceAddressSpaceId,
    string? AcceptedSnapshotSha256,
    string ParserId);

/// <summary>Read-only automatic output-name result resolved from the same accepted inputs as execution.</summary>
public sealed class CompositionOutputNamePreview
{
    /// <summary>Creates one immutable name preview and its input-admission diagnostics.</summary>
    public CompositionOutputNamePreview(
        string fileName,
        OutputNamingSummary? outputNaming,
        IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(issues);

        FileName = fileName;
        OutputNaming = outputNaming;
        Issues = Array.AsReadOnly([.. issues]);
    }

    /// <summary>Automatic filename when the accepted input snapshots were readable; otherwise the request fallback.</summary>
    public string FileName { get; }

    /// <summary>Report-safe token provenance when the compiled renderer resolved a name.</summary>
    public OutputNamingSummary? OutputNaming { get; }

    /// <summary>Input admission or unknown-metadata diagnostics produced without executing composition.</summary>
    public IReadOnlyList<CompositionIssue> Issues { get; }

    /// <summary>Whether no blocking input-admission diagnostic prevented an automatic name.</summary>
    public bool CanUseAutomaticName => !Issues.Any(static issue =>
        string.Equals(issue.Severity, CompositionIssueSeverity.Error, StringComparison.Ordinal));
}
