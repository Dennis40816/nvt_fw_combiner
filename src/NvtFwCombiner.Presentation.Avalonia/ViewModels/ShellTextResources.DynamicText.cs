// Resource bags intentionally expose many concise bindable labels; XML comments on each label add noise.
#pragma warning disable CS1591

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
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
                "Replace DP and optional LDC payloads without CRC postbuild.",
                "取代 DP 與選用 LDC payload；不執行 CRC postbuild。"),
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
        return mode switch
        {
            WorkbenchReplaceModes.CtrlRam => SelectLanguage(
                "Base firmware (FlashCode / TP FW)",
                "基底韌體 (FlashCode / TP FW)"),
            WorkbenchReplaceModes.Dp or WorkbenchReplaceModes.General => SelectLanguage(
                "Base firmware (FlashCode)",
                "基底韌體 (FlashCode)"),
            _ => SelectLanguage("Base firmware", "基底韌體"),
        };
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

    public static string GetAbSlotTitle(WorkbenchAbMergeInputRole role)
    {
        return role switch
        {
            WorkbenchAbMergeInputRole.DpAb => "DP_AB BIN",
            WorkbenchAbMergeInputRole.TpA => "TPA BIN",
            WorkbenchAbMergeInputRole.TpB => "TPB BIN",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
    }

    public string GetAbSlotDescription(WorkbenchAbMergeInputSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        string size = FormatInputLength(slot.RequiredEndExclusive);
        return slot.Role switch
        {
            WorkbenchAbMergeInputRole.DpAb => SelectLanguage(
                $"Complete two-bank DP container. Required prefix: {size}.",
                $"完整雙 bank DP container；必要 prefix：{size}。"),
            WorkbenchAbMergeInputRole.TpA => SelectLanguage(
                $"Touch payload for bank A. Required prefix: {size}.",
                $"Bank A 的 Touch payload；必要 prefix：{size}。"),
            WorkbenchAbMergeInputRole.TpB => SelectLanguage(
                $"Touch payload for bank B; relocation uses the compiled plan. Required prefix: {size}.",
                $"Bank B 的 Touch payload；relocation 由 compiled plan 定義。必要 prefix：{size}。"),
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot.Role, null),
        };
    }

    public static string GetAbVersionLabel(CompiledInputVersionKind kind)
    {
        return kind switch
        {
            CompiledInputVersionKind.DpA => "DP1",
            CompiledInputVersionKind.DpB => "DP2",
            CompiledInputVersionKind.TpA => "TPA",
            CompiledInputVersionKind.TpB => "TPB",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public string GetInputSlotInspectionStatus(AuthoringInputSlotStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return status.InspectionLifecycle switch
        {
            AuthoringSlotLifecycle.Error when status.FileStamp is null => SelectLanguage(
                "Error: this BIN could not be read. Select a readable local file.",
                "錯誤：無法讀取此 BIN，請選擇可讀取的本機檔案。"),
            AuthoringSlotLifecycle.Error => SelectLanguage(
                "Error: the selected BIN does not satisfy the compiled input contract.",
                "錯誤：所選 BIN 不符合 compiled input contract。"),
            AuthoringSlotLifecycle.Warning when StringComparer.Ordinal.Equals(
                status.InspectionIssueCode,
                InputArtifactInspectionIssueCodes.AbVersionMetadataUnknown) => SelectLanguage(
                "Warning: version metadata is Unknown; Build remains available.",
                "警告：版本資訊為 Unknown；仍可執行 Build。"),
            AuthoringSlotLifecycle.Warning => SelectLanguage(
                $"Warning: profile content check {status.InspectionIssueCode}; review before Build.",
                $"警告：profile 內容檢查 {status.InspectionIssueCode}；Build 前請確認。"),
            AuthoringSlotLifecycle.Verified => SelectLanguage(
                "Ready: the selected BIN satisfies the compiled input contract.",
                "Ready：所選 BIN 符合 compiled input contract。"),
            AuthoringSlotLifecycle.Empty or AuthoringSlotLifecycle.Selected or
            AuthoringSlotLifecycle.Checking or null => throw new ArgumentException(
                "Only terminal slot health can be displayed.", nameof(status)),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
    }

    public string GetAbMergeReadinessStatus(
        string ic,
        bool supported,
        int selectedCount,
        int requiredCount,
        int blockingCount,
        int warningCount)
    {
        return !supported
            ? SelectLanguage(
                $"{ic}: AB Code is not available. Select a declared AB Code route for this release.",
                $"{ic}：尚未開放 AB Code；請選擇本版本已宣告的 AB Code 路徑。")
            : blockingCount > 0
            ? SelectLanguage(
                $"Build blocked: {blockingCount} input error(s) · {selectedCount}/{requiredCount} selected.",
                $"Build blocked：{blockingCount} 個 input 錯誤 · 已選 {selectedCount}/{requiredCount}。")
            : selectedCount < requiredCount
            ? SelectLanguage(
                $"Select DP_AB, TPA, and TPB · {selectedCount}/{requiredCount} selected.",
                $"請選擇 DP_AB、TPA 與 TPB · 已選 {selectedCount}/{requiredCount}。")
            : warningCount > 0
            ? SelectLanguage(
                $"Ready with {warningCount} warning(s): review highlighted inputs before Build.",
                $"可執行，但有 {warningCount} 個 warning；Build 前請確認標示的 inputs。")
            : SelectLanguage(
                "Ready: Build will validate the current AB inputs and produce one report.",
                "Ready：Build 會驗證目前 AB inputs 並產生一份 report。");
    }

    public string GetIcDetailFamilyValue(WorkbenchIcFamilySummary family)
    {
        ArgumentNullException.ThrowIfNull(family);
        return family.FamilyId is null
            ? SelectLanguage("Standalone IC", "獨立 IC")
            : $"{family.FamilyId} · {GetIcFamilyLabel(family.Relationship)}";
    }

    public string GetIcDetailReuseValue(WorkbenchIcFamilySummary family)
    {
        ArgumentNullException.ThrowIfNull(family);
        return string.IsNullOrWhiteSpace(family.Scope)
            ? SelectLanguage("No cross-IC reuse declared", "未宣告跨 IC 沿用範圍")
            : family.Scope;
    }

    public string GetIcDetailRuntimeValue(
        bool standardMerge,
        bool abMerge,
        bool dpReplace,
        bool ctrlRamReplace,
        bool generalReplace)
    {
        List<string> workflows = [];
        Add("Standard", standardMerge);
        Add("AB", abMerge);
        Add("DP", dpReplace);
        Add("CtrlRAM", ctrlRamReplace);
        Add("Customized", generalReplace);
        return workflows.Count == 0
            ? SelectLanguage("No executable workflow", "目前沒有可執行流程")
            : string.Join(" · ", workflows);

        void Add(string label, bool available)
        {
            if (available)
            {
                workflows.Add(label);
            }
        }
    }

    public string GetIcDetailEvidenceValue(
        WorkbenchWorkflowReadiness dp,
        WorkbenchWorkflowReadiness ctrlRam,
        WorkbenchWorkflowReadiness general)
    {
        ArgumentNullException.ThrowIfNull(dp);
        ArgumentNullException.ThrowIfNull(ctrlRam);
        ArgumentNullException.ThrowIfNull(general);

        List<string> verified = [];
        List<string> open = [];
        List<string> unavailable = [];
        Add("DP", dp);
        Add("CtrlRAM", ctrlRam);
        Add("Customized", general);

        List<string> summaries = [];
        if (verified.Count > 0)
        {
            summaries.Add(SelectLanguage(
                $"✓ Verified: {string.Join(", ", verified)}",
                $"✓ 已驗證：{string.Join("、", verified)}"));
        }

        if (open.Count > 0)
        {
            summaries.Add(SelectLanguage(
                $"! Open: {string.Join(", ", open)}",
                $"! 待補：{string.Join("、", open)}"));
        }

        if (unavailable.Count > 0)
        {
            summaries.Add(SelectLanguage(
                $"— Unavailable: {string.Join(", ", unavailable)}",
                $"— 未開放：{string.Join("、", unavailable)}"));
        }

        return string.Join(" · ", summaries);

        void Add(string label, WorkbenchWorkflowReadiness readiness)
        {
            List<string> destination = readiness.EvidenceStatus switch
            {
                WorkbenchWorkflowEvidenceStatus.GoldenVerified => verified,
                WorkbenchWorkflowEvidenceStatus.EvidenceGated => open,
                WorkbenchWorkflowEvidenceStatus.NotAvailable => unavailable,
                _ => unavailable,
            };
            destination.Add(label);
        }
    }

    public string GetIcDetailSupportValue(bool abMerge)
    {
        return abMerge
            ? SelectLanguage(
                "AB runtime follows compiled profile contracts; function-open routes can still have direct golden closure pending. Metadata never selects support.",
                "AB runtime 依 compiled profile contract 決定；功能開放的路徑仍可能等待直接 golden 結案；metadata 絕不選擇支援範圍。")
            : SelectLanguage(
                "Availability follows compiled profiles and safety contracts; family labels do not expand support.",
                "可用性以 compiled profile 與 safety contract 為準；family 標示不會擴張支援範圍。");
    }

    private static string FormatInputLength(long bytes)
    {
        return bytes > 0 && bytes % 1024 == 0
            ? FormattableString.Invariant($"{bytes / 1024} KiB")
            : FormattableString.Invariant($"{bytes} bytes");
    }

    public string GetReplaceMemorySummary(string mode)
    {
        return mode switch
        {
            WorkbenchReplaceModes.Dp => SelectLanguage(
                "Blue shows new DP bytes; gray shows sections preserved or restored from the Reference FlashCode.",
                "藍色代表新的 DP bytes；灰色代表從 Reference FlashCode 保留或還原的區段。"),
            WorkbenchReplaceModes.CtrlRam => SelectLanguage(
                "Solid blocks are changed; diagonal blocks keep bytes from the base firmware.",
                "實色區塊表示已變更；斜線區塊保留 base firmware 的 bytes。"),
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

    public string GetDpInputSelectionReadinessLabel(
        ResolvedChildReadiness state)
    {
        return state switch
        {
            ResolvedChildReadiness.Ready =>
                SelectLanguage("Applicable", "適用"),
            ResolvedChildReadiness.PendingInput =>
                SelectLanguage("Pending input", "等待輸入"),
            ResolvedChildReadiness.NotApplicable =>
                SelectLanguage("Not applicable", "不適用"),
            ResolvedChildReadiness.Blocked =>
                SelectLanguage("Blocked", "已封鎖"),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
    }

    public string GetDpInputSelectionReadinessDetail(
        InputSelectionMemberReadiness member)
    {
        ArgumentNullException.ThrowIfNull(member);
        string reason = EnsureSentence(
            member.Reason,
            "The current capability has not resolved this input");
        return member.Readiness switch
        {
            ResolvedChildReadiness.Ready => SelectLanguage(
                "Available for the resolved Reference length.",
                "可用於已解析的 Reference 長度。"),
            ResolvedChildReadiness.PendingInput
                when member.NextAction?.Kind ==
                    InputSelectionNextActionKind.LoadArtifactFirst =>
                SelectLanguage(
                    $"Load {GetInputArtifactRoleLabel(member.NextAction.SubjectId)} first. {reason}",
                    $"請先載入 {GetInputArtifactRoleLabel(member.NextAction.SubjectId)}。{reason}"),
            ResolvedChildReadiness.PendingInput => SelectLanguage(
                reason,
                $"請完成必要的輸入選擇。{reason}"),
            ResolvedChildReadiness.NotApplicable => SelectLanguage(
                reason,
                $"此輸入不適用。{reason}"),
            ResolvedChildReadiness.Blocked => SelectLanguage(
                $"{reason} Correct the selected input before Build.",
                $"請先修正所選輸入，再執行 Build。{reason}"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(member),
                member.Readiness,
                null),
        };
    }

    public string GetStandardMergeInputSelectionReadinessDetail(
        InputSelectionMemberReadiness member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return member.Readiness == ResolvedChildReadiness.Ready
            ? SelectLanguage(
                "Available for the resolved Standard Merge map.",
                "可用於已解析的 Standard Merge map。")
            : GetDpInputSelectionReadinessDetail(member);
    }

    private static string GetInputArtifactRoleLabel(string artifactId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        return artifactId switch
        {
            WorkbenchAddressSpaceIds.ReferenceBase => "Reference",
            WorkbenchAddressSpaceIds.DpInput => "DP",
            WorkbenchAddressSpaceIds.TpInput => "TP",
            _ => artifactId,
        };
    }

    private static string EnsureSentence(string? value, string fallback)
    {
        string sentence = string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
        return sentence[^1] is '.' or '!' or '?'
            ? sentence
            : $"{sentence}.";
    }

    public string GetInputSelectionReadinessAutomationText(
        string label,
        string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        return SelectLanguage($"{label}. {detail}", $"{label}。{detail}");
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
            WorkbenchMergeModes.AbCode => SelectLanguage(
                "The bar shows final DP_AB ownership after the compiled TPA and relocated TPB overlays.",
                "此圖顯示 compiled TPA 與 relocated TPB overlay 後的最終 DP_AB ownership。"),
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
                $"{ic}: Customized Merge maps {generalMappingFileCount} source BIN file(s) into a blank output.",
                $"{ic}：Customized Merge 會將 {generalMappingFileCount} 個 source BIN mapping 寫入 blank output。"),
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
