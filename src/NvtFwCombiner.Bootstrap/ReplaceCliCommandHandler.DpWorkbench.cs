using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunWorkbenchDpReplaceAsync(
        string action,
        string icId,
        ParsedOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!RequireOption(options, "--ic-num", error, out string? icNumber) ||
            !RequireOption(options, "--base", error, out string? basePath) ||
            !RequireOption(options, "--dp", error, out string? dpPath))
        {
            return UsageError;
        }

        if (!WorkbenchIcNumberTokens.IsSingle(icNumber))
        {
            error.WriteLine($"error: {WorkbenchCompositionService.FormatBuiltInV2DpReplaceIcIds()} DP Replace requires --ic-num {WorkbenchIcNumberTokens.SingleChip}");
            return UsageError;
        }

        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = Path.GetFullPath(basePath),
            [WorkbenchSlotIds.ReplaceDp] = Path.GetFullPath(dpPath),
        };

        InputArtifactBinding[] bindings = CreateWorkbenchBindings(slotPaths);
        CliOutputTarget outputTarget = CliCompositionRunSupport.ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            WorkbenchCompositionService.GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.Dp));
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
            .RunReplaceAsync(icId, icNumber, WorkbenchReplaceModes.Dp, slotPaths, action == "build", cancellationToken, outputPath)
            .ConfigureAwait(false);
        await WriteWorkbenchReportFileIfRequestedAsync(
                result,
                options,
                bindings,
                action == "build" ? outputTarget.FullPath : null,
                output,
                cancellationToken)
            .ConfigureAwait(false);
        await PrintWorkbenchRunResultAsync(result, icId, IcWorkflowIds.DpReplace, output, error).ConfigureAwait(false);
        return result.Succeeded ? Success : CompositionFailed;
    }

    private static bool TryResolveDpPerspectiveDpReplaceIc(
        string selector,
        [NotNullWhen(true)] out string? icId)
    {
        return WorkbenchCompositionService.TryResolveDpPerspectiveDpReplaceSelector(selector, out icId);
    }

}
