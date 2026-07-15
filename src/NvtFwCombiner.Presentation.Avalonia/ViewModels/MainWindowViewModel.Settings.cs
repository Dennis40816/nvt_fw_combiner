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
    public string ThemePreferenceStatus => Text.GetThemePreferenceStatus(SelectedTheme);

    /// <summary>Gets the current strictness preference effect shown next to the selector.</summary>
    public string StrictnessPreferenceStatus => Text.GetStrictnessPreferenceStatus(SelectedStrictness);

    /// <summary>Gets the current language preference effect shown next to the selector.</summary>
    public string LanguagePreferenceStatus => Text.GetLanguagePreferenceStatus(SelectedLanguage);

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
        WorkbenchSettingsSnapshot snapshot = WorkbenchCompositionService.GetSettingsSnapshot();

        ReplaceSettingsRows(
            SettingsProfileRows,
            new SettingSummaryViewModel(
                Text.Language == ShellLanguage.ChineseTraditional ? "內建 profiles" : "Built-in profiles",
                $"{snapshot.StandardMergeProfileCount} merge / {snapshot.ReplaceProfileCount} replace",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "Standard Merge 使用 executable profiles；Replace workbench 使用 IC catalog data 與 contract fixtures。"
                    : "Standard Merge uses executable profiles; Replace workbench uses IC catalog data plus contract fixtures.",
                Text.Language == ShellLanguage.ChineseTraditional ? "已串接" : "Wired"),
            new SettingSummaryViewModel(
                Text.Language == ShellLanguage.ChineseTraditional ? "Flash-map catalog" : "Flash-map catalog",
                $"{snapshot.FlashMapIcCount} ICs",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "IC 與 CtrlRAM region choices 由 application flash-map catalog 載入。"
                    : "IC and CtrlRAM region choices are loaded from the application flash-map catalog.",
                Text.Language == ShellLanguage.ChineseTraditional ? "已串接" : "Wired"),
            new SettingSummaryViewModel(
                Text.Language == ShellLanguage.ChineseTraditional ? "作用中流程條件" : "Active workflow context",
                $"{SelectedIc} / {SelectedNumber}",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "選定的 IC 與 Number 只套用在 Merge / Replace workflow pages。"
                    : "The selected IC and Number only apply to Merge and Replace workflow pages.",
                IsDeviceContextVisible
                    ? Text.Language == ShellLanguage.ChineseTraditional ? "顯示中" : "Visible"
                    : Text.Language == ShellLanguage.ChineseTraditional ? "此頁隱藏" : "Hidden here"));

        ReplaceSettingsRows(
            SettingsToolRows,
            new SettingSummaryViewModel(
                "CRC/header refresh",
                Text.Language == ShellLanguage.ChineseTraditional ? "已設定" : "Configured",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "已打包的 processor support 可供核准的 postbuild refresh steps 使用。"
                    : "Packaged processor support is available for approved postbuild refresh steps.",
                Text.Language == ShellLanguage.ChineseTraditional ? "固定" : "Pinned"),
            new SettingSummaryViewModel(
                "Postbuild catalog",
                $"{snapshot.PostbuildProfileCount} ICs",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "Profile evidence 已正規化成 deterministic refresh plans。"
                    : "Profile evidence is normalized into deterministic refresh plans.",
                Text.Language == ShellLanguage.ChineseTraditional ? "已串接" : "Wired"),
            new SettingSummaryViewModel(
                Text.Language == ShellLanguage.ChineseTraditional ? "Tool manifest" : "Tool manifest",
                snapshot.ToolManifestPath,
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "執行檔名稱、SHA-256、adapter 與 timeout 由 manifest 固定。"
                    : "Executable name, SHA-256, adapter, and timeout are pinned by the manifest.",
                Text.Language == ShellLanguage.ChineseTraditional ? "已設定" : "Configured"));

        ReplaceSettingsRows(
            SettingsPreferenceRows,
            new SettingSummaryViewModel(
                Text.ThemeLabel,
                SelectedTheme,
                ThemePreferenceStatus,
                Text.Language == ShellLanguage.ChineseTraditional ? "已儲存" : "Saved"),
            new SettingSummaryViewModel(
                Text.StrictnessLabel,
                SelectedStrictness,
                StrictnessPreferenceStatus,
                Text.Language == ShellLanguage.ChineseTraditional ? "已儲存" : "Saved"),
            new SettingSummaryViewModel(
                Text.LanguageLabel,
                SelectedLanguage,
                LanguagePreferenceStatus,
                Text.Language == ShellLanguage.ChineseTraditional ? "已套用" : "Active"));

        ReplaceSettingsRows(
            SettingsDiagnosticsRows,
            new SettingSummaryViewModel(
                Text.Language == ShellLanguage.ChineseTraditional ? "Report 審查" : "Report review",
                HasLoadedReport
                    ? LoadedReport.Title
                    : Text.Language == ShellLanguage.ChineseTraditional ? "尚未載入 report" : "No report loaded",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "載入 report JSON 可在不執行韌體的情況下開啟可讀 evidence review panel。"
                    : "Load report JSON opens a readable evidence review panel without running firmware.",
                HasLoadedReport
                    ? Text.Language == ShellLanguage.ChineseTraditional ? "已載入" : "Loaded"
                    : Text.Language == ShellLanguage.ChineseTraditional ? "可使用" : "Ready"),
            new SettingSummaryViewModel(
                Text.Language == ShellLanguage.ChineseTraditional ? "最新執行" : "Latest run",
                LastRunResult.Title,
                LastRunResult.Detail,
                LastRunResult.Succeeded ? "OK" : Text.Language == ShellLanguage.ChineseTraditional ? "已阻擋" : "Blocked"),
            new SettingSummaryViewModel(
                Text.Language == ShellLanguage.ChineseTraditional ? "Report history 儲存" : "Report history store",
                Text.Language == ShellLanguage.ChineseTraditional ? "Local AppData" : "Local AppData",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "Report history 會儲存在本機；run report JSON 仍可另外明確儲存。"
                    : "Report history is persisted locally; run report JSON can still be saved explicitly.",
                Text.Language == ShellLanguage.ChineseTraditional ? "已啟用" : "Enabled"));

        ReplaceSettingsRows(
            SettingsReadinessRows,
            new SettingSummaryViewModel(
                Text.Language == ShellLanguage.ChineseTraditional ? "App 版本" : "App version",
                AppVersion,
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "Assembly informational version、VERSION file 與 shell header 已對齊。"
                    : "Assembly informational version, VERSION file, and shell header are aligned.",
                Text.Language == ShellLanguage.ChineseTraditional ? "已更新" : "Updated"),
            new SettingSummaryViewModel(
                Text.DeviceContextTitle,
                Text.Language == ShellLanguage.ChineseTraditional ? "僅 workflow pages" : "Workflow pages only",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "IC 與 Number 在 Home、Settings 與 Hex Editor 隱藏，僅在 Merge / Replace 顯示。"
                    : "IC and Number are hidden on Home, Settings, and Hex Editor, then shown only for Merge and Replace.",
                Text.Language == ShellLanguage.ChineseTraditional ? "已限縮" : "Scoped"),
            new SettingSummaryViewModel(
                Text.SettingsPreferencesTitle,
                Text.Language == ShellLanguage.ChineseTraditional ? "本機儲存" : "Saved locally",
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "Theme、review strictness 與 language 會在啟動時還原，但不改變 firmware gates。"
                    : "Theme, review strictness, and language are restored on startup without changing firmware gates.",
                Text.Language == ShellLanguage.ChineseTraditional ? "就緒" : "Ready"),
            new SettingSummaryViewModel(
                Text.Language == ShellLanguage.ChineseTraditional ? "導覽" : "Navigation",
                NavigationPath,
                Text.Language == ShellLanguage.ChineseTraditional
                    ? "Breadcrumb 會保留頁面 history，方便回到前面的層級。"
                    : "Breadcrumb entries keep page history so users can jump back multiple levels.",
                CanGoBack
                    ? Text.Language == ShellLanguage.ChineseTraditional ? "有 history" : "History available"
                    : Text.Language == ShellLanguage.ChineseTraditional ? "根層" : "At root"));
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
        ApplyTextResources(ShellTextResources.LanguageFromPreference(value));
        OnPropertyChanged(nameof(LanguagePreferenceStatus));
        RefreshSettingsState();
    }
}
