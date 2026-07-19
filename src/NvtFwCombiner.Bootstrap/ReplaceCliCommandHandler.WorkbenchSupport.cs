using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static bool TryResolveReplaceIc(
        string command,
        string selector,
        [NotNullWhen(true)] out string? icId)
    {
        return (command == IcWorkflowIds.DpReplace &&
                WorkbenchCompositionService.TryResolveBuiltInV2DpReplaceSelector(selector, out icId)) ||
            TryResolveWorkbenchIc(selector, out icId);
    }

    private static bool TryResolveWorkbenchIc(
        string selector,
        [NotNullWhen(true)] out string? icId)
    {
        string normalized = selector.Trim();
        icId = WorkbenchCompositionService.GetSupportedIcIds().FirstOrDefault(candidate =>
            string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CliCompositionRunSupport.GetIcNumber(candidate), normalized, StringComparison.OrdinalIgnoreCase));
        return icId is not null;
    }

    private static InputArtifactBinding[] CreateWorkbenchBindings(IReadOnlyDictionary<string, string> slotPaths)
    {
        return [
            .. slotPaths
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new InputArtifactBinding(
                    pair.Key == WorkbenchSlotIds.ReplaceBase ? CompositionAddressSpaceIds.ReferenceBase : pair.Key,
                    pair.Key,
                    pair.Value)),
        ];
    }

    private static async Task<int> RunWorkbenchReplaceAsync(
        string action,
        string icId,
        string modeId,
        string workflowId,
        ParsedCliOptions options,
        IReadOnlyDictionary<string, string> protectedInputPaths,
        Func<bool, string?, CancellationToken, ValueTask<WorkbenchRunResult>> run,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        InputArtifactBinding[] bindings = CreateWorkbenchBindings(protectedInputPaths);
        CliOutputTarget outputTarget = CliCompositionRunSupport.ResolveOutputTarget(
            options.Values.GetValueOrDefault("--output"),
            WorkbenchCompositionService.GetReplaceDefaultOutputFileName(icId, modeId));
        bool build = action == "build";
        if (build)
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
            build);

        string? outputPath = build ? outputTarget.FullPath : null;
        WorkbenchRunResult result = await run(build, outputPath, cancellationToken).ConfigureAwait(false);
        if (options.Values.TryGetValue("--report", out string? reportPath))
        {
            string fullPath = Path.GetFullPath(reportPath);
            ProtectedPathGuard.EnsureDoesNotAlias(
                fullPath,
                "Report path",
                ProtectedPathGuard.CreateProtectedPaths(bindings, outputPath),
                nameof(reportPath));
            await CliCompositionRunSupport.WriteReportJsonAsync(
                    fullPath,
                    result.ReportJson,
                    output,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await PrintWorkbenchRunResultAsync(result, icId, workflowId, output, error).ConfigureAwait(false);
        return result.Succeeded ? Success : CompositionFailed;
    }
}
