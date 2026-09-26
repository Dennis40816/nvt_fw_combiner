using System.Text.Json;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Local UI preference projection. These values never relax firmware validation gates.</summary>
public static class ShellPreferenceFileStore
{
    private const int SchemaVersion = 1;
    private const string PreferencesFileName = "preferences.v1.json";
    internal const long MaximumPreferencesFileBytes = 64L * 1024;

    /// <summary>Gets the default local preference path for the current user.</summary>
    public static string DefaultPreferencesPath => LocalJsonDocument.GetDefaultPath(PreferencesFileName);

    /// <summary>Loads a bounded preference snapshot without blocking framework initialization.</summary>
    internal static async Task<ShellPreferenceSnapshot> LoadAsync(ILocalFileStore files, string path)
    {
        ArgumentNullException.ThrowIfNull(files);
        try
        {
            ShellPreferenceFile? file = await files.ReadAsync(
                    path,
                    MaximumPreferencesFileBytes,
                    LocalJsonDocument.DeserializeAsync<ShellPreferenceFile>,
                    CancellationToken.None)
                .ConfigureAwait(false);
            return file is { SchemaVersion: SchemaVersion, Preferences: { } entry }
                ? new(
                    entry.Theme ?? string.Empty,
                    entry.Language ?? string.Empty,
                    entry.IsReducedMotionEnabled,
                    entry.ExpandInputDetailsByDefault)
                : ShellPreferenceSnapshot.Default;
        }
        catch (Exception exception) when (exception is LocalFileReadException or JsonException or NotSupportedException)
        {
            return ShellPreferenceSnapshot.Default;
        }
    }

    /// <summary>Atomically saves one preference snapshot; failures and cancellation reach the caller.</summary>
    internal static async Task SaveAsync(
        ILocalFileStore files,
        string path,
        ShellPreferenceSnapshot preferences,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(preferences);
        await files.WriteAsync(
                path,
                JsonSerializer.SerializeToUtf8Bytes(
                    new ShellPreferenceFile(
                        SchemaVersion,
                        new(
                            preferences.Theme,
                            "Strict",
                            preferences.Language,
                            preferences.IsReducedMotionEnabled,
                            preferences.ExpandInputDetailsByDefault)),
                    LocalJsonDocument.Options),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed record ShellPreferenceFile(int SchemaVersion, ShellPreferenceFileEntry? Preferences);

    private sealed record ShellPreferenceFileEntry(
        string? Theme,
        string? Strictness,
        string? Language,
        bool IsReducedMotionEnabled = false,
        bool ExpandInputDetailsByDefault = false);
}
