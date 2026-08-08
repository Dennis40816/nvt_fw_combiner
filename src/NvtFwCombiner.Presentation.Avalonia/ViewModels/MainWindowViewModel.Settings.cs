using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Gets version and supported-workflow facts shown on Settings.</summary>
    public ObservableCollection<SettingSummaryViewModel> SettingsOverviewRows { get; } = [];

    /// <summary>Gets implemented catalog and external-tool facts shown on Settings.</summary>
    public ObservableCollection<SettingSummaryViewModel> SettingsCapabilityRows { get; } = [];

    /// <summary>Gets theme choices for the settings surface.</summary>
    public IReadOnlyList<string> ThemeChoices { get; } =
    [
        "Light",
    ];

    /// <summary>Gets language choices for the settings surface.</summary>
    public IReadOnlyList<string> LanguageChoices { get; } =
    [
        "English",
        "Traditional Chinese",
    ];

    /// <summary>Gets or sets the selected UI theme preference.</summary>
    [ObservableProperty]
    public partial string SelectedTheme { get; set; } = "Light";

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

        SelectedTheme = NormalizePreference(preferences.Theme, ThemeChoices, SelectedTheme);
        SelectedLanguage = NormalizePreference(preferences.Language, LanguageChoices, SelectedLanguage);
        IsReducedMotionEnabled = preferences.IsReducedMotionEnabled;
    }

    private void RefreshSettingsState()
    {
        WorkbenchSettingsSnapshot snapshot = WorkbenchCompositionService.GetSettingsSnapshot();

        ReplaceRows(
            SettingsOverviewRows,
            [
            new SettingSummaryViewModel(
                Text.Language == ShellLanguage.ChineseTraditional ? "App 版本" : "App version",
                AppVersion,
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "目前安裝套件的應用程式版本。"
                    : "Application version in the installed package.",
                Text.Language == ShellLanguage.ChineseTraditional ? "目前版本" : "Current"),
            new SettingSummaryViewModel(
                Text.Language == ShellLanguage.ChineseTraditional ? "IC 目錄" : "IC catalog",
                $"{snapshot.CatalogIcCount}",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "共用選擇器中的 IC 項目；各 workflow availability 仍分開判定。"
                    : "IC entries in the shared selector; workflow availability is evaluated separately.",
                "Catalog"),
            new SettingSummaryViewModel(
                "Standard Merge",
                $"{snapshot.StandardMergeProfileCount} profiles",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "已編譯並可執行的 V2 Standard Merge profiles。"
                    : "Compiled executable V2 Standard Merge profiles.",
                Text.Language == ShellLanguage.ChineseTraditional ? "可執行" : "Executable"),
            new SettingSummaryViewModel(
                "DP Replace",
                $"{snapshot.DpReplaceProfileCount} profiles",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "已編譯並可執行的 V2 DP Replace profiles。"
                    : "Compiled executable V2 DP Replace profiles.",
                Text.Language == ShellLanguage.ChineseTraditional ? "可執行" : "Executable"),
            ]);

        ReplaceRows(
            SettingsCapabilityRows,
            [
            new SettingSummaryViewModel(
                Text.Language == ShellLanguage.ChineseTraditional ? "CtrlRAM Replace 可用 IC" : "CtrlRAM Replace available ICs",
                $"{snapshot.CtrlRamReplaceAvailableIcCount} ICs",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "已有 executable/safety contract；golden 驗證狀態由各 workflow 分開顯示。"
                    : "An executable/safety contract exists; golden verification remains a separate per-workflow status.",
                Text.Language == ShellLanguage.ChineseTraditional ? "可用" : "Available"),
            ]);
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
        CompositionProgress.SetReducedMotion(value);
    }
}
