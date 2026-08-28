using System.Text;
using System.Text.Json;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Schema-v1 projection over the bounded platform file adapter.</summary>
internal static class ReportHistoryFileStore
{
    private const int SchemaVersion = 1;
    private const string HistoryFileName = "report-history.v1.json";
    internal const long MaximumHistoryFileBytes = 64L * 1024 * 1024;

    internal static string DefaultHistoryPath => LocalJsonDocument.GetDefaultPath(HistoryFileName);

    internal static async Task<IReadOnlyList<ReportHistorySnapshot>> LoadAsync(
        ILocalFileStore files,
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        try
        {
            ReportHistoryFile? file = await files.ReadAsync(
                    path,
                    MaximumHistoryFileBytes,
                    LocalJsonDocument.DeserializeAsync<ReportHistoryFile>,
                    cancellationToken)
                .ConfigureAwait(false);
            return file?.SchemaVersion == SchemaVersion
                ? [.. (file.Entries ?? []).Select(ToSnapshot).OfType<ReportHistorySnapshot>()]
                : [];
        }
        catch (Exception exception) when (exception is LocalFileReadException or JsonException or NotSupportedException)
        {
            return [];
        }
    }

    internal static async Task SaveAsync(
        ILocalFileStore files,
        string path,
        IEnumerable<ReportHistorySnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(snapshots);
        List<ReportHistorySnapshot> retained = RetainPayloadBudget(snapshots);
        byte[] encoded = Serialize(retained);
        if (encoded.LongLength > MaximumHistoryFileBytes)
        {
            retained = [.. retained.Select(ReportPresentationViewModel.OmitDerivableReportHistoryMetadata)];
            encoded = Serialize(retained);
        }

        while (encoded.LongLength > MaximumHistoryFileBytes && retained.Count > 1)
        {
            retained.RemoveAt(retained.Count - 1);
            encoded = Serialize(retained);
        }

        if (encoded.LongLength > MaximumHistoryFileBytes)
        {
            throw new ReportHistoryPersistenceException(
                ReportHistoryPersistenceFailure.EntryTooLargeToPersist,
                "The newest report cannot fit the 64 MiB report-history envelope.");
        }

        await files.WriteAsync(path, encoded, cancellationToken).ConfigureAwait(false);
    }

    private static List<ReportHistorySnapshot> RetainPayloadBudget(IEnumerable<ReportHistorySnapshot> snapshots)
    {
        var retained = new List<ReportHistorySnapshot>(ReportPresentationViewModel.MaxReportHistoryEntries);
        long bytes = 0;
        long budget = ReportPresentationViewModel.MaximumReportHistoryStorageBytes;
        foreach (ReportHistorySnapshot snapshot in snapshots
                     .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.ReportJson))
                     .Take(ReportPresentationViewModel.MaxReportHistoryEntries))
        {
            long candidate = Encoding.UTF8.GetByteCount(snapshot.ReportJson) +
                Encoding.UTF8.GetByteCount(snapshot.OutputArtifactPath);
            if (retained.Count > 0 && (candidate > budget || bytes > budget - candidate))
            {
                continue;
            }

            retained.Add(snapshot);
            bytes += candidate;
        }

        return retained;
    }

    private static byte[] Serialize(IReadOnlyList<ReportHistorySnapshot> snapshots)
    {
        return JsonSerializer.SerializeToUtf8Bytes(
            new ReportHistoryFile(
                SchemaVersion,
                [.. snapshots.Select(snapshot => new ReportHistoryFileEntry(
                    snapshot.SourceName,
                    snapshot.ReportJson,
                    snapshot.OutputArtifactPath,
                    snapshot.Metadata))]),
            LocalJsonDocument.Options);
    }

    private static ReportHistorySnapshot? ToSnapshot(ReportHistoryFileEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.ReportJson)
            ? null
            : new(
                entry.SourceName ?? string.Empty,
                entry.ReportJson,
                entry.OutputArtifactPath ?? string.Empty,
                entry.Metadata ?? ReportHistoryMetadataSnapshot.Empty);
    }

    private sealed record ReportHistoryFile(
        int SchemaVersion,
        IReadOnlyList<ReportHistoryFileEntry>? Entries);

    private sealed record ReportHistoryFileEntry(
        string? SourceName,
        string? ReportJson,
        string? OutputArtifactPath,
        ReportHistoryMetadataSnapshot? Metadata = null);
}

internal enum ReportHistoryPersistenceFailure
{
    EntryTooLargeToPersist,
}

internal sealed class ReportHistoryPersistenceException(
    ReportHistoryPersistenceFailure failure,
    string message) : IOException(message)
{
    internal ReportHistoryPersistenceFailure Failure { get; } = failure;
}
