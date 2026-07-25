using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Support;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Gets version and supported-workflow facts shown on Settings.</summary>
    public ObservableCollection<SettingSummaryViewModel> SettingsOverviewRows { get; } = [];

    /// <summary>Gets implemented catalog and external-tool facts shown on Settings.</summary>
    public ObservableCollection<SettingSummaryViewModel> SettingsCapabilityRows { get; } = [];

    /// <summary>Gets the read-only exact-route Support Matrix shown on Settings.</summary>
    public ObservableCollection<SupportMatrixSettingsRowViewModel> SettingsSupportMatrixRows { get; } = [];

    /// <summary>Gets the localized Support Matrix title.</summary>
    public string SettingsSupportMatrixTitle { get; private set; } = string.Empty;

    /// <summary>Gets the localized Support Matrix explanation.</summary>
    public string SettingsSupportMatrixDescription { get; private set; } = string.Empty;

    /// <summary>Gets the localized migration state label for the Support Matrix.</summary>
    public string SettingsSupportMatrixStatus { get; private set; } = string.Empty;

    /// <summary>Gets shared badge classes for the Support Matrix migration state.</summary>
    public string SettingsSupportMatrixStatusBadgeClasses { get; private set; } = "reportBadge review";

    /// <summary>Whether the Support Matrix migration state uses the shared success treatment.</summary>
    public bool IsSettingsSupportMatrixSuccess => SettingsSupportMatrixStatusBadgeClasses == "reportBadge success";

    /// <summary>Whether the Support Matrix migration state uses the shared review treatment.</summary>
    public bool IsSettingsSupportMatrixReview => SettingsSupportMatrixStatusBadgeClasses == "reportBadge review";

    /// <summary>Gets the hash-closed policy identity used by the visible matrix snapshot.</summary>
    public string SettingsSupportMatrixPolicyDetail { get; private set; } = string.Empty;

    /// <summary>Gets the localized retained-diagnostic summary.</summary>
    public string SettingsSupportMatrixDiagnostics { get; private set; } = string.Empty;

    /// <summary>Gets the localized IC column header for the Support Matrix.</summary>
    public string SettingsSupportMatrixIcHeader { get; private set; } = "IC";

    /// <summary>Gets the localized workflow column header for the Support Matrix.</summary>
    public string SettingsSupportMatrixWorkflowHeader { get; private set; } = string.Empty;

    /// <summary>Gets the localized IC Count column header for the Support Matrix.</summary>
    public string SettingsSupportMatrixIcCountHeader { get; private set; } = string.Empty;

    /// <summary>Gets the localized map column header for the Support Matrix.</summary>
    public string SettingsSupportMatrixMapHeader { get; private set; } = string.Empty;

    /// <summary>Gets the localized authoring column header for the Support Matrix.</summary>
    public string SettingsSupportMatrixAuthoringHeader { get; private set; } = string.Empty;

    /// <summary>Gets the localized execution column header for the Support Matrix.</summary>
    public string SettingsSupportMatrixExecutionHeader { get; private set; } = string.Empty;

    /// <summary>Gets the localized publication column header for the Support Matrix.</summary>
    public string SettingsSupportMatrixPublicationHeader { get; private set; } = string.Empty;

    /// <summary>Gets the localized evidence column header for the Support Matrix.</summary>
    public string SettingsSupportMatrixEvidenceHeader { get; private set; } = string.Empty;

    /// <summary>Gets theme choices for the settings surface.</summary>
    public IReadOnlyList<string> ThemeChoices { get; } =
    [
        "System",
        "Light",
        "Dark",
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
        SupportMatrix supportMatrix = WorkbenchCompositionService.GetSupportMatrix();
        bool isChinese = Text.Language == ShellLanguage.ChineseTraditional;

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

        ReplaceRows(
            SettingsSupportMatrixRows,
            supportMatrix.Rows.Select(row => new SupportMatrixSettingsRowViewModel(row, Text.Language)));
        SettingsSupportMatrixTitle = isChinese ? "支援矩陣" : "Support Matrix";
        SettingsSupportMatrixDescription = isChinese
            ? "唯讀的 exact route 報告：建立、執行、發布與證據各自獨立；顯示不會授與 firmware 支援。"
            : "Read-only exact-route reporting: authoring, execution, publication, and evidence remain independent; display never grants firmware support.";
        SettingsSupportMatrixStatus = supportMatrix.IsMigrationReady
            ? isChinese ? "來源已完整對應" : "Source bindings complete"
            : isChinese ? "遷移審查中" : "Migration review";
        SettingsSupportMatrixStatusBadgeClasses = supportMatrix.IsMigrationReady
            ? "reportBadge success"
            : "reportBadge review";
        SettingsSupportMatrixPolicyDetail = isChinese
            ? $"政策 {supportMatrix.Policy.PolicyId} v{supportMatrix.Policy.PolicyVersion} · SHA-256 {supportMatrix.Policy.Sha256}"
            : $"Policy {supportMatrix.Policy.PolicyId} v{supportMatrix.Policy.PolicyVersion} · SHA-256 {supportMatrix.Policy.Sha256}";
        SettingsSupportMatrixDiagnostics = supportMatrix.Diagnostics.Count == 0
            ? isChinese ? "沒有 migration diagnostics。" : "No migration diagnostics."
            : isChinese
                ? $"{supportMatrix.Diagnostics.Count} 個 migration diagnostics 仍被保留在此 reporting snapshot。"
                : $"{supportMatrix.Diagnostics.Count} migration diagnostics remain retained in this reporting snapshot.";
        SettingsSupportMatrixIcHeader = "IC";
        SettingsSupportMatrixWorkflowHeader = isChinese ? "流程" : "Workflow";
        SettingsSupportMatrixIcCountHeader = isChinese ? "IC 數" : "IC Count";
        SettingsSupportMatrixMapHeader = isChinese ? "地圖" : "Map";
        SettingsSupportMatrixAuthoringHeader = isChinese ? "建立" : "Author";
        SettingsSupportMatrixExecutionHeader = isChinese ? "執行" : "Execute";
        SettingsSupportMatrixPublicationHeader = isChinese ? "發布" : "Publication";
        SettingsSupportMatrixEvidenceHeader = isChinese ? "證據" : "Evidence";
        OnPropertyChanged(nameof(SettingsSupportMatrixTitle));
        OnPropertyChanged(nameof(SettingsSupportMatrixDescription));
        OnPropertyChanged(nameof(SettingsSupportMatrixStatus));
        OnPropertyChanged(nameof(SettingsSupportMatrixStatusBadgeClasses));
        OnPropertyChanged(nameof(IsSettingsSupportMatrixSuccess));
        OnPropertyChanged(nameof(IsSettingsSupportMatrixReview));
        OnPropertyChanged(nameof(SettingsSupportMatrixPolicyDetail));
        OnPropertyChanged(nameof(SettingsSupportMatrixDiagnostics));
        OnPropertyChanged(nameof(SettingsSupportMatrixIcHeader));
        OnPropertyChanged(nameof(SettingsSupportMatrixWorkflowHeader));
        OnPropertyChanged(nameof(SettingsSupportMatrixIcCountHeader));
        OnPropertyChanged(nameof(SettingsSupportMatrixMapHeader));
        OnPropertyChanged(nameof(SettingsSupportMatrixAuthoringHeader));
        OnPropertyChanged(nameof(SettingsSupportMatrixExecutionHeader));
        OnPropertyChanged(nameof(SettingsSupportMatrixPublicationHeader));
        OnPropertyChanged(nameof(SettingsSupportMatrixEvidenceHeader));
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
