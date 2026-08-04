using NvtFwCombiner.Application.Capabilities;
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
            !RequireOption(options, "--base", error, out string? basePath))
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
        };
        bool replacementSelectionGroup =
            WorkbenchCompositionService.HasBuiltInV2DpReplaceSelectionGroup(icId);
        if (options.Values.TryGetValue("--dp", out string? dpPath))
        {
            slotPaths[WorkbenchSlotIds.ReplaceDp] = Path.GetFullPath(dpPath);
        }
        else if (!replacementSelectionGroup)
        {
            error.WriteLine("error: --dp is required");
            return UsageError;
        }

        bool requiresLdc = DpReplaceAuthoringCatalog.GetAdditionalPayloads(icId)
            .Any(static rule => rule.SlotId == WorkbenchSlotIds.ReplaceLdc);
        if (requiresLdc)
        {
            if (options.Values.TryGetValue("--ldc", out string? ldcPath))
            {
                slotPaths[WorkbenchSlotIds.ReplaceLdc] = Path.GetFullPath(ldcPath);
            }
        }
        else if (options.Values.ContainsKey("--ldc"))
        {
            error.WriteLine($"error: --ldc is not declared by the {icId} DP Replace profile");
            return UsageError;
        }

        if (replacementSelectionGroup)
        {
            IReadOnlyList<WorkbenchReplaceInputSlot> declaredSlots =
                WorkbenchCompositionService.GetReplaceInputSlots(
                    icId,
                    WorkbenchIcNumberTokens.SingleChip,
                    WorkbenchReplaceModes.Dp);
            string[] selectedInputSlotIds =
            [
                .. declaredSlots
                    .Where(slot => slotPaths.ContainsKey(slot.SlotId))
                    .Select(static slot => slot.AddressSpaceId),
            ];
            long? baseCapacity = File.Exists(basePath)
                ? new FileInfo(basePath).Length
                : null;
            if ((baseCapacity is not null || selectedInputSlotIds.Length == 0) &&
                WorkbenchCompositionService.TryResolveBuiltInV2DpReplaceInputSelection(
                    icId,
                    baseCapacity,
                    selectedInputSlotIds,
                    out InputSelectionReadinessSnapshot? readiness,
                    out _) &&
                !readiness!.CanBuild)
            {
                InputSelectionReadinessIssue issue = readiness.PrimaryIssue!;
                error.WriteLine($"error: {issue.Code}: {issue.Message}");
                return issue.Code == InputSelectionReadinessIssueCodes.SelectionPending
                    ? UsageError
                    : CompositionFailed;
            }
        }

        return await RunWorkbenchReplaceAsync(
                action,
                icId,
                WorkbenchReplaceModes.Dp,
                IcWorkflowIds.DpReplace,
                options,
                slotPaths,
                (_, token) => WorkbenchCompositionService.RunReplaceAsync(
                    icId,
                    icNumber,
                    WorkbenchReplaceModes.Dp,
                    slotPaths,
                    build: false,
                    token,
                    outputPath: null),
                (outputPath, token) => WorkbenchCompositionService.RunReplaceAsync(
                    icId,
                    icNumber,
                    WorkbenchReplaceModes.Dp,
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
