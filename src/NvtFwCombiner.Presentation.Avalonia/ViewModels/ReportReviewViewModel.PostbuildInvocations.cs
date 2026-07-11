namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReportReviewViewModel
{
    private static List<ReportPostbuildInvocationViewModel> CreatePostbuildInvocations(
        IReadOnlyList<ReportLineViewModel> operations,
        ShellLanguage language)
    {
        List<ReportPostbuildInvocationViewModel> invocations = [];
        foreach (ReportLineViewModel operation in operations.Where(operation =>
                     operation.HasRuntimeCommands || operation.HasCodeBlock))
        {
            string operationNumber = ExtractOperationFlowNumber(operation.Title);
            if (operation.RuntimeCommands.Count == 0)
            {
                invocations.Add(new ReportPostbuildInvocationViewModel(
                    $"{operationNumber}.00",
                    T(language, "Profile-declared plan", "Profile 宣告的計畫"),
                    operation.Title,
                    operation.OperationStatus,
                    operation.CodeBlock,
                    T(language, "No runtime invocation was recorded.", "未記錄實際呼叫。")));
                continue;
            }

            for (int index = 0; index < operation.RuntimeCommands.Count; index++)
            {
                ReportRuntimeCommandViewModel command = operation.RuntimeCommands[index];
                invocations.Add(new ReportPostbuildInvocationViewModel(
                    $"{operationNumber}.{(index + 1).ToString("D2", System.Globalization.CultureInfo.InvariantCulture)}",
                    T(language, "Runtime invocation", "實際呼叫"),
                    operation.Title,
                    operation.OperationStatus,
                    command.ArgumentListEvidence,
                    command.WorkingDirectoryDetail));
            }
        }

        return invocations;
    }
}
