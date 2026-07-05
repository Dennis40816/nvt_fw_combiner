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

    /// <summary>Gets compact firmware facts decoded from a selected BIN file.</summary>
    public static IReadOnlyList<FirmwareSlotFactViewModel> GetFirmwareSlotFacts(
        string icId,
        string path,
        bool includeInvalid = false)
    {
        WorkbenchFirmwareConfigMetadata? metadata = WorkbenchCompositionService.TryReadFirmwareConfigMetadata(
            icId,
            path);
        if (metadata is null || (!metadata.IsFirmwareVersionBarValid && !includeInvalid))
        {
            return [];
        }

        string firmwareVersion = metadata.IsFirmwareVersionBarValid
            ? FormattableString.Invariant($"0x{metadata.FirmwareVersion:X2}.0x{metadata.FirmwareSubVersion:X2} (bar OK)")
            : FormattableString.Invariant($"0x{metadata.FirmwareVersion:X2}.0x{metadata.FirmwareSubVersion:X2} (bar 0x{metadata.FirmwareVersionBar:X2} mismatch)");
        List<FirmwareSlotFactViewModel> facts =
        [
            new("Common FW", metadata.CommonFwVersion),
            new("FW", firmwareVersion, !metadata.IsFirmwareVersionBarValid),
            new("PID", FormattableString.Invariant($"0x{metadata.ProjectId:X4}")),
        ];
        if (!string.IsNullOrWhiteSpace(metadata.PostbuildCategory))
        {
            facts.Add(new FirmwareSlotFactViewModel("Postbuild", metadata.PostbuildCategory));
        }

        return facts;
    }

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

    /// <summary>Gets readable memory-map rows for the selected Standard Merge profile.</summary>
    public static IReadOnlyList<MemoryMapRowViewModel> GetStandardMergeMemoryMapRows(
        string icId,
        long? dpInputLength = null)
    {
        return
        [
            .. WorkbenchCompositionService.GetStandardMergeMemoryMapRows(icId, dpInputLength)
                .Select(ToMemoryMapRow),
        ];
    }

    /// <summary>Gets output address coverage text for the selected Standard Merge profile.</summary>
    public static string GetStandardMergeMemoryRangeLabel(string icId, long? dpInputLength = null)
    {
        return WorkbenchCompositionService.GetStandardMergeMemoryRangeLabel(icId, dpInputLength);
    }

    /// <summary>Gets final visual coverage segments for the selected Standard Merge profile.</summary>
    public static IReadOnlyList<MemoryCoverageSegmentViewModel> GetStandardMergeCoverageSegments(
        string icId,
        long? dpInputLength = null)
    {
        return
        [
            .. WorkbenchCompositionService.GetStandardMergeCoverageSegments(icId, dpInputLength)
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

    private static MemoryMapRowViewModel ToMemoryMapRow(WorkbenchMemoryMapRow row)
    {
        return new MemoryMapRowViewModel(
            row.RangeLabel,
            row.BeforeSource,
            row.ActionLabel,
            row.AfterSource,
            row.Detail);
    }

    private static string ToRange(long start, long length)
    {
        return FormattableString.Invariant($"0x{start:X5}-0x{start + length - 1:X5}");
    }

    private static string ToLength(long length)
    {
        return FormattableString.Invariant($"len 0x{length:X}");
    }
}
