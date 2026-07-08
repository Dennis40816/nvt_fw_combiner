// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

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
public sealed partial class ShellTextResources
{
    private static readonly PlanningCardText EmptyPlanningCard = new(string.Empty, string.Empty, [], string.Empty);

    private ShellTextResources()
    {
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

    /// <summary>Converts the persisted language preference into a resource language.</summary>
    public static ShellLanguage LanguageFromPreference(string? preference)
    {
        return string.Equals(preference, "Traditional Chinese", StringComparison.Ordinal)
            ? ShellLanguage.ChineseTraditional
            : ShellLanguage.English;
    }

    /// <summary>Gets the persisted language preference token for a resource language.</summary>
    public static string PreferenceFromLanguage(ShellLanguage language)
    {
        return language == ShellLanguage.ChineseTraditional ? "Traditional Chinese" : "English";
    }

    /// <summary>Gets the language represented by this bundle.</summary>
    public ShellLanguage Language { get; private init; }

    /// <summary>Gets the product title.</summary>
    public string ProductTitle { get; private init; } = "NVT FW Combiner";

    /// <summary>Gets the Home navigation label.</summary>
    public string HomeLabel { get; private init; } = string.Empty;

    /// <summary>Gets the workspace title.</summary>
    public string WorkspaceTitle { get; private init; } = string.Empty;

    /// <summary>Gets the workspace summary.</summary>
    public string WorkspaceSummary { get; private init; } = string.Empty;

    /// <summary>Gets the preview action label.</summary>
    public string PreviewActionLabel { get; private init; } = string.Empty;

    /// <summary>Gets the build action label.</summary>
    public string BuildActionLabel { get; private init; } = string.Empty;

    /// <summary>Gets the report modal action label.</summary>
    public string ReportModalActionLabel { get; private init; } = string.Empty;

    /// <summary>Gets the shared device context heading.</summary>
    public string DeviceContextTitle { get; private init; } = string.Empty;

    /// <summary>Gets the IC field label.</summary>
    public string IcLabel { get; private init; } = string.Empty;

    /// <summary>Gets the IC count/variant field label.</summary>
    public string NumberLabel { get; private init; } = string.Empty;

    /// <summary>Gets the shared device context status text.</summary>
    public string DeviceContextStatus { get; private init; } = string.Empty;

    /// <summary>Gets settings preview text.</summary>
    public PlanningCardText SettingsPreview { get; private init; } = EmptyPlanningCard;

    /// <summary>Gets merge preview text.</summary>
    public PlanningCardText MergePreview { get; private init; } = EmptyPlanningCard;

    /// <summary>Gets replace preview text.</summary>
    public PlanningCardText ReplacePreview { get; private init; } = EmptyPlanningCard;

    /// <summary>Gets footer status text.</summary>
    public string FooterStatus { get; private init; } = string.Empty;

    public string InitialRunTitle { get; private init; } = string.Empty;

    public string InitialRunDetail { get; private init; } = string.Empty;

    public string NoOutputLabel { get; private init; } = string.Empty;

    public string ConfigureKicker { get; private init; } = string.Empty;

    public string WorkflowKicker { get; private init; } = string.Empty;

    public string OpenSettingsLabel { get; private init; } = string.Empty;

    public string OpenLabel { get; private init; } = string.Empty;

    public string PendingLabel { get; private init; } = string.Empty;

    public string LoadJsonLabel { get; private init; } = string.Empty;

    public string LoadJsonTooltip { get; private init; } = string.Empty;

    public string BackTooltip { get; private init; } = string.Empty;

    public string ModeLabel { get; private init; } = string.Empty;

    public string TargetsLabel { get; private init; } = string.Empty;

    public string ReviewReplacementInputsTooltip { get; private init; } = string.Empty;

    public string InputFilesTitle { get; private init; } = string.Empty;

    public string OutputLayoutTitle { get; private init; } = string.Empty;

    public string PlanTitle { get; private init; } = string.Empty;

    public string ValidationTitle { get; private init; } = string.Empty;

    public string GeneralReplaceMappingTitle { get; private init; } = string.Empty;

    public string GeneralMergeMappingTitle { get; private init; } = string.Empty;

    public string ExplicitMappingsTitle { get; private init; } = string.Empty;

    public string AddRangeLabel { get; private init; } = string.Empty;

    public string AddMappingLabel { get; private init; } = string.Empty;

    public string StartLabel { get; private init; } = string.Empty;

    public string EndLabel { get; private init; } = string.Empty;

    public string SourceStartLabel { get; private init; } = string.Empty;

    public string TargetStartLabel { get; private init; } = string.Empty;

    public string LengthLabel { get; private init; } = string.Empty;

    public string SourceBinLabel { get; private init; } = string.Empty;

    public string ReplacementBinLabel { get; private init; } = string.Empty;

    public string OutputLengthLabel { get; private init; } = string.Empty;

    public string BrowseLabel { get; private init; } = string.Empty;

    public string RequiredLabel { get; private init; } = string.Empty;

    public string OptionalLabel { get; private init; } = string.Empty;

    public string NoBinSelectedLabel { get; private init; } = string.Empty;

    public string MergeDpSlotDescription { get; private init; } = string.Empty;

    public string MergeTpSlotDescription { get; private init; } = string.Empty;

    public string MergeLdSlotDescription { get; private init; } = string.Empty;

    public string BaseFlashBinTitle { get; private init; } = string.Empty;

    public string BaseFlashBinDescription { get; private init; } = string.Empty;

    public string DpReplacementBinTitle { get; private init; } = string.Empty;

    public string DpReplacementBinDescription { get; private init; } = string.Empty;

    public string TpReplacementBinTitle { get; private init; } = string.Empty;

    public string TpReplacementBinDescription { get; private init; } = string.Empty;

    public string LdReplacementBinTitle { get; private init; } = string.Empty;

    public string LdReplacementBinDescription { get; private init; } = string.Empty;

    public string CtrlRamReplacementBinDescription { get; private init; } = string.Empty;

    public string SelectReplacementBinTooltip { get; private init; } = string.Empty;

    public string SelectSourceBinTooltip { get; private init; } = string.Empty;

    public string RemoveRangeTooltip { get; private init; } = string.Empty;

    public string RemoveMappingTooltip { get; private init; } = string.Empty;

    public string GeneralReplaceRuleBaseTitle { get; private init; } = string.Empty;

    public string GeneralReplaceRuleBaseDetail { get; private init; } = string.Empty;

    public string GeneralReplaceRuleBoundsTitle { get; private init; } = string.Empty;

    public string GeneralReplaceRuleBoundsDetail { get; private init; } = string.Empty;

    public string GeneralReplaceRuleLengthTitle { get; private init; } = string.Empty;

    public string GeneralReplaceRuleLengthDetail { get; private init; } = string.Empty;

    public string GeneralReplaceValidationDetail { get; private init; } = string.Empty;

    public string GeneralReplaceMappingsDetail { get; private init; } = string.Empty;

    public string GeneralMergeMappingDetail { get; private init; } = string.Empty;

    public string GeneralMergeMappingsDetail { get; private init; } = string.Empty;

    public string CtrlRamInputFilesDetail { get; private init; } = string.Empty;

    public string AbCodeMergeTitle { get; private init; } = string.Empty;

    public string AbCodeMergeDetail { get; private init; } = string.Empty;

    public string SettingsCatalogTitle { get; private init; } = string.Empty;

    public string SettingsCatalogSubtitle { get; private init; } = string.Empty;

    public string SettingsRuntimeChecksTitle { get; private init; } = string.Empty;

    public string SettingsRuntimeChecksSubtitle { get; private init; } = string.Empty;

    public string SettingsDiagnosticsTitle { get; private init; } = string.Empty;

    public string SettingsDiagnosticsSubtitle { get; private init; } = string.Empty;

    public string SettingsPreferencesTitle { get; private init; } = string.Empty;

    public string SettingsPreferencesSubtitle { get; private init; } = string.Empty;

    public string ThemeLabel { get; private init; } = string.Empty;

    public string StrictnessLabel { get; private init; } = string.Empty;

    public string LanguageLabel { get; private init; } = string.Empty;

    public string SettingsInspectorKicker { get; private init; } = string.Empty;

    public string SettingsReadinessTitle { get; private init; } = string.Empty;

    public string ReportToastTitle { get; private init; } = string.Empty;

    public string ReplaceSelectionTitle { get; private init; } = string.Empty;

    public string CloseSelectionTooltip { get; private init; } = string.Empty;

    public string SelectedReplacementsTitle { get; private init; } = string.Empty;

    public string RequiredBeforeBuildTitle { get; private init; } = string.Empty;

    public string CloseLabel { get; private init; } = string.Empty;

    public string SaveReportLabel { get; private init; } = string.Empty;

    public string CloseReportTooltip { get; private init; } = string.Empty;

    public string ReportHistoryTitle { get; private init; } = string.Empty;

    public string BackToReportLabel { get; private init; } = string.Empty;

    public string ClearAllLabel { get; private init; } = string.Empty;

    public string ClearHistoryLabel { get; private init; } = string.Empty;

    public string ClearHistoryTooltip { get; private init; } = string.Empty;

    public string NoReportHistoryLabel { get; private init; } = string.Empty;

    public string RunLabel { get; private init; } = string.Empty;

    public string OutputLabel { get; private init; } = string.Empty;

    public string ChangeReviewTitle { get; private init; } = string.Empty;

    public string EvidenceTitle { get; private init; } = string.Empty;

    public string TraceLabel { get; private init; } = string.Empty;

    public string OpenReportHistoryTooltip { get; private init; } = string.Empty;

    public string OpenReportHistoryAutomationName { get; private init; } = string.Empty;

    public string ReportTabInputs { get; private init; } = string.Empty;

    public string ReportTabChanges { get; private init; } = string.Empty;

    public string ReportTabOperations { get; private init; } = string.Empty;

    public string ReportTabPostbuild { get; private init; } = string.Empty;

    public string ReportTabIssues { get; private init; } = string.Empty;

    public string ReportTabRaw { get; private init; } = string.Empty;

    public string RunMetadataTitle { get; private init; } = string.Empty;

    public string ReportFileLabel { get; private init; } = string.Empty;

    public string StatusLabel { get; private init; } = string.Empty;

    public string ArtifactPathLabel { get; private init; } = string.Empty;

    public string InputsAndHashesTitle { get; private init; } = string.Empty;

    public string EmptyInputsMessage { get; private init; } = string.Empty;

    public string EmptyByteChangesMessage { get; private init; } = string.Empty;

    public string OutputChangesTitle { get; private init; } = string.Empty;

    public string DiffLabel { get; private init; } = string.Empty;

    public string RangeLabel { get; private init; } = string.Empty;

    public string ResultLabel { get; private init; } = string.Empty;

    public string DetailLabel { get; private init; } = string.Empty;

    public string ExplanationLabel { get; private init; } = string.Empty;

    public string ReasonLabel { get; private init; } = string.Empty;

    public string ChangedRangesTitle { get; private init; } = string.Empty;

    public string EmptyOperationsMessage { get; private init; } = string.Empty;

    public string OperationStepsTitle { get; private init; } = string.Empty;

    public string StepLabel { get; private init; } = string.Empty;

    public string KindLabel { get; private init; } = string.Empty;

    public string SourceLabel { get; private init; } = string.Empty;

    public string TargetLabel { get; private init; } = string.Empty;

    public string ProcessorLabel { get; private init; } = string.Empty;

    public string EmptyPostbuildMessage { get; private init; } = string.Empty;

    public string HeaderRefreshTraceTitle { get; private init; } = string.Empty;

    public string EmptyIssuesMessage { get; private init; } = string.Empty;

    public string IssuesAndWarningsTitle { get; private init; } = string.Empty;

    public string RangeTableTitle { get; private init; } = string.Empty;

    public string AddressSpaceLabel { get; private init; } = string.Empty;

    public string CommandArgvLabel { get; private init; } = string.Empty;

    public string DeleteReportTooltip { get; private init; } = string.Empty;

    public string MergeModeTooltip { get; private init; } = string.Empty;

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

    public string GetReplaceMemorySummary(string mode, string ic)
    {
        return Language == ShellLanguage.ChineseTraditional
            ? mode switch
            {
                "DP" when ic is "NT51950" or "NT51951" => "藍色代表新的 DP bytes；灰色代表從 base firmware 還原的 TP。",
                "DP" => "Base flash 只會在核准的 DP 取代範圍內改變。",
                "CtrlRAM" => "有色區塊代表可取代的 CtrlRAM 位置；灰色保留 base firmware。",
                "General" => "Base flash 只會在核准的明確取代範圍內改變。",
                _ => "選擇 Replace 模式後查看目標範圍。",
            }
            : mode switch
            {
                "DP" when ic is "NT51950" or "NT51951" => "Blue shows new DP bytes; gray shows TP restored from the base firmware.",
                "DP" => "Base flash stays unchanged except approved DP replacement ranges.",
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

#pragma warning restore CS1591
