using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class CtrlRamReplaceTestSupport
{
    internal static async ValueTask<CompositionRunResult> RunAsync(
        CanonicalTestContext canonical,
        string icId,
        string number,
        string replaceMode,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        CtrlRamFirmwareVersionDraftState? ctrlRamFirmwareVersionEdit = null)
    {
        if (!StringComparer.Ordinal.Equals(replaceMode, ExperienceIds.CtrlRamReplace))
        {
            throw new ArgumentException(
                "CtrlRAM test support accepts only the CtrlRAM Replace workflow.",
                nameof(replaceMode));
        }
        if (!build && ctrlRamFirmwareVersionEdit is not null)
        {
            throw new ArgumentException(
                "TP firmware-version editing is available only for CtrlRAM Replace Build.",
                nameof(ctrlRamFirmwareVersionEdit));
        }

        (ActiveSessionSnapshot? snapshot, IReadOnlyList<CompositionIssue> issues) =
            Prepare(canonical, icId, number, slotPaths, ctrlRamFirmwareVersionEdit);
        ActiveSessionSnapshot accepted = RequireSnapshot(snapshot, issues);
        ExternalProcessorEnvironmentLease runtime =
            ExternalProcessorEnvironmentTestSupport.AcquireCurrent();
        CapabilityActionReadinessSnapshot readiness = await ResolveReadinessAsync(
            accepted,
            runtime.ReadinessProvider,
            runtime.Generation,
            ExternalProcessorEnvironmentTestSupport.IsCurrent,
            cancellationToken);
        ICompositionExecution adapter = CompositionExecutionTestSupport.Create(canonical);
        return await adapter.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    accepted,
                    slotPaths,
                    build,
                    outputPath: outputPath,
                    actionReadiness: readiness),
                new CompositionRunProgressFeed(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async ValueTask<CompositionRunResult> RunWithProcessorAsync(
        CanonicalTestContext canonical,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        CtrlRamFirmwareVersionDraftState? firmwareVersionEdit,
        IExternalProcessor? externalProcessor,
        CancellationToken cancellationToken)
    {
        (ActiveSessionSnapshot? snapshot, IReadOnlyList<CompositionIssue> issues) =
            Prepare(canonical, icId, number, slotPaths, firmwareVersionEdit);
        ActiveSessionSnapshot accepted = RequireSnapshot(snapshot, issues);
        CapabilityActionReadinessSnapshot readiness = await ResolveReadinessAsync(
            accepted,
            ReadyRuntimeDependencyReadinessProvider.Instance,
            generation: 1,
            static generation => generation == 1,
            cancellationToken);
        ICompositionExecution adapter = CompositionExecutionTestSupport.Create(
            canonical,
            () => new CompositionExternalProcessorLease(
                1,
                externalProcessor),
            static generation => generation == 1);
        return await adapter.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    accepted,
                    slotPaths,
                    build,
                    outputPath: outputPath,
                    actionReadiness: readiness),
                new CompositionRunProgressFeed(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async ValueTask<CompositionRunResult> RunWithProcessorAsync(
        CanonicalTestContext canonical,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        CtrlRamFirmwareVersionDraftState? firmwareVersionEdit,
        IExternalProcessor? externalProcessor,
        IRuntimeDependencyReadinessProvider runtimeDependencyReadinessProvider,
        long runtimeDependencyGeneration,
        Func<long, bool> runtimeGenerationIsCurrent,
        CancellationToken cancellationToken)
    {
        (ActiveSessionSnapshot? snapshot, IReadOnlyList<CompositionIssue> issues) =
            Prepare(canonical, icId, number, slotPaths, firmwareVersionEdit);
        ActiveSessionSnapshot accepted = RequireSnapshot(snapshot, issues);
        CapabilityActionReadinessSnapshot readiness = await ResolveReadinessAsync(
            accepted,
            runtimeDependencyReadinessProvider,
            runtimeDependencyGeneration,
            runtimeGenerationIsCurrent,
            cancellationToken);
        ICompositionExecution adapter = CompositionExecutionTestSupport.Create(
            canonical,
            () => new CompositionExternalProcessorLease(
                runtimeDependencyGeneration,
                externalProcessor),
            runtimeGenerationIsCurrent);
        return await adapter.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    accepted,
                    slotPaths,
                    build,
                    outputPath: outputPath,
                    actionReadiness: readiness),
                new CompositionRunProgressFeed(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static (ActiveSessionSnapshot? Snapshot, IReadOnlyList<CompositionIssue> Issues) Prepare(
        CanonicalTestContext canonical,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        CtrlRamFirmwareVersionDraftState? firmwareVersionEdit)
    {
        var inputBytes = slotPaths.ToDictionary(
            static pair => pair.Key,
            static pair => File.ReadAllBytes(pair.Value),
            StringComparer.Ordinal);
        var session = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        CtrlRamAuthoringSessionPreparation preparation =
            canonical.CtrlRamAuthoring.PrepareSession(
            session,
            icId,
            number,
            slotPaths,
            inputBytes,
            firmwareVersionEdit);
        return (preparation.AcceptedSession, preparation.Issues);
    }

    internal static async ValueTask<CapabilityActionReadinessSnapshot> GetReadinessAsync(
        CanonicalTestContext canonical,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        CtrlRamFirmwareVersionDraftState? firmwareVersionEdit,
        IRuntimeDependencyReadinessProvider readinessProvider,
        long generation,
        Func<long, bool> generationIsCurrent,
        CancellationToken cancellationToken)
    {
        (ActiveSessionSnapshot? snapshot, IReadOnlyList<CompositionIssue> issues) =
            Prepare(canonical, icId, number, slotPaths, firmwareVersionEdit);
        return await ResolveReadinessAsync(
            RequireSnapshot(snapshot, issues),
            readinessProvider,
            generation,
            generationIsCurrent,
            cancellationToken);
    }

    private static ActiveSessionSnapshot RequireSnapshot(
        ActiveSessionSnapshot? snapshot,
        IReadOnlyList<CompositionIssue> issues)
    {
        return snapshot ?? throw new InvalidOperationException(string.Join(
            Environment.NewLine,
            issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
    }

    private static async ValueTask<CapabilityActionReadinessSnapshot> ResolveReadinessAsync(
        ActiveSessionSnapshot session,
        IRuntimeDependencyReadinessProvider readinessProvider,
        long generation,
        Func<long, bool> generationIsCurrent,
        CancellationToken cancellationToken)
    {
        ResolvedCapability capability = session.GetAcceptedCapability(
                AuthoringDerivedResultKind.Inspection) ??
            throw new InvalidOperationException("CtrlRAM test execution requires accepted inspection.");
        CapabilityActionReadinessSnapshot readiness =
            await CapabilityActionReadinessResolver.RefreshAndResolveAsync(
                CapabilityAdmissionSnapshot.FromResolvedCapability(
                    capability,
                    session.AuthoringRevision),
                session.InputSlotStatuses.Select(static status =>
                    new CapabilityChildReadiness(
                        status.SlotId,
                        ResolvedChildReadiness.Ready)),
                RuntimeDependencyReadinessRequest.FromResolvedCapability(
                    capability,
                    session.AuthoringRevision),
                readinessProvider,
                generation,
                generationIsCurrent,
                cancellationToken);
        return CapabilityActionReadinessResolver.RequireRuntimeDependenciesForPreview(
            readiness);
    }

    private sealed class ReadyRuntimeDependencyReadinessProvider :
        IRuntimeDependencyReadinessProvider
    {
        internal static ReadyRuntimeDependencyReadinessProvider Instance { get; } = new();

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
