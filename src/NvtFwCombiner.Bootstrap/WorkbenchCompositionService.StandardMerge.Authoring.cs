using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static readonly CompiledAuthoringWorkflowService
        s_standardMergeAuthoring = new(new StandardMergeAuthoringResolver());

    /// <summary>Projects canonical Standard Merge picker readiness from accepted content identities.</summary>
    public static WorkbenchStandardMergeAuthoringSnapshot GetStandardMergeAuthoringSnapshot(
        string icId,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null)
    {
        CompiledAuthoringSelectionSnapshot snapshot =
            s_standardMergeAuthoring.ProjectSelection(
                icId,
                authoringRevision,
                selectedSlotIds,
                acceptedFileStamps,
                retainedSession);
        return new WorkbenchStandardMergeAuthoringSnapshot(
            snapshot.Catalog,
            snapshot.Slots,
            snapshot.Issues);
    }

    private sealed class StandardMergeAuthoringResolver
        : ICompiledAuthoringWorkflowResolver
    {
        public string WorkflowId => Profiles.IcWorkflowIds.StandardMerge;

        public CompiledAuthoringWorkflowDiscovery Discover(string icId)
        {
            bool compiled = TryCompileStandardMerge(
                icId,
                dpInputLength: null,
                selectedInputSlotIds: [],
                out _,
                out ResolvedCapability? capability,
                out IReadOnlyList<CompositionIssue> issues);
            if (!compiled &&
                TryGetBuiltInV2StandardMergeAuthoringDefaultCapacity(
                    icId,
                    out long capacity,
                    out IReadOnlyList<CompositionIssue> capacityIssues))
            {
                compiled = TryCompileStandardMerge(
                    icId,
                    capacity,
                    selectedInputSlotIds: [],
                    out _,
                    out capability,
                    out issues);
                if (!compiled && issues.Count == 0)
                {
                    issues = capacityIssues;
                }
            }

            ResolvedCapability resolvedCapability = compiled && capability is not null
                ? capability
                : throw new InvalidOperationException(
                    issues.Count == 0
                        ? $"No reviewed Standard Merge authoring route exists for '{icId}'."
                        : string.Join(" | ", issues.Select(static issue => issue.Message)));

            string? prerequisiteSlotId = IsBuiltInV2StandardMergeMapCapacityPending(icId)
                ? CompositionAddressSpaceIds.DpInput
                : null;
            ReviewedDiscoveryTransition? discoveryTransition = prerequisiteSlotId is null
                ? null
                : (s_canonicalCapabilityCatalog.CurrentSnapshot ??
                        throw new InvalidOperationException(
                            "Canonical capability publication is unavailable."))
                    .ResolveReviewedDiscoveryTransition(
                    resolvedCapability,
                    prerequisiteSlotId);
            return new CompiledAuthoringWorkflowDiscovery(
                resolvedCapability,
                GetStandardMergeInputAddressSpaces(icId),
                prerequisiteSlotId,
                discoveryTransition);
        }

        public CompiledAuthoringWorkflowResolution ResolveExact(
            string icId,
            long? prerequisiteLength,
            IReadOnlyCollection<string> selectedSlotIds)
        {
            bool compiled = TryCompileStandardMerge(
                icId,
                prerequisiteLength,
                selectedSlotIds,
                out _,
                out ResolvedCapability? capability,
                out IReadOnlyList<CompositionIssue> issues);
            return new CompiledAuthoringWorkflowResolution(
                compiled ? capability : null,
                issues);
        }
    }
}
