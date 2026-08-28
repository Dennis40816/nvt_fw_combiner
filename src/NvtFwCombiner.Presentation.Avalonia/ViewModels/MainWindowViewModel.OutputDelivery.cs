namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    public OutputDeliveryConfirmationViewModel OutputDelivery { get; }

    /// <summary>Whether closing Build Settings may safely restore its captured Build control.</summary>
    public bool CanRestoreOutputDeliveryFocus => !IsSettingsModalOpen && !IsOtherBlockingSurfaceOpen;

    private void OutputDelivery_OnPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OutputDeliveryConfirmationViewModel.IsOpen))
        {
            OnPropertyChanged(nameof(OutputDelivery));
            NotifyCompositionActionRailVisibilityChanged();
        }
    }
}
