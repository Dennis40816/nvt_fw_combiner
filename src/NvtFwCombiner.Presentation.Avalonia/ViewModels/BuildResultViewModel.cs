using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed class BuildResultViewModel : ObservableObject
{
    private readonly IFileRevealService _fileRevealService;
    private readonly Func<string> _openFolderErrorText;
    private CompositionDeliveryArtifact? _additionalOutput;

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

    public bool IsOpen { get; private set; }

    public string OutputPath { get; private set; } = string.Empty;

    public string OutputDisplayName => Path.GetFileName(OutputPath);

    public string LatestCommittedOutputPath { get; private set; } = string.Empty;

    /// <summary>True after this application session has committed at least one output BIN.</summary>
    public bool HasLatestCommittedOutput => !string.IsNullOrWhiteSpace(LatestCommittedOutputPath);

    public string OpenFolderError { get; private set; } = string.Empty;

    public bool HasOpenFolderError => !string.IsNullOrWhiteSpace(OpenFolderError);

    public bool HasAdditionalOutput => _additionalOutput is not null;

    public string AdditionalOutputPath => _additionalOutput?.OutputPath ?? string.Empty;

    public string AdditionalOutputDisplayName => _additionalOutput?.OutputFileName ?? string.Empty;

    public IRelayCommand CloseCommand { get; }

    public IRelayCommand RevealOutputCommand { get; }

    internal bool TryShow(CompositionRunResult result, bool build)
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
