using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Authoring;

internal sealed partial class AbMergeAuthoringExperience :
    IAbMergeAuthoring,
    IAbMergeInputSlotInspector
{
    private readonly CanonicalCapabilityCompilerAdapter _compiler;
    private readonly ICanonicalCapabilityQuery _catalog;
    private readonly IRuntimeDependencyReadinessLeaseProvider _runtimeLeases;
    private readonly Func<CancellationToken, Task<IEventBufferFormatConfigurationSession>>? _getConfiguration;

    internal AbMergeAuthoringExperience(
        CanonicalCapabilityCompilerAdapter compiler,
        ICanonicalCapabilityQuery catalog,
        IRuntimeDependencyReadinessLeaseProvider runtimeLeases,
        Func<CancellationToken, Task<IEventBufferFormatConfigurationSession>>? getConfiguration = null)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _runtimeLeases = runtimeLeases ?? throw new ArgumentNullException(nameof(runtimeLeases));
        _getConfiguration = getConfiguration;
    }

    /// <summary>Adopts one inspected format through the current canonical publication and original source leases.</summary>
    public AuthoringSessionTransitionResult AdoptInspectedBatch(AuthoringSessionState session,
        AuthoringCapabilityCatalogSnapshot catalog, IReadOnlyList<AuthoringSlotInspectionLease> leases,
        IReadOnlyDictionary<string, AuthoringInputSlotStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(leases);
        ArgumentNullException.ThrowIfNull(statuses);
        ActiveSessionSnapshot? current = session.CurrentSnapshot;
        AuthoringCapabilityRoute? target = catalog.Routes.Count == 1 ? catalog.Routes[0] : null;
        ResolvedCapabilityRoute? sourceRoute = current is null ? null : _catalog.ResolveDynamicRoute(current.SelectedRouteId).Route;
        ResolvedCapabilityRoute? targetRoute = target is null ? null : _catalog.ResolveDynamicRoute(target.Identity.RouteId).Route;
        bool invalid = current is null || sourceRoute is null || targetRoute is null || target is null ||
            sourceRoute.ResolutionToken != current.ResolutionToken || targetRoute.ResolutionToken != catalog.ResolutionToken ||
            targetRoute.CapabilityFingerprint != target.CapabilityFingerprint || sourceRoute.Identity.WorkflowId != ExperienceIds.AbMerge ||
            targetRoute.Identity.WorkflowId != ExperienceIds.AbMerge || targetRoute.Identity.IcId != current.SelectedIc ||
            sourceRoute.AbMergeTopologyChoice?.Token != targetRoute.AbMergeTopologyChoice?.Token ||
            (target.ExactCapability is { } exact && _catalog.ResolveCurrentCompilation(exact.CompiledComposition, exact) is null);
        return invalid
            ? new(current, new AuthoringSessionIssue(AuthoringSessionIssueCodes.StaleInspection,
                "The inspected AB format no longer matches the current IC, topology or publication.", ExperienceIds.AbMerge))
            : target!.ExactCapability is null ||
                (current!.SelectedRouteId == target.Identity.RouteId && current.CompilationFingerprint == target.CompilationFingerprint)
            ? session.TryCompleteSlotFileInspectionBatch(catalog, leases, statuses)
            : session.TryAdoptExactSlotFileInspectionBatch(catalog, leases, [.. statuses.Values]);
    }

    /// <summary>Returns whether the selected IC owns an authorable AB Merge route.</summary>
    public bool IsAvailable(string icId)
    {
        return _catalog.HasAuthorableCapability(
            IcIdentifier.Normalize(icId),
            ExperienceIds.AbMerge);
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
        ActiveSessionSnapshot? retainedSession = null,
        AbMergeDpMode dpMode = AbMergeDpMode.Normal)
    {
        TopologySelection? topology = _compiler.ResolveAbMergeTopologySelection(icId, topologyToken);
        (ResolvedCapabilityRoute route, CanonicalAbAuthoringDefinition definition) = GetAbDeclarations(icId, topology);
        if (definition.Family.AbFormatPolicy is not null)
        {
            ResolvedCapability? retained = retainedSession?.ExactCapability;
            return retained is not null && retained.Identity.IcId == route.Identity.IcId &&
                retained.Identity.WorkflowId == ExperienceIds.AbMerge &&
                _catalog.ResolveCurrentCompilation(retained.CompiledComposition, retained) is not null &&
                retained.CompiledComposition.V2Details.Provenance.ResolvedMap.ImageMap.Applicability.TopologyRequirement.Matches(topology) &&
                retained.CompiledComposition.V2Details.InputContract.Slots.Any(slot =>
                    definition.SelectionGroupMemberSlotIds.Contains(slot.SlotId, StringComparer.Ordinal)) == (dpMode == AbMergeDpMode.Normal)
                ? new CompiledAuthoringWorkflowService(new AbMergeAuthoringResolver(topology, _compiler, dpMode, retained))
                    .ProjectSelection(icId, authoringRevision, selectedSlotIds, acceptedFileStamps, retainedSession)
                : DeclarationSelection(route, definition, selectedSlotIds, dpMode);
        }
        return CreateAbMergeAuthoringService(topology, _compiler, dpMode)
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
        IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs,
        AbMergeDpMode dpMode = AbMergeDpMode.Normal)
    {
        ArgumentNullException.ThrowIfNull(session);
        TopologySelection? topology = _compiler.ResolveAbMergeTopologySelection(icId, topologyToken);
        (ResolvedCapabilityRoute route, CanonicalAbAuthoringDefinition definition) = GetAbDeclarations(icId, topology);
        if (definition.Family.AbFormatPolicy is not null)
        {
            CompiledAuthoringSelectionSnapshot declaration = DeclarationSelection(route, definition,
                [.. inputs.Select(static input => input.SlotId)], dpMode,
                [new CompositionIssue("AB_FORMAT_CAPTURE_REQUIRED", "Use asynchronous AB preparation to capture the persisted format configuration.")]);
            AuthoringSessionTransitionResult invalidated = session.Activate(declaration.Catalog);
            return new(invalidated.Snapshot, declaration, null, invalidated.Issue);
        }
        return CreateAbMergeAuthoringService(topology, _compiler, dpMode)
            .PrepareExactSession(icId, session, inputs);
    }

    private static CompiledAuthoringWorkflowService CreateAbMergeAuthoringService(
        TopologySelection? topology,
        CanonicalCapabilityCompilerAdapter compiler,
        AbMergeDpMode dpMode)
    {
        return Enum.IsDefined(dpMode)
            ? new CompiledAuthoringWorkflowService(new AbMergeAuthoringResolver(topology, compiler, dpMode))
            : throw new ArgumentOutOfRangeException(nameof(dpMode));
    }

    private sealed class AbMergeAuthoringResolver(
        TopologySelection? topology,
        CanonicalCapabilityCompilerAdapter compiler,
        AbMergeDpMode dpMode,
        ResolvedCapability? inspectionCapability = null)
        : ICompiledAuthoringWorkflowResolver
    {
        private string? _icId;
        private ResolvedCapability? _capability;

        public string WorkflowId => ExperienceIds.AbMerge;

        public CompiledAuthoringWorkflowDiscovery Discover(string icId)
        {
            ResolvedCapability? capability = inspectionCapability;
            IReadOnlyList<CompositionIssue> issues = [];
            if (capability is null)
            {
                _ = compiler.TryCompileAbMergeCapability(icId, topology,
                    selectedInputSlotIds: dpMode == AbMergeDpMode.Dummy ? [] : null,
                    out _, out capability, out issues);
            }
            _capability = capability ??
                throw new InvalidOperationException(
                    issues.Count == 0
                        ? $"No reviewed AB Merge authoring route exists for '{icId}'."
                        : string.Join(" | ", issues.Select(static issue => issue.Message)));
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
