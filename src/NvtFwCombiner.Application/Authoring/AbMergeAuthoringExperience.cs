using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Authoring;

internal sealed partial class AbMergeAuthoringExperience :
    IAbMergeAuthoring,
    ICompiledInputSlotInspector<AbMergeInspectionBatch>
{
    private readonly CanonicalCapabilityCompilerAdapter _compiler;
    private readonly ICanonicalCapabilityQuery _catalog;

    internal AbMergeAuthoringExperience(
        CanonicalCapabilityCompilerAdapter compiler,
        ICanonicalCapabilityQuery catalog)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>Returns whether the selected IC owns an authorable AB Merge route.</summary>
    public bool IsAvailable(string icId)
    {
        return _compiler.IsAbMergeSupported(icId);
    }

    /// <summary>Gets the explicit topology choices for one AB Merge route.</summary>
    public IReadOnlyList<CapabilityTopologyChoice> GetTopologyChoices(string icId)
    {
        return _compiler.GetAbMergeTopologyChoices(icId);
    }

    /// <summary>Projects canonical AB Merge picker readiness from accepted content identities.</summary>
    public CompiledAuthoringSelectionSnapshot GetAuthoringSnapshot(
        string icId,
        string? topologyToken,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null)
    {
        return CreateAbMergeAuthoringService(
                _compiler.ResolveAbMergeTopologySelection(
                    icId,
                    topologyToken),
                _catalog)
            .ProjectSelection(
                icId,
                authoringRevision,
                selectedSlotIds,
                acceptedFileStamps,
            retainedSession);
    }

    /// <summary>Atomically prepares one exact AB Merge session from immutable inputs.</summary>
    public CompiledAuthoringSessionPreparation PrepareSession(
        AuthoringSessionState session,
        string icId,
        string? topologyToken,
        IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs)
    {
        return CreateAbMergeAuthoringService(
                _compiler.ResolveAbMergeTopologySelection(icId, topologyToken),
                _catalog)
            .PrepareExactSession(icId, session, inputs);
    }

    private static CompiledAuthoringWorkflowService CreateAbMergeAuthoringService(
        TopologySelection? topology,
        ICanonicalCapabilityQuery catalog)
    {
        return new CompiledAuthoringWorkflowService(
            new AbMergeAuthoringResolver(topology, catalog));
    }

    private sealed class AbMergeAuthoringResolver(
        TopologySelection? topology,
        ICanonicalCapabilityQuery catalog)
        : ICompiledAuthoringWorkflowResolver
    {
        private string? _icId;
        private ResolvedCapability? _capability;

        public string WorkflowId => ExperienceIds.AbMerge;

        public CompiledAuthoringWorkflowDiscovery Discover(string icId)
        {
            CapabilityResolutionResult resolution = catalog.ResolveUniqueTopologyRoute(
                    IcIdentifier.Normalize(icId),
                    ExperienceIds.AbMerge,
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
            AuthoringRevision authoringRevision,
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
                    IcIdentifier.Normalize(icId)))
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
