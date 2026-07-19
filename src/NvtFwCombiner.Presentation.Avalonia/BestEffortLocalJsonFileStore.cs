using System.Text.Json;
using System.Text.Json.Serialization;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Shared best-effort JSON persistence for non-critical local UI state.</summary>
internal static class BestEffortLocalJsonFileStore
{
    private const string ProductFolderName = "NvtFwCombiner";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string GetDefaultPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductFolderName,
            fileName);
    }

    public static TResult Load<TDocument, TResult>(
        string path,
        TResult fallback,
        Func<TDocument?, TResult> project,
        long? maximumFileBytes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(project);
        if (maximumFileBytes is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFileBytes));
        }

        try
        {
            bool canLoad = File.Exists(path) &&
                (maximumFileBytes is not long limit || new FileInfo(path).Length <= limit);
            return canLoad
                ? project(JsonSerializer.Deserialize<TDocument>(File.ReadAllText(path), JsonOptions))
                : fallback;
        }
        catch (Exception exception) when (exception is
            ArgumentException or IOException or JsonException or NotSupportedException or UnauthorizedAccessException)
        {
            return fallback;
        }
    }

    public static void Save<TDocument>(string path, TDocument document)
        where TDocument : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            string tempPath = $"{path}.tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(document, JsonOptions));
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        // Local UI convenience state must not block startup or firmware workflows.
        catch (Exception exception) when (exception is
            ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
        }
    }

    public static async Task SaveAsync<TDocument>(
        string path,
        TDocument document,
        CancellationToken cancellationToken)
        where TDocument : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        string? tempPath = null;
        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await using (var stream = new FileStream(
                             tempPath,
                             new FileStreamOptions
                             {
                                 Mode = FileMode.CreateNew,
                                 Access = FileAccess.Write,
                                 Share = FileShare.None,
                                 Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                             }))
            {
                await JsonSerializer.SerializeAsync(
                        stream,
                        document,
                        JsonOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }

            tempPath = null;
        }
        // Local UI convenience state must not block firmware workflows or publish a cancelled snapshot.
        catch (Exception exception) when (exception is
            ArgumentException or IOException or NotSupportedException or
            OperationCanceledException or UnauthorizedAccessException)
        {
        }
        finally
        {
            DeleteTemporaryFileBestEffort(tempPath);
        }
    }

    private static void DeleteTemporaryFileBestEffort(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
