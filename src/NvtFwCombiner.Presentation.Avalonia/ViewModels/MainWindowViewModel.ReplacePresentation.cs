using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Focused Replace-page presentation child.</summary>
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

    private WorkbenchFirmwareInspection? GetSelectedReplaceBaseInspection()
    {
        return WorkflowSession.InspectionSession.TryGetBase(
            SelectedIc,
            Replace.ReplaceBaseSlot.FilePath,
            out WorkbenchFirmwareInspection inspection)
                ? inspection
                : null;
    }

    private Task RefreshSelectedReplaceFirmwareInspectionsAsync()
    {
        return WorkflowSession.RefreshSelectedReplaceFirmwareInspectionsAsync();
    }
}
