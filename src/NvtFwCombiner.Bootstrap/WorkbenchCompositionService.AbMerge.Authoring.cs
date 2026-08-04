using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Projects canonical AB Merge picker readiness from accepted content identities.</summary>
    public static WorkbenchAbMergeAuthoringSnapshot GetAbMergeAuthoringSnapshot(
        string icId,
        string? topologyToken,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null)
    {
        CompiledAuthoringSelectionSnapshot snapshot = CreateAbMergeAuthoringService(
                AbMergeWorkbenchCompositionService.ResolveTopologySelection(topologyToken))
            .ProjectSelection(
                icId,
                authoringRevision,
                selectedSlotIds,
                acceptedFileStamps,
                retainedSession);
        return new WorkbenchAbMergeAuthoringSnapshot(
            snapshot.Catalog,
            snapshot.Slots,
            snapshot.Issues);
    }

    private static CompiledAuthoringWorkflowService CreateAbMergeAuthoringService(
        TopologySelection? topology)
    {
        return new CompiledAuthoringWorkflowService(new AbMergeAuthoringResolver(topology));
    }

    private sealed class AbMergeAuthoringResolver(TopologySelection? topology)
        : ICompiledAuthoringWorkflowResolver
    {
        private string? _icId;
        private ResolvedCapability? _capability;

        public string WorkflowId => Profiles.IcWorkflowIds.AbMerge;

        public CompiledAuthoringWorkflowDiscovery Discover(string icId)
        {
            CapabilityResolutionResult resolution = ResolveCanonicalAbMergeCapability(
                Profiles.IcSupportCatalog.NormalizeIcId(icId),
                topology);
            _capability = resolution.Capability ??
                throw new InvalidOperationException(
                    resolution.Issue?.Message ?? $"No reviewed AB Merge authoring route exists for '{icId}'.");
            _icId = _capability.Identity.IcId;
            return new CompiledAuthoringWorkflowDiscovery(
                _capability,
                [
                    .. _capability.CompiledComposition.V2Details.InputContract.Slots
                        .Select(static slot => slot.SlotId),
                ],
                CompilationPrerequisiteSlotId: null);
        }

        public CompiledAuthoringWorkflowResolution ResolveExact(
            string icId,
            long? prerequisiteLength,
            IReadOnlyCollection<string> selectedSlotIds)
        {
            ArgumentNullException.ThrowIfNull(selectedSlotIds);
            if (prerequisiteLength is not null)
            {
                return Rejected("AB Merge does not resolve compilation from an artifact length.");
            }

            ResolvedCapability? capability = _capability;
            if (capability is null ||
                !StringComparer.Ordinal.Equals(
                    _icId,
                    Profiles.IcSupportCatalog.NormalizeIcId(icId)))
            {
                return Rejected("AB Merge exact resolution requires its current reviewed discovery.");
            }

            var members = capability.CompiledComposition.V2Details.InputContract.Slots
                .Select(static slot => slot.SlotId)
                .ToHashSet(StringComparer.Ordinal);
            return selectedSlotIds.All(members.Contains)
                ? new CompiledAuthoringWorkflowResolution(capability, [])
                : Rejected("The selected AB input is absent from the exact compiled contract.");
        }

        private static CompiledAuthoringWorkflowResolution Rejected(string message)
        {
            return new CompiledAuthoringWorkflowResolution(
                null,
                [new CompositionIssue(
                    InputSelectionReadinessIssueCodes.SelectionNotApplicable,
                    message)]);
        }
    }
}
