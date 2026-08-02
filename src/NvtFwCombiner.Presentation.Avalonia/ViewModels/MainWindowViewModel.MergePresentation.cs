namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Focused Merge-page presentation child.</summary>
    public MergePresentationViewModel Merge { get; }

    private void Merge_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Merge));
        if (e.PropertyName == nameof(MergePresentationViewModel.IsAbAFlashCodeDeliveryPromptOpen))
        {
            NotifyCompositionActionRailVisibilityChanged();
        }
    }
}
