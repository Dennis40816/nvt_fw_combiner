namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Serializable report history data that can restore a report without re-running workflows.</summary>
public sealed class ReportHistorySnapshot(
    string sourceName,
    string reportJson,
    string outputArtifactPath,
    ReportHistoryMetadataSnapshot? metadata = null)
{
    /// <summary>File name or parser source label.</summary>
    public string SourceName { get; } = sourceName ?? throw new ArgumentNullException(nameof(sourceName));

    /// <summary>Original machine-readable run report JSON.</summary>
    public string ReportJson { get; } = reportJson ?? throw new ArgumentNullException(nameof(reportJson));

    /// <summary>UI-local output artifact path metadata, stored outside the report JSON contract.</summary>
    public string OutputArtifactPath { get; } = outputArtifactPath ??
        throw new ArgumentNullException(nameof(outputArtifactPath));

    /// <summary>Persisted audit summary derived from the report JSON for fast history review.</summary>
    public ReportHistoryMetadataSnapshot Metadata { get; } = metadata ?? ReportHistoryMetadataSnapshot.Empty;
}

/// <summary>UI-local report history summary. The original report JSON remains the contract source of truth.</summary>
public sealed record ReportHistoryMetadataSnapshot(
    string Title,
    string Status,
    string Context,
    string Output,
    string OutputHash,
    string CommandSummary,
    string IssueSummary,
    string EvidenceSummary,
    string RunId,
    string StartedAtUtc,
    string IcId,
    string ModeId,
    string ExperienceId,
    string CompositionKind)
{
    /// <summary>Gets an empty metadata snapshot for older local history files.</summary>
    public static ReportHistoryMetadataSnapshot Empty { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty);
}
