using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using V2CompositionPlanCompileResult = NvtFwCombiner.Profiles.V2.V2CompositionPlanCompileResult;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static async ValueTask<WorkbenchRunResult> RunCtrlRamReplaceAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? firmwareVersionEdit,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        return await RunCtrlRamReplaceWithProcessorCoreAsync(
            icId,
            number,
            slotPaths,
            build,
            outputPath,
            firmwareVersionEdit,
            ExternalProcessorFactory.GetOrCreateOrNull(),
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<WorkbenchRunResult> RunCtrlRamReplaceWithProcessorAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? firmwareVersionEdit,
        IExternalProcessor? externalProcessor,
        CancellationToken cancellationToken)
    {
        return await RunCtrlRamReplaceWithProcessorCoreAsync(
            icId,
            number,
            slotPaths,
            build,
            outputPath,
            firmwareVersionEdit,
            externalProcessor,
            progress: null,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<WorkbenchRunResult> RunCtrlRamReplaceWithProcessorCoreAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? firmwareVersionEdit,
        IExternalProcessor? externalProcessor,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        CtrlRamReplaceRunContext context = CreateCtrlRamReplaceRunContext(
            icId,
            number,
            slotPaths,
            firmwareVersionEdit);
        WorkbenchRunResult Blocked(
            IReadOnlyList<CompositionIssue> issues,
            string? outputFileName = null)
        {
            return CreateReplaceReportRunResult(
                icId,
                WorkbenchReplaceModes.CtrlRam,
                slotPaths,
                build,
                CreateCtrlRamPlanningOperations(
                    icId,
                    context.Selection,
                    context.Sources,
                    slotPaths,
                    runnablePreview: false,
                    context.PostbuildProfile,
                    context.CommandPlan),
                issues,
                outputFileName ?? GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.CtrlRam));
        }

        if (context.ValidationIssues.Count != 0 || context.BasePath is null || context.BaseBytes is null ||
            context.PostbuildProfile is null ||
            context.CommandPlan is null)
        {
            return Blocked(context.ValidationIssues);
        }

        byte[] referenceBytes = context.BaseBytes;
        var referencePayload = new FirmwareArtifactPayload(CompositionAddressSpaceIds.ReferenceBase, referenceBytes);
        if (CtrlRamV2RouteRegistry.TryResolve(context.CommandPlan, out CtrlRamV2Route? route))
        {
            WorkbenchFirmwareContextSuggestion? firmware = ReadFirmwareContextSuggestion(icId, referenceBytes);
            int topologyCount = context.CommandPlan.Selector.ResolveTopologyCount(
                context.Selection,
                firmware?.ChipNumber);
            V2CompositionPlanCompileResult v2Compile = CompileCtrlRamV2(
                context,
                route,
                new(
                    topologyCount,
                    context.CommandPlan.Selector.Token,
                    TopologySelectionSource.Requested,
                    "ic-number"),
                referencePayload);
            return !v2Compile.IsCompiled
                ? Blocked(v2Compile.Issues, $"{icId.ToLowerInvariant()}-ctrlram-replace.bin")
                : await RunCompiledCompositionAsync(
                    CtrlRamReplaceRunIdPrefix,
                    v2Compile.CompiledComposition!,
                    CreateCtrlRamReplaceBindings(
                        v2Compile.CompiledComposition!,
                        context,
                        slotPaths),
                    context.BasePath!,
                    build,
                    outputPath,
                    externalProcessor,
                    icNumberSelection: ToIcNumberSelection(context.CommandPlan.Selector.Token),
                    cancellationToken,
                    virtualArtifacts: new Dictionary<string, byte[]>(StringComparer.Ordinal)
                    {
                        [context.BasePath!] = referenceBytes,
                    },
                    progress: progress).ConfigureAwait(false);
        }

        return Blocked(
            [
                new CompositionIssue(
                    WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
                    "The selected CtrlRAM Replace shape has no exact evidence-backed V2 route.",
                    "number"),
            ]);
    }
}
