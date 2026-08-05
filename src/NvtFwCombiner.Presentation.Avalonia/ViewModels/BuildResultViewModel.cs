using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Owns the successful Build-result confirmation and latest committed output projection.</summary>
public sealed class BuildResultViewModel : ObservableObject
{
    private readonly IFileRevealService _fileRevealService;
    private readonly Func<string> _openFolderErrorText;
    private WorkbenchDeliveryArtifact? _additionalOutput;

    internal BuildResultViewModel(
        IFileRevealService fileRevealService,
        Func<string> openFolderErrorText)
    {
        ArgumentNullException.ThrowIfNull(fileRevealService);
        ArgumentNullException.ThrowIfNull(openFolderErrorText);
        _fileRevealService = fileRevealService;
        _openFolderErrorText = openFolderErrorText;
        CloseCommand = new RelayCommand(Close);
        RevealOutputCommand = new RelayCommand(RevealOutput);
    }

    /// <summary>True while the successful Build confirmation is visible.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Gets the committed BIN path shown in the successful Build confirmation.</summary>
    public string OutputPath { get; private set; } = string.Empty;

    /// <summary>Gets the committed BIN file name shown in the successful Build confirmation.</summary>
    public string OutputDisplayName => Path.GetFileName(OutputPath);

    /// <summary>Gets the most recent successfully committed BIN path for the fixed action rail.</summary>
    public string LatestCommittedOutputPath { get; private set; } = string.Empty;

    /// <summary>True after this application session has committed at least one output BIN.</summary>
    public bool HasLatestCommittedOutput => !string.IsNullOrWhiteSpace(LatestCommittedOutputPath);

    /// <summary>Gets the localized failure shown when the output folder could not be opened.</summary>
    public string OpenFolderError { get; private set; } = string.Empty;

    /// <summary>True when opening the committed output folder failed.</summary>
    public bool HasOpenFolderError => !string.IsNullOrWhiteSpace(OpenFolderError);

    /// <summary>True when the completed Build delivered the optional A FlashCode alongside the primary AB BIN.</summary>
    public bool HasAdditionalOutput => _additionalOutput is not null;

    /// <summary>Gets the additional committed output path shown beside the primary AB BIN.</summary>
    public string AdditionalOutputPath => _additionalOutput?.OutputPath ?? string.Empty;

    /// <summary>Gets the additional committed output file name shown beside the primary AB BIN.</summary>
    public string AdditionalOutputDisplayName => _additionalOutput?.OutputFileName ?? string.Empty;

    /// <summary>Command that dismisses the successful Build confirmation.</summary>
    public IRelayCommand CloseCommand { get; }

    /// <summary>Command that reveals the committed BIN from the Build confirmation.</summary>
    public IRelayCommand RevealOutputCommand { get; }

    internal bool TryShow(WorkbenchRunResult result, bool build)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!build || !result.Succeeded || string.IsNullOrWhiteSpace(result.CommittedOutputId))
        {
            return false;
        }

        LatestCommittedOutputPath = result.CommittedOutputId;
        if (!result.IsDeliveryComplete)
        {
            NotifyStateChanged();
            return false;
        }

        OutputPath = LatestCommittedOutputPath;
        OpenFolderError = string.Empty;
        _additionalOutput = result.DeliveryArtifacts.SingleOrDefault();
        IsOpen = true;
        NotifyStateChanged();
        return true;
    }

    /// <summary>Closes the successful Build confirmation.</summary>
    public void Close()
    {
        if (!IsOpen && string.IsNullOrEmpty(OutputPath))
        {
            return;
        }

        IsOpen = false;
        OutputPath = string.Empty;
        OpenFolderError = string.Empty;
        _additionalOutput = null;
        NotifyStateChanged();
    }

    private void RevealOutput()
    {
        if (_fileRevealService.TryRevealFile(OutputPath))
        {
            Close();
            return;
        }

        OpenFolderError = _openFolderErrorText();
        OnPropertyChanged(nameof(OpenFolderError));
        OnPropertyChanged(nameof(HasOpenFolderError));
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(OutputPath));
        OnPropertyChanged(nameof(OutputDisplayName));
        OnPropertyChanged(nameof(LatestCommittedOutputPath));
        OnPropertyChanged(nameof(HasLatestCommittedOutput));
        OnPropertyChanged(nameof(OpenFolderError));
        OnPropertyChanged(nameof(HasOpenFolderError));
        OnPropertyChanged(nameof(HasAdditionalOutput));
        OnPropertyChanged(nameof(AdditionalOutputPath));
        OnPropertyChanged(nameof(AdditionalOutputDisplayName));
    }
}
