using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

internal sealed partial class AbMergeAuthoringExperience
{
    /// <inheritdoc />
    public async ValueTask<CapabilityActionReadinessSnapshot?> GetActionReadinessAsync(
        ActiveSessionSnapshot acceptedSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(acceptedSession);
        ResolvedCapability? capability = acceptedSession.GetAcceptedCapability(
            AuthoringDerivedResultKind.Inspection);
        if (capability is null ||
            !acceptedSession.HasCurrentInputInspection ||
            !StringComparer.Ordinal.Equals(
                acceptedSession.WorkflowId,
                ExperienceIds.AbMerge))
        {
            return null;
        }

        RuntimeDependencyReadinessRequest request =
            RuntimeDependencyReadinessRequest.FromResolvedCapability(
                capability,
                acceptedSession.AuthoringRevision);
        CapabilityAdmissionSnapshot admission =
            CapabilityAdmissionSnapshot.FromResolvedCapability(
                capability,
                acceptedSession.AuthoringRevision);
        IEnumerable<CapabilityChildReadiness> inputs =
            acceptedSession.InputSlotStatuses.Select(static status =>
                new CapabilityChildReadiness(
                    status.SlotId,
                    ResolvedChildReadiness.Ready));
        if (request.Dependencies.Count == 0)
        {
            return CapabilityActionReadinessResolver.Resolve(
                admission,
                inputs,
                new RuntimeDependencyReadinessSnapshot(
                    request.RouteId,
                    request.CapabilityFingerprint,
                    request.CompilationFingerprint,
                    request.ResolutionToken,
                    request.AuthoringRevision,
                    generation: 0,
                    DateTimeOffset.UnixEpoch,
                    []),
                currentRuntimeDependencyGeneration: 0);
        }

        RuntimeDependencyReadinessLease runtime = _runtimeLeases.AcquireCurrent();
        CapabilityActionReadinessSnapshot readiness =
            await CapabilityActionReadinessResolver.RefreshAndResolveAsync(
                admission,
                inputs,
                request,
                runtime.ReadinessProvider,
                runtime.Generation,
                runtime.GenerationIsCurrent,
                cancellationToken).ConfigureAwait(false);
        return CapabilityActionReadinessResolver.RequireRuntimeDependenciesForPreview(
            readiness);
    }
}
