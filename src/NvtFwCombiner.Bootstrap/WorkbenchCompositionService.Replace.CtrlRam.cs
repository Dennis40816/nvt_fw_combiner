using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
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
        ExternalProcessorGenerationLease runtime =
            ExternalProcessorFactory.AcquireCurrent();
        return await RunCtrlRamReplaceWithProcessorCoreAsync(
            icId,
            number,
            slotPaths,
            build,
            outputPath,
            firmwareVersionEdit,
            runtime.Processor,
            runtime.ReadinessProvider,
            runtime.Generation,
            ExternalProcessorFactory.IsCurrent,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts a new external-processor discovery generation. The next
    /// Preview/Build acquires both readiness and execution from that generation.
    /// </summary>
    public static void RefreshCtrlRamRuntimeDependencies()
    {
        ExternalProcessorFactory.Refresh();
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
            InjectedExternalProcessorReadinessProvider.Instance,
            runtimeDependencyGeneration: 1,
            static generation => generation == 1,
            progress: null,
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
        IRuntimeDependencyReadinessProvider runtimeDependencyReadinessProvider,
        long runtimeDependencyGeneration,
        Func<long, bool> runtimeGenerationIsCurrent,
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
            runtimeDependencyReadinessProvider,
            runtimeDependencyGeneration,
            runtimeGenerationIsCurrent,
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
        IRuntimeDependencyReadinessProvider runtimeDependencyReadinessProvider,
        long runtimeDependencyGeneration,
        Func<long, bool> runtimeGenerationIsCurrent,
        CompositionRunProgressFeed? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtimeDependencyReadinessProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(runtimeDependencyGeneration, 1);
        ArgumentNullException.ThrowIfNull(runtimeGenerationIsCurrent);
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
            CapabilityRouteIdentity capabilityIdentity =
                CanonicalDynamicRouteInventory.ResolveCtrlRamIdentity(
                    route,
                    context.CommandPlan,
                    referencePayload.LengthBytes);
            CapabilityRouteResolutionResult capabilityResolution =
                s_canonicalCapabilityCatalog.ResolveDynamicRoute(
                    capabilityIdentity.RouteId);
            if (!capabilityResolution.Succeeded)
            {
                return Blocked(
                    [new CompositionIssue(
                        capabilityResolution.Issue!.Code,
                        capabilityResolution.Issue.Message)]);
            }

            int topologyCount = context.CommandPlan.TopologyCount;
            V2CompositionPlanCompileResult v2Compile = CompileCtrlRamV2(
                context,
                route,
                new(
                    topologyCount,
                    context.CommandPlan.Selector.Token,
                    TopologySelectionSource.Requested,
                    "ic-number"),
                referencePayload);
            if (!v2Compile.IsCompiled)
            {
                return Blocked(
                    v2Compile.Issues,
                    $"{icId.ToLowerInvariant()}-ctrlram-replace.bin");
            }

            CompiledComposition unboundComposition =
                v2Compile.CompiledComposition!;
            MetadataPlanDefinition metadataPlan =
                CreateCtrlRamReportMetadataPlan(
                    route.Key.IcId,
                    referencePayload.LengthBytes);
            ResolvedCapability resolvedCapability =
                capabilityResolution.Route!.BindCompilation(
                    unboundComposition,
                    metadataPlan,
                    CreateCtrlRamCapabilitySemanticBindings(
                        route,
                        context.CommandPlan,
                        referencePayload.LengthBytes));
            CompiledComposition compiledComposition =
                resolvedCapability.CompiledComposition;
            IReadOnlyList<InputArtifactBinding> bindings =
                CreateCtrlRamReplaceBindings(
                    compiledComposition,
                    context,
                    slotPaths);
            var authoringRevision = new AuthoringRevision(0);
            var admission =
                CapabilityAdmissionSnapshot.FromResolvedCapability(
                    resolvedCapability,
                    authoringRevision);
            var dependencyRequest =
                RuntimeDependencyReadinessRequest.FromResolvedCapability(
                    resolvedCapability,
                    authoringRevision);
            CapabilityActionReadinessSnapshot readiness =
                await CapabilityActionReadinessResolver.RefreshAndResolveAsync(
                    admission,
                    CreateInputReadiness(compiledComposition, bindings),
                    dependencyRequest,
                    runtimeDependencyReadinessProvider,
                    runtimeDependencyGeneration,
                    runtimeGenerationIsCurrent,
                    cancellationToken).ConfigureAwait(false);
            IReadOnlyList<CapabilityActionBlocker> attemptBlockers = build
                ? readiness.Build.Blockers
                :
                [
                    .. readiness.Build.Blockers.Where(static blocker =>
                        blocker.Dimension ==
                        CapabilityReadinessDimension.RuntimeDependency),
                ];
            return attemptBlockers.Count != 0
                ? Blocked(
                    [
                        .. attemptBlockers.Select(static blocker =>
                        new CompositionIssue(
                            blocker.Code,
                            $"{blocker.Message} ({blocker.SubjectId})")),
                    ],
                    $"{icId.ToLowerInvariant()}-ctrlram-replace.bin")
                : await RunCompiledCompositionAsync(
                    CtrlRamReplaceRunIdPrefix,
                    compiledComposition,
                    bindings,
                    context.BasePath!,
                    build,
                    outputPath,
                    externalProcessor,
                    icNumberSelection: context.Selection,
                    cancellationToken,
                    virtualArtifacts: new Dictionary<string, byte[]>(StringComparer.Ordinal)
                    {
                        [context.BasePath!] = referenceBytes,
                    },
                    progress: progress,
                    advisoryIssues: context.AdvisoryIssues,
                    resolvedCapability: resolvedCapability).ConfigureAwait(false);
        }

        return Blocked(
            [
                new CompositionIssue(
                    WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
                    "The selected CtrlRAM Replace shape has no exact evidence-backed V2 route.",
                    "number"),
            ]);
    }

    private static IReadOnlyList<CapabilityChildReadiness> CreateInputReadiness(
        CompiledComposition compiledComposition,
        IReadOnlyList<InputArtifactBinding> bindings)
    {
        HashSet<string> boundSpaces =
        [
            .. bindings.Select(static binding => binding.AddressSpaceId),
        ];
        return
        [
            .. compiledComposition.Plan.RequiredInputAddressSpaceIds.Select(
                addressSpaceId => new CapabilityChildReadiness(
                    addressSpaceId,
                    boundSpaces.Contains(addressSpaceId)
                        ? ResolvedChildReadiness.Ready
                        : ResolvedChildReadiness.PendingInput)),
        ];
    }

    private sealed class InjectedExternalProcessorReadinessProvider :
        IRuntimeDependencyReadinessProvider
    {
        internal static InjectedExternalProcessorReadinessProvider Instance { get; } =
            new();

        public ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
            RuntimeDependencyReadinessRequest request,
            long generation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new RuntimeDependencyReadinessSnapshot(
                request.RouteId,
                request.CapabilityFingerprint,
                request.CompilationFingerprint,
                request.ResolutionToken,
                request.AuthoringRevision,
                generation,
                DateTimeOffset.UnixEpoch,
                request.Dependencies.Select(static dependency =>
                    RuntimeDependencyEntry.Ready(
                        dependency.ProcessorId,
                        dependency.ToolBindingId))));
        }
    }
}
