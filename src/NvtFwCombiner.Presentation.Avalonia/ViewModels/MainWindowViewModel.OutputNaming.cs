using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string CreateFlashCodeOutputFileName(IEnumerable<FirmwareSlotViewModel> candidateSlots)
    {
        ArgumentNullException.ThrowIfNull(candidateSlots);
        return WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
            SelectedIc,
            [.. candidateSlots.Select(ToInspectedOutputNameCandidate)]).FileName;
    }

    private static WorkbenchInspectedOutputNameCandidate ToInspectedOutputNameCandidate(FirmwareSlotViewModel slot)
    {
        return new WorkbenchInspectedOutputNameCandidate(
            slot.SlotKind switch
            {
                FirmwareSlotKind.Dp => WorkbenchOutputNameCandidateKind.Dp,
                FirmwareSlotKind.Tp => WorkbenchOutputNameCandidateKind.Tp,
                FirmwareSlotKind.CtrlRam => WorkbenchOutputNameCandidateKind.CtrlRam,
                FirmwareSlotKind.Base => WorkbenchOutputNameCandidateKind.Base,
                FirmwareSlotKind.Unknown => WorkbenchOutputNameCandidateKind.Unknown,
                _ => WorkbenchOutputNameCandidateKind.Unknown,
            },
            slot.FirmwareInspection);
    }
}
