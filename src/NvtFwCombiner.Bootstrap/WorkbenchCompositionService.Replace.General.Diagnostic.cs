using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    internal sealed record GeneralReplacePostbuildReadinessOverride(
        IRuntimeDependencyReadinessProvider ReadinessProvider,
        long Generation,
        Func<long, bool> GenerationIsCurrent);

    private sealed record GeneralReplacePostbuildReadinessResult(
        CapabilityActionReadinessSnapshot Readiness,
        string? RequiredStageId);

    private static async ValueTask<GeneralReplacePostbuildReadinessResult>
        ResolveGeneralReplacePostbuildReadinessAsync(
            GeneralAuthoringAdmissionResult admission,
            SavedRuleV2GeneralReplaceRuntimeAuthority authority,
            ResolvedCapability resolvedCapability,
            GeneralReplacePostbuildReadinessOverride? runtimeOverride,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(resolvedCapability);

        SavedRuleV2ParentBinding parent = authority.ParentBinding;
        bool hasStageAuthority = authority.ProcessorStageIds.Count != 0;
        CapabilityActionBlocker? executionBlocker = hasStageAuthority
            ? null
            : new CapabilityActionBlocker(
                CapabilityActionReadinessIssueCodes
                    .PostbuildStageAuthorityMissing,
                CapabilityReadinessDimension.Execution,
                parent.ProfileId,
                "Selected General Replace targets require POSTBUILD, but the exact Parent does not declare the required stage.",
                CapabilityReadinessNextAction.ReviewCompilation);
        var capability = new CapabilityAdmissionSnapshot(
            resolvedCapability.Identity.RouteId,
            resolvedCapability.CapabilityFingerprint,
            resolvedCapability.CompiledComposition.CompilationFingerprint,
            resolvedCapability.ResolutionToken,
            new AuthoringRevision(1),
            resolvedCapability.Authoring.Value,
            executionAdmitted:
                resolvedCapability.ExecutionAdmitted && hasStageAuthority,
            resolvedCapability.Evidence.Value,
            resolvedCapability.Publication.Value,
            executionBlocker);
        CapabilityChildReadiness[] inputs =
        [
            new CapabilityChildReadiness(
                WorkbenchSlotIds.ReplaceBase,
                ResolvedChildReadiness.Ready),
            .. admission.InputResources.Select(static input =>
                new CapabilityChildReadiness(
                    input.SlotId,
                    ResolvedChildReadiness.Ready)),
        ];
        string? requiredStageId = hasStageAuthority
            ? string.Join(">", authority.ProcessorStageIds)
            : null;
        if (!hasStageAuthority)
        {
            var runtime = new RuntimeDependencyReadinessSnapshot(
                capability.RouteId,
                capability.CapabilityFingerprint,
                capability.CompilationFingerprint,
                capability.ResolutionToken,
                capability.AuthoringRevision,
                generation: 1,
                DateTimeOffset.UnixEpoch,
                []);
            return new GeneralReplacePostbuildReadinessResult(
                CapabilityActionReadinessResolver.Resolve(
                    capability,
                    inputs,
                    runtime,
                    currentRuntimeDependencyGeneration: 1),
                requiredStageId);
        }

        ExternalProcessorGenerationLease? lease = runtimeOverride is null
            ? ExternalProcessorFactory.AcquireCurrent()
            : null;
        IRuntimeDependencyReadinessProvider provider =
            runtimeOverride?.ReadinessProvider ?? lease!.ReadinessProvider;
        long generation = runtimeOverride?.Generation ?? lease!.Generation;
        Func<long, bool> generationIsCurrent =
            runtimeOverride?.GenerationIsCurrent ??
            ExternalProcessorFactory.IsCurrent;
        var request = new RuntimeDependencyReadinessRequest(
            capability.RouteId,
            capability.CapabilityFingerprint,
            capability.CompilationFingerprint,
            capability.ResolutionToken,
            capability.AuthoringRevision,
            authority.RuntimeDependencies);
        CapabilityActionReadinessSnapshot readiness =
            await CapabilityActionReadinessResolver.RefreshAndResolveAsync(
                capability,
                inputs,
                request,
                provider,
                generation,
                generationIsCurrent,
                cancellationToken).ConfigureAwait(false);
        return new GeneralReplacePostbuildReadinessResult(
            readiness,
            requiredStageId);
    }
}
