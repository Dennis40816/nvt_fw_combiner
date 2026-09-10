using System.Text;
using System.Globalization;
using System.Text.Json;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One compact session-local report snapshot that materializes a full review only when opened.</summary>
internal sealed class ReportHistoryEntryViewModel
{
    private readonly ReportHistorySnapshot snapshot;
    private readonly int? _issueCount;
    private readonly bool? _blocking;
    private readonly bool? _warnings;

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
        (_issueCount, _blocking, _warnings) = (snapshot.Metadata.IssueCount, snapshot.Metadata.HasBlockingIssues, snapshot.Metadata.HasWarnings);
        if (_issueCount is null)
        {
            try
            {
                using var document = JsonDocument.Parse(snapshot.ReportJson);
                (_issueCount, _blocking, _warnings) = ReportReviewViewModel.ReadHistoryIssueFacts(document.RootElement, ShellLanguage.English, CancellationToken.None);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
            {
                // Legacy metadata remains usable even when the raw report shape cannot supply new columns.
            }
        }
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

    public DateTimeOffset? StartedAt => DateTimeOffset.TryParse(snapshot.Metadata.StartedAtUtc, CultureInfo.InvariantCulture,
        DateTimeStyles.None, out DateTimeOffset date) ? date : null;

    public string RunDate => StartedAt?.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture) ?? "—";

    public string Ic => string.IsNullOrWhiteSpace(snapshot.Metadata.IcId) ? "—" : snapshot.Metadata.IcId;

    public string RunType => ShellTextResources.SupportMatrixWorkflowValue(
        !string.IsNullOrWhiteSpace(snapshot.Metadata.ExperienceId) ? snapshot.Metadata.ExperienceId :
        !string.IsNullOrWhiteSpace(snapshot.Metadata.ModeId) ? snapshot.Metadata.ModeId :
        !string.IsNullOrWhiteSpace(snapshot.Metadata.CompositionKind) ? snapshot.Metadata.CompositionKind : "—");

    public string Issues => _issueCount?.ToString(CultureInfo.InvariantCulture) ?? "—";

    public bool IsSuccess => _blocking == false && _warnings == false;
    public bool IsWarning => _blocking == false && _warnings == true;
    public bool IsError => _blocking == true;

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
