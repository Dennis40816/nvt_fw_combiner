using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Local UI preference persistence. These values never relax firmware validation gates.</summary>
public static class ShellPreferenceFileStore
{
    private const int SchemaVersion = 1;
    private const string PreferencesFileName = "preferences.v1.json";

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
            (ShellPreferenceFile? file) => file is { SchemaVersion: SchemaVersion, Preferences: { } entry }
                ? new ShellPreferenceSnapshot(
                    entry.Theme ?? string.Empty,
                    entry.Language ?? string.Empty,
                    entry.IsReducedMotionEnabled)
                : ShellPreferenceSnapshot.Default);
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
                new ShellPreferenceFileEntry(
                    preferences.Theme,
                    "Strict",
                    preferences.Language,
                    preferences.IsReducedMotionEnabled)));
    }

    private sealed record ShellPreferenceFile(
        int SchemaVersion,
        ShellPreferenceFileEntry? Preferences);

    private sealed record ShellPreferenceFileEntry(
        string? Theme,
        string? Strictness,
        string? Language,
        bool IsReducedMotionEnabled = false);
}
