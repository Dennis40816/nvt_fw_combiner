using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

internal sealed class DpReplaceAuthoringExperience :
    IDpReplaceAuthoring,
    ICompiledInputSlotInspector<FirmwareInspectionStatusBatch>
{
    private readonly CanonicalCapabilityCompilerAdapter _compiler;
    private readonly ICanonicalCapabilityQuery _catalog;

    internal DpReplaceAuthoringExperience(
        CanonicalCapabilityCompilerAdapter compiler,
        ICanonicalCapabilityQuery catalog)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>Projects canonical DP Replace picker readiness from accepted content identities.</summary>
    public CompiledAuthoringSelectionSnapshot GetAuthoringSnapshot(
        string icId,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        AuthoringRevision authoringRevision,
        ActiveSessionSnapshot? retainedSession = null)
    {
        return CreateUnavailableSelection(icId) ??
            CreateAuthoringService().ProjectSelection(
                icId,
                authoringRevision,
                selectedSlotIds,
                acceptedFileStamps,
                retainedSession);
    }

    /// <summary>Atomically prepares one exact DP Replace session from already-read immutable inputs.</summary>
    public CompiledAuthoringSessionPreparation PrepareSession(
        AuthoringSessionState session,
        string icId,
        IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(session);
        return CreateUnavailableSelection(icId) is { } unavailable
            ? new CompiledAuthoringSessionPreparation(
                session.CurrentSnapshot,
                unavailable,
                Inspection: null,
                SessionIssue: null)
            : CreateAuthoringService()
                .PrepareExactSession(icId, session, inputs);
    }

    private CompiledAuthoringSelectionSnapshot? CreateUnavailableSelection(
        string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        string normalizedIcId = IcIdentifier.Normalize(icId);
        return _catalog.HasAuthorableCapability(
                normalizedIcId,
                ExperienceIds.DpReplace)
            ? null
            : new CompiledAuthoringSelectionSnapshot(
                AuthoringCapabilityCatalogSnapshot.FromCanonical(
                    _catalog.GetCurrentSnapshot(),
                    ExperienceIds.DpReplace),
                [],
                [],
                [new CompositionIssue(
                    CompositionPlanningIssueCodes.ReplaceWorkflowNotSupported,
                    $"Not available: {normalizedIcId} DP Replace authoring is hidden until the 1.1.0 retirement decision.")]);
    }

    public FirmwareInspectionStatusBatch InspectInputSlots(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
        Func<string, byte[]?> readFirmwareImage)
    {
        FirmwareInspectionSnapshotInput[] selected = [.. inputs.Where(static input => input.DpReplaceAddressSpaceId is not null)];
        if (selected.Length == 0)
        {
            return FirmwareInspectionStatusBatch.Empty;
        }

        var authoringRevision = new AuthoringRevision(selected.Select(static input => input.AuthoringRevision).Distinct().Single());
        ResolvedCapability? exactCapability = selected[0].ExactCapability;
        if (selected.Any(input => !ReferenceEquals(input.ExactCapability, exactCapability)))
        {
            throw new InvalidOperationException("DP Replace inspection leases disagree on the exact compilation.");
        }
        CompiledAuthoringInspectionBatch batch = CreateAuthoringService().InspectBatch(
            icId,
            authoringRevision,
            [.. selected.Select(input => new CompiledAuthoringSelectedInput(
                input.DpReplaceAddressSpaceId!,
                input.Path,
                readFirmwareImage(input.Path) is { } image
                    ? image
                    : (ReadOnlyMemory<byte>?)null))],
            exactCapability);
        return new FirmwareInspectionStatusBatch(
            batch.Catalog,
            selected.ToDictionary(
                static input => input.InspectionId,
                input => batch.Statuses[input.DpReplaceAddressSpaceId!],
                StringComparer.Ordinal),
            batch.Issues);
    }

    private CompiledAuthoringWorkflowService CreateAuthoringService()
    {
        return new CompiledAuthoringWorkflowService(
            new DpReplaceAuthoringResolver(_compiler, _catalog));
    }

    private sealed class DpReplaceAuthoringResolver(
        CanonicalCapabilityCompilerAdapter compiler,
        ICanonicalCapabilityQuery catalog)
        : ICompiledAuthoringWorkflowResolver
    {
        public string WorkflowId => ExperienceIds.DpReplace;

        public CompiledAuthoringWorkflowDiscovery Discover(string icId)
        {
            IReadOnlyList<long> capacities = compiler.GetDynamicMapCapacities(
                icId,
                ExperienceIds.DpReplace,
                out IReadOnlyList<CompositionIssue> capacityIssues);
            ResolvedCapability? capability = null;
            IReadOnlyList<CompositionIssue> issues = capacityIssues;
            bool compiled = capacityIssues.Count == 0 && capacities.Count != 0 &&
                compiler.TryCompileDpReplace(
                    icId, capacities[0], selectedInputSlotIds: null,
                    out _, out capability, out issues);
            ResolvedCapability discovery = compiled && capability is not null
                ? capability
                : throw new InvalidOperationException(string.Join(
                    " | ", (capacityIssues.Count == 0 ? issues : capacityIssues)
                        .Select(static issue => issue.Message)));
            if (!TryResolveDpReplaceContracts(
                    compiler,
                    icId,
                    out IReadOnlyList<CompiledComposition>? compositions))
            {
                throw new InvalidOperationException(
                    $"DP Replace compiled-contract discovery is unavailable for '{icId}'.");
            }
            string[] available =
            [
                .. compositions.SelectMany(static composition =>
                        composition.V2Details.InputContract.Slots)
                    .Select(static slot => slot.SlotId)
                    .Distinct(StringComparer.Ordinal),
            ];
            ReviewedDiscoveryTransition transition =
                (catalog.TryGetCurrentSnapshot() ??
                    throw new InvalidOperationException("Canonical capability publication is unavailable."))
                .ResolveReviewedDiscoveryTransition(
                    discovery,
                    CompositionAddressSpaceIds.ReferenceBase);
            return new CompiledAuthoringWorkflowDiscovery(
                discovery,
                available,
                CompositionAddressSpaceIds.ReferenceBase,
                transition,
                [
                    .. compositions.SelectMany(static composition =>
                            composition.V2Details.InputContract.SpaceBindings)
                        .Select(static binding => new CompiledAuthoringInputBinding(
                            binding.SlotId,
                            binding.AddressSpaceId))
                        .Distinct()
                        .OrderBy(static binding => binding.SlotId, StringComparer.Ordinal)
                        .ThenBy(static binding => binding.AddressSpaceId, StringComparer.Ordinal),
                ]);
        }

        public CompiledAuthoringWorkflowResolution ResolveExact(
            string icId,
            AuthoringRevision authoringRevision,
            long? prerequisiteLength,
            IReadOnlyCollection<string> selectedSlotIds)
        {
            ResolvedCapability? capability = null;
            IReadOnlyList<CompositionIssue> issues = [];
            string[] replacements =
            [
                .. selectedSlotIds.Where(static slotId => !StringComparer.Ordinal.Equals(
                    slotId, CompositionAddressSpaceIds.ReferenceBase)),
            ];
            InputSelectionReadinessSnapshot? readiness = null;
            if (prerequisiteLength is not null)
            {
                compiler.CompileDynamicDefinition(
                    icId,
                    ExperienceIds.DpReplace,
                    prerequisiteLength.Value,
                    selectedInputSlotIds: null,
                    out CompiledComposition? discoveryComposition,
                    out issues);
                if (discoveryComposition is not null && issues.Count == 0)
                {
                    IReadOnlyList<CompiledInputSelectionGroup> groups =
                        discoveryComposition.V2Details.InputContract.SelectionGroups;
                    var members = groups.SelectMany(static group =>
                            group.MemberSlotIds)
                        .ToHashSet(StringComparer.Ordinal);
                    readiness = InputSelectionReadinessResolver.Resolve(
                        authoringRevision,
                        groups,
                        replacements.Where(members.Contains));
                    if (!readiness.CanBuild &&
                        !(replacements.Length == 0 &&
                            readiness.PrimaryIssue?.Code ==
                                InputSelectionReadinessIssueCodes.SelectionPending))
                    {
                        InputSelectionReadinessIssue primary = readiness.PrimaryIssue!;
                        return new CompiledAuthoringWorkflowResolution(
                            null,
                            [new CompositionIssue(primary.Code, primary.Message)],
                            readiness);
                    }
                }
            }

            bool compiled = prerequisiteLength is not null &&
                compiler.TryCompileDpReplace(
                    icId,
                    prerequisiteLength.Value,
                    replacements.Length == 0 ? null : replacements,
                    out _, out capability, out issues);
            if (compiled && capability is not null)
            {
                capability = capability.BindDpExecutionPlan(
                    new AcceptedDpExecutionPlan(
                        IcNumberSelection.FromToken(
                            IcNumberSelectionTokens.SingleChip)));
            }

            return new CompiledAuthoringWorkflowResolution(
                compiled ? capability : null,
                issues,
                readiness);
        }
    }

    private static bool TryResolveDpReplaceContracts(
        CanonicalCapabilityCompilerAdapter compiler,
        string icId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out IReadOnlyList<CompiledComposition>? compositions)
    {
        compositions = null;
        IReadOnlyList<long> capacities = compiler.GetDynamicMapCapacities(
            icId,
            ExperienceIds.DpReplace,
            out IReadOnlyList<CompositionIssue> capacityIssues);
        if (capacityIssues.Count != 0 || capacities.Count == 0)
        {
            return false;
        }

        var resolved = new List<CompiledComposition>();
        foreach (long capacity in capacities.Order())
        {
            compiler.CompileDynamicDefinition(
                icId,
                ExperienceIds.DpReplace,
                capacity,
                selectedInputSlotIds: null,
                out CompiledComposition? defaultComposition,
                out IReadOnlyList<CompositionIssue> issues);
            if (defaultComposition is null || issues.Count != 0)
            {
                return false;
            }

            resolved.Add(defaultComposition);
            IReadOnlyList<CompiledInputSelectionGroup> groups =
                defaultComposition.V2Details.InputContract.SelectionGroups;
            foreach (CompiledInputSelectionGroup group in groups)
            {
                foreach (string memberSlotId in group.ApplicableMemberSlotIds
                             .Except(group.SelectedSlotIds, StringComparer.Ordinal))
                {
                    string[] selectedSlotIds = CreateDpReplaceProjectionSelection(
                        groups,
                        group,
                        memberSlotId);
                    compiler.CompileDynamicDefinition(
                        icId,
                        ExperienceIds.DpReplace,
                        capacity,
                        selectedSlotIds,
                        out CompiledComposition? memberComposition,
                        out issues);
                    if (memberComposition is null || issues.Count != 0)
                    {
                        return false;
                    }

                    resolved.Add(memberComposition);
                }
            }
        }

        compositions = resolved.AsReadOnly();
        return true;
    }

    private static string[] CreateDpReplaceProjectionSelection(
        IReadOnlyList<CompiledInputSelectionGroup> groups,
        CompiledInputSelectionGroup targetGroup,
        string memberSlotId)
    {
        var selected = groups
            .SelectMany(static group => group.SelectedSlotIds)
            .ToHashSet(StringComparer.Ordinal);
        if (targetGroup.SelectedSlotIds.Count >= targetGroup.MaximumSelected)
        {
            _ = selected.Remove(targetGroup.SelectedSlotIds[^1]);
        }

        _ = selected.Add(memberSlotId);
        return [.. selected.Order(StringComparer.Ordinal)];
    }
}
