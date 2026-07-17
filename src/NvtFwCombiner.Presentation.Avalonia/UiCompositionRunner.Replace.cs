using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Gets visible CtrlRAM rows for a selected IC and IC-number context.</summary>
    public static IReadOnlyList<CtrlRamRegionViewModel> GetCtrlRamRegions(
        string icId,
        string number,
        string? basePath = null)
    {
        return
        [
            .. WorkbenchCompositionService.GetCtrlRamRegions(icId, number, basePath)
                .Select(region => new CtrlRamRegionViewModel(
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
        string replaceMode,
        string? basePath = null)
    {
        FirmwareSlotKind kind = replaceMode switch
        {
            WorkbenchReplaceModes.Dp => FirmwareSlotKind.Dp,
            WorkbenchReplaceModes.CtrlRam => FirmwareSlotKind.CtrlRam,
            _ => FirmwareSlotKind.Unknown,
        };
        return
        [
            .. WorkbenchCompositionService.GetReplaceInputSlots(icId, number, replaceMode, basePath)
                .Select(slot => new FirmwareSlotViewModel(
                    slot.SlotId,
                    slot.Title,
                    slot.Description,
                    kind,
                    slot.IsOptional)),
        ];
    }

    /// <summary>Gets readable memory-map rows for the selected Replace mode.</summary>
    public static IReadOnlyList<MemoryMapRowViewModel> GetReplaceMemoryMapRows(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength = null,
        string? ctrlRamBasePath = null)
    {
        return
        [
            .. WorkbenchCompositionService.GetReplaceMemoryMapRows(
                    icId,
                    number,
                    replaceMode,
                    dpBaseLength,
                    ctrlRamBasePath)
                .Select(ToMemoryMapRow),
        ];
    }

    /// <summary>Gets visual coverage segments for the selected Replace mode.</summary>
    public static IReadOnlyList<MemoryCoverageSegmentViewModel> GetReplaceCoverageSegments(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength = null,
        string? ctrlRamBasePath = null)
    {
        return
        [
            .. WorkbenchCompositionService.GetReplaceCoverageSegments(
                    icId,
                    number,
                    replaceMode,
                    dpBaseLength,
                    ctrlRamBasePath)
                .Select(segment => new MemoryCoverageSegmentViewModel(
                    segment.RangeLabel,
                    segment.SourceLabel,
                    segment.Detail,
                    segment.Fill,
                    segment.BarWidth,
                    segment.IsChanged)),
        ];
    }

}
