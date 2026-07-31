using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    internal sealed record GeneralReplacePostbuildReadinessOverride(
        SavedRuleV2GeneralReplaceRuntimeAuthority Authority,
        IRuntimeDependencyReadinessProvider ReadinessProvider,
        long Generation,
        Func<long, bool> GenerationIsCurrent);

    private sealed record GeneralReplacePostbuildReadinessResult(
        CapabilityActionReadinessSnapshot Readiness,
        string? RequiredStageId);

    private static async ValueTask<GeneralReplacePostbuildReadinessResult>
        ResolveGeneralReplacePostbuildReadinessAsync(
            GeneralAuthoringAdmissionResult admission,
            GeneralReplacePostbuildReadinessOverride? runtimeOverride,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admission);
        SavedRuleV2GeneralReplaceRuntimeAuthority exactAuthority =
            GetNt51926GeneralReplaceRuntimeAuthority();
        SavedRuleV2GeneralReplaceRuntimeAuthority authority =
            runtimeOverride?.Authority ?? exactAuthority;
        if (authority.ParentBinding != exactAuthority.ParentBinding)
        {
            throw new ArgumentException(
                "General Replace runtime readiness must use the exact resolved Parent.",
                nameof(runtimeOverride));
        }

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
            $"general-replace:{parent.ProfileId}",
            parent.ProfileContentHash,
            new ResolutionToken(
                $"bundle:{parent.BundleId}:{parent.BundleVersion}:{parent.BundleContentHash}"),
            new AuthoringRevision(1),
            CapabilityAuthoringAvailability.Available,
            executionAdmitted: hasStageAuthority,
            CapabilityEvidenceStatus.Missing,
            CapabilityPublicationStatus.Internal,
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
