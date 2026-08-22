using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Localizes stable Application activity entries without owning history state.</summary>
internal static class SystemActivityText
{
    internal static string FormatSessionActivitySummary(this ShellTextResources text, int count)
    {
        return Pick(text, $"This session · {count} events", $"本次工作階段 · {count} 個事件");
    }

    internal static string GetSystemActivityCategory(
        this ShellTextResources text,
        SystemActivityCategory category)
    {
        return category switch
        {
            SystemActivityCategory.Session => Pick(text, "Session", "工作階段"),
            SystemActivityCategory.Diagnostics => Pick(text, "Diagnostics", "診斷"),
            SystemActivityCategory.Navigation => Pick(text, "Navigation", "導覽"),
            SystemActivityCategory.Workflow => Pick(text, "Workflow", "工作流程"),
            SystemActivityCategory.Input => Pick(text, "Input", "輸入"),
            SystemActivityCategory.Composition => Pick(text, "Composition", "合成"),
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };
    }

    internal static string GetSystemActivityStatus(
        this ShellTextResources text,
        SystemActivitySeverity severity)
    {
        return severity switch
        {
            SystemActivitySeverity.Information => Pick(text, "Info", "資訊"),
            SystemActivitySeverity.Success => Pick(text, "Completed", "已完成"),
            SystemActivitySeverity.Warning => Pick(text, "Attention", "注意"),
            SystemActivitySeverity.Error => Pick(text, "Failed", "失敗"),
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };
    }

    internal static string GetSystemActivityTitle(
        this ShellTextResources text,
        SystemActivityEntry activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return activity.Code switch
        {
            SystemActivityCodes.ApplicationStarted => Pick(text, "Application started", "應用程式已啟動"),
            SystemActivityCodes.DiagnosticActivated => Pick(text, "System issue detected", "偵測到系統問題"),
            SystemActivityCodes.DiagnosticResolved => Pick(text, "System issue resolved", "系統問題已解除"),
            SystemActivityCodes.SystemRefreshed => Pick(text, "System information refreshed", "系統資訊已重新整理"),
            SystemActivityCodes.UserNavigated => Pick(text, "Page changed", "已切換頁面"),
            SystemActivityCodes.SettingsOpened => Pick(text, "Settings opened", "已開啟設定"),
            SystemActivityCodes.MessageCenterOpened => Pick(text, "Message Center opened", "已開啟訊息中心"),
            SystemActivityCodes.ModeSelected => Pick(text, "Mode selected", "已選擇模式"),
            SystemActivityCodes.IcSelected => Pick(text, "IC selected", "已選擇 IC"),
            SystemActivityCodes.NumberSelected => Pick(text, "Firmware number selected", "已選擇韌體編號"),
            SystemActivityCodes.InputSelected => Pick(text, "Input selected", "已選擇輸入檔"),
            SystemActivityCodes.PreviewStarted => Pick(text, "Preview started", "預覽已開始"),
            SystemActivityCodes.BuildStarted => Pick(text, "Build started", "Build 已開始"),
            SystemActivityCodes.PreviewCompleted => Pick(text, "Preview completed", "預覽已完成"),
            SystemActivityCodes.BuildCompleted => Pick(text, "Build completed", "Build 已完成"),
            SystemActivityCodes.PreviewFailed => Pick(text, "Preview needs attention", "預覽需要處理"),
            SystemActivityCodes.BuildFailed => Pick(text, "Build failed", "Build 失敗"),
            SystemActivityCodes.DiagnosticsRefreshRequested => Pick(text, "Refresh requested", "已要求重新整理"),
            SystemActivityCodes.DiagnosticsExported => Pick(text, "Activity exported", "活動紀錄已匯出"),
            SystemActivityCodes.DiagnosticsExportFailed => Pick(text, "Activity export failed", "活動紀錄匯出失敗"),
            _ => activity.Code,
        };
    }

    internal static string GetSystemActivityDetail(
        this ShellTextResources text,
        SystemActivityEntry activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        string tokens = string.Join(" · ", new[] { activity.SubjectId, activity.ContextId }
            .Where(static value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrEmpty(tokens)
            ? Pick(text, "No additional context.", "沒有其他內容。")
            : tokens;
    }

    private static string Pick(
        ShellTextResources text,
        string english,
        string traditionalChinese)
    {
        return text.Language == ShellLanguage.ChineseTraditional
            ? traditionalChinese
            : english;
    }
}
