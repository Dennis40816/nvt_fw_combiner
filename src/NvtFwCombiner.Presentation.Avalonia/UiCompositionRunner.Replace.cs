using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

internal static partial class UiCompositionRunner
{
    /// <summary>Projects visible CtrlRAM rows from an already-read inspection snapshot.</summary>
    internal static IReadOnlyList<CtrlRamRegionViewModel> GetCtrlRamRegions(
        IReadOnlyList<CtrlRamRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        return
        [
            .. regions.Select(region => new CtrlRamRegionViewModel(
                region.DisplayName,
                ToRange(region.Start, region.Length),
                ToLength(region.Length),
                region.IsMultiChipOnly)),
        ];
    }

    /// <summary>Projects CtrlRAM input slots from an already-read inspection snapshot.</summary>
    internal static IReadOnlyList<FirmwareSlotViewModel> GetCtrlRamReplaceInputSlots(
        IReadOnlyList<ReplaceInputSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        return
        [
            .. slots.Select(slot => new FirmwareSlotViewModel(
                slot.SlotId,
                slot.Title,
                slot.Description,
                FirmwareSlotKind.CtrlRam,
                slot.IsOptional,
                slot.RegionId,
                slot.AddressSpaceId,
                slot.RegionGroup,
                slot.InputRole,
                compiledSlotId: slot.CompiledSlotId,
                ctrlRamDescriptionFacts: slot.CtrlRamDescription)),
        ];
    }

}
