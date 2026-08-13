using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    public BuildResultViewModel BuildResult { get; }

    internal bool TryShowBuildCompleted(CompositionRunResult result, bool build)
    {
        return BuildResult.TryShow(result, build);
    }

    internal void NotifyFileRevealFailed()
    {
        Reports.SetShellToast(Text.FileRevealFailedTitle, Text.FileRevealFailedDetail);
    }

    private void RevealFile(string? filePath)
    {
        if (!_fileRevealService.TryRevealFile(filePath))
        {
            NotifyFileRevealFailed();
        }
    }

    private void BuildResult_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BuildResultViewModel.IsOpen))
        {
            OnPropertyChanged(nameof(BuildResult));
        }

        if (e.PropertyName is nameof(BuildResultViewModel.IsOpen) or
            nameof(BuildResultViewModel.HasLatestCommittedOutput))
        {
            NotifyCompositionActionRailVisibilityChanged();
        }
    }
}
