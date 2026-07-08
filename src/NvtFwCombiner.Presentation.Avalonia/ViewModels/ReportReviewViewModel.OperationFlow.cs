using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static List<ReportOperationFlowNodeViewModel> CreateOperationFlow(
        IReadOnlyList<ReportLineViewModel> inputs,
        IReadOnlyList<ReportLineViewModel> operations,
        string outputFileName,
        string status,
        ShellLanguage language)
    {
        List<ReportOperationFlowNodeViewModel> nodes = [];
        if (operations.Count > 0)
        {
            for (int operationIndex = 0; operationIndex < operations.Count; operationIndex++)
            {
                ReportLineViewModel operation = operations[operationIndex];
                nodes.Add(new ReportOperationFlowNodeViewModel(
                    ExtractOperationFlowNumber(operation.Title),
                    FormatOperationFlowTitle(operation, language),
                    operation.OperationTarget,
                    ExtractOperationFlowName(operation.Title),
                    FormatOperationFlowStatus(operation.OperationStatus, language),
                    hasConnector: operationIndex < operations.Count - 1));
            }

            return nodes;
        }

        int index = 1;
        ReportLineViewModel[] baseInputs = [.. inputs.Where(input =>
            string.Equals(input.Classification, "base", StringComparison.Ordinal))];
        if (baseInputs.Length > 0)
        {
            nodes.Add(new ReportOperationFlowNodeViewModel(
                index++.ToString(CultureInfo.InvariantCulture),
                T(language, "Load base image", "載入基準映像"),
                baseInputs[0].Title,
                baseInputs[0].Detail,
                T(language, "input", "輸入"),
                hasConnector: true));
        }

        ReportLineViewModel[] replacementInputs = [.. inputs.Where(input =>
            string.Equals(input.Classification, "ctrlram", StringComparison.Ordinal))];
        if (replacementInputs.Length > 0)
        {
            nodes.Add(new ReportOperationFlowNodeViewModel(
                index++.ToString(CultureInfo.InvariantCulture),
                T(language, "Apply replacement BINs", "套用替換 BIN"),
                T(
                    language,
                    string.Create(CultureInfo.InvariantCulture, $"{replacementInputs.Length} CtrlRAM region payload(s) selected"),
                    string.Create(CultureInfo.InvariantCulture, $"已選擇 {replacementInputs.Length} 個 CtrlRAM region payload")),
                T(language, "profile-authorized regions", "profile 核准區域"),
                T(language, "replace", "替換"),
                hasConnector: !string.IsNullOrWhiteSpace(outputFileName)));
        }

        if (!string.IsNullOrWhiteSpace(outputFileName))
        {
            nodes.Add(new ReportOperationFlowNodeViewModel(
                index++.ToString(CultureInfo.InvariantCulture),
                T(language, "Write output", "寫出輸出"),
                outputFileName,
                T(language, "final artifact", "最終產物"),
                FormatOperationFlowStatus(status, language)));
        }

        return nodes;
    }

    private static string ExtractOperationFlowNumber(string title)
    {
        int dotIndex = title.IndexOf('.', StringComparison.Ordinal);
        return dotIndex > 0 ? title[..dotIndex] : title;
    }

    private static string ExtractOperationFlowName(string title)
    {
        int dotIndex = title.IndexOf('.', StringComparison.Ordinal);
        return dotIndex > 0 && dotIndex + 1 < title.Length ? title[(dotIndex + 1)..].Trim() : title;
    }

    private static string FormatOperationFlowTitle(
        ReportLineViewModel operation,
        ShellLanguage language)
    {
        return string.Equals(operation.OperationKind, "RunExternalProcessor", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(operation.OperationKind, "run-external-processor", StringComparison.OrdinalIgnoreCase)
            ? T(language, "Run postbuild refresh", "執行 postbuild refresh")
            : operation.Title;
    }

    private static string FormatOperationFlowStatus(string status, ShellLanguage language)
    {
        return string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase)
            ? T(language, "OK", "OK")
            : string.IsNullOrWhiteSpace(status)
                ? T(language, "Unknown", "未知")
                : status;
    }
}
