using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static async ValueTask<WorkbenchRunResult> RunBuiltInV2DpReplaceAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        if (!TryCreateBuiltInV2DpReplaceRunContext(
                icId,
                number,
                slotPaths,
                build,
                out BuiltInV2DpReplaceRunContext? context,
                out WorkbenchRunResult? failure))
        {
            return failure!;
        }

        if (!TryCompileBuiltInV2DpReplace(
                icId,
                context!.Capacity,
                out CompiledComposition? compiledComposition,
                out IReadOnlyList<CompositionIssue> issues))
        {
            return CreatePlanningRunResult(
                icId,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                WorkbenchIssueCodes.ReplaceDpProfilePending,
                $"No supported V2 DP Replace profile is registered for {icId}.");
        }

        if (compiledComposition is null)
        {
            CompositionIssue issue = issues.Count > 0
                ? issues[0]
                : new CompositionIssue(
                    BuiltInV2Bundle.CompilationFailed,
                    $"The built-in V2 DP Replace profile for {icId} did not produce an executable composition.");
            return CreatePlanningRunResult(
                icId,
                WorkbenchReplaceModes.Dp,
                slotPaths,
                build,
                issue.Code,
                issue.Message);
        }

        InputArtifactBinding[] bindings =
        [
            CompiledCompositionInputBindingFactory.Create(
                compiledComposition,
                CompositionAddressSpaceIds.ReferenceBase,
                context.BasePath),
            context.SlotPaths.TryGetValue(WorkbenchSlotIds.ReplaceDp, out string? path) &&
                !string.IsNullOrWhiteSpace(path)
                ? CompiledCompositionInputBindingFactory.Create(
                    compiledComposition,
                    CompositionAddressSpaceIds.DpReplacement,
                    Path.GetFullPath(path))
                : throw new InvalidOperationException($"Input slot '{WorkbenchSlotIds.ReplaceDp}' is required."),
        ];
        return await RunCompiledCompositionAsync(
            DpReplaceRunIdPrefix,
            compiledComposition,
            bindings,
            context.BasePath,
            build,
            outputPath,
            externalProcessor: null,
            icNumberSelection: context.Selection,
            overwrite: true,
            cancellationToken,
            progress: progress).ConfigureAwait(false);
    }

    private static List<WorkbenchReplaceInputSlot> GetDpReplaceInputSlots(string icId)
    {
        List<WorkbenchReplaceInputSlot> slots =
        [
            new(
                WorkbenchSlotIds.ReplaceDp,
                "DP replacement BIN",
                TryGetV2DpReplaceInputDescription(icId, out string v2Description)
                    ? v2Description
                    : "Replacement DP payload. Build stays gated until this IC has approved DP Replace mapping evidence.",
                false,
                CompositionAddressSpaceIds.DpReplacement,
                "dp"),
        ];
        foreach (DpReplaceAdditionalPayloadRule rule in DpReplaceAuthoringCatalog.GetAdditionalPayloads(icId))
        {
            slots.Add(new WorkbenchReplaceInputSlot(
                rule.SlotId,
                rule.Title,
                rule.Description,
                false,
                rule.AddressSpaceId,
                rule.RegionId));
        }

        return slots;
    }

    private static string FormatHexLength(long length)
    {
        return string.Create(CultureInfo.InvariantCulture, $"0x{length:X}");
    }

}
