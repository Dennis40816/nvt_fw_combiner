namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One completed runtime process invocation rendered from report audit evidence.</summary>
public sealed class ReportRuntimeCommandViewModel
{
    /// <summary>Creates a readable completed process invocation.</summary>
    public ReportRuntimeCommandViewModel(
        string title,
        string argumentListEvidence,
        string workingDirectoryDetail)
    {
        Title = title;
        ArgumentListEvidence = argumentListEvidence;
        WorkingDirectoryDetail = workingDirectoryDetail;
    }

    /// <summary>Ordinal invocation label.</summary>
    public string Title { get; }

    /// <summary>Executable and expanded argv, with one exact argument value per line.</summary>
    public string ArgumentListEvidence { get; }

    /// <summary>Working-directory evidence for the staging invocation.</summary>
    public string WorkingDirectoryDetail { get; }
}
