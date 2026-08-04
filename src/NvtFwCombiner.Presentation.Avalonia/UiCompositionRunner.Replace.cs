using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Gets visible CtrlRAM rows for a selected IC and IC-number context.</summary>
    public static IReadOnlyList<CtrlRamRegionViewModel> GetCtrlRamRegions(
        string icId,
        string number)
    {
        return
        [
            .. WorkbenchCompositionService.GetCtrlRamRegions(icId, number, basePath: null)
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
        string icId,
        string number,
        string replaceMode)
    {
        FirmwareSlotKind kind = replaceMode switch
        {
            WorkbenchReplaceModes.Dp => FirmwareSlotKind.Dp,
            WorkbenchReplaceModes.CtrlRam => FirmwareSlotKind.CtrlRam,
            _ => FirmwareSlotKind.Unknown,
        };
        return
        [
            .. WorkbenchCompositionService.GetReplaceInputSlots(icId, number, replaceMode, basePath: null)
                .Select(slot => new FirmwareSlotViewModel(
                    slot.SlotId,
                    slot.Title,
                    slot.Description,
                    kind,
                    slot.IsOptional,
                    slot.RegionId,
                    slot.AddressSpaceId,
                    slot.RegionGroup,
                    slot.InputRole)),
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
                slot.InputRole)),
        ];
    }

    /// <summary>Projects one Replace memory display snapshot.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetReplaceMemoryDisplay(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength = null,
        IEnumerable<string>? selectedRegionIds = null,
        ShellTextResources? text = null)
    {
        WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetReplaceMemoryDisplay(
            icId,
            number,
            replaceMode,
            dpBaseLength,
            ctrlRamBasePath: null);
        if (selectedRegionIds is not null &&
            string.Equals(replaceMode, WorkbenchReplaceModes.CtrlRam, StringComparison.Ordinal))
        {
            display = WorkbenchCompositionService.ApplyReplaceCoverageSelection(display, selectedRegionIds);
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
        long referenceCapacity,
        GeneralAuthoringAdmissionResult admission)
    {
        WorkbenchMemoryDisplay display = WorkbenchCompositionService
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
        long referenceCapacity,
        IReadOnlyList<AuthoringMappingState> authoringStates)
    {
        WorkbenchMemoryDisplay display = WorkbenchCompositionService
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
        WorkbenchMemoryDisplay display,
        IEnumerable<string>? selectedRegionIds = null,
        ShellTextResources? text = null)
    {
        ArgumentNullException.ThrowIfNull(display);
        if (selectedRegionIds is not null)
        {
            display = WorkbenchCompositionService.ApplyReplaceCoverageSelection(display, selectedRegionIds);
        }

        return (
            display.RangeLabel,
            [.. display.MemoryMapRows.Select(ToMemoryMapRow)],
            [.. display.CoverageSegments.Select(segment => ToMemoryCoverageSegment(segment, text))]);
    }

}
