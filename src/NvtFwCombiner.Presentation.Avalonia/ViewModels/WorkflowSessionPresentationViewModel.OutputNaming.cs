namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class WorkflowSessionPresentationViewModel
{
    internal string CreateFlashCodeOutputFileName(IEnumerable<FirmwareSlotViewModel> candidateSlots)
    {
        return FirmwareOutputNamingProjection.CreateFlashCodeOutputFileName(
            _compositionServices.OutputNaming,
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
            _compositionServices.OutputNaming,
            SelectedIc,
            candidateSlots,
            InspectionSession,
            edit);
    }
}
