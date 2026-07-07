namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Supported shell text languages.</summary>
public enum ShellLanguage
{
    /// <summary>English UI text.</summary>
    English,

    /// <summary>Traditional Chinese UI text.</summary>
    ChineseTraditional,
}

/// <summary>Localized text bundle for the production-backed UI shell.</summary>
public sealed class ShellTextResources
{
    private ShellTextResources(
        string workspaceTitle,
        string workspaceSummary,
        string previewActionLabel,
        string buildActionLabel,
        string reportModalActionLabel,
        string deviceContextTitle,
        string icLabel,
        string numberLabel,
        string deviceContextStatus,
        PlanningCardText settingsPreview,
        PlanningCardText mergePreview,
        PlanningCardText replacePreview,
        string footerStatus)
    {
        WorkspaceTitle = workspaceTitle;
        WorkspaceSummary = workspaceSummary;
        PreviewActionLabel = previewActionLabel;
        BuildActionLabel = buildActionLabel;
        ReportModalActionLabel = reportModalActionLabel;
        DeviceContextTitle = deviceContextTitle;
        IcLabel = icLabel;
        NumberLabel = numberLabel;
        DeviceContextStatus = deviceContextStatus;
        SettingsPreview = settingsPreview;
        MergePreview = mergePreview;
        ReplacePreview = replacePreview;
        FooterStatus = footerStatus;
    }

    /// <summary>Gets the resource bundle for a language.</summary>
    public static ShellTextResources For(ShellLanguage language)
    {
        return language switch
        {
            ShellLanguage.English => CreateEnglish(),
            ShellLanguage.ChineseTraditional => CreateChineseTraditional(),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
        };
    }

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

    /// <summary>Gets the IC count/variant field label.</summary>
    public string NumberLabel { get; }

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

    private static ShellTextResources CreateEnglish()
    {
        return new ShellTextResources(
            "Merge / Replace workspace",
            "Production-backed shell with built-in profiles, flash-map catalog, and report review.",
            "Preview",
            "Build",
            "Report",
            "Device context",
            "IC",
            "Number",
            "refresh profile, slots, validation",
            new PlanningCardText(
                "Settings",
                "Configure the app before running firmware workflows.",
                [
                    "Catalog and profile status",
                    "Tool binding and report access",
                    "Theme, strictness, and language preference",
                ],
                "Production-backed settings"),
            new PlanningCardText(
                "Merge",
                "Standard Merge workflow.",
                [
                    "Profile: built-in Standard Merge",
                    "Slots: DP, TP, optional LD",
                    "950/951 TP: 0x0A000-0x36FFF (len 0x2D000)",
                ],
                "Build wired"),
            new PlanningCardText(
                "Replace",
                "DP, CtrlRAM, and General policies.",
                [
                    "Device context: shared IC and Number",
                    "DP Replace includes separate DP and LD payloads",
                    "CtrlRAM Replace uses approved CtrlRAM regions",
                    "CRC/header: combiner.exe postbuild core",
                ],
                "Build wired; CtrlRAM postbuild enabled"),
            "Profile catalog: built-in | Merge build: wired | Replace build wired; CtrlRAM postbuild enabled | Settings: catalog-backed");
    }

    private static ShellTextResources CreateChineseTraditional()
    {
        return new ShellTextResources(
            "合併 / 取代工作區",
            "使用內建 profile、flash-map catalog 與 report review 的生產導向介面。",
            "Preview",
            "Build",
            "Report",
            "Device context",
            "IC",
            "Number",
            "刷新 profile、slot、validation",
            new PlanningCardText(
                "設定",
                "執行韌體流程前的 app 設定。",
                [
                    "Catalog 與 profile 狀態",
                    "Tool binding 與 report access",
                    "Theme、strictness 與 language preference",
                ],
                "Production-backed settings"),
            new PlanningCardText(
                "合併",
                "Standard Merge 流程。",
                [
                    "Profile：built-in Standard Merge",
                    "Slots：DP、TP、選用 LD",
                    "950/951 TP：0x0A000-0x36FFF (len 0x2D000)",
                ],
                "Build wired"),
            new PlanningCardText(
                "取代",
                "DP、CtrlRAM、General policy。",
                [
                    "Device context：共用 IC 與 Number",
                    "DP Replace 包含分開的 DP 與 LD payload",
                    "CtrlRAM Replace 使用核准的 CtrlRAM regions",
                    "CRC/header：combiner.exe postbuild core",
                ],
                "Build wired；CtrlRAM postbuild enabled"),
            "Profile catalog：built-in | Merge build：wired | Replace build wired；CtrlRAM postbuild enabled | Settings：catalog-backed");
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
