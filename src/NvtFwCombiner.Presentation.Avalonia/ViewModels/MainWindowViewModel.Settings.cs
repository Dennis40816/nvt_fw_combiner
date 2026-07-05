using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Gets profile/catalog rows shown on Settings.</summary>
    public ObservableCollection<SettingSummaryViewModel> SettingsProfileRows { get; } = [];

    /// <summary>Gets external tool rows shown on Settings.</summary>
    public ObservableCollection<SettingSummaryViewModel> SettingsToolRows { get; } = [];

    /// <summary>Gets preference rows shown on Settings.</summary>
    public ObservableCollection<SettingSummaryViewModel> SettingsPreferenceRows { get; } = [];

    /// <summary>Gets diagnostics/report rows shown on Settings.</summary>
    public ObservableCollection<SettingSummaryViewModel> SettingsDiagnosticsRows { get; } = [];

    /// <summary>Gets readiness rows shown in the Settings inspector.</summary>
    public ObservableCollection<SettingSummaryViewModel> SettingsReadinessRows { get; } = [];

    /// <summary>Gets theme choices for the settings surface.</summary>
    public IReadOnlyList<string> ThemeChoices { get; } =
    [
        "System",
        "Light",
        "Dark",
        "High contrast",
    ];

    /// <summary>Gets strictness choices for the settings surface.</summary>
    public IReadOnlyList<string> StrictnessChoices { get; } =
    [
        "Strict",
        "Warn only",
    ];

    /// <summary>Gets language choices for the settings surface.</summary>
    public IReadOnlyList<string> LanguageChoices { get; } =
    [
        "English",
        "Traditional Chinese",
    ];

    /// <summary>Gets or sets the selected UI theme preference.</summary>
    [ObservableProperty]
    public partial string SelectedTheme { get; set; } = "System";

    /// <summary>Gets or sets the selected validation strictness preference.</summary>
    [ObservableProperty]
    public partial string SelectedStrictness { get; set; } = "Strict";

    /// <summary>Gets or sets the selected language preference.</summary>
    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = "English";

    /// <summary>Gets the current theme preference effect shown next to the selector.</summary>
    public string ThemePreferenceStatus => SelectedTheme switch
    {
        "System" => "Follows the operating-system theme.",
        "Light" => "Light theme is applied to this window.",
        "Dark" => "Dark theme is applied to this window.",
        "High contrast" => "Uses the dark visual variant until a contrast palette is added.",
        _ => "Theme preference is saved locally.",
    };

    /// <summary>Gets the current strictness preference effect shown next to the selector.</summary>
    public string StrictnessPreferenceStatus => SelectedStrictness switch
    {
        "Strict" => "Unsupported workflow states stay fail-closed.",
        "Warn only" => "Preference is saved; firmware gates still fail closed.",
        _ => "Strictness preference is saved locally.",
    };

    /// <summary>Gets the current language preference effect shown next to the selector.</summary>
    public string LanguagePreferenceStatus => SelectedLanguage switch
    {
        "English" => "English shell resources are active.",
        "Traditional Chinese" => "Preference is saved; full XAML localization is pending.",
        _ => "Language preference is saved locally.",
    };

    /// <summary>Exports local shell preferences for best-effort UI persistence.</summary>
    public ShellPreferenceSnapshot ExportShellPreferences()
    {
        return new ShellPreferenceSnapshot(SelectedTheme, SelectedStrictness, SelectedLanguage);
    }

    /// <summary>Loads local shell preferences, ignoring values that are no longer valid choices.</summary>
    public void LoadShellPreferences(ShellPreferenceSnapshot preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        SelectedTheme = NormalizePreference(preferences.Theme, ThemeChoices, SelectedTheme);
        SelectedStrictness = NormalizePreference(preferences.Strictness, StrictnessChoices, SelectedStrictness);
        SelectedLanguage = NormalizePreference(preferences.Language, LanguageChoices, SelectedLanguage);
        RefreshSettingsState();
    }

    private void RefreshSettingsState()
    {
        WorkbenchSettingsSnapshot snapshot = UiCompositionRunner.GetSettingsSnapshot();

        ReplaceSettingsRows(
            SettingsProfileRows,
            new SettingSummaryViewModel(
                "Built-in profiles",
                $"{snapshot.StandardMergeProfileCount} merge / {snapshot.ReplaceProfileCount} replace",
                "Standard Merge uses executable profiles; Replace workbench uses IC catalog data plus contract fixtures.",
                "Wired"),
            new SettingSummaryViewModel(
                "Flash-map catalog",
                $"{snapshot.FlashMapIcCount} ICs",
                "IC and CtrlRAM region choices are loaded from the application flash-map catalog.",
                "Wired"),
            new SettingSummaryViewModel(
                "Active workflow context",
                $"{SelectedIc} / {SelectedNumber}",
                "The selected IC and Number only apply to Merge and Replace workflow pages.",
                IsDeviceContextVisible ? "Visible" : "Hidden here"));

        ReplaceSettingsRows(
            SettingsToolRows,
            new SettingSummaryViewModel(
                "External tool binding",
                snapshot.ToolBindingIds,
                "CtrlRAM postbuild profiles reference approved Combiner.exe bindings by id.",
                "Pinned"),
            new SettingSummaryViewModel(
                "Postbuild catalog",
                $"{snapshot.PostbuildProfileCount} ICs",
                "Command sequences are normalized from owner-provided postbuild evidence.",
                "Wired"),
            new SettingSummaryViewModel(
                "Tool manifest",
                snapshot.ToolManifestPath,
                "The packaged tool manifest pins executable name, SHA-256, adapter, and timeout.",
                "Configured"));

        ReplaceSettingsRows(
            SettingsPreferenceRows,
            new SettingSummaryViewModel(
                "Theme",
                SelectedTheme,
                ThemePreferenceStatus,
                SelectedTheme == "High contrast" ? "Pending" : "Saved"),
            new SettingSummaryViewModel(
                "Strictness",
                SelectedStrictness,
                StrictnessPreferenceStatus,
                "Saved"),
            new SettingSummaryViewModel(
                "Language",
                SelectedLanguage,
                LanguagePreferenceStatus,
                SelectedLanguage == "English" ? "Saved" : "Pending"));

        ReplaceSettingsRows(
            SettingsDiagnosticsRows,
            new SettingSummaryViewModel(
                "Report review",
                HasLoadedReport ? LoadedReport.Title : "No report loaded",
                "Load report JSON opens a readable evidence review panel without running firmware.",
                HasLoadedReport ? "Loaded" : "Ready"),
            new SettingSummaryViewModel(
                "Latest run",
                LastRunResult.Title,
                LastRunResult.Detail,
                LastRunResult.Succeeded ? "OK" : "Blocked"),
            new SettingSummaryViewModel(
                "Report history store",
                "Local AppData",
                "Report history is persisted locally; run report JSON can still be saved explicitly.",
                "Enabled"),
            new SettingSummaryViewModel(
                "Diagnostics log",
                "Read-only and sanitized",
                "Run-specific diagnostics stay in Preview/Build report surfaces.",
                "Planned"));

        ReplaceSettingsRows(
            SettingsReadinessRows,
            new SettingSummaryViewModel(
                "App version",
                AppVersion,
                "Assembly informational version, VERSION file, and shell header are aligned.",
                "Updated"),
            new SettingSummaryViewModel(
                "Device context",
                "Workflow pages only",
                "IC and Number are hidden on Home and Settings, then shown for Merge and Replace.",
                "Scoped"),
            new SettingSummaryViewModel(
                "Preferences",
                "Saved locally",
                "Theme, strictness, and language are restored on startup without changing firmware gates.",
                "Ready"),
            new SettingSummaryViewModel(
                "Navigation",
                NavigationPath,
                "Breadcrumb entries keep page history so users can jump back multiple levels.",
                CanGoBack ? "History available" : "At root"));
    }

    private static void ReplaceSettingsRows(
        ObservableCollection<SettingSummaryViewModel> target,
        params SettingSummaryViewModel[] rows)
    {
        ReplaceRows(target, rows);
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

    partial void OnSelectedThemeChanged(string value)
    {
        OnPropertyChanged(nameof(ThemePreferenceStatus));
        RefreshSettingsState();
    }

    partial void OnSelectedStrictnessChanged(string value)
    {
        OnPropertyChanged(nameof(StrictnessPreferenceStatus));
        RefreshSettingsState();
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        OnPropertyChanged(nameof(LanguagePreferenceStatus));
        RefreshSettingsState();
    }
}
