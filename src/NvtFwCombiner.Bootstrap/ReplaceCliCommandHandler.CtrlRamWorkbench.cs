using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunWorkbenchCtrlRamReplaceAsync(
        string action,
        string icId,
        ParsedCliOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!RequireOption(options, "--ic-num", error, out string? icNumber) ||
            !RequireOption(options, "--base", error, out string? basePath))
        {
            return UsageError;
        }

        if (!TryCreateWorkbenchCtrlRamSlotPaths(
                icId,
                icNumber,
                basePath,
                options,
                error,
                out Dictionary<string, string>? slotPaths))
        {
            return UsageError;
        }

        string modeId = WorkbenchReplaceModes.CtrlRam;
        return await RunWorkbenchReplaceAsync(
                action,
                icId,
                modeId,
                ExperienceIds.CtrlRamReplace,
                options,
                slotPaths,
                (_, token) => CompositionExecutionAdapter.RunReplaceAsync(
                    icId,
                    icNumber,
                    modeId,
                    slotPaths,
                    build: false,
                    token,
                    outputPath: null),
                (outputPath, token) => CompositionExecutionAdapter.RunReplaceAsync(
                    icId,
                    icNumber,
                    modeId,
                    slotPaths,
                    build: true,
                    token,
                    outputPath),
                output,
                error,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
