using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunWorkbenchDpReplaceAsync(
        string action,
        string icId,
        ParsedCliOptions options,
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
        bool requiresLdc = DpReplaceAuthoringCatalog.GetAdditionalPayloads(icId)
            .Any(static rule => rule.SlotId == WorkbenchSlotIds.ReplaceLdc);
        if (requiresLdc)
        {
            if (!RequireOption(options, "--ldc", error, out string? ldcPath))
            {
                return UsageError;
            }

            slotPaths[WorkbenchSlotIds.ReplaceLdc] = Path.GetFullPath(ldcPath);
        }
        else if (options.Values.ContainsKey("--ldc"))
        {
            error.WriteLine($"error: --ldc is not declared by the {icId} DP Replace profile");
            return UsageError;
        }

        return await RunWorkbenchReplaceAsync(
                action,
                icId,
                WorkbenchReplaceModes.Dp,
                IcWorkflowIds.DpReplace,
                options,
                slotPaths,
                (build, outputPath, token) => WorkbenchCompositionService.RunReplaceAsync(
                    icId,
                    icNumber,
                    WorkbenchReplaceModes.Dp,
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
