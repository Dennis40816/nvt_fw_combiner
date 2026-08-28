using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

internal sealed partial class StandardMergeAuthoringExperience :
    IStandardMergeAuthoring,
    ICompiledInputSlotInspector<FirmwareInspectionStatusBatch>
{
    private readonly CanonicalCapabilityCompilerAdapter _compiler;
    private readonly ICanonicalCapabilityQuery _catalog;
    private readonly CanonicalCapabilityExperience _projection;

    internal StandardMergeAuthoringExperience(
        CanonicalCapabilityCompilerAdapter compiler,
        ICanonicalCapabilityQuery catalog,
        CanonicalCapabilityExperience projection)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
    }

    /// <summary>Returns true when the selected IC has a canonical Standard Merge route.</summary>
    public bool IsSupported(string icId)
    {
        return _catalog.HasAuthorableCapability(
            IcIdentifier.Normalize(icId),
            ExperienceIds.StandardMerge);
    }

    /// <summary>Gets the canonical Standard Merge profile id for the selected IC, if any.</summary>
    public string? GetProfileId(string icId)
    {
        return _projection.FindStandardMergeProfileSummary(icId)?.ProfileId;
    }

    /// <summary>Gets required Standard Merge input spaces for the selected IC.</summary>
    public IReadOnlyList<string> GetRequiredAddressSpaces(string icId)
    {
        return _projection
            .FindStandardMergeProfileSummary(icId)?
            .RequiredInputAddressSpaceIds ?? [];
    }

    /// <summary>Gets all fixed input spaces exposed by canonical Standard Merge authoring.</summary>
    public IReadOnlyList<string> GetInputAddressSpaces(string icId)
    {
        IReadOnlyList<string> required = GetRequiredAddressSpaces(icId);
        return Array.AsReadOnly(
        [
            .. required
                .Concat(_compiler.GetPublishedDynamicSelectionGroupMemberSlotIds(
                    icId,
                    ExperienceIds.StandardMerge,
                    "selector-free"))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ]);
    }

    /// <summary>Projects canonical Standard Merge picker readiness from accepted content identities.</summary>
    public CompiledAuthoringSelectionSnapshot GetAuthoringSnapshot(
        string icId,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null)
    {
        return CreateAuthoringService()
            .ProjectSelection(
            icId,
            authoringRevision,
            selectedSlotIds,
            acceptedFileStamps,
            retainedSession);
    }

    /// <summary>Atomically prepares one exact Standard Merge session from immutable inputs.</summary>
    public CompiledAuthoringSessionPreparation PrepareSession(
        AuthoringSessionState session,
        string icId,
        IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs)
    {
        return CreateAuthoringService()
            .PrepareExactSession(icId, session, inputs);
    }

    private CompiledAuthoringWorkflowService CreateAuthoringService()
    {
        return new CompiledAuthoringWorkflowService(
            new StandardMergeAuthoringResolver(this));
    }

    private sealed class StandardMergeAuthoringResolver(
        StandardMergeAuthoringExperience owner)
        : ICompiledAuthoringWorkflowResolver
    {
        public string WorkflowId => ExperienceIds.StandardMerge;

        public CompiledAuthoringWorkflowDiscovery Discover(string icId)
        {
            bool compiled = owner._compiler.TryCompileStandardMerge(
                icId,
                dpInputLength: null,
                selectedInputSlotIds: [],
                out _,
                out ResolvedCapability? capability,
                out IReadOnlyList<CompositionIssue> issues);
            IReadOnlyList<long> capacities = owner._compiler.GetDynamicMapCapacities(
                icId,
                ExperienceIds.StandardMerge,
                out IReadOnlyList<CompositionIssue> capacityIssues);
            if (!compiled && capacityIssues.Count == 0 && capacities.Count > 1)
            {
                compiled = owner._compiler.TryCompileStandardMerge(
                    icId,
                    capacities[^1],
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

            string? prerequisiteSlotId =
                capacityIssues.Count == 0 && capacities.Count > 1
                ? CompositionAddressSpaceIds.DpInput
                : null;
            ReviewedDiscoveryTransition? discoveryTransition = prerequisiteSlotId is null
                ? null
                : (owner._catalog.TryGetCurrentSnapshot() ??
                        throw new InvalidOperationException(
                            "Canonical capability publication is unavailable."))
                    .ResolveReviewedDiscoveryTransition(
                    resolvedCapability,
                    prerequisiteSlotId);
            return new CompiledAuthoringWorkflowDiscovery(
                resolvedCapability,
                owner.GetInputAddressSpaces(icId),
                prerequisiteSlotId,
                discoveryTransition);
        }

        public CompiledAuthoringWorkflowResolution ResolveExact(
            string icId,
            AuthoringRevision authoringRevision,
            long? prerequisiteLength,
            IReadOnlyCollection<string> selectedSlotIds)
        {
            bool compiled = owner._compiler.TryCompileStandardMerge(
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
