using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MergePresentationViewModel
{
    internal void ApplyFirmwareSlotText()
    {
        MergeDpSlot.ApplyDisplayText(
            "DP BIN",
            Text.MergeDpSlotDescription,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
        MergeTpSlot.ApplyDisplayText(
            "TP BIN",
            Text.MergeTpSlotDescription,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
        MergeLdcSlot.ApplyDisplayText(
            "LDC BIN",
            Text.MergeLdcSlotDescription,
            Text.RequiredLabel,
            Text.OptionalLabel,
            Text.NoBinSelectedLabel);
        foreach (FirmwareSlotViewModel slot in new[] { MergeDpSlot, MergeTpSlot, MergeLdcSlot })
        {
            slot.ApplyExperienceText(Text);
        }

        if (IsNormalMergeModeSelected)
        {
            RefreshStandardMergeAuthoringState();
        }
        foreach (CompiledAuthoringInputBinding input in _abMergeBindingsByAddressSpace.Values)
        {
            if (AbMergeSlotsByAddressSpace.TryGetValue(input.AddressSpaceId, out FirmwareSlotViewModel? slot))
            {
                slot.ApplyDisplayText(
                    ShellTextResources.GetAbSlotTitle(input.Role),
                    Text.GetAbSlotDescription(input),
                    Text.RequiredLabel,
                    Text.OptionalLabel,
                    Text.NoBinSelectedLabel);
            }
        }
    }

}
