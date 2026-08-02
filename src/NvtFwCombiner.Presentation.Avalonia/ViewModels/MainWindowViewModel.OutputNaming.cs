using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string CreateFlashCodeOutputFileName(IEnumerable<FirmwareSlotViewModel> candidateSlots)
    {
        return FirmwareOutputNamingProjection.CreateFlashCodeOutputFileName(
            SelectedIc,
            candidateSlots,
            WorkflowSession.InspectionSession,
            edit: null);
    }

    private string CreateCtrlRamReplaceOutputFileName(
        IEnumerable<FirmwareSlotViewModel> candidateSlots,
        WorkbenchCtrlRamFirmwareVersionEdit? edit)
    {
        return FirmwareOutputNamingProjection.CreateCtrlRamReplaceOutputFileName(
            SelectedIc,
            candidateSlots,
            WorkflowSession.InspectionSession,
            edit);
    }
}
