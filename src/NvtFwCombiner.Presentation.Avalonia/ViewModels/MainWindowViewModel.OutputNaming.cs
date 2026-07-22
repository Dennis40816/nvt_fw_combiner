using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string CreateFlashCodeOutputFileName(IEnumerable<FirmwareSlotViewModel> candidateSlots)
    {
        return FirmwareOutputNamingProjection.CreateFlashCodeOutputFileName(
            SelectedIc,
            candidateSlots,
            _firmwareInspectionSession,
            edit: null);
    }

    /// <summary>Creates the CtrlRAM Replace output name from the confirmed version choice.</summary>
    public string CreateCtrlRamReplaceOutputFileName(WorkbenchCtrlRamFirmwareVersionEdit? edit)
    {
        return FirmwareOutputNamingProjection.CreateFlashCodeOutputFileName(
            SelectedIc,
            ReplaceSlots.Concat([ReplaceBaseSlot]),
            _firmwareInspectionSession,
            edit);
    }
}
