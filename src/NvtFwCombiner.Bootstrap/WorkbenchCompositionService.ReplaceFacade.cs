namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets the default output file name for a Replace build.</summary>
    public static string GetReplaceDefaultOutputFileName(string icId, string replaceMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replaceMode);

        string normalizedIc = icId.ToLowerInvariant();
        return replaceMode switch
        {
            WorkbenchReplaceModes.Dp => $"{normalizedIc}-dp-replace.bin",
            WorkbenchReplaceModes.CtrlRam => $"{normalizedIc}-ctrlram-replace.bin",
            WorkbenchReplaceModes.General => $"{normalizedIc}-general-replace.bin",
            _ => "nvt-fw-combiner-replace.bin",
        };
    }

    /// <summary>Runs a Replace preview or build through the workbench Replace facade.</summary>
    public static async ValueTask<WorkbenchRunResult> RunReplaceAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit = null)
    {
        return await RunReplaceAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            [],
            [],
            build,
            cancellationToken,
            outputPath,
            ctrlRamFirmwareVersionEdit: ctrlRamFirmwareVersionEdit).ConfigureAwait(false);
    }

    /// <summary>Runs a Replace preview or build through the workbench Replace facade.</summary>
    public static async ValueTask<WorkbenchRunResult> RunReplaceAsync(
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
        return await RunReplaceAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            generalReplaceMappings,
            [],
            build,
            cancellationToken,
            outputPath,
            ctrlRamFirmwareVersionEdit: ctrlRamFirmwareVersionEdit).ConfigureAwait(false);
    }

    /// <summary>Runs Replace preview or build with file-backed and virtual General Replace authoring inputs.</summary>
    public static async ValueTask<WorkbenchRunResult> RunReplaceAsync(
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> generalReplaceMappings,
        IReadOnlyList<WorkbenchGeneralReplacePatchInput> generalReplacePatches,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        WorkbenchCtrlRamFirmwareVersionEdit? ctrlRamFirmwareVersionEdit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(replaceMode);
        ArgumentNullException.ThrowIfNull(slotPaths);
        ArgumentNullException.ThrowIfNull(generalReplaceMappings);
        ArgumentNullException.ThrowIfNull(generalReplacePatches);
        return ctrlRamFirmwareVersionEdit is not null &&
            (!build || !string.Equals(replaceMode, WorkbenchReplaceModes.CtrlRam, StringComparison.Ordinal))
            ? throw new ArgumentException(
                "TP FW version editing is available only for CtrlRAM Replace Build.",
                nameof(ctrlRamFirmwareVersionEdit))
            : replaceMode switch
            {
                WorkbenchReplaceModes.Dp when IsDpPerspectiveIc(icId) => await RunDpPerspectiveDpReplaceAsync(
                    icId,
                    number,
                    slotPaths,
                    build,
                    outputPath,
                    cancellationToken).ConfigureAwait(false),
                WorkbenchReplaceModes.Dp => CreatePlanningRunResult(
                    icId,
                    replaceMode,
                    slotPaths,
                    build,
                    WorkbenchIssueCodes.ReplaceDpProfilePending,
                    $"DP Replace output is enabled only for {FormatBuiltInV2DpReplaceIcIds()} until per-IC DP source mapping and golden evidence are approved."),
                WorkbenchReplaceModes.CtrlRam => await RunCtrlRamReplaceAsync(
                    icId,
                    number,
                    slotPaths,
                    build,
                    outputPath,
                    ctrlRamFirmwareVersionEdit,
                    cancellationToken).ConfigureAwait(false),
                WorkbenchReplaceModes.General => await RunGeneralReplaceAsync(
                    icId,
                    number,
                    slotPaths,
                    generalReplaceMappings,
                    generalReplacePatches,
                    build,
                    outputPath,
                    cancellationToken).ConfigureAwait(false),
                _ => CreatePlanningRunResult(
                    icId,
                    replaceMode,
                    slotPaths,
                    build,
                    WorkbenchIssueCodes.ReplaceModeUnknown,
                    $"Unknown Replace mode '{replaceMode}'."),
            };
    }
}
