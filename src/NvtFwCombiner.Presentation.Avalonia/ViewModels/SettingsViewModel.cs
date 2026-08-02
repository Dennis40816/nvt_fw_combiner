using System.Collections.ObjectModel;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Focused Settings catalog/status presentation.</summary>
public sealed class SettingsViewModel
{
    private readonly string _appVersion;

    internal SettingsViewModel(string appVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);
        _appVersion = appVersion;
    }

    /// <summary>Version and supported-workflow facts shown on Settings.</summary>
    public ObservableCollection<SettingSummaryViewModel> OverviewRows { get; } = [];

    /// <summary>Catalog capability and external-tool facts shown on Settings.</summary>
    public ObservableCollection<SettingSummaryViewModel> CapabilityRows { get; } = [];

    /// <summary>Theme choices rendered by the global shell preference selector.</summary>
    public IReadOnlyList<string> ThemeChoices { get; } = ["System", "Light", "Dark"];

    /// <summary>Language choices rendered by the global shell preference selector.</summary>
    public IReadOnlyList<string> LanguageChoices { get; } = ["English", "Traditional Chinese"];

    internal void Refresh(ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(text);
        WorkbenchSettingsSnapshot snapshot = WorkbenchCompositionService.GetSettingsSnapshot();
        bool chinese = text.Language == ShellLanguage.ChineseTraditional;

        ReplaceRows(
            OverviewRows,
            [
                new SettingSummaryViewModel(
                    chinese ? "App 版本" : "App version",
                    _appVersion,
                    chinese ? "目前安裝套件的應用程式版本。" : "Application version in the installed package.",
                    chinese ? "目前版本" : "Current"),
                new SettingSummaryViewModel(
                    chinese ? "IC 目錄" : "IC catalog",
                    $"{snapshot.CatalogIcCount}",
                    chinese
                        ? "共用選擇器中的 IC 項目；各 workflow availability 仍分開判定。"
                        : "IC entries in the shared selector; workflow availability is evaluated separately.",
                    "Catalog"),
                new SettingSummaryViewModel(
                    "Standard Merge",
                    $"{snapshot.StandardMergeProfileCount} profiles",
                    chinese
                        ? "已編譯並可執行的 V2 Standard Merge profiles。"
                        : "Compiled executable V2 Standard Merge profiles.",
                    chinese ? "可執行" : "Executable"),
                new SettingSummaryViewModel(
                    "DP Replace",
                    $"{snapshot.DpReplaceProfileCount} profiles",
                    chinese
                        ? "已編譯並可執行的 V2 DP Replace profiles。"
                        : "Compiled executable V2 DP Replace profiles.",
                    chinese ? "可執行" : "Executable"),
            ]);

        ReplaceRows(
            CapabilityRows,
            [
                new SettingSummaryViewModel(
                    chinese ? "CtrlRAM Replace 可用 IC" : "CtrlRAM Replace available ICs",
                    $"{snapshot.CtrlRamReplaceAvailableIcCount} ICs",
                    chinese
                        ? "已有 executable/safety contract；golden 驗證狀態由各 workflow 分開顯示。"
                        : "An executable/safety contract exists; golden verification remains a separate per-workflow status.",
                    chinese ? "可用" : "Available"),
            ]);
    }

    private static void ReplaceRows<T>(ObservableCollection<T> target, IEnumerable<T> rows)
    {
        target.Clear();
        foreach (T row in rows)
        {
            target.Add(row);
        }
    }
}
