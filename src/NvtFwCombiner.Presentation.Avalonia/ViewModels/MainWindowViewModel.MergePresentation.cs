namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    public MergePresentationViewModel Merge { get; }

    private void Merge_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(Merge)));
        if (e.PropertyName is nameof(MergePresentationViewModel.IsAbSameTpConflictPromptOpen) or
            nameof(MergePresentationViewModel.IsAbDummyDpPromptOpen))
        {
            NotifyCompositionActionRailVisibilityChanged();
        }
    }

}
