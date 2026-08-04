using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

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
        ActiveSessionSnapshot? acceptedSession,
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
            acceptedSession,
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

    /// <summary>Refreshes CtrlRAM Preview/Build readiness without creating a run report.</summary>
    public static async ValueTask<CapabilityActionReadinessSnapshot?>
        GetCtrlRamReplaceActionReadinessAsync(
            string icId,
            string number,
            IReadOnlyDictionary<string, string> slotPaths,
            ActiveSessionSnapshot acceptedSession,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slotPaths);
        ArgumentNullException.ThrowIfNull(acceptedSession);
        ResolvedCapability? capability = acceptedSession.GetAcceptedCapability(
            AuthoringDerivedResultKind.Inspection);
        CtrlRamReplaceRunContext context = CreateCtrlRamReplaceRunContext(
            icId, number, slotPaths, firmwareVersionEdit: null);
        if (capability is null ||
            !StringComparer.Ordinal.Equals(
                acceptedSession.WorkflowId,
                Profiles.IcWorkflowIds.CtrlRamReplace) ||
            !IsAcceptedCtrlRamSession(
                context, slotPaths, acceptedSession, capability))
        {
            return null;
        }

        ExternalProcessorGenerationLease runtime =
            ExternalProcessorFactory.AcquireCurrent();
        return await CapabilityActionReadinessResolver.RefreshAndResolveAsync(
            CapabilityAdmissionSnapshot.FromResolvedCapability(
                capability,
                acceptedSession.AuthoringRevision),
            acceptedSession.InputSlotStatuses.Select(static status =>
                new CapabilityChildReadiness(
                    status.SlotId,
                    ResolvedChildReadiness.Ready)),
            RuntimeDependencyReadinessRequest.FromResolvedCapability(
                capability,
                acceptedSession.AuthoringRevision),
            runtime.ReadinessProvider,
            runtime.Generation,
            ExternalProcessorFactory.IsCurrent,
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
            InjectedExternalProcessorReadinessProvider.Instance,
            runtimeDependencyGeneration: 1,
            static generation => generation == 1,
            progress: null,
            acceptedSession: null,
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
            acceptedSession: null,
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
        ActiveSessionSnapshot? acceptedSession,
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

        ResolvedCapability? acceptedCapability = acceptedSession is null
            ? null
            : RequireAcceptedCapability(
                acceptedSession,
                Profiles.IcWorkflowIds.CtrlRamReplace,
                icId,
                AuthoringDerivedResultKind.Inspection);
        if (acceptedCapability is not null &&
            !IsAcceptedCtrlRamSession(
                context, slotPaths, acceptedSession!, acceptedCapability))
        {
            return Blocked([new CompositionIssue(
                WorkbenchIssueCodes.ReplaceWorkflowNotSupported,
                "The accepted CtrlRAM compilation does not match the current IC Count or map.",
                "number")]);
        }
        CtrlRamFirmwareVersionDraftState? requestedDraft = firmwareVersionEdit is null
            ? null
            : new CtrlRamFirmwareVersionDraftState(
                firmwareVersionEdit.FirmwareVersion,
                firmwareVersionEdit.FirmwareSubVersion);
        if (acceptedSession is not null &&
            !HasSameCtrlRamVersionDraft(
                acceptedSession.DraftState,
                requestedDraft))
        {
            return Blocked([new CompositionIssue(
                AuthoringSessionIssueCodes.StaleInspection,
                "The confirmed CtrlRAM firmware-version edit was not compiled and inspected in the accepted authoring revision.")]);
        }
        if (acceptedSession is not null &&
            !HasCurrentCtrlRamSessionFiles(context, acceptedSession))
        {
            return Blocked([new CompositionIssue(
                AuthoringSessionIssueCodes.StaleInspection,
                "One or more CtrlRAM inputs changed after the accepted inspection. Reload the changed input before running again.")]);
        }

        ResolvedCapability? resolvedCapability = acceptedCapability;
        if (acceptedCapability is null)
        {
            if (!TryResolveCtrlRamCapability(
                    context, out ResolvedCapability? currentCapability,
                    out IReadOnlyList<CompositionIssue> capabilityIssues))
            {
                return Blocked(capabilityIssues);
            }
            ResolvedCapability current = currentCapability!;
            resolvedCapability = current;
        }

        byte[] referenceBytes = context.BaseBytes!;
        CompiledComposition compiledComposition = resolvedCapability!.CompiledComposition;
        IReadOnlyList<InputArtifactBinding> bindings =
            CreateCtrlRamReplaceBindings(
                compiledComposition,
                context,
                slotPaths,
                acceptedSession);
        AuthoringRevision authoringRevision =
            acceptedSession?.AuthoringRevision ?? new AuthoringRevision(1);
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
