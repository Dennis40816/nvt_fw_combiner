using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Local UI preference persistence. These values never relax firmware validation gates.</summary>
public static class ShellPreferenceFileStore
{
    private const int SchemaVersion = 1;
    private const string ProductFolderName = "NvtFwCombiner";
    private const string PreferencesFileName = "preferences.v1.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Gets the default local preference path for the current user.</summary>
    public static string DefaultPreferencesPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProductFolderName,
        PreferencesFileName);

    /// <summary>Loads persisted preferences into the view model from the default path.</summary>
    public static void LoadInto(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        viewModel.LoadShellPreferences(Load(DefaultPreferencesPath));
    }

    /// <summary>Saves the view model preferences to the default path.</summary>
    public static void Save(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        Save(DefaultPreferencesPath, viewModel.ExportShellPreferences());
    }

    /// <summary>Loads persisted shell preferences from a specific path.</summary>
    public static ShellPreferenceSnapshot Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        // Local preferences are best-effort UI state. Bad JSON must not block startup or weaken gates.
        try
        {
            if (!File.Exists(path))
            {
                return ShellPreferenceSnapshot.Default;
            }

            string json = File.ReadAllText(path);
            ShellPreferenceFile? file = JsonSerializer.Deserialize<ShellPreferenceFile>(json, JsonOptions);
            return file?.SchemaVersion == SchemaVersion
                ? file.Preferences.ToSnapshot()
                : ShellPreferenceSnapshot.Default;
        }
        catch (ArgumentException)
        {
            return ShellPreferenceSnapshot.Default;
        }
        catch (IOException)
        {
            return ShellPreferenceSnapshot.Default;
        }
        catch (JsonException)
        {
            return ShellPreferenceSnapshot.Default;
        }
        catch (NotSupportedException)
        {
            return ShellPreferenceSnapshot.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return ShellPreferenceSnapshot.Default;
        }
    }

    /// <summary>Saves shell preferences to a specific path.</summary>
    public static void Save(string path, ShellPreferenceSnapshot preferences)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(preferences);

        // Preferences are convenience state only; failed writes must not block firmware workflows.
        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            var file = new ShellPreferenceFile(
                SchemaVersion,
                ShellPreferenceFileEntry.FromSnapshot(preferences));
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

    private sealed class ShellPreferenceFile
    {
        public ShellPreferenceFile(int schemaVersion, ShellPreferenceFileEntry preferences)
        {
            SchemaVersion = schemaVersion;
            Preferences = preferences ?? ShellPreferenceFileEntry.FromSnapshot(ShellPreferenceSnapshot.Default);
        }

        public int SchemaVersion { get; }

        public ShellPreferenceFileEntry Preferences { get; }
    }

    private sealed class ShellPreferenceFileEntry
    {
        public ShellPreferenceFileEntry(string theme, string strictness, string language)
        {
            Theme = theme ?? string.Empty;
            Strictness = strictness ?? string.Empty;
            Language = language ?? string.Empty;
        }

        public string Theme { get; }

        public string Strictness { get; }

        public string Language { get; }

        public static ShellPreferenceFileEntry FromSnapshot(ShellPreferenceSnapshot snapshot)
        {
            return new ShellPreferenceFileEntry(
                snapshot.Theme,
                snapshot.Strictness,
                snapshot.Language);
        }

        public ShellPreferenceSnapshot ToSnapshot()
        {
            return new ShellPreferenceSnapshot(
                Theme ?? string.Empty,
                Strictness ?? string.Empty,
                Language ?? string.Empty);
        }
    }
}
