namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Focused Replace-page presentation child.</summary>
    public ReplacePresentationViewModel Replace { get; }

    private void Replace_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Replace));
        if (e.PropertyName == nameof(ReplacePresentationViewModel.IsReplaceSelectionModalOpen))
        {
            NotifyCompositionActionRailVisibilityChanged();
        }
    }
}
