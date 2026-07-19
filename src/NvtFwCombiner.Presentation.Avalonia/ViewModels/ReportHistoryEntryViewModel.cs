using System.Text;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One compact session-local report snapshot that materializes a full review only when opened.</summary>
public sealed class ReportHistoryEntryViewModel
{
    private readonly ReportHistorySnapshot snapshot;

    /// <summary>Creates a report history entry.</summary>
    public ReportHistoryEntryViewModel(int sequence, ReportHistorySnapshot snapshot)
        : this(sequence, snapshot, reportJsonUtf8ByteCount: null)
    {
    }

    internal ReportHistoryEntryViewModel(
        int sequence,
        ReportHistorySnapshot snapshot,
        long? reportJsonUtf8ByteCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfNegative(reportJsonUtf8ByteCount ?? 0);
        if (snapshot.Metadata == ReportHistoryMetadataSnapshot.Empty)
        {
            throw new ArgumentException("Report history metadata must be materialized before creating an entry.", nameof(snapshot));
        }

        this.snapshot = snapshot;
        Sequence = sequence;
        SequenceLabel = $"#{sequence}";
        Title = snapshot.Metadata.Title;
        Status = snapshot.Metadata.Status;
        Context = snapshot.Metadata.Context;
        Output = snapshot.Metadata.Output;
        OutputHash = snapshot.Metadata.OutputHash;
        CommandSummary = snapshot.Metadata.CommandSummary;
        IssueSummary = snapshot.Metadata.IssueSummary;
        EvidenceSummary = snapshot.Metadata.EvidenceSummary;
        StoredByteCount = (reportJsonUtf8ByteCount ?? Encoding.UTF8.GetByteCount(snapshot.ReportJson)) +
            Encoding.UTF8.GetByteCount(snapshot.OutputArtifactPath);
    }

    /// <summary>Monotonic session sequence.</summary>
    public int Sequence { get; }

    /// <summary>Compact sequence label.</summary>
    public string SequenceLabel { get; }

    /// <summary>Report title.</summary>
    public string Title { get; }

    /// <summary>Run status.</summary>
    public string Status { get; }

    /// <summary>Composition context label.</summary>
    public string Context { get; }

    /// <summary>Output artifact summary.</summary>
    public string Output { get; }

    /// <summary>Short output hash label.</summary>
    public string OutputHash { get; }

    /// <summary>External processor command summary.</summary>
    public string CommandSummary { get; }

    /// <summary>Issue summary.</summary>
    public string IssueSummary { get; }

    /// <summary>Counts of report evidence sections.</summary>
    public string EvidenceSummary { get; }

    /// <summary>File name or parser source label.</summary>
    public string SourceName => snapshot.SourceName;

    /// <summary>Original report JSON for Save report.</summary>
    public string ReportJson => snapshot.ReportJson;

    /// <summary>Session-local artifact path.</summary>
    public string ArtifactPath => snapshot.OutputArtifactPath;

    /// <summary>UTF-8 bytes retained by this history entry's persisted string payloads.</summary>
    public long StoredByteCount { get; }

    /// <summary>Exports this entry as a persistable local history snapshot.</summary>
    public ReportHistorySnapshot ToSnapshot()
    {
        return snapshot;
    }
}
