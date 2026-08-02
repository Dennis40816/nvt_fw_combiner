using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class WorkflowSessionPresentationViewModel
{
    internal string CreateFlashCodeOutputFileName(IEnumerable<FirmwareSlotViewModel> candidateSlots)
    {
        return FirmwareOutputNamingProjection.CreateFlashCodeOutputFileName(
            SelectedIc,
            candidateSlots,
            InspectionSession,
            edit: null);
    }

    internal string CreateCtrlRamReplaceOutputFileName(
        IEnumerable<FirmwareSlotViewModel> candidateSlots,
        WorkbenchCtrlRamFirmwareVersionEdit? edit)
    {
        return FirmwareOutputNamingProjection.CreateCtrlRamReplaceOutputFileName(
            SelectedIc,
            candidateSlots,
            InspectionSession,
            edit);
    }
}
