// Resource bags intentionally expose concise bindable labels; per-label XML comments add noise.
#pragma warning disable CS1591

using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal enum MemoryPendingPrerequisite
{
    DpBin,
    BaseBin,
    CtrlRamReplacement,
    GeneralMergeSourceMapping,
}

internal sealed partial class ShellTextResources
{
    public (string Label, string Detail) GetPendingInputText(string? addressSpaceId, string fallbackLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackLabel);
        return FormatPendingInputText(GetInputLabel(addressSpaceId, fallbackLabel));
    }

    public (string Label, string Detail) GetPendingInputText(MemoryPendingPrerequisite prerequisite)
    {
        return prerequisite switch
        {
            MemoryPendingPrerequisite.DpBin => FormatPendingInputText("DP BIN"),
            MemoryPendingPrerequisite.BaseBin => FormatPendingInputText("Base BIN"),
            MemoryPendingPrerequisite.CtrlRamReplacement => (
                SelectLanguage("Waiting for CtrlRAM replacement", "等待 CtrlRAM 替換輸入"),
                SelectLanguage(
                    "Select and inspect at least one CtrlRAM region BIN to resolve the output layout.",
                    "選擇並檢查至少一個 CtrlRAM 區域 BIN 後即可顯示輸出配置。")),
            MemoryPendingPrerequisite.GeneralMergeSourceMapping => (
                SelectLanguage("Waiting for source mapping", "等待來源對應"),
                SelectLanguage(
                    "Choose a source BIN and complete its output mapping to resolve the output layout.",
                    "選擇來源 BIN 並完成輸出對應後即可顯示輸出配置。")),
            _ => throw new ArgumentOutOfRangeException(nameof(prerequisite), prerequisite, null),
        };
    }

    public (string Label, string Detail) GetBlockingInputText(
        string? addressSpaceId,
        string fallbackLabel,
        string diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
        string inputLabel = GetInputLabel(addressSpaceId, fallbackLabel);
        return (
            SelectLanguage($"{inputLabel} needs attention", $"{inputLabel} 需要處理"),
            diagnostic);
    }

    private (string Label, string Detail) FormatPendingInputText(string inputLabel)
    {
        return (
            SelectLanguage($"Waiting for {inputLabel}", $"等待 {inputLabel}"),
            SelectLanguage(
                $"Load and inspect {inputLabel} to resolve the output layout.",
                $"載入並檢查 {inputLabel} 後即可顯示輸出配置。"));
    }

    private static string GetInputLabel(string? addressSpaceId, string fallbackLabel)
    {
        return addressSpaceId switch
        {
            CompositionAddressSpaceIds.ReferenceBase => "Base BIN",
            CompositionAddressSpaceIds.DpInput or CompositionAddressSpaceIds.DpAbInput => "DP BIN",
            CompositionAddressSpaceIds.TpInput or CompositionAddressSpaceIds.TpAInput or
                CompositionAddressSpaceIds.TpBInput => "TP BIN",
            _ => fallbackLabel,
        };
    }

    public string GetMemoryPlanSourceLabel(MemoryPlanSource source)
    {
        if (!Enum.IsDefined(source.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source.Kind, null);
        }

        if (source.Kind is MemoryPlanSourceKind.Technical or MemoryPlanSourceKind.Localized)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(source.DisplayText);
            return source.DisplayText;
        }

        return source.Kind switch
        {
            MemoryPlanSourceKind.NoOutput => NoOutputLabel,
            MemoryPlanSourceKind.Reserved => SelectLanguage("Reserved", "保留區"),
            MemoryPlanSourceKind.Unmapped => SelectLanguage("Unmapped", "未對應"),
            MemoryPlanSourceKind.BaseFirmware => SelectLanguage("Base flash", "基礎韌體"),
            MemoryPlanSourceKind.Output => SelectLanguage("Output", "輸出"),
            MemoryPlanSourceKind.DpBin => "DP BIN",
            MemoryPlanSourceKind.DpReplacementBin =>
                SelectLanguage("Replacement DP BIN", "替換用 DP BIN"),
            MemoryPlanSourceKind.TpBin => "TP BIN",
            MemoryPlanSourceKind.Tpb => "TPB",
            MemoryPlanSourceKind.LdcBin => "LDC BIN",
            MemoryPlanSourceKind.LdcReplacementBin =>
                SelectLanguage("Replacement LDC BIN", "替換用 LDC BIN"),
            MemoryPlanSourceKind.CtrlRamBin => "CtrlRAM BIN",
            MemoryPlanSourceKind.DpAb => "DP AB",
            MemoryPlanSourceKind.Tpa => "TPA",
            MemoryPlanSourceKind.OverlapError =>
                SelectLanguage("Overlap error", "範圍重疊錯誤"),
            MemoryPlanSourceKind.Technical or MemoryPlanSourceKind.Localized =>
                source.DisplayText!,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source.Kind, null),
        };
    }

    public static string GetCtrlRamRegionTechnicalLabel(CtrlRamRegionRole role)
    {
        return role switch
        {
            CtrlRamRegionRole.Nf => "NF CtrlRAM",
            CtrlRamRegionRole.Normal => "Normal CtrlRAM",
            CtrlRamRegionRole.Mp => "MP CtrlRAM",
            CtrlRamRegionRole.Vn => "VN CtrlRAM",
            CtrlRamRegionRole.Vector => "Vector CtrlRAM",
            CtrlRamRegionRole.DiffDlm => "DiffDLM",
            CtrlRamRegionRole.Other => "CtrlRAM",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
    }

    public string GetOutputLayoutStateLabel(
        MemoryWorkflowDisposition disposition,
        MemoryObservedChange observedChange)
    {
        return !Enum.IsDefined(disposition)
            ? throw new ArgumentOutOfRangeException(nameof(disposition), disposition, null)
            : observedChange switch
            {
                MemoryObservedChange.Changed => OutputLayoutChangedStateLabel,
                MemoryObservedChange.Unchanged => SelectLanguage("Unchanged", "未變更"),
                MemoryObservedChange.NotObserved => disposition switch
                {
                    MemoryWorkflowDisposition.WillWrite or
                    MemoryWorkflowDisposition.DpAbBase or
                    MemoryWorkflowDisposition.TpaOverlay or
                    MemoryWorkflowDisposition.TpbOverlay => SelectLanguage("Will write", "將寫入"),
                    MemoryWorkflowDisposition.WillReplace => SelectLanguage("Will replace", "將替換"),
                    MemoryWorkflowDisposition.Kept => OutputLayoutKeptStateLabel,
                    MemoryWorkflowDisposition.Resolved or
                    MemoryWorkflowDisposition.Blank => string.Empty,
                    _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, null),
                },
                _ => throw new ArgumentOutOfRangeException(nameof(observedChange), observedChange, null),
            };
    }

    public string GetMemoryPlanActionLabel(MemoryPlanActionKind action)
    {
        return action switch
        {
            MemoryPlanActionKind.Browse => BrowseLabel,
            MemoryPlanActionKind.Blocked => SelectLanguage("Blocked", "已阻擋"),
            MemoryPlanActionKind.Restore => SelectLanguage("Restore", "還原"),
            MemoryPlanActionKind.TransformAndOverlay =>
                SelectLanguage("Transform + Overlay", "轉換並覆寫"),
            MemoryPlanActionKind.Postbuild => SelectLanguage("Postbuild", "後處理"),
            MemoryPlanActionKind.Copy => SelectLanguage("Copy", "複製"),
            MemoryPlanActionKind.ReplaceAndCrc =>
                SelectLanguage("Replace + CRC", "替換 + CRC"),
            MemoryPlanActionKind.Replace => SelectLanguage("Replace", "替換"),
            MemoryPlanActionKind.Preserve => SelectLanguage("Preserve", "保留"),
            MemoryPlanActionKind.Initialize => SelectLanguage("Initialize", "初始化"),
            MemoryPlanActionKind.Overlay => SelectLanguage("Overlay", "覆寫"),
            MemoryPlanActionKind.Project => SelectLanguage("Project", "投影"),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };
    }

    public string GetMemoryPlanDetail(MemoryPlanDetailKind detail)
    {
        return detail switch
        {
            MemoryPlanDetailKind.ProtectedCustomerInformationFromDp => SelectLanguage(
                "Protected customer information is supplied by DP BIN; TP overlay does not write here.",
                "受保護的客戶資訊由 DP BIN 提供；TP 覆寫不會寫入此範圍。"),
            MemoryPlanDetailKind.ProtectedCustomerInformationFromDpReplacement => SelectLanguage(
                "Protected customer information is supplied by the DP replacement BIN; TP restore does not write here.",
                "受保護的客戶資訊由替換用 DP BIN 提供；TP 還原不會寫入此範圍。"),
            MemoryPlanDetailKind.ReservedUnwritten => SelectLanguage(
                "Output range remains reserved; no input writes it.",
                "輸出範圍維持保留；沒有輸入檔案寫入。"),
            MemoryPlanDetailKind.Unmapped => SelectLanguage(
                "No source is assigned to this physical range.",
                "此實體範圍未指定來源。"),
            MemoryPlanDetailKind.CopiedFromDp =>
                SelectLanguage(
                    "Output range will be copied from DP BIN.",
                    "輸出範圍將由 DP BIN 複製。"),
            MemoryPlanDetailKind.OverlaidFromTp =>
                SelectLanguage(
                    "Output range will be overlaid from TP BIN.",
                    "輸出範圍將由 TP BIN 覆寫。"),
            _ => throw new ArgumentOutOfRangeException(nameof(detail), detail, null),
        };
    }

    public string FormatMemoryLayoutConflictDetail(IReadOnlyList<string> mappingIds)
    {
        ArgumentNullException.ThrowIfNull(mappingIds);
        string mappings = mappingIds.Count == 0
            ? SelectLanguage("No mapping IDs were reported.", "未提供對應 ID。")
            : string.Join(", ", mappingIds);
        return SelectLanguage(
            $"These mappings overlap in this output range: {mappings} Adjust Start or Length to remove the overlap.",
            $"下列對應的輸出範圍重疊：{mappings}。請調整起始位置或長度以移除重疊。");
    }

    public string FormatOutputLayoutSourceDetail(
        string sourceLabel,
        MemoryWorkflowDisposition disposition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLabel);
        return disposition is
            MemoryWorkflowDisposition.WillWrite or
            MemoryWorkflowDisposition.WillReplace or
            MemoryWorkflowDisposition.DpAbBase or
            MemoryWorkflowDisposition.TpaOverlay or
            MemoryWorkflowDisposition.TpbOverlay
            ? SelectLanguage(
                $"Output range will be written from {sourceLabel}.",
                $"輸出範圍將由 {sourceLabel} 寫入。")
            : SelectLanguage(
                $"Output range uses bytes from {sourceLabel}.",
                $"輸出範圍使用來自 {sourceLabel} 的資料。");
    }

    public string GetOutputLayoutBaseDetail(bool restores)
    {
        return restores
            ? SelectLanguage(
                "Output range will restore bytes from the base firmware.",
                "輸出範圍將還原基礎韌體的資料。")
            : SelectLanguage(
                "Output range keeps bytes from the base firmware.",
                "輸出範圍保留基礎韌體的資料。");
    }

    public string FormatMemoryLayoutTechnicalDetail(
        string regionId,
        byte? blankFillByte,
        IReadOnlyList<CompositionOperation> contributingOperations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        ArgumentNullException.ThrowIfNull(contributingOperations);
        string initialization = blankFillByte is { } fillByte
            ? SelectLanguage(
                $"Blank fill 0x{fillByte:X2}. ",
                $"空白填充值 0x{fillByte:X2}。")
            : string.Empty;
        string? operationList = contributingOperations.Count == 0
            ? null
            : string.Join(", ", contributingOperations.Select(operation =>
                SelectLanguage(
                    $"{operation.OperationId} (Sequence {operation.Sequence})",
                    $"{operation.OperationId}（順序 {operation.Sequence}）")));
        string operations = operationList is null
            ? SelectLanguage(
                "No compiled operation writes this range.",
                "沒有編譯操作寫入此範圍。")
            : SelectLanguage(
                $"Compiled operations: {operationList}.",
                $"編譯操作：{operationList}。");
        return SelectLanguage(
            $"{regionId}. {initialization}{operations}",
            $"{regionId}。{initialization}{operations}");
    }
}

#pragma warning restore CS1591
