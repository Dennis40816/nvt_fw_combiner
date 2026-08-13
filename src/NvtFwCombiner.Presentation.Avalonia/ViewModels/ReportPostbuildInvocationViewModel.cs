namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One independently numbered postbuild invocation projected from runtime process evidence.</summary>
internal sealed class ReportPostbuildInvocationViewModel(
    string number,
    string title,
    string operationTitle,
    string status,
    string argumentListEvidence,
    string workingDirectoryDetail)
{
    public string Number { get; } = number ?? string.Empty;

    public string Title { get; } = title ?? string.Empty;

    public string OperationTitle { get; } = operationTitle ?? string.Empty;

    public string Status { get; } = status ?? string.Empty;

    /// <summary>Executable and expanded argv evidence.</summary>
    public string ArgumentListEvidence { get; } = argumentListEvidence ?? string.Empty;

    /// <summary>Working-directory evidence or the declared-plan fallback detail.</summary>
    public string WorkingDirectoryDetail { get; } = workingDirectoryDetail ?? string.Empty;
}
