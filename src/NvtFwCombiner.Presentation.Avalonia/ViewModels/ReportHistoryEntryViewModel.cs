using System.Text;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One compact session-local report snapshot that materializes a full review only when opened.</summary>
internal sealed class ReportHistoryEntryViewModel
{
    private readonly ReportHistorySnapshot snapshot;

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

    public int Sequence { get; }

    public string SequenceLabel { get; }

    public string Title { get; }

    public string Status { get; }

    public string Context { get; }

    public string Output { get; }

    public string OutputHash { get; }

    /// <summary>External processor command summary.</summary>
    public string CommandSummary { get; }

    public string IssueSummary { get; }

    /// <summary>Counts of report evidence sections.</summary>
    public string EvidenceSummary { get; }

    public string SourceName => snapshot.SourceName;

    public string ReportJson => snapshot.ReportJson;

    public string ArtifactPath => snapshot.OutputArtifactPath;

    /// <summary>UTF-8 bytes retained by this history entry's persisted string payloads.</summary>
    public long StoredByteCount { get; }

    /// <summary>Exports this entry as a persistable local history snapshot.</summary>
    public ReportHistorySnapshot ToSnapshot()
    {
        return snapshot;
    }
}
