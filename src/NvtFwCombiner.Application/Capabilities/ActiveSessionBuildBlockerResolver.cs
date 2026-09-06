using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Projects the typed primary Build blocker already owned by an authoring session.</summary>
public static class ActiveSessionBuildBlockerResolver
{
    /// <summary>Returns the highest-priority blocker without creating a run or report.</summary>
    public static CapabilityActionBlocker? Resolve(
        ActiveSessionSnapshot? session,
        string workflowId,
        CapabilityActionReadinessSnapshot? currentReadiness = null)
    {
        return ResolveBuildAvailability(session, workflowId, currentReadiness).PrimaryBlocker;
    }

    /// <summary>Returns every canonical check-time Build blocker without creating a run or report.</summary>
    public static CapabilityActionAvailability ResolveBuildAvailability(
        ActiveSessionSnapshot? session,
        string workflowId,
        CapabilityActionReadinessSnapshot? currentReadiness = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        if (session is null)
        {
            return Availability(Pending(workflowId, "Select the required inputs before continuing."));
        }

        if (session.ExactCapability is not { } capability)
        {
            return ResolvePreCompilationInputAvailability(session, workflowId);
        }

        var admission =
            CapabilityAdmissionSnapshot.FromResolvedCapability(
                capability,
                session.AuthoringRevision);
        if (currentReadiness is not null &&
            StringComparer.Ordinal.Equals(currentReadiness.RouteId, admission.RouteId) &&
            StringComparer.Ordinal.Equals(
                currentReadiness.CapabilityFingerprint,
                admission.CapabilityFingerprint) &&
            StringComparer.Ordinal.Equals(
                currentReadiness.CompilationFingerprint,
                admission.CompilationFingerprint) &&
            currentReadiness.ResolutionToken == admission.ResolutionToken &&
            currentReadiness.AuthoringRevision == admission.AuthoringRevision)
        {
            return currentReadiness.Build;
        }

        CapabilityChildReadiness[] inputs =
        [
            .. session.Slots.Select(slot => Project(
                slot,
                session.InputSlotStatuses.FirstOrDefault(status =>
                    StringComparer.Ordinal.Equals(status.SlotId, slot.DefinitionId)))),
        ];
        var runtime =
            RuntimeDependencyReadinessRequest.FromResolvedCapability(
                capability,
                session.AuthoringRevision);
        return CapabilityActionReadinessResolver.ResolveBuildAvailabilityBeforeRuntimeRefresh(
            admission,
            inputs,
            runtime);
    }

    private static CapabilityActionAvailability ResolvePreCompilationInputAvailability(
        ActiveSessionSnapshot session,
        string workflowId)
    {
        // ActiveSessionSnapshot rejects duplicate status identities. Preserve its status
        // candidates, including orphan identities, rather than coalescing by slot state.
        var statusCandidateSlots = new HashSet<string>(StringComparer.Ordinal);
        var blockers = new List<RankedBlocker>();
        foreach (AuthoringInputSlotStatus status in session.InputSlotStatuses
                     .Where(static status => status.Readiness == ResolvedChildReadiness.Blocked || status.BlocksBuild)
                     .OrderBy(static status => status.SlotId, StringComparer.Ordinal))
        {
            _ = statusCandidateSlots.Add(status.SlotId);
            blockers.Add(new RankedBlocker(
                0,
                new CapabilityActionBlocker(
                    CapabilityActionReadinessIssueCodes.InputBlocked,
                    CapabilityReadinessDimension.Input,
                    status.SlotId,
                    status.SelectionReadiness.Reason ??
                        "Correct the selected input before continuing.",
                    CapabilityReadinessNextAction.CorrectInput)));
        }

        foreach (AuthoringInputSlotStatus status in session.InputSlotStatuses
                     .Where(static status => status.Readiness == ResolvedChildReadiness.PendingInput)
                     .Where(status => !statusCandidateSlots.Contains(status.SlotId))
                     .OrderBy(static status => status.SlotId, StringComparer.Ordinal))
        {
            _ = statusCandidateSlots.Add(status.SlotId);
            blockers.Add(new RankedBlocker(
                1,
                Pending(
                    status.SlotId,
                    status.SelectionReadiness.Reason ??
                        "Load the required input before continuing.")));
        }

        foreach (AuthoringSlotState slot in session.Slots
                     .Where(slot => !statusCandidateSlots.Contains(slot.DefinitionId))
                     .Where(static slot => slot.Lifecycle == AuthoringSlotLifecycle.Error)
                     .OrderBy(static slot => slot.DefinitionId, StringComparer.Ordinal))
        {
            blockers.Add(new RankedBlocker(
                2,
                new CapabilityActionBlocker(
                    CapabilityActionReadinessIssueCodes.InputBlocked,
                    CapabilityReadinessDimension.Input,
                    slot.DefinitionId,
                    "Correct the selected input before continuing.",
                    CapabilityReadinessNextAction.CorrectInput)));
        }

        foreach (AuthoringSlotState slot in session.Slots
                     .Where(slot => !statusCandidateSlots.Contains(slot.DefinitionId))
                     .Where(static slot => slot.SelectedPath is null || slot.Lifecycle is
                         AuthoringSlotLifecycle.Empty or
                         AuthoringSlotLifecycle.Selected or
                         AuthoringSlotLifecycle.Checking)
                     .OrderBy(static slot => slot.DefinitionId, StringComparer.Ordinal))
        {
            blockers.Add(new RankedBlocker(
                3,
                Pending(
                    slot.DefinitionId,
                    slot.Lifecycle == AuthoringSlotLifecycle.Checking
                        ? "Wait for input verification to finish before continuing."
                        : "Load the required input before continuing.")));
        }

        return blockers.Count == 0
            ? Availability(Pending(workflowId, "Load the required input before continuing."))
            : new CapabilityActionAvailability(blockers);
    }

    private static CapabilityChildReadiness Project(
        AuthoringSlotState slot,
        AuthoringInputSlotStatus? status)
    {
        return status?.BlocksBuild == true ||
            status?.Readiness == ResolvedChildReadiness.Blocked ||
            slot.Lifecycle == AuthoringSlotLifecycle.Error
            ? new CapabilityChildReadiness(
                slot.DefinitionId,
                ResolvedChildReadiness.Blocked,
                status?.InspectionIssueCode ??
                    status?.SelectionReadiness.IssueCode ??
                    CapabilityActionReadinessIssueCodes.InputBlocked,
                status?.SelectionReadiness.Reason ??
                    "Correct the selected input before continuing.")
            : status?.Readiness == ResolvedChildReadiness.PendingInput ||
            slot.Lifecycle is AuthoringSlotLifecycle.Empty or
                AuthoringSlotLifecycle.Selected or
                AuthoringSlotLifecycle.Checking
            ? new CapabilityChildReadiness(
                slot.DefinitionId,
                ResolvedChildReadiness.PendingInput)
            : new CapabilityChildReadiness(
            slot.DefinitionId,
            status?.Readiness ?? ResolvedChildReadiness.Ready);
    }

    private static CapabilityActionBlocker Pending(string subjectId, string message)
    {
        return new CapabilityActionBlocker(
            CapabilityActionReadinessIssueCodes.InputPending,
            CapabilityReadinessDimension.Input,
            subjectId,
            message,
            CapabilityReadinessNextAction.LoadRequiredInput);
    }

    private static CapabilityActionAvailability Availability(CapabilityActionBlocker blocker)
    {
        return new CapabilityActionAvailability([new RankedBlocker(0, blocker)]);
    }
}
