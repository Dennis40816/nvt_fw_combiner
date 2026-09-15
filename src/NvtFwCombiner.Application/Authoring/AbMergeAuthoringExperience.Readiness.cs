using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

internal sealed partial class AbMergeAuthoringExperience
{
    // A null selection with no issues is permitted only for an explicitly policy-absent family.
    internal async ValueTask<(AbMergeFormatSelection? Selection, IReadOnlyList<CompositionIssue> Issues)> AssessAcceptedFormatAsync(
        ActiveSessionSnapshot session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        ResolvedCapability? accepted = session.GetAcceptedCapability(AuthoringDerivedResultKind.Inspection);
        if (accepted is null || session.WorkflowId != ExperienceIds.AbMerge || !session.HasCurrentInputInspection ||
            _catalog.ResolveCurrentCompilation(accepted.CompiledComposition, accepted) is null)
        {
            return (null, [new CompositionIssue("AB_FORMAT_SESSION_STALE", "Inspect the current AB inputs before continuing.")]);
        }
        CapabilityRouteResolutionResult currentRoute = _catalog.ResolveDynamicRoute(accepted.Identity.RouteId);
        if (currentRoute.Route is null)
        {
            return (null, [new CompositionIssue("AB_FORMAT_SESSION_STALE", "The accepted AB route is no longer published.")]);
        }
        if (!_compiler.TryGetAbAuthoringDefinition(currentRoute.Route,
            out CanonicalAbAuthoringDefinition? definition, out IReadOnlyList<CompositionIssue> issues))
        {
            return (null, issues);
        }
        if (definition.Family.AbFormatPolicy is null) { return (null, []); }
        AbMergeDpMode mode = GetAcceptedAbDpMode(accepted, definition);
        CompiledAuthoringSelectedInput[] inputs = CaptureAcceptedAbInputs(session);
        FormatResolution resolved = await ResolveAbFormatAsync(accepted.Identity.IcId,
            CapabilityPublicationCoherence.GetAcceptedAbMergeTopologySelection(accepted), inputs, mode, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return resolved.Capability is null
            ? (null, resolved.Issues)
            : !CompiledAuthoringWorkflowService.IsEquivalentExactCapability(accepted, resolved.Capability)
                ? (null, [new CompositionIssue("AB_FORMAT_CHANGED", "Event Buffer Format rules changed the output layout. Reapply the selected inputs before continuing.")])
                : (resolved.Format, resolved.Issues);
    }

    private static AbMergeDpMode GetAcceptedAbDpMode(ResolvedCapability accepted, CanonicalAbAuthoringDefinition definition)
    {
        return GetDeclaredAbDpMode(accepted.CompiledComposition.V2Details.InputContract.Slots.Select(static slot => slot.SlotId), definition);
    }

    private static AbMergeDpMode GetDeclaredAbDpMode(IEnumerable<string> slotIds, CanonicalAbAuthoringDefinition definition)
    {
        return slotIds.Any(slotId => definition.SelectionGroupMemberSlotIds.Contains(slotId, StringComparer.Ordinal))
            ? AbMergeDpMode.Normal : AbMergeDpMode.Dummy;
    }

    private static CompiledAuthoringSelectedInput[] CaptureAcceptedAbInputs(ActiveSessionSnapshot session)
    {
        return CaptureAbInputs([.. session.InputSlotStatuses.Select(static status =>
            new CompiledAuthoringSelectedInput(status.SlotId, status.SelectedPathHint!, status.AcceptedBytes))]);
    }

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

        (AbMergeFormatSelection? _, IReadOnlyList<CompositionIssue> formatIssues) =
            await AssessAcceptedFormatAsync(acceptedSession, cancellationToken).ConfigureAwait(false);
        if (formatIssues.Count != 0)
        {
            var unavailable = new CapabilityActionAvailability(formatIssues.Select(issue => new RankedBlocker(0,
                new CapabilityActionBlocker(issue.Code, CapabilityReadinessDimension.Input, capability.Identity.RouteId,
                    issue.Message, CapabilityReadinessNextAction.CorrectInput))));
            return new(capability.Identity.RouteId, capability.CapabilityFingerprint, capability.CompiledComposition.CompilationFingerprint,
                capability.ResolutionToken, acceptedSession.AuthoringRevision, 0, unavailable, unavailable);
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
