namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Firmware metadata helpers for selected BIN slots.</summary>
public sealed partial class MainWindowViewModel
{
    private void RefreshAllSelectedSlotFirmwareFacts()
    {
        HashSet<FirmwareSlotViewModel> slots = [];
        foreach (FirmwareSlotViewModel slot in MergeSlots.Concat(ReplaceSlots).Concat([ReplaceBaseSlot]))
        {
            if (slots.Add(slot))
            {
                RefreshFirmwareFacts(slot);
            }
        }
    }

    private void RefreshFirmwareFacts(FirmwareSlotViewModel slot)
    {
        if (!slot.HasFile || !SlotSupportsFirmwareFacts(slot))
        {
            slot.SetFirmwareFacts([]);
            return;
        }

        slot.SetFirmwareFacts(slot.SlotKind == FirmwareSlotKind.Dp
            ? UiCompositionRunner.GetDpFirmwareSlotFacts(
                SelectedIc,
                slot.FilePath!,
                slot.SlotId == MergeDpSlotId ? _mergeTpSlot.FilePath : null)
            : UiCompositionRunner.GetFirmwareSlotFacts(
                SelectedIc,
                slot.FilePath!,
                includeInvalid: slot.SlotKind == FirmwareSlotKind.Base));
    }

    private static bool SlotSupportsFirmwareFacts(FirmwareSlotViewModel slot)
    {
        return slot.SlotKind is FirmwareSlotKind.Base or FirmwareSlotKind.Dp or FirmwareSlotKind.Tp;
    }
}
