using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    public partial string SelectedTheme { get; set; } = "System";

    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = "English";

    [ObservableProperty]
    public partial bool IsReducedMotionEnabled { get; set; }

    public ShellPreferenceSnapshot ExportShellPreferences()
    {
        return new ShellPreferenceSnapshot(SelectedTheme, SelectedLanguage, IsReducedMotionEnabled);
    }

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
        Merge.InspectionLifecycles.SetReducedMotion(value);
        Replace.InspectionLifecycles.SetReducedMotion(value);
        RunSession.NotifyReducedMotionChanged();
    }
}
