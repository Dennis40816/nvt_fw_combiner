namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One independently numbered postbuild invocation projected from runtime process evidence.</summary>
public sealed class ReportPostbuildInvocationViewModel(
    string number,
    string title,
    string operationTitle,
    string status,
    string argumentListEvidence,
    string workingDirectoryDetail)
{
    /// <summary>Stable display number within the originating operation, such as <c>300.01</c>.</summary>
    public string Number { get; } = number ?? string.Empty;

    /// <summary>Invocation kind, either a completed runtime call or a declared fallback plan.</summary>
    public string Title { get; } = title ?? string.Empty;

    /// <summary>Logical report operation that produced this invocation.</summary>
    public string OperationTitle { get; } = operationTitle ?? string.Empty;

    /// <summary>Outcome status inherited from the originating operation.</summary>
    public string Status { get; } = status ?? string.Empty;

    /// <summary>Executable and expanded argv evidence.</summary>
    public string ArgumentListEvidence { get; } = argumentListEvidence ?? string.Empty;

    /// <summary>Working-directory evidence or the declared-plan fallback detail.</summary>
    public string WorkingDirectoryDetail { get; } = workingDirectoryDetail ?? string.Empty;
}
