using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Focused Settings status and choice presentation.</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>Gets or sets the selected UI theme preference.</summary>
    [ObservableProperty]
    public partial string SelectedTheme { get; set; } = "System";

    /// <summary>Gets or sets the selected language preference.</summary>
    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = "English";

    /// <summary>Gets or sets whether non-essential progress motion is replaced with static emphasis.</summary>
    [ObservableProperty]
    public partial bool IsReducedMotionEnabled { get; set; }

    /// <summary>Exports local shell preferences for best-effort UI persistence.</summary>
    public ShellPreferenceSnapshot ExportShellPreferences()
    {
        return new ShellPreferenceSnapshot(SelectedTheme, SelectedLanguage, IsReducedMotionEnabled);
    }

    /// <summary>Loads local shell preferences, ignoring values that are no longer valid choices.</summary>
    public void LoadShellPreferences(ShellPreferenceSnapshot preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        SelectedTheme = NormalizePreference(preferences.Theme, Settings.ThemeChoices, SelectedTheme);
        SelectedLanguage = NormalizePreference(preferences.Language, Settings.LanguageChoices, SelectedLanguage);
        IsReducedMotionEnabled = preferences.IsReducedMotionEnabled;
    }

    private void RefreshSettingsState()
    {
        Settings.Refresh(Text);
    }

    private static string NormalizePreference(
        string? value,
        IReadOnlyList<string> choices,
        string fallback)
    {
        return !string.IsNullOrWhiteSpace(value) && choices.Contains(value, StringComparer.Ordinal)
            ? value
            : fallback;
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (!_isInitializing)
        {
            ApplyTextResources(ShellTextResources.LanguageFromPreference(value));
        }
    }

    partial void OnIsReducedMotionEnabledChanged(bool value)
    {
        RunSession.CompositionProgress.SetReducedMotion(value);
        RunSession.NotifyReducedMotionChanged();
    }
}
