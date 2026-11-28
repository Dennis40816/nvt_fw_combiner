using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ShellTextResources
{
    internal string BuildIssueCaption => SelectLanguage("Build status", "Build 狀態");
    internal string BuildBlockedTitle => SelectLanguage("Build blocked", "Build 已阻擋");

    internal string FormatAdditionalBuildBlockers(int count, string next)
    {
        return SelectLanguage($"{count} more blocker{(count == 1 ? "" : "s")}: {next}", $"另有 {count} 個阻擋原因：{next}");
    }

    internal IssueCardViewModel CreateInputIssueCard(AuthoringInputSlotStatus status, string subject)
    {
        bool error = status.InspectionLifecycle == AuthoringSlotLifecycle.Error;
        string code = status.InspectionIssueCode ?? string.Empty;
        (string Title, string Detail)? help = GetInputIssueHelp(code, error ? "error" : "warning", status.Inspection?.DiagnosticEvidence, status.Inspection);
        string summary = GetIgnoredTrailingInputDescription(status)
            ?? (code == InputArtifactInspectionIssueCodes.SourceUnreadable && status.FileStamp is null
                ? GetInputSlotInspectionStatus(status) : code == "DP_NONSTANDARD_SIZE_WARNING" ? help?.Detail : help?.Title)
            ?? GetInputSlotInspectionStatus(status);
        // Show short-file guidance once; keep typed facts in the existing action/detail row.
        bool incomplete = error && code == "input.source-view.incomplete" && status.Inspection is { } inspection &&
            inspection.ActualLength < inspection.RequiredEndExclusive;
        return CreateIssueCard(subject, incomplete ? InputTooShortStatus : summary, error) with
        {
            DiagnosticCode = code,
            Action = code == "DP_NONSTANDARD_SIZE_WARNING" ? string.Empty
                : incomplete ? help!.Value.Detail.Split(Environment.NewLine)[0]
                : error && code is InputArtifactInspectionIssueCodes.TpChipCountRequired or InputArtifactInspectionIssueCodes.TpChipCountUnreadable
                    ? help!.Value.Detail
                : error ? SelectLanguage("The selected BIN has an input error; review its status and select a compatible BIN.", "所選 BIN 有輸入錯誤，請查看其狀態並選擇相容的 BIN。")
                    : SelectLanguage($"Confirm this is the intended {subject}.", $"請確認這是預期的 {subject}。"),
        };
    }

    private string InputTooShortStatus => SelectLanguage("The selected BIN is too short; select a complete, compatible BIN.", "所選 BIN 太短，請選擇完整且相容的 BIN。");

    private string? GetIgnoredTrailingInputDescription(AuthoringInputSlotStatus status)
    {
        return status.InspectionLifecycle == AuthoringSlotLifecycle.Warning &&
            status.InspectionNextAction == CompiledInputArtifactInspectionNextAction.ReviewIgnoredTrailingBytes
            ? GetIgnoredTrailingInputDescription(status.Inspection) : null;
    }

    private string? GetIgnoredTrailingInputDescription(CompiledInputArtifactInspectionResult? result)
    {
        return result is
        {
            Severity: CompiledInputArtifactInspectionSeverity.Warning,
            NextAction: CompiledInputArtifactInspectionNextAction.ReviewIgnoredTrailingBytes,
            AcceptedSnapshotRange: { Length: > 0 } accepted,
            IgnoredTrailingRange: { Length: > 0 } ignored,
        } inspection ? SelectLanguage(
            $"Input: {FormatInputLength(inspection.ActualLength)} · Used: {FormatInputLength(accepted.Length)}\nTrailing {FormatInputLength(ignored.Length)} will be ignored.",
            $"輸入：{FormatInputLength(inspection.ActualLength)} · 使用：{FormatInputLength(accepted.Length)}\n尾端 {FormatInputLength(ignored.Length)} 將被忽略。") : null;
    }

    internal IssueCardViewModel CreateIssueCard(string subject, string summary, bool error)
    {
        return new IssueCardViewModel(
            SelectLanguage("Input issue", "輸入問題"),
            subject,
            summary,
            error ? SelectLanguage("Build is blocked.", "目前無法 Build。")
                : SelectLanguage("Does not block Build.", "不會阻擋 Build。"),
            error ? SelectLanguage("This input is blocked; review its status before Build.", "此輸入目前受阻擋，請在 Build 前查看其狀態。")
                : SelectLanguage("Check that this is the intended BIN.", "請確認這是預期的 BIN。"),
            error);
    }

    // Display help for typed diagnostics only; this does not classify input bytes or severity.
    internal (string Title, string Detail)? GetInputIssueHelp(string code, string severity,
        InputDiagnosticEvidence? evidence = null, CompiledInputArtifactInspectionResult? inspection = null,
        long? actualLength = null, IReadOnlyList<long>? expectedOuterLengths = null)
    {
        (string Title, string Detail)? help = severity.ToLowerInvariant() switch
        {
            "warning" => code switch
            {
                "DP_NONSTANDARD_SIZE_WARNING" => DpSizeWarningText.Get(Language,
                    inspection?.ActualLength ?? actualLength ?? evidence?.ActualLength,
                    inspection?.ExpectedOuterLengths ?? expectedOuterLengths),
                "DP_UNIFORM_CONTENT_WARNING" => UniformInputHelp("DP"),
                "TP_UNIFORM_CONTENT_WARNING" => UniformInputHelp("TP"),
                "LDC_UNIFORM_CONTENT_WARNING" => UniformInputHelp("LDC"),
                _ => null,
            },
            "error" => code switch
            {
                InputArtifactInspectionIssueCodes.TpChipCountRequired => (
                    SelectLanguage(
                        "The TP firmware IC count is zero; select a BIN with a valid IC count.",
                        "TP 韌體的 IC 數量為零，請選擇 IC 數量有效的 BIN。"),
                    SelectLanguage("FWConfig Chip_Num (offset 0x17): 0.", "FWConfig Chip_Num（offset 0x17）：0。")),
                InputArtifactInspectionIssueCodes.TpChipCountUnreadable => (
                    SelectLanguage(
                        "The TP firmware IC count could not be read; select a BIN with valid firmware configuration.",
                        "無法讀取 TP 韌體的 IC 數量，請選擇韌體設定有效的 BIN。"),
                    SelectLanguage("Check the canonical NVT FWConfig Backup and its validity.", "請檢查 canonical NVT FWConfig Backup 與其有效性。")),
                "input.inspection.extension-not-accepted" => (
                    SelectLanguage("This file extension is not accepted; select a file with an accepted extension.", "不接受此副檔名，請選擇使用允許副檔名的檔案。"),
                    SelectLanguage("This input does not accept this file extension. Select a compatible BIN before retrying Build.",
                        "此輸入不接受該副檔名；請選擇相容的 BIN，再重試 Build。")),
                "input.inspection.source-unreadable" or "input.artifact.read-failed" => (
                    SelectLanguage("Input file cannot be read", "無法讀取輸入檔"),
                    SelectLanguage("The input file could not be read. Check that the path exists and you have read access, then select the file again before retrying Build.",
                        "無法讀取輸入檔。請確認路徑存在且有讀取權限，重新選擇檔案後再重試 Build。")),
                "input.artifact.content-snapshot-mismatch" => (
                    SelectLanguage("Input changed after inspection", "輸入檔在檢查後已變更"),
                    SelectLanguage("The input no longer matches the inspected file. Select and inspect the current file again before retrying Build.",
                        "目前輸入與先前檢查的檔案不一致。請重新選擇並檢查目前的檔案，再重試 Build。")),
                "input.binding.missing" => (
                    SelectLanguage("A required input is missing", "缺少必要輸入檔"),
                    SelectLanguage("A required input is missing. Select the BIN required by the current IC/profile before retrying Build.",
                        "缺少必要輸入。請選擇目前 IC/profile 所需的 BIN，再重試 Build。")),
                CompositionIssueCodes.InputAddressSpaceLengthMismatch => (
                    SelectLanguage("Input size does not match", "輸入檔大小不符"),
                    SelectLanguage("The BIN size does not match the selected IC/profile. Select a file with the required capacity before retrying Build; do not pad or truncate it by guesswork.",
                        "BIN 大小不符合所選 IC/profile。請選擇正確容量的檔案，再重試 Build；不要自行猜測補齊或截斷。")),
                "input.source-view.incomplete" => (
                    SelectLanguage("Input is too short", "輸入檔太短"),
                    SelectLanguage("The BIN is too short to cover the required source range. Select a complete compatible BIN before retrying Build.",
                        "BIN 太短，未涵蓋必要的來源範圍。請選擇完整且相容的 BIN，再重試 Build。")),
                _ => null,
            },
            _ => null,
        };
        if (help is null)
        {
            return help;
        }

        if (severity.Equals("warning", StringComparison.OrdinalIgnoreCase) &&
            evidence?.RepeatedByte is { } value && evidence.SourceRange is { Length: > 0 } range)
        {
            string input = code switch
            {
                "DP_UNIFORM_CONTENT_WARNING" => "DP",
                "TP_UNIFORM_CONTENT_WARNING" => "TP",
                "LDC_UNIFORM_CONTENT_WARNING" => "LDC",
                _ => string.Empty,
            };
            if (input.Length > 0)
            {
                string title = SelectLanguage($"{input} region contains only 0x{value:X2}", $"{input} 區段全部為 0x{value:X2}");
                return (title, title + ". " + SelectLanguage(
                    "Check that this is the intended BIN. This warning does not block Build.",
                    "請確認這是預期的 BIN。此警告不會阻擋 Build。") + Environment.NewLine +
                    FormatDiagnosticRange(evidence.AddressSpaceId, range));
            }
        }

        if (severity.Equals("error", StringComparison.OrdinalIgnoreCase) && code == "input.source-view.incomplete" &&
            (evidence?.ActualLength ?? inspection?.ActualLength) is { } actual &&
            (evidence?.RequiredEndExclusive ?? inspection?.RequiredEndExclusive) is { } required && actual < required)
        {
            string facts = SelectLanguage(
                FormattableString.Invariant($"Actual: {actual:N0} bytes · Required: ≥ {required:N0} bytes · Missing: {required - actual:N0} bytes"),
                FormattableString.Invariant($"實際：{actual:N0} bytes · 需要：≥ {required:N0} bytes · 缺少：{required - actual:N0} bytes"));
            string minimum = SelectLanguage($"Required: ≥ {FormatInputLength(required)}", $"需要：≥ {FormatInputLength(required)}");
            string title = evidence is null ? InputTooShortStatus : $"{GetInputArtifactRoleLabel(evidence.AddressSpaceId)} (≥ {FormatInputLength(required)}) · {InputTooShortStatus}";
            return (title, minimum + Environment.NewLine + facts +
                (evidence?.SourceRange is { Length: > 0 } sourceRange
                    ? Environment.NewLine + FormatDiagnosticRange(evidence.AddressSpaceId, sourceRange)
                    : string.Empty));
        }
        return help;
    }

    private string FormatDiagnosticRange(string addressSpaceId, ByteRange range)
    {
        return SelectLanguage("First failing source range", "第一個未通過的來源區段") +
            FormattableString.Invariant($": {addressSpaceId} [0x{range.Start:X}, 0x{range.EndExclusive:X}) ({range.Length:N0} bytes)");
    }

    private (string Title, string Detail) UniformInputHelp(string input)
    {
        return (
            SelectLanguage($"{input} range contains repeated data", $"{input} 範圍疑似為填充值"),
            SelectLanguage(
                $"The profile-declared {input} range contains the same byte throughout (not only 00/FF). It may be blank, placeholder or intentional data. Check the source BIN is the intended file. This warning does not block Build.",
                $"profile 指定的 {input} 範圍全部為同一個 byte（不限於 00/FF）。可能是空白、佔位資料，也可能是刻意內容。請確認來源 BIN 是否正確。此警告不會阻擋 Build。"));
    }
}
