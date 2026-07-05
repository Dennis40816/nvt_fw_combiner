namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Serializable report history data that can restore a report without re-running workflows.</summary>
public sealed class ReportHistorySnapshot
{
    /// <summary>Creates a report history snapshot.</summary>
    public ReportHistorySnapshot(string sourceName, string reportJson, string outputArtifactPath)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(reportJson);
        ArgumentNullException.ThrowIfNull(outputArtifactPath);

        SourceName = sourceName;
        ReportJson = reportJson;
        OutputArtifactPath = outputArtifactPath;
    }

    /// <summary>File name or parser source label.</summary>
    public string SourceName { get; }

    /// <summary>Original machine-readable run report JSON.</summary>
    public string ReportJson { get; }

    /// <summary>UI-local output artifact path metadata, stored outside the report JSON contract.</summary>
    public string OutputArtifactPath { get; }
}
