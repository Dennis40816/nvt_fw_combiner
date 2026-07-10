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

    /// <summary>Gets profile-authorized General Replace ranges for the hexadecimal editor range picker.</summary>
    public static IReadOnlyList<GeneralReplaceEditableRangeViewModel> GetGeneralReplaceEditableRanges(
        string icId,
        string number,
        string? basePath,
        WorkbenchGeneralReplaceBaseSnapshot? baseSnapshot = null)
    {
        return
        [
            .. WorkbenchCompositionService.GetGeneralReplaceEditableRanges(icId, number, basePath, baseSnapshot)
                .Select(range => new GeneralReplaceEditableRangeViewModel(
                    range.RegionId,
                    range.DisplayName,
                    ToRange(range.Start, range.EndInclusive - range.Start + 1),
                    FormattableString.Invariant($"0x{range.Start:X6}"),
                    FormattableString.Invariant($"0x{range.EndInclusive:X6}"),
                    range.RequiresPostbuild,
                    range.Detail)),
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
        string? outputPath = null)
    {
        return WorkbenchCompositionService.RunReplaceAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            build,
            cancellationToken,
            outputPath);
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
        string? outputPath = null)
    {
        return WorkbenchCompositionService.RunReplaceAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            generalReplaceMappings,
            build,
            cancellationToken,
            outputPath);
    }

    /// <summary>Runs Replace with file-backed mappings and host-owned virtual General Replace patches.</summary>
    public static ValueTask<WorkbenchRunResult> RunReplaceAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> generalReplaceMappings,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> generalReplacePatches,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        WorkbenchGeneralReplaceBaseSnapshot? baseSnapshot = null)
    {
        return WorkbenchCompositionService.RunReplaceAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            generalReplaceMappings,
            generalReplacePatches,
            build,
            cancellationToken,
            outputPath,
            baseSnapshot);
    }

    /// <summary>Loads one immutable base image snapshot for an experimental Hex Editor session.</summary>
    public static bool TryLoadGeneralReplaceBaseSnapshot(
        string basePath,
        out WorkbenchGeneralReplaceBaseSnapshot? snapshot,
        out string? errorMessage)
    {
        bool loaded = WorkbenchCompositionService.TryLoadGeneralReplaceBaseSnapshot(
            basePath,
            out snapshot,
            out errorMessage);
        return loaded;
    }

    /// <summary>Gets a fixed-width base-BIN hexadecimal viewport with staged patches overlaid in memory.</summary>
    public static WorkbenchGeneralReplaceHexViewport CreateGeneralReplaceHexViewport(
        string basePath,
        long viewportStart,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> patches)
    {
        return WorkbenchCompositionService.CreateGeneralReplaceHexViewport(basePath, viewportStart, patches);
    }

    /// <summary>Gets a fixed-width hexadecimal viewport from the loaded in-memory base snapshot.</summary>
    public static WorkbenchGeneralReplaceHexViewport CreateGeneralReplaceHexViewport(
        WorkbenchGeneralReplaceBaseSnapshot baseSnapshot,
        long viewportStart,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> patches)
    {
        return WorkbenchCompositionService.CreateGeneralReplaceHexViewport(baseSnapshot, viewportStart, patches);
    }
}
