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
            "DP" => $"{normalizedIc}-dp-replace.bin",
            "CtrlRAM" => $"{normalizedIc}-ctrlram-replace.bin",
            "General" => $"{normalizedIc}-general-replace.bin",
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
        string? outputPath = null)
    {
        return await RunReplaceAsync(
            icId,
            number,
            replaceMode,
            slotPaths,
            [],
            build,
            cancellationToken,
            outputPath).ConfigureAwait(false);
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
        string? outputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(replaceMode);
        ArgumentNullException.ThrowIfNull(slotPaths);
        ArgumentNullException.ThrowIfNull(generalReplaceMappings);

        return replaceMode switch
        {
            "DP" when IsNt51950Or51(icId) => await RunNt51950DpReplaceAsync(
                icId,
                number,
                slotPaths,
                build,
                outputPath,
                cancellationToken).ConfigureAwait(false),
            "DP" => CreatePlanningRunResult(
                icId,
                number,
                replaceMode,
                slotPaths,
                build,
                "replace.dp.profile-pending",
                "DP Replace output is enabled only for NT51950/NT51951 until per-IC DP source mapping and golden evidence are approved."),
            "CtrlRAM" => await RunCtrlRamReplaceAsync(
                icId,
                number,
                slotPaths,
                build,
                outputPath,
                cancellationToken).ConfigureAwait(false),
            "General" => await RunGeneralReplaceAsync(
                icId,
                number,
                slotPaths,
                generalReplaceMappings,
                build,
                outputPath,
                cancellationToken).ConfigureAwait(false),
            _ => CreatePlanningRunResult(
                icId,
                number,
                replaceMode,
                slotPaths,
                build,
                "replace.mode.unknown",
                $"Unknown Replace mode '{replaceMode}'."),
        };
    }
}
