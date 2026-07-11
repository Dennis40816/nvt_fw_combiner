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
        int sequence = 100;
        ReportLineViewModel[] baseInputs = [.. inputs.Where(input =>
            string.Equals(input.Classification, ReportInputClassifications.Base, StringComparison.Ordinal))];
        if (baseInputs.Length > 0)
        {
            nodes.Add(new ReportOperationFlowNodeViewModel(
                NextFlowNumber(ref sequence),
                T(language, "Load base image", "載入基準映像"),
                baseInputs[0].Title,
                T(language, "reference input", "reference 輸入"),
                T(language, "input", "輸入"),
                hasConnector: true));
        }

        ReportLineViewModel[] replacementInputs = [
            .. inputs.Where(input => !string.Equals(
                input.Classification,
                ReportInputClassifications.Base,
                StringComparison.Ordinal)),
        ];
        if (replacementInputs.Length > 0)
        {
            nodes.Add(new ReportOperationFlowNodeViewModel(
                NextFlowNumber(ref sequence),
                T(language, "Apply replacement BINs", "套用替換 BIN"),
                FormatReplacementFlowDetail(replacementInputs.Length, language),
                T(language, "listed in Inputs tab", "列於輸入分頁"),
                T(language, "replace", "替換"),
                hasConnector: true));
        }

        ReportLineViewModel[] stepOperations = [.. operations.Where(operation => !operation.HasCodeBlock)];
        if (stepOperations.Length > 0)
        {
            foreach (ReportLineViewModel operation in stepOperations)
            {
                nodes.Add(new ReportOperationFlowNodeViewModel(
                    NextFlowNumber(ref sequence),
                    FormatOperationFlowTitle(operation, language),
                    operation.OperationTarget,
                    ExtractOperationFlowName(operation.Title),
                    FormatOperationFlowStatus(operation.OperationStatus, language),
                    hasConnector: true));
            }
        }

        ReportLineViewModel[] commandOperations = [.. operations.Where(operation =>
            operation.HasCodeBlock || operation.HasRuntimeCommands)];
        if (commandOperations.Length > 0)
        {
            int invocationCount = CountRuntimeInvocations(commandOperations);
            nodes.Add(new ReportOperationFlowNodeViewModel(
                NextFlowNumber(ref sequence),
                T(language, "Refresh header and CRC", "刷新 header 與 CRC"),
                FormatPostbuildFlowDetail(invocationCount, language),
                T(language, "command details in Postbuild tab", "command 細節在 Postbuild 分頁"),
                FormatOperationFlowStatus(commandOperations[^1].OperationStatus, language),
                hasConnector: true));
        }

        if (!string.IsNullOrWhiteSpace(outputFileName))
        {
            nodes.Add(new ReportOperationFlowNodeViewModel(
                NextFlowNumber(ref sequence),
                T(language, "Write output", "寫出輸出"),
                outputFileName,
                T(language, "final artifact", "最終產物"),
                FormatOperationFlowStatus(status, language)));
        }

        if (nodes.Count == 0 && operations.Count > 0)
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
        }

        return nodes;
    }

    private static string NextFlowNumber(ref int sequence)
    {
        string number = sequence.ToString(CultureInfo.InvariantCulture);
        sequence += 100;
        return number;
    }

    private static string FormatReplacementFlowDetail(int count, ShellLanguage language)
    {
        return count == 1
            ? T(language, "1 replacement BIN selected", "已選擇 1 個 replacement BIN")
            : T(
                language,
                string.Create(CultureInfo.InvariantCulture, $"{count} replacement BINs selected"),
                string.Create(CultureInfo.InvariantCulture, $"已選擇 {count} 個 replacement BIN"));
    }

    private static string FormatPostbuildFlowDetail(int count, ShellLanguage language)
    {
        return count == 0
            ? T(language, "Postbuild plan recorded without a runtime invocation", "已記錄 postbuild 計畫，但沒有實際呼叫")
            : count == 1
            ? T(language, "1 postbuild command refreshes header/CRC", "1 個 postbuild command 更新 header/CRC")
            : T(
                language,
                string.Create(CultureInfo.InvariantCulture, $"{count} postbuild commands refresh header/CRC"),
                string.Create(CultureInfo.InvariantCulture, $"{count} 個 postbuild command 更新 header/CRC"));
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
