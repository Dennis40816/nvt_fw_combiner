// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ShellTextResources
{
    private string SelectLanguage(string english, string traditionalChinese)
    {
        return Language == ShellLanguage.ChineseTraditional ? traditionalChinese : english;
    }

    public string GetReplaceModeDescription(string mode)
    {
        return mode switch
        {
            WorkbenchReplaceModes.Dp => SelectLanguage(
                "Replace DP and optional LD payloads without CRC postbuild.",
                "取代 DP 與選用 LD payload；不執行 CRC postbuild。"),
            WorkbenchReplaceModes.CtrlRam => SelectLanguage(
                "Replace CtrlRAM payloads, then run combiner.exe postbuild for CRC/header refresh.",
                "取代 CtrlRAM payload 後執行 combiner.exe postbuild 更新 CRC/header。"),
            WorkbenchReplaceModes.General => SelectLanguage(
                "Author explicit profile-approved ranges; TP ranges require combiner.exe CRC/header refresh.",
                "編輯 profile 核准的明確範圍；碰到 TP 範圍時必須執行 combiner.exe CRC/header 更新。"),
            _ => SelectLanguage("Select a replace mode.", "選擇一個 Replace 模式。"),
        };
    }

    public string GetReplaceBaseTitle(string mode)
    {
        return mode == WorkbenchReplaceModes.Dp
            ? "Reference FlashCode"
            : SelectLanguage("Reference firmware", "參考韌體");
    }

    public string GetReplaceBaseDescription(string mode, string? dpReferenceCapacityLabel)
    {
        return mode switch
        {
            WorkbenchReplaceModes.Dp => SelectLanguage(
                $"Complete FlashCode for the same IC ({dpReferenceCapacityLabel ?? "profile-declared"}). Only declared DP ranges change.",
                $"同一 IC 的完整 FlashCode（{dpReferenceCapacityLabel ?? "由 profile 宣告"}）；只變更已宣告的 DP 範圍。"),
            WorkbenchReplaceModes.CtrlRam => SelectLanguage(
                "Complete FlashCode or TP FW recognized for this IC. Other regions remain unchanged.",
                "此 IC 可辨識的完整 FlashCode 或 TP FW；其他區域保持不變。"),
            WorkbenchReplaceModes.General => SelectLanguage(
                "Complete FlashCode or base image. Only approved mappings change.",
                "完整 FlashCode 或基底映像；只變更核准的 mappings。"),
            _ => SelectLanguage("Complete source image cloned before replacement.", "Replace 前完整複製的來源映像。"),
        };
    }

    public string GetWorkflowEvidenceLabel(WorkbenchWorkflowEvidenceStatus status)
    {
        return status switch
        {
            WorkbenchWorkflowEvidenceStatus.GoldenVerified => SelectLanguage("Golden verified", "Golden 已驗證"),
            WorkbenchWorkflowEvidenceStatus.EvidenceGated => SelectLanguage("Evidence open", "Evidence 待補"),
            WorkbenchWorkflowEvidenceStatus.NotAvailable => SelectLanguage("Not available", "尚未開放"),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
    }

    public string GetWorkflowEvidenceTooltip(WorkbenchWorkflowReadiness readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        return SelectLanguage(
            $"Evidence: {readiness.Reason}\nOpen condition: {readiness.OpenCondition}\nThis reports verification only; it is not a product-support promise.",
            $"Evidence：{readiness.Reason}\n開放條件：{readiness.OpenCondition}\n此狀態只表示驗證程度，不代表產品支援承諾。");
    }

    public string GetIcFamilyLabel(WorkbenchIcFamilyRelationship relationship)
    {
        return relationship switch
        {
            WorkbenchIcFamilyRelationship.PerfectAlias => SelectLanguage("Perfect IC Family", "完整 IC Family"),
            WorkbenchIcFamilyRelationship.PartialAlias => SelectLanguage("Partial IC Family", "部分 IC Family"),
            WorkbenchIcFamilyRelationship.Canonical => SelectLanguage("IC Family source", "IC Family 基準"),
            WorkbenchIcFamilyRelationship.Standalone => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(relationship), relationship, null),
        };
    }

    public string GetIcFamilyTooltip(WorkbenchIcFamilySummary family)
    {
        ArgumentNullException.ThrowIfNull(family);
        return family.FamilyId is null
            ? string.Empty
            : SelectLanguage(
                $"Family: {family.FamilyId}\nCanonical IC: {family.CanonicalIcId}\nReusable scope: {family.Scope}\nFamily reuse never expands executable ranges by itself.",
                $"Family：{family.FamilyId}\n基準 IC：{family.CanonicalIcId}\n可沿用範圍：{family.Scope}\nFamily 關係本身不會擴張可執行的 firmware range。");
    }

    public string GetReplaceMemorySummary(string mode)
    {
        return mode switch
        {
            WorkbenchReplaceModes.Dp => SelectLanguage(
                "Blue shows new DP bytes; gray shows sections preserved or restored from the Reference FlashCode.",
                "藍色代表新的 DP bytes；灰色代表從 Reference FlashCode 保留或還原的區段。"),
            WorkbenchReplaceModes.CtrlRam => SelectLanguage(
                "Colored blocks show replaceable CtrlRAM positions; gray stays from the base firmware.",
                "有色區塊代表可取代的 CtrlRAM 位置；灰色保留 base firmware。"),
            WorkbenchReplaceModes.General => SelectLanguage(
                "Base flash stays unchanged except approved explicit replacement ranges.",
                "Base flash 只會在核准的明確取代範圍內改變。"),
            _ => SelectLanguage(
                "Select a replace mode to inspect its target ranges.",
                "選擇 Replace 模式後查看目標範圍。"),
        };
    }

    public string GetReplaceReadinessStatus(string mode, bool canRun)
    {
        return mode switch
        {
            WorkbenchReplaceModes.Dp when canRun => SelectLanguage(
                "Ready: Build will validate DP Replace inputs, then write output and report.",
                "Ready：Build 會先驗證 DP Replace input，再寫出 output 與 report。"),
            WorkbenchReplaceModes.Dp => SelectLanguage(
                "Build blocked: Reference FlashCode and required DP replacement inputs are required.",
                "Build blocked：需要 Reference FlashCode 與必要的 DP replacement input。"),
            WorkbenchReplaceModes.CtrlRam when canRun => SelectLanguage(
                "Ready: Build will replace selected CtrlRAM regions and run postbuild.",
                "Ready：Build 會取代選定的 CtrlRAM region 並執行 postbuild。"),
            WorkbenchReplaceModes.CtrlRam => SelectLanguage(
                "Build blocked: base BIN and at least one CtrlRAM region BIN are required.",
                "Build blocked：需要 base BIN 與至少一個 CtrlRAM region BIN。"),
            WorkbenchReplaceModes.General when canRun => SelectLanguage(
                "Ready: Build will compile explicit mappings and run postbuild when TP ranges are touched.",
                "Ready：Build 會編譯明確 mapping；碰到 TP range 時會執行 postbuild。"),
            WorkbenchReplaceModes.General => SelectLanguage(
                "Build blocked: base BIN and at least one explicit replacement mapping are required.",
                "Build blocked：需要 base BIN 與至少一筆 replacement mapping。"),
            _ => SelectLanguage("Build blocked: select a Replace mode.", "Build blocked：請選擇 Replace 模式。"),
        };
    }

    public string GetMergeMemorySummary(string mode, bool isStandardMergeSupported, bool hasGeneralMapping)
    {
        return mode switch
        {
            WorkbenchMergeModes.Standard when isStandardMergeSupported => SelectLanguage(
                "The bar shows which input file occupies each final flash position.",
                "此圖顯示每個最終 flash 位置由哪個 input file 寫入。"),
            WorkbenchMergeModes.Standard => SelectLanguage(
                "No merge profile is available for the selected IC.",
                "所選 IC 尚未有 Merge profile。"),
            WorkbenchMergeModes.General when hasGeneralMapping => SelectLanguage(
                "The bar starts reserved and marks each explicit source mapping written into the output.",
                "輸出先以 reserved byte 初始化，再標出每筆明確 source mapping 寫入的位置。"),
            WorkbenchMergeModes.General => SelectLanguage(
                "The bar starts reserved and marks each explicit source mapping written into the output.",
                "輸出先以 reserved byte 初始化；新增 mapping 後會標出寫入位置。"),
            _ => SelectLanguage("This merge mode is reserved.", "此 Merge 模式保留中。"),
        };
    }

    public string GetStandardMergeSupportSummary(string ic, bool supported, string requiredSlots)
    {
        return supported
            ? SelectLanguage(
                $"{ic}: Standard Merge profile found. Required slots: {requiredSlots}.",
                $"{ic}：找到 Standard Merge profile。必要 slots：{requiredSlots}。")
            : SelectLanguage($"{ic}: no Standard Merge profile yet.", $"{ic}：尚未提供 Standard Merge profile。");
    }

    public string GetMergeReadinessStatus(string mode, string ic, string requiredSlots, bool isStandardMergeSupported, int generalMappingFileCount)
    {
        return mode switch
        {
            WorkbenchMergeModes.Standard when isStandardMergeSupported => SelectLanguage(
                $"{ic}: drop {requiredSlots} BIN files.",
                $"{ic}：放入 {requiredSlots} BIN files。"),
            WorkbenchMergeModes.Standard => SelectLanguage(
                $"{ic}: Standard Merge is not available yet.",
                $"{ic}：Standard Merge 尚未可用。"),
            WorkbenchMergeModes.General when generalMappingFileCount > 0 => SelectLanguage(
                $"{ic}: General Merge maps {generalMappingFileCount} source BIN file(s) into a blank output.",
                $"{ic}：General Merge 會將 {generalMappingFileCount} 個 source BIN mapping 寫入 blank output。"),
            WorkbenchMergeModes.General => SelectLanguage(
                $"{ic}: add at least one source BIN mapping.",
                $"{ic}：至少新增一筆 source BIN mapping。"),
            _ => SelectLanguage(
                "AB Code Merge is reserved for a later workflow.",
                "AB Code Merge 保留給後續流程。"),
        };
    }

    public string GetReportHistorySummary(int count)
    {
        return count switch
        {
            <= 0 => SelectLanguage("No reports in history", "目前沒有 report history"),
            1 => SelectLanguage("1 report in history", "history 中有 1 份 report"),
            _ => SelectLanguage($"{count} reports in history", $"history 中有 {count} 份 report"),
        };
    }

    public string GetReportHistoryStorageSummary(string byteCount)
    {
        return SelectLanguage($"{byteCount} stored locally", $"{byteCount} 儲存在本機");
    }

    public string GetReportHistoryStorageWarning(string total, string limit)
    {
        return SelectLanguage(
            $"History uses {total}, above the {limit} limit. Clear history to keep local UI state small.",
            $"History 使用 {total}，已超過 {limit} 限制。清除 history 可維持本機 UI 狀態精簡。");
    }

    public string GetReportActionLabel(bool hasLoadedReport)
    {
        return hasLoadedReport
            ? SelectLanguage("Open report", "開啟 report")
            : SelectLanguage("No report", "尚無 report");
    }

    public string GetReportActionStatus(bool hasLoadedReport, string loadedStatus)
    {
        return hasLoadedReport
            ? loadedStatus
            : SelectLanguage("Build creates one", "Build 後產生");
    }

    public string FormatReportLoadedToast(string sourceName)
    {
        return SelectLanguage($"Report loaded: {sourceName}", $"Report loaded：{sourceName}");
    }

    public string FormatReportIssueToast(string sourceName)
    {
        return SelectLanguage($"Report issue: {sourceName}", $"Report issue：{sourceName}");
    }

    public string FormatReportSavedToast(string destinationName)
    {
        return SelectLanguage($"Report saved: {destinationName}", $"Report saved：{destinationName}");
    }

    public string FormatReportGeneratedToast(string action)
    {
        return SelectLanguage($"{action} report generated", $"{action} report 已產生");
    }

    public string FormatVerifiedFirmwareContextToast(string selectionLabel, byte chipNumber)
    {
        return SelectLanguage(
            $"IC number set to {selectionLabel} from the unique, verified NVT FWConfig (Chip Num 0x{chipNumber:X2}).",
            $"已依唯一且驗證一致的 NVT FWConfig 將 IC 數量設為 {selectionLabel}（Chip Num 0x{chipNumber:X2}）。");
    }

    public string FormatFirmwareSelectionNotRetainedToast(string fileName)
    {
        return SelectLanguage(
            $"{fileName} was not retained because the selected IC does not expose the same safe input slot. Select a compatible BIN again.",
            $"所選 IC 沒有相同的安全輸入 slot，因此未保留 {fileName}。請重新選擇相容的 BIN。");
    }
}

#pragma warning restore CS1591
