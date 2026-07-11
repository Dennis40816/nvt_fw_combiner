namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One independently numbered postbuild invocation projected from runtime process evidence.</summary>
public sealed class ReportPostbuildInvocationViewModel
{
    /// <summary>Creates one independently reviewable postbuild invocation.</summary>
    public ReportPostbuildInvocationViewModel(
        string number,
        string title,
        string operationTitle,
        string status,
        string argumentListEvidence,
        string workingDirectoryDetail)
    {
        Number = number ?? string.Empty;
        Title = title ?? string.Empty;
        OperationTitle = operationTitle ?? string.Empty;
        Status = status ?? string.Empty;
        ArgumentListEvidence = argumentListEvidence ?? string.Empty;
        WorkingDirectoryDetail = workingDirectoryDetail ?? string.Empty;
    }

    /// <summary>Stable display number within the originating operation, such as <c>300.01</c>.</summary>
    public string Number { get; }

    /// <summary>Invocation kind, either a completed runtime call or a declared fallback plan.</summary>
    public string Title { get; }

    /// <summary>Logical report operation that produced this invocation.</summary>
    public string OperationTitle { get; }

    /// <summary>Outcome status inherited from the originating operation.</summary>
    public string Status { get; }

    /// <summary>Executable and expanded argv evidence.</summary>
    public string ArgumentListEvidence { get; }

    /// <summary>Working-directory evidence or the declared-plan fallback detail.</summary>
    public string WorkingDirectoryDetail { get; }
}
