using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
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

    /// <summary>Projects DP Replace input slots from compiler-owned discovery.</summary>
    internal static IReadOnlyList<FirmwareSlotViewModel> GetDpReplaceInputSlots(
        CompiledAuthoringSelectionSnapshot selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return
        [
            .. selection.InputBindings
                .Where(static binding => !StringComparer.Ordinal.Equals(
                    binding.AddressSpaceId,
                    CompositionAddressSpaceIds.ReferenceBase))
                .DistinctBy(static binding => binding.AddressSpaceId)
                .OrderBy(static binding => binding.AddressSpaceId, StringComparer.Ordinal)
                .Select(binding =>
                {
                    InputSelectionMemberReadiness readiness = selection.Slots.Single(slot =>
                        StringComparer.Ordinal.Equals(slot.SlotId, binding.SlotId));
                    bool isLdc = StringComparer.Ordinal.Equals(
                        binding.AddressSpaceId,
                        CompositionAddressSpaceIds.LdcReplacement);
                    bool isInitialCode = StringComparer.Ordinal.Equals(
                        binding.AddressSpaceId,
                        CompositionAddressSpaceIds.InitialCodeReplacement);
                    return new FirmwareSlotViewModel(
                        isLdc ? CompositionSlotIds.ReplaceLdc : CompositionSlotIds.ReplaceDp,
                        isLdc
                            ? "LDC replacement BIN"
                            : isInitialCode
                                ? "Initial Code replacement BIN"
                                : "DP replacement BIN",
                        isLdc
                            ? "LDC payload declared by the compiled DP Replace profile."
                            : "Replacement DP payload declared by the compiled DP Replace profile.",
                        FirmwareSlotKind.Dp,
                        isOptional: !readiness.IsRequired,
                        regionId: null,
                        binding.AddressSpaceId,
                        ReplaceRegionGroup.Common,
                        ReplaceInputRole.Dp,
                        compiledSlotId: binding.SlotId);
                }),
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
