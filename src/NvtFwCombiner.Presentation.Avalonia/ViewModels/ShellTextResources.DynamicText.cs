// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ShellTextResources
{
    public string GetThemePreferenceStatus(string selectedTheme)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? selectedTheme switch
            {
                "System" => "跟隨作業系統主題。",
                "Light" => "目前視窗已套用亮色主題。",
                "Dark" => "目前視窗已套用暗色主題。",
                "High contrast" => "目前使用暗色視覺變體；韌體 gate 不受影響。",
                _ => "主題偏好已儲存在本機。",
            }
            : selectedTheme switch
            {
                "System" => "Follows the operating-system theme.",
                "Light" => "Light theme is applied to this window.",
                "Dark" => "Dark theme is applied to this window.",
                "High contrast" => "Uses the dark visual variant; firmware gates are unchanged.",
                _ => "Theme preference is saved locally.",
            };
    }

    public string GetStrictnessPreferenceStatus(string selectedStrictness)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? selectedStrictness switch
            {
                "Strict" => "Preview/Build 維持 fail-closed；所有阻擋問題都必須修正。",
                "Warn only" => "只調整 UI review 語氣；韌體 gate 仍維持 fail-closed。",
                _ => "審查嚴格度偏好已儲存在本機。",
            }
            : selectedStrictness switch
            {
                "Strict" => "Preview/Build stays fail-closed; blocking issues must be fixed.",
                "Warn only" => "Changes only the UI review tone; firmware gates still fail closed.",
                _ => "Review strictness preference is saved locally.",
            };
    }

    public string GetLanguagePreferenceStatus(string selectedLanguage)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? selectedLanguage switch
            {
                "Traditional Chinese" => "繁體中文介面已套用並會在啟動時還原。",
                "English" => "英文介面已套用並會在啟動時還原。",
                _ => "語言偏好已儲存在本機。",
            }
            : selectedLanguage switch
            {
                "Traditional Chinese" => "Traditional Chinese shell resources are active and restored on startup.",
                "English" => "English shell resources are active and restored on startup.",
                _ => "Language preference is saved locally.",
            };
    }

    public string GetReplaceModeDescription(string mode)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? mode switch
            {
                "DP" => "取代 DP 與選用 LD payload；不執行 CRC postbuild。",
                "CtrlRAM" => "取代 CtrlRAM payload 後執行 combiner.exe postbuild 更新 CRC/header。",
                "General" => "編輯 profile 核准的明確範圍；碰到 TP 範圍時必須執行 combiner.exe CRC/header 更新。",
                _ => "選擇一個 Replace 模式。",
            }
            : mode switch
            {
                "DP" => "Replace DP and optional LD payloads without CRC postbuild.",
                "CtrlRAM" => "Replace CtrlRAM payloads, then run combiner.exe postbuild for CRC/header refresh.",
                "General" => "Author explicit profile-approved ranges; TP ranges require combiner.exe CRC/header refresh.",
                _ => "Select a replace mode.",
            };
    }

    public string GetReplaceMemorySummary(string mode)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? mode switch
            {
                "DP" => "藍色代表新的 DP bytes；灰色代表從 base firmware 保留或還原的區段。",
                "CtrlRAM" => "有色區塊代表可取代的 CtrlRAM 位置；灰色保留 base firmware。",
                "General" => "Base flash 只會在核准的明確取代範圍內改變。",
                _ => "選擇 Replace 模式後查看目標範圍。",
            }
            : mode switch
            {
                "DP" => "Blue shows new DP bytes; gray shows sections preserved or restored from the base firmware.",
                "CtrlRAM" => "Colored blocks show replaceable CtrlRAM positions; gray stays from the base firmware.",
                "General" => "Base flash stays unchanged except approved explicit replacement ranges.",
                _ => "Select a replace mode to inspect its target ranges.",
            };
    }

    public string GetReplaceReadinessStatus(string mode, bool canRun)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? mode switch
            {
                "DP" when canRun => "Ready：Build 會先驗證 DP Replace input，再寫出 output 與 report。",
                "DP" => "Build blocked：需要 base BIN 與必要的 DP replacement input。",
                "CtrlRAM" when canRun => "Ready：Build 會取代選定的 CtrlRAM region 並執行 postbuild。",
                "CtrlRAM" => "Build blocked：需要 base BIN 與至少一個 CtrlRAM region BIN。",
                "General" when canRun => "Ready：Build 會編譯明確 mapping；碰到 TP range 時會執行 postbuild。",
                "General" => "Build blocked：需要 base BIN 與至少一筆 replacement mapping。",
                _ => "Build blocked：請選擇 Replace 模式。",
            }
            : mode switch
            {
                "DP" when canRun => "Ready: Build will validate DP Replace inputs, then write output and report.",
                "DP" => "Build blocked: base BIN and required DP replacement inputs are required.",
                "CtrlRAM" when canRun => "Ready: Build will replace selected CtrlRAM regions and run postbuild.",
                "CtrlRAM" => "Build blocked: base BIN and at least one CtrlRAM region BIN are required.",
                "General" when canRun => "Ready: Build will compile explicit mappings and run postbuild when TP ranges are touched.",
                "General" => "Build blocked: base BIN and at least one explicit replacement mapping are required.",
                _ => "Build blocked: select a Replace mode.",
            };
    }

    public string GetMergeMemorySummary(string mode, bool isStandardMergeSupported, bool hasGeneralMapping)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? mode switch
            {
                "Normal" when isStandardMergeSupported => "此圖顯示每個最終 flash 位置由哪個 input file 寫入。",
                "Normal" => "所選 IC 尚未有 Merge profile。",
                "General" when hasGeneralMapping => "輸出先以 reserved byte 初始化，再標出每筆明確 source mapping 寫入的位置。",
                "General" => "輸出先以 reserved byte 初始化；新增 mapping 後會標出寫入位置。",
                _ => "此 Merge 模式保留中。",
            }
            : mode switch
            {
                "Normal" when isStandardMergeSupported => "The bar shows which input file occupies each final flash position.",
                "Normal" => "No merge profile is available for the selected IC.",
                "General" => "The bar starts reserved and marks each explicit source mapping written into the output.",
                _ => "This merge mode is reserved.",
            };
    }

    public string GetStandardMergeSupportSummary(string ic, bool supported, string requiredSlots)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? supported
                ? $"{ic}：找到 Standard Merge profile。必要 slots：{requiredSlots}。"
                : $"{ic}：尚未提供 Standard Merge profile。"
            : supported
                ? $"{ic}: Standard Merge profile found. Required slots: {requiredSlots}."
                : $"{ic}: no Standard Merge profile yet.";
    }

    public string GetMergeReadinessStatus(string mode, string ic, string requiredSlots, bool isStandardMergeSupported, int generalMappingFileCount)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? mode switch
            {
                "Normal" when isStandardMergeSupported => $"{ic}：放入 {requiredSlots} BIN files。",
                "Normal" => $"{ic}：Standard Merge 尚未可用。",
                "General" when generalMappingFileCount > 0 => $"{ic}：General Merge 會將 {generalMappingFileCount} 個 source BIN mapping 寫入 blank output。",
                "General" => $"{ic}：至少新增一筆 source BIN mapping。",
                _ => "AB Code Merge 保留給後續流程。",
            }
            : mode switch
            {
                "Normal" when isStandardMergeSupported => $"{ic}: drop {requiredSlots} BIN files.",
                "Normal" => $"{ic}: Standard Merge is not available yet.",
                "General" when generalMappingFileCount > 0 => $"{ic}: General Merge maps {generalMappingFileCount} source BIN file(s) into a blank output.",
                "General" => $"{ic}: add at least one source BIN mapping.",
                _ => "AB Code Merge is reserved for a later workflow.",
            };
    }

    public string GetCtrlRamRegionSummary(string ic, string number)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? string.Equals(number, "single", StringComparison.OrdinalIgnoreCase)
                ? $"{ic} single：需要 multi-chip context 的 TP Overview regions 已隱藏。"
                : $"{ic} {number}：TP Overview CtrlRAM regions 由 production flash-map catalog 載入。"
            : string.Equals(number, "single", StringComparison.OrdinalIgnoreCase)
                ? $"{ic} single: TP Overview regions that require multi-chip context are hidden."
                : $"{ic} {number}: TP Overview CtrlRAM regions are loaded from the production flash-map catalog.";
    }

    public string GetBuildActionTip(string readinessStatus, bool canBuild)
    {
        return canBuild
            ? Language == ShellLanguage.ChineseTraditional
                ? $"{readinessStatus} Build 會先驗證，再寫出 output 與 report。"
                : $"{readinessStatus} Build validates first, then writes output and report."
            : readinessStatus;
    }

    public string GetOpenReportForDetailsSentence()
    {
        return Language == ShellLanguage.ChineseTraditional
            ? "開啟 report 查看詳細內容。"
            : "Open report for details.";
    }

    public string GetReportHistorySummary(int count)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? count switch
            {
                <= 0 => "目前沒有 report history",
                1 => "history 中有 1 份 report",
                _ => $"history 中有 {count} 份 report",
            }
            : count switch
            {
                <= 0 => "No reports in history",
                1 => "1 report in history",
                _ => $"{count} reports in history",
            };
    }

    public string GetReportHistoryStorageSummary(string byteCount)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? $"{byteCount} 儲存在本機"
            : $"{byteCount} stored locally";
    }

    public string GetReportHistoryStorageWarning(string total, string limit)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? $"History 使用 {total}，已超過 {limit} 限制。清除 history 可維持本機 UI 狀態精簡。"
            : $"History uses {total}, above the {limit} limit. Clear history to keep local UI state small.";
    }

    public string GetReportActionLabel(bool hasLoadedReport)
    {
        return hasLoadedReport
            ? Language == ShellLanguage.ChineseTraditional ? "開啟 report" : "Open report"
            : Language == ShellLanguage.ChineseTraditional ? "尚無 report" : "No report";
    }

    public string GetReportActionStatus(bool hasLoadedReport, string loadedStatus)
    {
        return hasLoadedReport
            ? loadedStatus
            : Language == ShellLanguage.ChineseTraditional ? "Build 後產生" : "Build creates one";
    }

    public string FormatReportLoadedToast(string sourceName)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? $"Report loaded：{sourceName}"
            : $"Report loaded: {sourceName}";
    }

    public string FormatReportIssueToast(string sourceName)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? $"Report issue：{sourceName}"
            : $"Report issue: {sourceName}";
    }

    public string FormatReportSavedToast(string destinationName)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? $"Report saved：{destinationName}"
            : $"Report saved: {destinationName}";
    }

    public string FormatReportGeneratedToast(string action)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? $"{action} report 已產生"
            : $"{action} report generated";
    }
}

#pragma warning restore CS1591
