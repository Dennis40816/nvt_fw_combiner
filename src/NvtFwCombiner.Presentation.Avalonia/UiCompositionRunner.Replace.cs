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
        return
        [
            .. WorkbenchCompositionService.GetReplaceInputSlots(icId, number, replaceMode, basePath)
                .Select(slot => new FirmwareSlotViewModel(
                    slot.SlotId,
                    slot.Title,
                    slot.Description,
                    slot.IsOptional)),
        ];
    }

    /// <summary>Gets the default Replace build output file name.</summary>
    public static string GetReplaceDefaultOutputFileName(string icId, string replaceMode)
    {
        return WorkbenchCompositionService.GetReplaceDefaultOutputFileName(icId, replaceMode);
    }

    /// <summary>Gets catalog-backed DP Replace policy text.</summary>
    public static string GetDpReplacePolicySummary(string icId)
    {
        return WorkbenchCompositionService.GetDpReplacePolicySummary(icId);
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

    /// <summary>Gets TP Overview address coverage text for the selected Replace context.</summary>
    public static string GetReplaceMemoryRangeLabel(string icId, string number)
    {
        return WorkbenchCompositionService.GetReplaceMemoryRangeLabel(icId, number, replaceMode: string.Empty);
    }

    /// <summary>Gets TP Overview address coverage text for the selected Replace context and mode.</summary>
    public static string GetReplaceMemoryRangeLabel(
        string icId,
        string number,
        string replaceMode,
        long? dpBaseLength = null,
        string? ctrlRamBasePath = null)
    {
        return WorkbenchCompositionService.GetReplaceMemoryRangeLabel(
            icId,
            number,
            replaceMode,
            dpBaseLength,
            ctrlRamBasePath);
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

    /// <summary>Runs Replace preview or build through the Bootstrap workbench facade.</summary>
    public static ValueTask<WorkbenchRunResult> RunReplaceAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit = null)
    {
        return WorkbenchCompositionService.RunReplaceAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            build,
            cancellationToken,
            outputPath,
            ctrlRamFirmwareVersionEdit);
    }

    /// <summary>Runs Replace preview or build with workbench-authored General Replace mappings.</summary>
    public static ValueTask<WorkbenchRunResult> RunReplaceAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> generalReplaceMappings,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit = null)
    {
        return WorkbenchCompositionService.RunReplaceAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            generalReplaceMappings,
            build,
            cancellationToken,
            outputPath,
            ctrlRamFirmwareVersionEdit);
    }

}
