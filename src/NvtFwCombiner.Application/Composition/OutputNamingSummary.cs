using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Report-safe provenance for an output name rendered from compiled execution facts.</summary>
public sealed class OutputNamingSummary(
    string rendererKind,
    string template,
    string automaticFileName,
    string actualFileName,
    bool isExplicitOverride,
    string dateSource,
    DateTimeOffset resolvedAtUtc,
    IReadOnlyList<OutputNamingTokenSummary> tokens,
    OutputNamingAdmissionSummary? admission = null)
{
    /// <summary>Closed compiled renderer identifier.</summary>
    public string RendererKind { get; } = CompositionSummaryValue.NotBlank(
        rendererKind,
        nameof(rendererKind));

    /// <summary>Compiled profile template, never a presentation string.</summary>
    public string Template { get; } = CompositionSummaryValue.NotBlank(template, nameof(template));

    /// <summary>Automatic filename candidate rendered from execution snapshots.</summary>
    public string AutomaticFileName { get; } = CompositionSummaryValue.NotBlank(
        automaticFileName,
        nameof(automaticFileName));

    /// <summary>Actual requested or committed filename.</summary>
    public string ActualFileName { get; } = CompositionSummaryValue.NotBlank(
        actualFileName,
        nameof(actualFileName));

    /// <summary>Whether the actual filename came from an explicit UI/CLI override.</summary>
    public bool IsExplicitOverride { get; } = isExplicitOverride;

    /// <summary>Clock source used to render the date token.</summary>
    public string DateSource { get; } = CompositionSummaryValue.NotBlank(dateSource, nameof(dateSource));

    /// <summary>UTC run instant captured once before the execution snapshots were read.</summary>
    public DateTimeOffset ResolvedAtUtc { get; } = resolvedAtUtc;

    /// <summary>Stable token values and their immutable parsing provenance.</summary>
    public IReadOnlyList<OutputNamingTokenSummary> Tokens { get; } = CompositionSummaryValue.ReadOnlySnapshot(
        tokens,
        nameof(tokens));

    /// <summary>Exact publication and revision used by normal naming.</summary>
    public OutputNamingAdmissionSummary? Admission { get; } = admission;
}

/// <summary>Report-safe identity of one admitted output-naming publication.</summary>
public sealed record OutputNamingAdmissionSummary
{
    /// <summary>Creates one checked report-safe publication identity.</summary>
    public OutputNamingAdmissionSummary(
        string routeId,
        string compilationFingerprint,
        string resolutionToken,
        long authoringRevision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(compilationFingerprint);
        if (!CapabilityRouteIdentity.IsSha256(compilationFingerprint))
        {
            throw new ArgumentException(
                "Output naming admission summary requires a lowercase SHA-256 compilation fingerprint.",
                nameof(compilationFingerprint));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(resolutionToken);
        ArgumentOutOfRangeException.ThrowIfNegative(authoringRevision);
        RouteId = routeId;
        CompilationFingerprint = compilationFingerprint;
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
    }

    /// <summary>Stable exact capability route.</summary>
    public string RouteId { get; }

    /// <summary>Exact compiled-composition fingerprint admitted for naming.</summary>
    public string CompilationFingerprint { get; }

    /// <summary>Exact publication token value.</summary>
    public string ResolutionToken { get; }

    /// <summary>Authoring revision current at admission.</summary>
    public long AuthoringRevision { get; }
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
public sealed class CompositionOutputNamePreview(
    string fileName,
    OutputNamingSummary? outputNaming,
    IReadOnlyList<CompositionIssue> issues)
{
    /// <summary>Automatic filename when the accepted input snapshots were readable; otherwise the request fallback.</summary>
    public string FileName { get; } = CompositionSummaryValue.NotBlank(fileName, nameof(fileName));

    /// <summary>Report-safe token provenance when the compiled renderer resolved a name.</summary>
    public OutputNamingSummary? OutputNaming { get; } = outputNaming;

    /// <summary>Input admission or unknown-metadata diagnostics produced without executing composition.</summary>
    public IReadOnlyList<CompositionIssue> Issues { get; } = CompositionSummaryValue.ReadOnlySnapshot(
        issues,
        nameof(issues));

    /// <summary>Whether no blocking input-admission diagnostic prevented an automatic name.</summary>
    public bool CanUseAutomaticName => !Issues.Any(static issue =>
        string.Equals(issue.Severity, CompositionIssueSeverity.Error, StringComparison.Ordinal));
}
