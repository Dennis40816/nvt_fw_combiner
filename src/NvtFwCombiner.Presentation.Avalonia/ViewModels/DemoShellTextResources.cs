namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Supported demo-shell text languages.</summary>
public enum DemoShellLanguage
{
    /// <summary>English UI text.</summary>
    English,

    /// <summary>Traditional Chinese UI text.</summary>
    ChineseTraditional,
}

/// <summary>Localized text bundle for the 0.1.1 demo shell.</summary>
public sealed class DemoShellTextResources
{
    private DemoShellTextResources(
        string shellVersion,
        string workspaceTitle,
        string workspaceSummary,
        string previewActionLabel,
        string buildActionLabel,
        string reportModalActionLabel,
        IReadOnlyList<string> navigationItems,
        PlanningCardText mergePreview,
        PlanningCardText replacePreview,
        PlanningCardText reportsPreview,
        PlanningCardText reportModalPreview,
        string footerStatus)
    {
        ShellVersion = shellVersion;
        WorkspaceTitle = workspaceTitle;
        WorkspaceSummary = workspaceSummary;
        PreviewActionLabel = previewActionLabel;
        BuildActionLabel = buildActionLabel;
        ReportModalActionLabel = reportModalActionLabel;
        NavigationItems = navigationItems;
        MergePreview = mergePreview;
        ReplacePreview = replacePreview;
        ReportsPreview = reportsPreview;
        ReportModalPreview = reportModalPreview;
        FooterStatus = footerStatus;
    }

    /// <summary>Gets the resource bundle for a language.</summary>
    public static DemoShellTextResources For(DemoShellLanguage language)
    {
        return language switch
        {
            DemoShellLanguage.English => CreateEnglish(),
            DemoShellLanguage.ChineseTraditional => CreateChineseTraditional(),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
        };
    }

    /// <summary>Gets the shell milestone label.</summary>
    public string ShellVersion { get; }

    /// <summary>Gets the workspace title.</summary>
    public string WorkspaceTitle { get; }

    /// <summary>Gets the workspace summary.</summary>
    public string WorkspaceSummary { get; }

    /// <summary>Gets the preview action label.</summary>
    public string PreviewActionLabel { get; }

    /// <summary>Gets the build action label.</summary>
    public string BuildActionLabel { get; }

    /// <summary>Gets the report modal action label.</summary>
    public string ReportModalActionLabel { get; }

    /// <summary>Gets localized top tab labels.</summary>
    public IReadOnlyList<string> NavigationItems { get; }

    /// <summary>Gets merge preview text.</summary>
    public PlanningCardText MergePreview { get; }

    /// <summary>Gets replace preview text.</summary>
    public PlanningCardText ReplacePreview { get; }

    /// <summary>Gets report preview text.</summary>
    public PlanningCardText ReportsPreview { get; }

    /// <summary>Gets report modal preview text.</summary>
    public PlanningCardText ReportModalPreview { get; }

    /// <summary>Gets footer status text.</summary>
    public string FooterStatus { get; }

    private static DemoShellTextResources CreateEnglish()
    {
        return new DemoShellTextResources(
            "0.1.1 demo shell",
            "UI planning workspace",
            "Synthetic preview only. No firmware files are read and no external tool is executed.",
            "Preview unavailable until 0.2.0",
            "Build disabled until 0.2.0",
            "Open report modal",
            ["Settings", "Merge", "Replace"],
            new PlanningCardText(
                "Merge preview",
                "Modes: Standard / AB / General (AB deferred)",
                [
                    "Profile selector: demo-standard-merge",
                    "NT51950/NT51951: map pending from owner",
                    "Slot cards: DP demo.bin, TP demo.bin, optional LD placeholder",
                    "Preview: visual-first shared Memory coverage before/after supplied by application core later",
                ],
                "Status: synthetic data, build blocked"),
            new PlanningCardText(
                "Replace preview",
                "Personas: Display / TP HW / TP FW / General",
                [
                    "IC num selector/input: required before region choices",
                    "Display: DP declared partitions and TP whole only; CtrlRAM hidden",
                    "TP HW: CtrlRAM only; TP firmware regions denied",
                    "Post-replace CRC/header: legacy combiner.exe transform planned",
                    "TP FW: non-CtrlRAM TP firmware regions only; CtrlRAM denied",
                    "General: profile-declared explicit ranges only; protected regions denied",
                    "Preview: visual-first shared Memory coverage before/after and protected warnings",
                ],
                "Status: access policy display only"),
            new PlanningCardText(
                "Reports preview",
                "Secondary surfaces in this shell",
                [
                    "Reports: opened from Preview/Build report modals",
                    "Settings exposes diagnostics configuration and export only",
                    "Report export includes runId, output hash, diagnostics, and sanitized logs.",
                ],
                "Status: report schema wiring arrives after core execution"),
            new PlanningCardText(
                "Report modal preview",
                "Opened after Preview or Build",
                [
                    "Run summary, output hash, mutation summary",
                    "Validation issues and sanitized logs stay inside the modal",
                    "Copy/export actions are scoped to the current runId",
                ],
                "Status: modal trigger disabled until preview core is wired"),
            "Profile catalog: demo only | Validation: preview unavailable | Report modal: planned | Firmware mutation: none");
    }

    private static DemoShellTextResources CreateChineseTraditional()
    {
        return new DemoShellTextResources(
            "0.1.1 展示殼層",
            "UI 規劃工作區",
            "僅使用合成預覽。不讀取韌體檔案，也不執行外部工具。",
            "Preview 於 0.2.0 前暫不可用",
            "Build 於 0.2.0 前停用",
            "開啟報告視窗",
            ["設定", "合併", "取代"],
            new PlanningCardText(
                "合併預覽",
                "模式：Standard / AB / General（AB deferred）",
                [
                    "Profile selector：demo-standard-merge",
                    "NT51950/NT51951：等待 owner 提供 memory map",
                    "Slot cards：DP demo.bin、TP demo.bin、選用 LD placeholder",
                    "Preview：視覺優先的共用 Memory 覆蓋前後圖，之後由 application core 提供",
                ],
                "狀態：合成資料，Build 停用"),
            new PlanningCardText(
                "取代預覽",
                "情境：Display / TP HW / TP FW / General",
                [
                    "IC num selector/input：選擇後才顯示 region choices",
                    "Display：只允許 DP 宣告分區與完整 TP；CtrlRAM 隱藏",
                    "TP HW：只允許 CtrlRAM；拒絕 TP firmware regions",
                    "Post-replace CRC/header：規劃使用 legacy combiner.exe transform",
                    "TP FW：只允許非 CtrlRAM 的 TP firmware regions；拒絕 CtrlRAM",
                    "General：只允許 profile 宣告的明確 ranges；拒絕 protected regions",
                    "Preview：視覺優先的共用 Memory 覆蓋前後圖與 protected warnings",
                ],
                "狀態：僅顯示 access policy"),
            new PlanningCardText(
                "報告預覽",
                "此 shell 的次要介面",
                [
                    "Reports：由 Preview/Build report modal 開啟",
                    "Settings 只提供 diagnostics configuration 與 export",
                    "Report export 包含 runId、output hash、diagnostics 與 sanitized logs。",
                ],
                "狀態：report schema 會在 core execution 後接線"),
            new PlanningCardText(
                "報告視窗預覽",
                "Preview 或 Build 後開啟",
                [
                    "Run summary、output hash、mutation summary",
                    "Validation issues 與 sanitized logs 留在 modal 內",
                    "Copy/export actions 綁定目前 runId",
                ],
                "狀態：modal trigger 等 preview core 接線後啟用"),
            "Profile catalog：demo only | Validation：preview unavailable | Report modal：planned | Firmware mutation：none");
    }
}

/// <summary>Localized text for a planning card.</summary>
public sealed class PlanningCardText
{
    /// <summary>Initializes localized planning-card text.</summary>
    public PlanningCardText(string title, string subtitle, IReadOnlyList<string> rows, string status)
    {
        Title = title;
        Subtitle = subtitle;
        Rows = rows;
        Status = status;
    }

    /// <summary>Gets the card title.</summary>
    public string Title { get; }

    /// <summary>Gets the card subtitle.</summary>
    public string Subtitle { get; }

    /// <summary>Gets the card detail rows.</summary>
    public IReadOnlyList<string> Rows { get; }

    /// <summary>Gets the card status.</summary>
    public string Status { get; }
}
