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
        string deviceContextTitle,
        string icLabel,
        string icNumberLabel,
        string icNumberModeLabel,
        string deviceContextStatus,
        PlanningCardText settingsPreview,
        PlanningCardText mergePreview,
        PlanningCardText replacePreview,
        string footerStatus)
    {
        ShellVersion = shellVersion;
        WorkspaceTitle = workspaceTitle;
        WorkspaceSummary = workspaceSummary;
        PreviewActionLabel = previewActionLabel;
        BuildActionLabel = buildActionLabel;
        ReportModalActionLabel = reportModalActionLabel;
        DeviceContextTitle = deviceContextTitle;
        IcLabel = icLabel;
        IcNumberLabel = icNumberLabel;
        IcNumberModeLabel = icNumberModeLabel;
        DeviceContextStatus = deviceContextStatus;
        SettingsPreview = settingsPreview;
        MergePreview = mergePreview;
        ReplacePreview = replacePreview;
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

    /// <summary>Gets the shared device context heading.</summary>
    public string DeviceContextTitle { get; }

    /// <summary>Gets the IC field label.</summary>
    public string IcLabel { get; }

    /// <summary>Gets the IC number field label.</summary>
    public string IcNumberLabel { get; }

    /// <summary>Gets the IC number mode field label.</summary>
    public string IcNumberModeLabel { get; }

    /// <summary>Gets the shared device context status text.</summary>
    public string DeviceContextStatus { get; }

    /// <summary>Gets settings preview text.</summary>
    public PlanningCardText SettingsPreview { get; }

    /// <summary>Gets merge preview text.</summary>
    public PlanningCardText MergePreview { get; }

    /// <summary>Gets replace preview text.</summary>
    public PlanningCardText ReplacePreview { get; }

    /// <summary>Gets footer status text.</summary>
    public string FooterStatus { get; }

    private static DemoShellTextResources CreateEnglish()
    {
        return new DemoShellTextResources(
            "0.1.1 demo shell",
            "Merge / Replace workspace",
            "Synthetic demo state. File execution is blocked until core wiring is connected.",
            "Preview",
            "Build",
            "Report",
            "Device context",
            "IC",
            "IC Num",
            "Mode",
            "Shared by Settings, Replace, and Merge.",
            new PlanningCardText(
                "Settings",
                "Configure the app before running firmware workflows.",
                [
                    "Profile catalog",
                    "Tool folders",
                    "Diagnostics export",
                ],
                "Demo settings only"),
            new PlanningCardText(
                "Merge",
                "Normal merge first. AB Code is disabled.",
                [
                    "Profile: demo-standard-merge",
                    "Slots: DP, TP, optional LD",
                    "950/951 maps pending",
                ],
                "Build blocked"),
            new PlanningCardText(
                "Replace",
                "DP, CtrlRAM, and General policies.",
                [
                    "Device context: shared IC and IC Num",
                    "DP Replace includes separate DP and LD payloads",
                    "CtrlRAM Replace uses approved CtrlRAM regions",
                    "CRC/header waits for combiner.exe details",
                ],
                "Policy display only"),
            "Profile catalog: demo | Preview: blocked | Report modal: planned | Firmware mutation: none");
    }

    private static DemoShellTextResources CreateChineseTraditional()
    {
        return new DemoShellTextResources(
            "0.1.1 展示殼層",
            "合併 / 取代工作區",
            "合成展示狀態。Core 接線前不執行檔案流程。",
            "Preview",
            "Build",
            "Report",
            "Device context",
            "IC",
            "IC Num",
            "Mode",
            "Settings、Replace、Merge 共用。",
            new PlanningCardText(
                "設定",
                "執行韌體流程前的 app 設定。",
                [
                    "Profile catalog",
                    "Tool folders",
                    "Diagnostics export",
                ],
                "Demo settings only"),
            new PlanningCardText(
                "合併",
                "先支援 Normal merge。AB Code 停用。",
                [
                    "Profile：demo-standard-merge",
                    "Slots：DP、TP、選用 LD",
                    "950/951 maps pending",
                ],
                "Build blocked"),
            new PlanningCardText(
                "取代",
                "DP、CtrlRAM、General policy。",
                [
                    "Device context：共用 IC 與 IC Num",
                    "DP Replace 包含分開的 DP 與 LD payload",
                    "CtrlRAM Replace 使用核准的 CtrlRAM regions",
                    "CRC/header 等待 combiner.exe 細節",
                ],
                "Policy display only"),
            "Profile catalog：demo | Preview：blocked | Report modal：planned | Firmware mutation：none");
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
