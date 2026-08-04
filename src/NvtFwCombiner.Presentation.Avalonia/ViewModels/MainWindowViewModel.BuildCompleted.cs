using System.ComponentModel;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Focused successful Build-result and latest-output presentation.</summary>
    public BuildResultViewModel BuildResult { get; }

    internal bool TryShowBuildCompleted(WorkbenchRunResult result, bool build)
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
