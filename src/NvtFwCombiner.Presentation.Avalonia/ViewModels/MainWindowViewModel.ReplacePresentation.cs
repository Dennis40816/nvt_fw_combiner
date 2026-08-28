using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    public ReplacePresentationViewModel Replace { get; }

    private void Replace_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Replace));
        if (e.PropertyName == nameof(ReplacePresentationViewModel.SelectedReplaceMode) &&
            !string.IsNullOrWhiteSpace(Replace.SelectedReplaceMode))
        {
            RecordDebugActivity(
                SystemActivityCodes.ModeSelected,
                SystemActivityCategory.Workflow,
                Replace.SelectedReplaceMode,
                "replace");
        }
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
