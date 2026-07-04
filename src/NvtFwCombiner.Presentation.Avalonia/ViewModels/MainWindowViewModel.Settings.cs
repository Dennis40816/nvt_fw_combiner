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
                "Theme selection is stored in shell state; persistence is still pending.",
                "Local"),
            new SettingSummaryViewModel(
                "Strictness",
                SelectedStrictness,
                "Strict keeps unsupported workflow states closed until contracts are ready.",
                "Local"),
            new SettingSummaryViewModel(
                "Language",
                SelectedLanguage,
                "Text resources support English and Traditional Chinese architecture.",
                "Local"));

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

    partial void OnSelectedThemeChanged(string value)
    {
        RefreshSettingsState();
    }

    partial void OnSelectedStrictnessChanged(string value)
    {
        RefreshSettingsState();
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        RefreshSettingsState();
    }
}
