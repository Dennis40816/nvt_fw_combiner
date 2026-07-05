namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Serializable report history data that can restore a report without re-running workflows.</summary>
public sealed class ReportHistorySnapshot
{
    /// <summary>Creates a report history snapshot.</summary>
    public ReportHistorySnapshot(
        string sourceName,
        string reportJson,
        string outputArtifactPath,
        ReportHistoryMetadataSnapshot? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(reportJson);
        ArgumentNullException.ThrowIfNull(outputArtifactPath);

        SourceName = sourceName;
        ReportJson = reportJson;
        OutputArtifactPath = outputArtifactPath;
        Metadata = metadata ?? ReportHistoryMetadataSnapshot.Empty;
    }

    /// <summary>File name or parser source label.</summary>
    public string SourceName { get; }

    /// <summary>Original machine-readable run report JSON.</summary>
    public string ReportJson { get; }

    /// <summary>UI-local output artifact path metadata, stored outside the report JSON contract.</summary>
    public string OutputArtifactPath { get; }

    /// <summary>Persisted audit summary derived from the report JSON for fast history review.</summary>
    public ReportHistoryMetadataSnapshot Metadata { get; }
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
