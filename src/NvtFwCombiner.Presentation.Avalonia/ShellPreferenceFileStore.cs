using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Local UI preference persistence. These values never relax firmware validation gates.</summary>
public static class ShellPreferenceFileStore
{
    private const int SchemaVersion = 1;
    private const string PreferencesFileName = "preferences.v1.json";
    internal const long MaximumPreferencesFileBytes = 64L * 1024;

    /// <summary>Gets the default local preference path for the current user.</summary>
    public static string DefaultPreferencesPath => BestEffortLocalJsonFileStore.GetDefaultPath(PreferencesFileName);

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
        return BestEffortLocalJsonFileStore.Load(
            path,
            ShellPreferenceSnapshot.Default,
            (ShellPreferenceFile? file) => file?.SchemaVersion == SchemaVersion
                ? file.Preferences.ToSnapshot()
                : ShellPreferenceSnapshot.Default,
            MaximumPreferencesFileBytes);
    }

    /// <summary>Saves shell preferences to a specific path.</summary>
    public static void Save(string path, ShellPreferenceSnapshot preferences)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(preferences);

        BestEffortLocalJsonFileStore.Save(
            path,
            new ShellPreferenceFile(
                SchemaVersion,
                ShellPreferenceFileEntry.FromSnapshot(preferences)));
    }

    /// <summary>Saves a default-path snapshot without blocking the UI dispatcher.</summary>
    internal static Task SaveAsync(
        ShellPreferenceSnapshot preferences,
        CancellationToken cancellationToken)
    {
        return SaveAsync(DefaultPreferencesPath, preferences, cancellationToken);
    }

    internal static Task SaveAsync(
        string path,
        ShellPreferenceSnapshot preferences,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(preferences);
        return BestEffortLocalJsonFileStore.SaveAsync(
            path,
            new ShellPreferenceFile(
                SchemaVersion,
                ShellPreferenceFileEntry.FromSnapshot(preferences)),
            cancellationToken);
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
        public ShellPreferenceFileEntry(
            string theme,
            string strictness,
            string language,
            bool isReducedMotionEnabled = false)
        {
            Theme = theme ?? string.Empty;
            Strictness = strictness ?? string.Empty;
            Language = language ?? string.Empty;
            IsReducedMotionEnabled = isReducedMotionEnabled;
        }

        public string Theme { get; }

        public string Strictness { get; }

        public string Language { get; }

        public bool IsReducedMotionEnabled { get; }

        public static ShellPreferenceFileEntry FromSnapshot(ShellPreferenceSnapshot snapshot)
        {
            return new ShellPreferenceFileEntry(
                snapshot.Theme,
                "Strict",
                snapshot.Language,
                snapshot.IsReducedMotionEnabled);
        }

        public ShellPreferenceSnapshot ToSnapshot()
        {
            return new ShellPreferenceSnapshot(
                Theme ?? string.Empty,
                Language ?? string.Empty,
                IsReducedMotionEnabled);
        }
    }
}
