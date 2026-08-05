using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReplacePresentationViewModel
{
    internal void ApplyFirmwareSlotText()
    {
        ReplaceBaseSlot.ApplyDisplayText(
            Text.GetReplaceBaseTitle(SelectedReplaceMode),
            Text.GetReplaceBaseDescription(
                SelectedReplaceMode,
                _stateBindings.IsWorkflowLoaded()
                    ? _compositionServices.Memory.GetDpReplaceReferenceCapacityLabel(SelectedIc)
                    : null),
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);

        foreach (FirmwareSlotViewModel slot in ReplaceSlots.Where(slot => !ReferenceEquals(slot, ReplaceBaseSlot)))
        {
            ApplyReplaceSlotText(slot);
        }

        foreach (FirmwareSlotViewModel slot in ReplaceSlots)
        {
            slot.ApplyExperienceText(Text);
        }
    }

    private void ApplyReplaceSlotText(FirmwareSlotViewModel slot)
    {
        if (string.Equals(slot.SlotId, WorkbenchSlotIds.ReplaceDp, StringComparison.Ordinal))
        {
            slot.ApplyDisplayText(
                Text.DpReplacementBinTitle,
                slot.Description,
                Text.RequiredLabel,
                Text.OptionalLabel,
                Text.NoBinSelectedLabel);
            return;
        }

        slot.ApplyDisplayText(
            slot.Title,
            slot.Description,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
    }

}
