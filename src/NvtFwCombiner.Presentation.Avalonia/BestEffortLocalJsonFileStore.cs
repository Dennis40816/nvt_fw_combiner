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
        Func<TDocument?, TResult> project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(project);

        try
        {
            return File.Exists(path)
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
}
