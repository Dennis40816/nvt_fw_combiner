namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    public ReplacePresentationViewModel Replace { get; }

    private void Replace_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Replace));
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

    private Task RefreshSelectedReplaceFirmwareInspectionsAsync()
    {
        return WorkflowSession.RefreshSelectedReplaceFirmwareInspectionsAsync();
    }
}
