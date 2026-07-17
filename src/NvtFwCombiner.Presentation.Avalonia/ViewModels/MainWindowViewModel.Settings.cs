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
        "System",
        "Light",
        "Dark",
        "High contrast",
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

    /// <summary>Gets or sets the selected language preference.</summary>
    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = "English";

    /// <summary>Gets the current theme preference effect shown next to the selector.</summary>
    public string ThemePreferenceStatus => Text.GetThemePreferenceStatus(SelectedTheme);

    /// <summary>Gets the current language preference effect shown next to the selector.</summary>
    public string LanguagePreferenceStatus => Text.GetLanguagePreferenceStatus(SelectedLanguage);

    /// <summary>Exports local shell preferences for best-effort UI persistence.</summary>
    public ShellPreferenceSnapshot ExportShellPreferences()
    {
        return new ShellPreferenceSnapshot(SelectedTheme, SelectedLanguage);
    }

    /// <summary>Loads local shell preferences, ignoring values that are no longer valid choices.</summary>
    public void LoadShellPreferences(ShellPreferenceSnapshot preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        SelectedTheme = NormalizePreference(preferences.Theme, ThemeChoices, SelectedTheme);
        SelectedLanguage = NormalizePreference(preferences.Language, LanguageChoices, SelectedLanguage);
        RefreshSettingsState();
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
                Text.Language == ShellLanguage.ChineseTraditional ? "支援 IC" : "Supported ICs",
                $"{snapshot.SupportedIcCount}",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "可由共用 IC 選擇器載入的 catalog 項目。"
                    : "Catalog entries available from the shared IC selector.",
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
                $"{snapshot.ReplaceProfileCount} profiles",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "已編譯並可執行的 V2 DP Replace profiles。"
                    : "Compiled executable V2 DP Replace profiles.",
                Text.Language == ShellLanguage.ChineseTraditional ? "可執行" : "Executable"),
            ]);

        ReplaceRows(
            SettingsCapabilityRows,
            [
            new SettingSummaryViewModel(
                "CtrlRAM catalog coverage",
                $"{snapshot.CtrlRamReplaceIcCount} ICs",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "具有實體 CtrlRAM region 與 postbuild profile 的 IC。"
                    : "ICs with physical CtrlRAM regions and postbuild profiles.",
                "Catalog"),
            new SettingSummaryViewModel(
                Text.Language == ShellLanguage.ChineseTraditional ? "外部 postbuild tools" : "External postbuild tools",
                snapshot.ExternalToolBindingCount == 1
                    ? "1 binding"
                    : $"{snapshot.ExternalToolBindingCount} bindings",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "已註冊且由 manifest 固定 hash 的 processor tool bindings。"
                    : "Processor tool bindings registered with manifest-pinned hashes.",
                Text.Language == ShellLanguage.ChineseTraditional ? "固定" : "Pinned"),
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

    partial void OnSelectedThemeChanged(string value)
    {
        OnPropertyChanged(nameof(ThemePreferenceStatus));
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        ApplyTextResources(ShellTextResources.LanguageFromPreference(value));
        OnPropertyChanged(nameof(LanguagePreferenceStatus));
    }
}
