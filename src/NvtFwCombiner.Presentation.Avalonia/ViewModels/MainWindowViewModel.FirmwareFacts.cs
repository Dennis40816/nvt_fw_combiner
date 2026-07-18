using NvtFwCombiner.Bootstrap;

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

    private void RefreshFirmwareFacts(FirmwareSlotViewModel slot, bool allowFileReadFallback = true)
    {
        if (!slot.HasFile || !SlotSupportsFirmwareFacts(slot))
        {
            slot.SetFirmwareFacts([]);
            return;
        }

        WorkbenchFirmwareArtifactSnapshot? snapshot = slot.ArtifactSnapshot;
        if (snapshot is null && allowFileReadFallback)
        {
            snapshot = WorkbenchCompositionService.TryCaptureFirmwareArtifact(slot.FilePath!);
        }

        if (snapshot is null)
        {
            slot.SetFirmwareFacts([]);
            return;
        }

        WorkbenchFirmwareInspection inspection = WorkbenchCompositionService.InspectFirmwareArtifact(
            SelectedIc,
            snapshot,
            GetTpSnapshotFor(slot));
        if (slot.ArtifactSnapshot is null)
        {
            slot.SetFirmwareInspection(snapshot, inspection);
        }
        else
        {
            slot.SetFirmwareInspection(inspection);
        }

        slot.SetFirmwareFacts(slot.SlotKind == FirmwareSlotKind.Dp
            ? UiCompositionRunner.GetDpFirmwareSlotFacts(inspection)
            : UiCompositionRunner.GetFirmwareSlotFacts(
                inspection,
                includeBaseFacts: slot.SlotKind == FirmwareSlotKind.Base));
    }

    private static bool SlotSupportsFirmwareFacts(FirmwareSlotViewModel slot)
    {
        return slot.SlotKind is FirmwareSlotKind.Base or FirmwareSlotKind.Dp or FirmwareSlotKind.Tp;
    }
}
