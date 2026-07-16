using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Local UI report history persistence. This does not alter the machine-readable run report JSON.</summary>
public static class ReportHistoryFileStore
{
    private const int SchemaVersion = 1;
    private const string HistoryFileName = "report-history.v1.json";

    /// <summary>Gets the default local report history path for the current user.</summary>
    public static string DefaultHistoryPath => BestEffortLocalJsonFileStore.GetDefaultPath(HistoryFileName);

    /// <summary>Loads persisted history into the view model from the default path.</summary>
    public static void LoadInto(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        viewModel.LoadReportHistory(Load(DefaultHistoryPath));
    }

    /// <summary>Saves the view model history to the default path.</summary>
    public static void Save(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        Save(DefaultHistoryPath, viewModel.ExportReportHistory());
    }

    /// <summary>Loads persisted report history snapshots from a specific path.</summary>
    public static IReadOnlyList<ReportHistorySnapshot> Load(string path)
    {
        return BestEffortLocalJsonFileStore.Load<ReportHistoryFile, IReadOnlyList<ReportHistorySnapshot>>(
            path,
            [],
            file => file?.SchemaVersion == SchemaVersion
                ? [.. file.Entries.Select(entry => entry.ToSnapshot()).OfType<ReportHistorySnapshot>()]
                : []);
    }

    /// <summary>Saves report history snapshots to a specific path.</summary>
    public static void Save(string path, IEnumerable<ReportHistorySnapshot> snapshots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(snapshots);

        BestEffortLocalJsonFileStore.Save(
            path,
            new ReportHistoryFile(
                SchemaVersion,
                [.. snapshots
                    .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.ReportJson))
                    .Select(ReportHistoryFileEntry.FromSnapshot)]));
    }

    private sealed class ReportHistoryFile
    {
        public ReportHistoryFile(int schemaVersion, IReadOnlyList<ReportHistoryFileEntry> entries)
        {
            SchemaVersion = schemaVersion;
            Entries = entries ?? [];
        }

        public int SchemaVersion { get; }

        public IReadOnlyList<ReportHistoryFileEntry> Entries { get; }
    }

    private sealed class ReportHistoryFileEntry
    {
        public ReportHistoryFileEntry(
            string sourceName,
            string reportJson,
            string outputArtifactPath,
            ReportHistoryMetadataSnapshot? metadata = null)
        {
            SourceName = sourceName ?? string.Empty;
            ReportJson = reportJson ?? string.Empty;
            OutputArtifactPath = outputArtifactPath ?? string.Empty;
            Metadata = metadata ?? ReportHistoryMetadataSnapshot.Empty;
        }

        public string SourceName { get; }

        public string ReportJson { get; }

        public string OutputArtifactPath { get; }

        public ReportHistoryMetadataSnapshot Metadata { get; }

        public static ReportHistoryFileEntry FromSnapshot(ReportHistorySnapshot snapshot)
        {
            return new ReportHistoryFileEntry(
                snapshot.SourceName,
                snapshot.ReportJson,
                snapshot.OutputArtifactPath,
                snapshot.Metadata);
        }

        public ReportHistorySnapshot? ToSnapshot()
        {
            return string.IsNullOrWhiteSpace(ReportJson)
                ? null
                : new ReportHistorySnapshot(
                    SourceName ?? string.Empty,
                    ReportJson,
                    OutputArtifactPath ?? string.Empty,
                    Metadata);
        }
    }
}
