using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string CreateFlashCodeOutputFileName(IEnumerable<FirmwareSlotViewModel> candidateSlots)
    {
        ArgumentNullException.ThrowIfNull(candidateSlots);
        return WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
            SelectedIc,
            [.. candidateSlots.Select(ToOutputNameInspectionCandidate)]).FileName;
    }

    /// <summary>Creates the CtrlRAM Replace output name from the confirmed version choice.</summary>
    public string CreateCtrlRamReplaceOutputFileName(WorkbenchCtrlRamFirmwareVersionEdit? edit)
    {
        WorkbenchOutputNameInspectionCandidate[] candidates =
            [.. ReplaceSlots.Concat([ReplaceBaseSlot]).Select(ToOutputNameInspectionCandidate)];
        return edit is null
            ? WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
                SelectedIc,
                candidates).FileName
            : WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(
                SelectedIc,
                candidates,
                edit).FileName;
    }

    private WorkbenchOutputNameInspectionCandidate ToOutputNameInspectionCandidate(FirmwareSlotViewModel slot)
    {
        WorkbenchFirmwareInspection? inspection =
            slot.FilePath is { } path &&
            _firmwareFileProjections.TryGetValue(slot.SlotId, out FirmwareFileProjection projection) &&
            projection.Matches(path)
                ? projection.Inspection
                : null;
        return new WorkbenchOutputNameInspectionCandidate(
            slot.SlotKind switch
            {
                FirmwareSlotKind.Dp => WorkbenchOutputNameCandidateKind.Dp,
                FirmwareSlotKind.Tp => WorkbenchOutputNameCandidateKind.Tp,
                FirmwareSlotKind.CtrlRam => WorkbenchOutputNameCandidateKind.CtrlRam,
                FirmwareSlotKind.Base => WorkbenchOutputNameCandidateKind.Base,
                FirmwareSlotKind.Unknown => WorkbenchOutputNameCandidateKind.Unknown,
                _ => WorkbenchOutputNameCandidateKind.Unknown,
            },
            inspection);
    }
}
