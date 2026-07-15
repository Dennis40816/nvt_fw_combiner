namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One completed runtime process invocation rendered from report audit evidence.</summary>
public sealed class ReportRuntimeCommandViewModel(
    string title,
    string argumentListEvidence,
    string workingDirectoryDetail)
{
    /// <summary>Ordinal invocation label.</summary>
    public string Title { get; } = title;

    /// <summary>Executable and expanded argv, with one exact argument value per line.</summary>
    public string ArgumentListEvidence { get; } = argumentListEvidence;

    /// <summary>Working-directory evidence for the staging invocation.</summary>
    public string WorkingDirectoryDetail { get; } = workingDirectoryDetail;
}
