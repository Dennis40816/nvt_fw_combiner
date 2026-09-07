using NvtFwCombiner.Application.Authoring;
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
        string summary = GetInputIssueHelp(code, error ? "error" : "warning", status.Inspection?.DiagnosticEvidence)?.Title
            ?? GetInputSlotInspectionStatus(status);
        if (error && code == "input.source-view.incomplete" && status.Inspection is { } inspection &&
            inspection.ActualLength < inspection.RequiredEndExclusive)
        {
            summary = SelectLanguage(
                $"{subject} too short (≥ {FormatInputLength(inspection.RequiredEndExclusive)})",
                $"{subject} 太短（≥ {FormatInputLength(inspection.RequiredEndExclusive)}）");
        }
        return CreateIssueCard(subject, summary, error) with
        {
            DiagnosticCode = code,
            Action = error && code == "input.source-view.incomplete"
                ? SelectLanguage($"Select a complete {subject} and load it again.", $"請選擇完整的 {subject} 並重新載入。")
                : error ? SelectLanguage("Check the input file and load it again.", "請檢查輸入檔並重新載入。")
                    : SelectLanguage($"Confirm this is the intended {subject}.", $"請確認這是預期的 {subject}。"),
        };
    }

    internal IssueCardViewModel CreateIssueCard(string subject, string summary, bool error)
    {
        return new IssueCardViewModel(
            SelectLanguage("Input issue", "輸入問題"),
            subject,
            summary,
            error ? SelectLanguage("Build is blocked.", "目前無法 Build。")
                : SelectLanguage("Does not block Build.", "不會阻擋 Build。"),
            error ? SelectLanguage("Select a compatible input and retry.", "請更換相容輸入後重試。")
                : SelectLanguage("Check that this is the intended BIN.", "請確認這是預期的 BIN。"),
            error);
    }

    // Display help for typed diagnostics only; this does not classify input bytes or severity.
    internal (string Title, string Detail)? GetInputIssueHelp(string code, string severity, InputDiagnosticEvidence? evidence = null)
    {
        (string Title, string Detail)? help = severity.ToLowerInvariant() switch
        {
            "warning" => code switch
            {
                "DP_UNIFORM_CONTENT_WARNING" => UniformInputHelp("DP"),
                "TP_UNIFORM_CONTENT_WARNING" => UniformInputHelp("TP"),
                "LDC_UNIFORM_CONTENT_WARNING" => UniformInputHelp("LDC"),
                _ => null,
            },
            "error" => code switch
            {
                "input.inspection.extension-not-accepted" => (
                    SelectLanguage("File extension is not accepted", "副檔名不符"),
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
        if (help is null || evidence is null)
        {
            return help;
        }

        if (severity.Equals("warning", StringComparison.OrdinalIgnoreCase) &&
            evidence.RepeatedByte is { } value && evidence.SourceRange is { Length: > 0 } range)
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
            evidence.ActualLength is { } actual && evidence.RequiredEndExclusive is { } required && actual < required)
        {
            string subject = GetInputArtifactRoleLabel(evidence.AddressSpaceId);
            string title = SelectLanguage($"{subject} too short (requires ≥ {FormatInputLength(required)})",
                $"{subject} 太短（需 ≥ {FormatInputLength(required)}）");
            string facts = SelectLanguage(
                FormattableString.Invariant($"Actual: {actual:N0} bytes · Required: ≥ {required:N0} bytes · Missing: {required - actual:N0} bytes"),
                FormattableString.Invariant($"實際：{actual:N0} bytes · 需要：≥ {required:N0} bytes · 缺少：{required - actual:N0} bytes"));
            return (title, help.Value.Detail + Environment.NewLine + facts +
                (evidence.SourceRange is { Length: > 0 } sourceRange
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
