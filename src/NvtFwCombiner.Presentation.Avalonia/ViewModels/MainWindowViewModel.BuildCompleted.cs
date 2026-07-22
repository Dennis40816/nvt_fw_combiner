using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>True while the successful Build confirmation is visible.</summary>
    public bool IsBuildCompletedModalOpen { get; private set; }

    /// <summary>Gets the committed BIN path shown in the successful Build confirmation.</summary>
    public string BuildCompletedOutputPath { get; private set; } = string.Empty;

    /// <summary>Gets the committed BIN file name shown in the successful Build confirmation.</summary>
    public string BuildCompletedOutputDisplayName => Path.GetFileName(BuildCompletedOutputPath);

    /// <summary>Gets the most recent successfully committed BIN path for the fixed action rail.</summary>
    public string LatestCommittedOutputPath { get; private set; } = string.Empty;

    /// <summary>True after this application session has committed at least one output BIN.</summary>
    public bool HasLatestCommittedOutput => !string.IsNullOrWhiteSpace(LatestCommittedOutputPath);

    /// <summary>Gets the localized failure shown when the output folder could not be opened.</summary>
    public string BuildCompletedOpenFolderError { get; private set; } = string.Empty;

    /// <summary>True when opening the committed output folder failed.</summary>
    public bool HasBuildCompletedOpenFolderError =>
        !string.IsNullOrWhiteSpace(BuildCompletedOpenFolderError);

    internal bool TryShowBuildCompleted(WorkbenchRunResult result, bool build)
    {
        if (!build || !result.Succeeded || string.IsNullOrWhiteSpace(result.CommittedOutputId))
        {
            return false;
        }

        LatestCommittedOutputPath = result.CommittedOutputId;
        BuildCompletedOutputPath = LatestCommittedOutputPath;
        BuildCompletedOpenFolderError = string.Empty;
        IsBuildCompletedModalOpen = true;
        NotifyBuildCompletedChanged();
        return true;
    }

    /// <summary>Closes the successful Build confirmation.</summary>
    public void CloseBuildCompletedModal()
    {
        if (!IsBuildCompletedModalOpen && string.IsNullOrEmpty(BuildCompletedOutputPath))
        {
            return;
        }

        IsBuildCompletedModalOpen = false;
        BuildCompletedOutputPath = string.Empty;
        BuildCompletedOpenFolderError = string.Empty;
        NotifyBuildCompletedChanged();
    }

    internal void ShowLatestOutputFolderOpenFailed()
    {
        if (!HasLatestCommittedOutput)
        {
            return;
        }

        BuildCompletedOutputPath = LatestCommittedOutputPath;
        BuildCompletedOpenFolderError = Text.BuildCompletedOpenFolderError;
        IsBuildCompletedModalOpen = true;
        NotifyBuildCompletedChanged();
    }

    internal void NotifyBuildCompletedOpenFolderFailed()
    {
        BuildCompletedOpenFolderError = Text.BuildCompletedOpenFolderError;
        OnPropertyChanged(nameof(BuildCompletedOpenFolderError));
        OnPropertyChanged(nameof(HasBuildCompletedOpenFolderError));
    }

    internal void NotifyFileRevealFailed()
    {
        SetShellToast(Text.FileRevealFailedTitle, Text.FileRevealFailedDetail);
    }

    private void RevealFile(string? filePath)
    {
        if (!_fileRevealService.TryRevealFile(filePath))
        {
            NotifyFileRevealFailed();
        }
    }

    private void RevealBuildCompletedOutput()
    {
        if (_fileRevealService.TryRevealFile(BuildCompletedOutputPath))
        {
            CloseBuildCompletedModal();
            return;
        }

        NotifyBuildCompletedOpenFolderFailed();
    }

    private void NotifyBuildCompletedChanged()
    {
        OnPropertyChanged(nameof(IsBuildCompletedModalOpen));
        OnPropertyChanged(nameof(BuildCompletedOutputPath));
        OnPropertyChanged(nameof(BuildCompletedOutputDisplayName));
        OnPropertyChanged(nameof(LatestCommittedOutputPath));
        OnPropertyChanged(nameof(HasLatestCommittedOutput));
        OnPropertyChanged(nameof(IsLatestOutputActionVisible));
        OnPropertyChanged(nameof(BuildCompletedOpenFolderError));
        OnPropertyChanged(nameof(HasBuildCompletedOpenFolderError));
    }
}
