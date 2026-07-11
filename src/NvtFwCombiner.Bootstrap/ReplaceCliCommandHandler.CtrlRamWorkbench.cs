using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunWorkbenchCtrlRamReplaceAsync(
        string action,
        string profileSelector,
        ParsedOptions options,
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

        InputArtifactBinding[] bindings = CreateWorkbenchBindings(slotPaths);
        CliOutputTarget outputTarget = CliCompositionRunSupport.ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            WorkbenchCompositionService.GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.CtrlRam));
        string? outputPath = action == "build" ? outputTarget.FullPath : null;
        if (action == "build")
        {
            CliCompositionRunSupport.EnsureOutputDoesNotAliasInputs(outputTarget, bindings);
            if (!options.Flags.Contains("--overwrite") && File.Exists(outputTarget.FullPath))
            {
                await error.WriteLineAsync(
                        $"error: output file already exists: {outputTarget.FullPath}; pass --overwrite to replace it.")
                    .ConfigureAwait(false);
                return SoftwareError;
            }
        }

        CliCompositionRunSupport.EnsureReportDoesNotAliasProtectedPaths(
            options.Values.GetValueOrDefault("--report"),
            bindings,
            outputTarget,
            action == "build");

        WorkbenchRunResult result = await WorkbenchCompositionService
            .RunReplaceAsync(icId, icNumber, WorkbenchReplaceModes.CtrlRam, slotPaths, action == "build", cancellationToken, outputPath)
            .ConfigureAwait(false);
        await WriteWorkbenchReportFileIfRequestedAsync(
                result,
                options,
                bindings,
                action == "build" ? outputTarget.FullPath : null,
                output,
                cancellationToken)
            .ConfigureAwait(false);
        await PrintWorkbenchRunResultAsync(result, icId, IcWorkflowIds.CtrlRamReplace, output, error).ConfigureAwait(false);
        return result.Succeeded ? Success : CompositionFailed;
    }
}
