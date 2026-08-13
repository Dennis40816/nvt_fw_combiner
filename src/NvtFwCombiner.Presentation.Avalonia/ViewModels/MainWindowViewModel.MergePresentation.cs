namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    public MergePresentationViewModel Merge { get; }

    private void Merge_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Merge));
        if (e.PropertyName == nameof(MergePresentationViewModel.IsAbAFlashCodeDeliveryPromptOpen))
        {
            NotifyCompositionActionRailVisibilityChanged();
        }
    }

    private bool IsFirmwareInspectionLoading()
    {
        return WorkflowSession.IsFirmwareInspectionLoading;
    }

    private long? GetInspectedFileLength(FirmwareSlotViewModel slot)
    {
        return slot.InspectedFileLength;
    }

    private ReportPresentationViewModel GetReportPresentation()
    {
        return Reports;
    }

    private Task RefreshSelectedMergeFirmwareInspectionsAsync()
    {
        return WorkflowSession.RefreshSelectedMergeFirmwareInspectionsAsync();
    }

    private void NotifyMergeSharedContextChanged()
    {
        WorkflowSession.NotifyContextTextChanged();
    }
}
