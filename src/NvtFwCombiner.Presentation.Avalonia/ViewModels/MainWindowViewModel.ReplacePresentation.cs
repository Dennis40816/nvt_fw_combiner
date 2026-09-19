namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    public ReplacePresentationViewModel Replace { get; }

    private void Replace_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        PresentationObserver.Invoke(() => OnPropertyChanged(nameof(Replace)));
        if (e.PropertyName is nameof(ReplacePresentationViewModel.IsReplaceSelectionModalOpen) or
            nameof(ReplacePresentationViewModel.IsCtrlRamFirmwareVersionModalOpen))
        {
            NotifyCompositionActionRailVisibilityChanged();
        }
    }

    private FirmwareInspectionSnapshot? GetSelectedReplaceBaseInspection()
    {
        return Replace.ReplaceBaseSlot.CurrentInspectionProjection;
    }

}
