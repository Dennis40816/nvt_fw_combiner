using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private WorkbenchDeliveryArtifact? _buildCompletedAdditionalOutput;

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

    /// <summary>True when the completed Build delivered the optional A FlashCode alongside the primary AB BIN.</summary>
    public bool HasBuildCompletedAdditionalOutput => _buildCompletedAdditionalOutput is not null;

    /// <summary>Gets the additional committed output path shown beside the primary AB BIN.</summary>
    public string BuildCompletedAdditionalOutputPath => _buildCompletedAdditionalOutput?.OutputPath ?? string.Empty;

    /// <summary>Gets the additional committed output file name shown beside the primary AB BIN.</summary>
    public string BuildCompletedAdditionalOutputDisplayName =>
        _buildCompletedAdditionalOutput?.OutputFileName ?? string.Empty;

    internal bool TryShowBuildCompleted(WorkbenchRunResult result, bool build)
    {
        if (!build || !result.Succeeded || string.IsNullOrWhiteSpace(result.CommittedOutputId))
        {
            return false;
        }

        LatestCommittedOutputPath = result.CommittedOutputId;
        if (!result.IsDeliveryComplete)
        {
            NotifyBuildCompletedChanged();
            return false;
        }

        BuildCompletedOutputPath = LatestCommittedOutputPath;
        BuildCompletedOpenFolderError = string.Empty;
        _buildCompletedAdditionalOutput = result.DeliveryArtifacts.SingleOrDefault();
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
        _buildCompletedAdditionalOutput = null;
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
        OnPropertyChanged(nameof(HasBuildCompletedAdditionalOutput));
        OnPropertyChanged(nameof(BuildCompletedAdditionalOutputPath));
        OnPropertyChanged(nameof(BuildCompletedAdditionalOutputDisplayName));
    }
}
