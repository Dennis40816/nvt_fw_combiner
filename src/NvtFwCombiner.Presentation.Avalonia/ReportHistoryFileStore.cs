using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Local UI report history persistence. This does not alter the machine-readable run report JSON.</summary>
public static class ReportHistoryFileStore
{
    private const int SchemaVersion = 1;
    private const string ProductFolderName = "NvtFwCombiner";
    private const string HistoryFileName = "report-history.v1.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Gets the default local report history path for the current user.</summary>
    public static string DefaultHistoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProductFolderName,
        HistoryFileName);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        // Local history is best-effort UI state. A bad or inaccessible file must not block app startup.
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            string json = File.ReadAllText(path);
            ReportHistoryFile? file = JsonSerializer.Deserialize<ReportHistoryFile>(json, JsonOptions);
            return file?.SchemaVersion == SchemaVersion
                ? [.. file.Entries.Select(entry => entry.ToSnapshot()).OfType<ReportHistorySnapshot>()]
                : [];
        }
        catch (ArgumentException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (NotSupportedException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Saves report history snapshots to a specific path.</summary>
    public static void Save(string path, IEnumerable<ReportHistorySnapshot> snapshots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(snapshots);

        // Run reports remain available in memory and through explicit Save report; local history writes are non-critical.
        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            var file = new ReportHistoryFile(
                SchemaVersion,
                [.. snapshots
                    .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.ReportJson))
                    .Select(ReportHistoryFileEntry.FromSnapshot)]);
            string json = JsonSerializer.Serialize(file, JsonOptions);
            string tempPath = $"{path}.tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        catch (ArgumentException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }
        catch (NotSupportedException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
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
        public ReportHistoryFileEntry(string sourceName, string reportJson, string outputArtifactPath)
        {
            SourceName = sourceName ?? string.Empty;
            ReportJson = reportJson ?? string.Empty;
            OutputArtifactPath = outputArtifactPath ?? string.Empty;
        }

        public string SourceName { get; }

        public string ReportJson { get; }

        public string OutputArtifactPath { get; }

        public static ReportHistoryFileEntry FromSnapshot(ReportHistorySnapshot snapshot)
        {
            return new ReportHistoryFileEntry(
                snapshot.SourceName,
                snapshot.ReportJson,
                snapshot.OutputArtifactPath);
        }

        public ReportHistorySnapshot? ToSnapshot()
        {
            return string.IsNullOrWhiteSpace(ReportJson)
                ? null
                : new ReportHistorySnapshot(
                    SourceName ?? string.Empty,
                    ReportJson,
                    OutputArtifactPath ?? string.Empty);
        }
    }
}
