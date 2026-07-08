using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string CreateFlashCodeOutputFileName()
    {
        return UiCompositionRunner.CreateFlashCodeOutputFileName(
            SelectedIc,
            [.. EnumerateVersionCandidateSlots().Select(ToOutputNameCandidate)]);
    }

    private IEnumerable<FirmwareSlotViewModel> EnumerateVersionCandidateSlots()
    {
        return MergeSlots
            .Concat(ReplaceSlots)
            .Concat([ReplaceBaseSlot]);
    }

    private static WorkbenchOutputNameCandidate ToOutputNameCandidate(FirmwareSlotViewModel slot)
    {
        return new WorkbenchOutputNameCandidate(
            slot.SlotKind switch
            {
                FirmwareSlotKind.Dp => WorkbenchOutputNameCandidateKind.Dp,
                FirmwareSlotKind.Tp => WorkbenchOutputNameCandidateKind.Tp,
                FirmwareSlotKind.CtrlRam => WorkbenchOutputNameCandidateKind.CtrlRam,
                FirmwareSlotKind.Base => WorkbenchOutputNameCandidateKind.Base,
                FirmwareSlotKind.Unknown => WorkbenchOutputNameCandidateKind.Unknown,
                _ => WorkbenchOutputNameCandidateKind.Unknown,
            },
            slot.FilePath);
    }
}
