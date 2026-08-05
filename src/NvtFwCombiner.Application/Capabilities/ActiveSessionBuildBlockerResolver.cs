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
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        if (session is null)
        {
            return Pending(workflowId, "Select the required inputs before continuing.");
        }

        if (session.ExactCapability is not { } capability)
        {
            return ResolvePreCompilationInputBlocker(session, workflowId);
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
            return currentReadiness.Build.PrimaryBlocker;
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
        return CapabilityActionReadinessResolver.ResolvePrimaryBuildBlockerBeforeRuntimeRefresh(
            admission,
            inputs,
            runtime);
    }

    private static CapabilityActionBlocker ResolvePreCompilationInputBlocker(
        ActiveSessionSnapshot session,
        string workflowId)
    {
        AuthoringInputSlotStatus? blocked = session.InputSlotStatuses
            .Where(static status => status.Readiness == ResolvedChildReadiness.Blocked || status.BlocksBuild)
            .OrderBy(static status => status.SlotId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (blocked is not null)
        {
            return new CapabilityActionBlocker(
                CapabilityActionReadinessIssueCodes.InputBlocked,
                CapabilityReadinessDimension.Input,
                blocked.SlotId,
                blocked.SelectionReadiness.Reason ??
                    "Correct the selected input before continuing.",
                CapabilityReadinessNextAction.CorrectInput);
        }

        AuthoringInputSlotStatus? pending = session.InputSlotStatuses
            .Where(static status => status.Readiness == ResolvedChildReadiness.PendingInput)
            .OrderBy(static status => status.SlotId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (pending is not null)
        {
            return Pending(
                pending.SlotId,
                pending.SelectionReadiness.Reason ??
                    "Load the required input before continuing.");
        }

        AuthoringSlotState? error = session.Slots
            .Where(static slot => slot.Lifecycle == AuthoringSlotLifecycle.Error)
            .OrderBy(static slot => slot.DefinitionId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (error is not null)
        {
            return new CapabilityActionBlocker(
                CapabilityActionReadinessIssueCodes.InputBlocked,
                CapabilityReadinessDimension.Input,
                error.DefinitionId,
                "Correct the selected input before continuing.",
                CapabilityReadinessNextAction.CorrectInput);
        }

        AuthoringSlotState? incomplete = session.Slots
            .Where(static slot => slot.SelectedPath is null || slot.Lifecycle is
                AuthoringSlotLifecycle.Empty or
                AuthoringSlotLifecycle.Selected or
                AuthoringSlotLifecycle.Checking)
            .OrderBy(static slot => slot.DefinitionId, StringComparer.Ordinal)
            .FirstOrDefault();
        return Pending(
            incomplete?.DefinitionId ?? workflowId,
            incomplete?.Lifecycle == AuthoringSlotLifecycle.Checking
                ? "Wait for input verification to finish before continuing."
                : "Load the required input before continuing.");
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
}
