using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Gets visible CtrlRAM rows for a selected IC and IC-number context.</summary>
    public static IReadOnlyList<CtrlRamRegionViewModel> GetCtrlRamRegions(
        PresentationCompositionServices services,
        string icId,
        string number)
    {
        ArgumentNullException.ThrowIfNull(services);
        return
        [
            .. services.Authoring.GetCtrlRamRegions(icId, number, basePath: null)
                .Select(region => new CtrlRamRegionViewModel(
                    region.DisplayName,
                    ToRange(region.Start, region.Length),
                    ToLength(region.Length),
                    region.IsMultiChipOnly)),
        ];
    }

    /// <summary>Projects visible CtrlRAM rows from an already-read inspection snapshot.</summary>
    public static IReadOnlyList<CtrlRamRegionViewModel> GetCtrlRamRegions(
        IReadOnlyList<WorkbenchCtrlRamRegion> regions)
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

    /// <summary>Gets structured Replace input slots for the selected device context.</summary>
    public static IReadOnlyList<FirmwareSlotViewModel> GetReplaceInputSlots(
        PresentationCompositionServices services,
        string icId,
        string number,
        string replaceMode)
    {
        ArgumentNullException.ThrowIfNull(services);
        FirmwareSlotKind kind = replaceMode switch
        {
            WorkbenchReplaceModes.Dp => FirmwareSlotKind.Dp,
            WorkbenchReplaceModes.CtrlRam => FirmwareSlotKind.CtrlRam,
            _ => FirmwareSlotKind.Unknown,
        };
        return
        [
            .. services.Memory.GetReplaceInputSlots(icId, number, replaceMode, basePath: null)
                .Select(slot => new FirmwareSlotViewModel(
                    slot.SlotId,
                    slot.Title,
                    slot.Description,
                    kind,
                    slot.IsOptional,
                    slot.RegionId,
                    slot.AddressSpaceId,
                    slot.RegionGroup,
                    slot.InputRole,
                    compiledSlotId: slot.CompiledSlotId)),
        ];
    }

    /// <summary>Projects CtrlRAM input slots from an already-read inspection snapshot.</summary>
    public static IReadOnlyList<FirmwareSlotViewModel> GetCtrlRamReplaceInputSlots(
        IReadOnlyList<WorkbenchReplaceInputSlot> slots)
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
                compiledSlotId: slot.CompiledSlotId)),
        ];
    }

    /// <summary>Projects one Replace memory display snapshot.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetReplaceMemoryDisplay(
        PresentationCompositionServices services,
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength = null,
        IEnumerable<string>? selectedRegionIds = null,
        ShellTextResources? text = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        WorkbenchMemoryDisplay display = services.Memory.GetReplaceMemoryDisplay(
            icId,
            number,
            replaceMode,
            dpBaseLength,
            ctrlRamBasePath: null);
        if (selectedRegionIds is not null &&
            string.Equals(replaceMode, WorkbenchReplaceModes.CtrlRam, StringComparison.Ordinal))
        {
            display = services.Memory.ApplyReplaceCoverageSelection(display, selectedRegionIds);
        }

        return (
            display.RangeLabel,
            [.. display.MemoryMapRows.Select(ToMemoryMapRow)],
            [.. display.CoverageSegments.Select(segment => ToMemoryCoverageSegment(segment, text))]);
    }

    /// <summary>Projects General Replace from one canonical Application admission result.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetGeneralReplaceMemoryDisplay(
        PresentationCompositionServices services,
        long referenceCapacity,
        GeneralAuthoringAdmissionResult admission)
    {
        ArgumentNullException.ThrowIfNull(services);
        WorkbenchMemoryDisplay display = services.Memory
            .GetGeneralReplaceMemoryDisplay(referenceCapacity, admission);
        return (
            display.RangeLabel,
            [.. display.MemoryMapRows.Select(ToMemoryMapRow)],
            [.. display.CoverageSegments.Select(segment => ToMemoryCoverageSegment(segment))]);
    }

    /// <summary>Projects stable General Replace authoring issues beside the Reference layout.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetGeneralReplaceMemoryDisplay(
        PresentationCompositionServices services,
        long referenceCapacity,
        IReadOnlyList<AuthoringMappingState> authoringStates)
    {
        ArgumentNullException.ThrowIfNull(services);
        WorkbenchMemoryDisplay display = services.Memory
            .GetGeneralReplaceMemoryDisplay(referenceCapacity, authoringStates);
        return (
            display.RangeLabel,
            [.. display.MemoryMapRows.Select(ToMemoryMapRow)],
            [.. display.CoverageSegments.Select(segment => ToMemoryCoverageSegment(segment))]);
    }

    /// <summary>Projects one already-read Replace memory display snapshot.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetReplaceMemoryDisplay(
        PresentationCompositionServices services,
        WorkbenchMemoryDisplay display,
        IEnumerable<string>? selectedRegionIds = null,
        ShellTextResources? text = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(display);
        if (selectedRegionIds is not null)
        {
            display = services.Memory.ApplyReplaceCoverageSelection(display, selectedRegionIds);
        }

        return (
            display.RangeLabel,
            [.. display.MemoryMapRows.Select(ToMemoryMapRow)],
            [.. display.CoverageSegments.Select(segment => ToMemoryCoverageSegment(segment, text))]);
    }

}
