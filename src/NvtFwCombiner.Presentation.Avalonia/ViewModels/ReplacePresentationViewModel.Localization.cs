namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReplacePresentationViewModel
{
    internal void ApplyFirmwareSlotText()
    {
        ReplaceBaseSlot.ApplyDisplayText(
            Text.GetReplaceBaseTitle(SelectedReplaceMode),
            Text.GetReplaceBaseDescription(
                SelectedReplaceMode,
                _stateBindings.IsWorkflowLoaded() && HasSelectedIc
                    ? _compositionServices.Capabilities.GetDpReplaceReferenceCapacityLabel(SelectedIc)
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
        (string title, string description) = Text.GetReplaceInputText(
            slot.AddressSpaceId,
            slot.ReplaceInputRole,
            slot.RegionGroup,
            slot.DeclaredTitle,
            slot.DeclaredDescription,
            slot.CtrlRamDescriptionFacts);
        slot.ApplyDisplayText(
            title,
            description,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
    }

}
