using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Thin UI adapter for invoking Bootstrap workbench contracts.</summary>
public static class UiCompositionRunner
{
    /// <summary>Returns true when the selected IC has a built-in standard merge profile.</summary>
    public static bool IsStandardMergeSupported(string icId)
    {
        return WorkbenchCompositionService.IsStandardMergeSupported(icId);
    }

    /// <summary>Gets the built-in standard merge profile id for the selected IC, if any.</summary>
    public static string? GetStandardMergeProfileId(string icId)
    {
        return WorkbenchCompositionService.GetStandardMergeProfileId(icId);
    }

    /// <summary>Gets required standard merge input address spaces for the selected IC.</summary>
    public static IReadOnlyList<string> GetStandardMergeRequiredAddressSpaces(string icId)
    {
        return WorkbenchCompositionService.GetStandardMergeRequiredAddressSpaces(icId);
    }

    /// <summary>Gets the profile-owned default Standard Merge output file name for the selected IC.</summary>
    public static string GetStandardMergeDefaultOutputFileName(string icId)
    {
        return WorkbenchCompositionService.GetStandardMergeDefaultOutputFileName(icId);
    }

    /// <summary>Gets selectable IC ids from the workbench catalog.</summary>
    public static IReadOnlyList<string> GetSupportedIcIds()
    {
        return WorkbenchCompositionService.GetSupportedIcIds();
    }

    /// <summary>Gets supported IC-number choices from the workbench catalog.</summary>
    public static IReadOnlyList<string> GetNumberChoices(string icId)
    {
        return WorkbenchCompositionService.GetNumberChoices(icId);
    }

    /// <summary>Gets visible CtrlRAM rows for a selected IC and IC-number context.</summary>
    public static IReadOnlyList<CtrlRamRegionViewModel> GetCtrlRamRegions(string icId, string number)
    {
        return
        [
            .. WorkbenchCompositionService.GetCtrlRamRegions(icId, number)
                .Select(region => new CtrlRamRegionViewModel(
                    region.DisplayName,
                    ToHex(region.Start),
                    ToHex(region.Length),
                    region.IsMultiChipOnly)),
        ];
    }

    /// <summary>Gets structured Replace input slots for the selected device context.</summary>
    public static IReadOnlyList<FirmwareSlotViewModel> GetReplaceInputSlots(
        string icId,
        string number,
        string replaceMode)
    {
        return
        [
            .. WorkbenchCompositionService.GetReplaceInputSlots(icId, number, replaceMode)
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

    /// <summary>Gets readable memory-map rows for the selected Standard Merge profile.</summary>
    public static IReadOnlyList<MemoryMapRowViewModel> GetStandardMergeMemoryMapRows(string icId)
    {
        return
        [
            .. WorkbenchCompositionService.GetStandardMergeMemoryMapRows(icId)
                .Select(ToMemoryMapRow),
        ];
    }

    /// <summary>Gets output address coverage text for the selected Standard Merge profile.</summary>
    public static string GetStandardMergeMemoryRangeLabel(string icId)
    {
        return WorkbenchCompositionService.GetStandardMergeMemoryRangeLabel(icId);
    }

    /// <summary>Gets final visual coverage segments for the selected Standard Merge profile.</summary>
    public static IReadOnlyList<MemoryCoverageSegmentViewModel> GetStandardMergeCoverageSegments(string icId)
    {
        return
        [
            .. WorkbenchCompositionService.GetStandardMergeCoverageSegments(icId)
                .Select(segment => new MemoryCoverageSegmentViewModel(
                    segment.RangeLabel,
                    segment.SourceLabel,
                    segment.Detail,
                    segment.Fill,
                    segment.BarWidth)),
        ];
    }

    /// <summary>Gets readable memory-map rows for the selected Replace mode.</summary>
    public static IReadOnlyList<MemoryMapRowViewModel> GetReplaceMemoryMapRows(
        string icId,
        string number,
        string replaceMode)
    {
        return
        [
            .. WorkbenchCompositionService.GetReplaceMemoryMapRows(icId, number, replaceMode)
                .Select(ToMemoryMapRow),
        ];
    }

    /// <summary>Gets TP Overview address coverage text for the selected Replace context.</summary>
    public static string GetReplaceMemoryRangeLabel(string icId, string number)
    {
        return WorkbenchCompositionService.GetReplaceMemoryRangeLabel(icId, number, replaceMode: string.Empty);
    }

    /// <summary>Gets TP Overview address coverage text for the selected Replace context and mode.</summary>
    public static string GetReplaceMemoryRangeLabel(string icId, string number, string replaceMode)
    {
        return WorkbenchCompositionService.GetReplaceMemoryRangeLabel(icId, number, replaceMode);
    }

    /// <summary>Gets visual coverage segments for the selected Replace mode.</summary>
    public static IReadOnlyList<MemoryCoverageSegmentViewModel> GetReplaceCoverageSegments(
        string icId,
        string number,
        string replaceMode)
    {
        return
        [
            .. WorkbenchCompositionService.GetReplaceCoverageSegments(icId, number, replaceMode)
                .Select(segment => new MemoryCoverageSegmentViewModel(
                    segment.RangeLabel,
                    segment.SourceLabel,
                    segment.Detail,
                    segment.Fill,
                    segment.BarWidth,
                    segment.IsChanged)),
        ];
    }

    /// <summary>Gets catalog and tool summary data for the Settings page.</summary>
    public static WorkbenchSettingsSnapshot GetSettingsSnapshot()
    {
        return WorkbenchCompositionService.GetSettingsSnapshot();
    }

    /// <summary>Runs Standard Merge preview or build through the application core.</summary>
    public static ValueTask<WorkbenchRunResult> RunStandardMergeAsync(
        string icId,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        return WorkbenchCompositionService.RunStandardMergeAsync(icId, slotPaths, build, cancellationToken, outputPath);
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

    private static MemoryMapRowViewModel ToMemoryMapRow(WorkbenchMemoryMapRow row)
    {
        return new MemoryMapRowViewModel(
            row.RangeLabel,
            row.BeforeSource,
            row.ActionLabel,
            row.AfterSource,
            row.Detail);
    }

    private static string ToHex(long value)
    {
        return $"0x{value:X5}";
    }
}
