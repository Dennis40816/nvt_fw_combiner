using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunWorkbenchCtrlRamReplaceAsync(
        string action,
        string profileSelector,
        ParsedCliOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryResolveWorkbenchIc(profileSelector, out string? icId))
        {
            return await UnknownReplaceProfileAsync(IcWorkflowIds.CtrlRamReplace, profileSelector, error).ConfigureAwait(false);
        }

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
                IcWorkflowIds.CtrlRamReplace,
                options,
                slotPaths,
                (build, outputPath, token) => WorkbenchCompositionService.RunReplaceAsync(
                    icId,
                    icNumber,
                    modeId,
                    slotPaths,
                    build,
                    token,
                    outputPath),
                output,
                error,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
