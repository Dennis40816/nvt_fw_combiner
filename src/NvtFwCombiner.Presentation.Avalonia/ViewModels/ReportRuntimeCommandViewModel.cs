namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One completed runtime process invocation rendered from report audit evidence.</summary>
internal sealed class ReportRuntimeCommandViewModel(
    string title,
    string argumentListEvidence,
    string workingDirectoryDetail)
{
    public string Title { get; } = title;

    public string ArgumentListEvidence { get; } = argumentListEvidence;

    /// <summary>Working-directory evidence for the staging invocation.</summary>
    public string WorkingDirectoryDetail { get; } = workingDirectoryDetail;
}
