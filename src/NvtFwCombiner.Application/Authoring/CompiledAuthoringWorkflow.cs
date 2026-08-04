using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Compiler adapter used by one Application-owned fixed authoring workflow.</summary>
public interface ICompiledAuthoringWorkflowResolver
{
    /// <summary>Workflow identity permanently owned by this resolver.</summary>
    string WorkflowId { get; }

    /// <summary>Returns the reviewed route and complete authoring membership before exact compilation.</summary>
    CompiledAuthoringWorkflowDiscovery Discover(string icId);

    /// <summary>Compiles one exact selected-input state without fallback.</summary>
    CompiledAuthoringWorkflowResolution ResolveExact(
        string icId,
        long? prerequisiteLength,
        IReadOnlyCollection<string> selectedSlotIds);
}

/// <summary>Reviewed pre-compilation facts exposed by a compiler adapter.</summary>
public sealed record CompiledAuthoringWorkflowDiscovery(
    ResolvedCapability DiscoveryCapability,
    IReadOnlyList<string> AvailableSlotIds,
    string? CompilationPrerequisiteSlotId,
    ReviewedDiscoveryTransition? DiscoveryTransition = null);

/// <summary>One exact compiler result with its original issues retained.</summary>
public sealed record CompiledAuthoringWorkflowResolution(
    ResolvedCapability? Capability,
    IReadOnlyList<CompositionIssue> Issues)
{
    /// <summary>True only when one exact compiled capability exists without issues.</summary>
    public bool Succeeded => Capability is not null && Issues.Count == 0;
}

/// <summary>One already-read immutable selected input supplied by a host adapter.</summary>
public sealed record CompiledAuthoringSelectedInput(
    string SlotId,
    string SelectedPathHint,
    ReadOnlyMemory<byte>? Bytes);

/// <summary>Application-owned picker projection for one authoring revision.</summary>
public sealed record CompiledAuthoringSelectionSnapshot(
    AuthoringCapabilityCatalogSnapshot Catalog,
    IReadOnlyList<InputSelectionMemberReadiness> Slots,
    IReadOnlyList<CompositionIssue> Issues);

/// <summary>Application-owned coherent inspection result for one exact selected batch.</summary>
public sealed record CompiledAuthoringInspectionBatch(
    AuthoringCapabilityCatalogSnapshot Catalog,
    IReadOnlyDictionary<string, AuthoringInputSlotStatus> Statuses,
    IReadOnlyList<CompositionIssue> Issues);

/// <summary>
/// Owns prerequisite readiness, exact compilation selection, and immutable
/// input inspection for one fixed workflow. Hosts only read bytes and adapt
/// compiler calls; Presentation only renders these typed results.
/// </summary>
public sealed class CompiledAuthoringWorkflowService
{
    private readonly ICompiledAuthoringWorkflowResolver _resolver;

    /// <summary>Creates one workflow use case over its compiler adapter.</summary>
    public CompiledAuthoringWorkflowService(ICompiledAuthoringWorkflowResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolver.WorkflowId);
        _resolver = resolver;
    }

    /// <summary>Projects current picker readiness from accepted content identities only.</summary>
    public CompiledAuthoringSelectionSnapshot ProjectSelection(
        string icId,
        AuthoringRevision authoringRevision,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        ActiveSessionSnapshot? retainedSession = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(selectedSlotIds);
        ArgumentNullException.ThrowIfNull(acceptedFileStamps);
        CompiledAuthoringWorkflowDiscovery discovery = _resolver.Discover(icId);
        ValidateDiscovery(discovery);
        ResolvedCapability? retained = TryRetainExactCapability(
            retainedSession,
            icId,
            selectedSlotIds,
            acceptedFileStamps,
            discovery.CompilationPrerequisiteSlotId is null
                ? discovery.DiscoveryCapability
                : null);
        if (retained is null &&
            discovery.CompilationPrerequisiteSlotId is { } prerequisite &&
            !acceptedFileStamps.ContainsKey(prerequisite))
        {
            return new CompiledAuthoringSelectionSnapshot(
                DiscoveryCatalog(discovery),
                ProjectPendingPrerequisite(
                    discovery,
                    selectedSlotIds,
                    prerequisite),
                []);
        }

        long? prerequisiteLength = discovery.CompilationPrerequisiteSlotId is { } prerequisiteSlot
            ? acceptedFileStamps[prerequisiteSlot].Length
            : null;
        CompiledAuthoringWorkflowResolution exact = retained is null
            ? _resolver.ResolveExact(icId, prerequisiteLength, selectedSlotIds)
            : new CompiledAuthoringWorkflowResolution(retained, []);
        if (!exact.Succeeded)
        {
            ResolvedCapability? nearestCapability = selectedSlotIds
                .Where(slotId => !StringComparer.Ordinal.Equals(
                    slotId, discovery.CompilationPrerequisiteSlotId))
                .Select(removed => _resolver.ResolveExact(
                    icId,
                    prerequisiteLength,
                    [.. selectedSlotIds.Where(slotId => !StringComparer.Ordinal.Equals(slotId, removed))]))
                .FirstOrDefault(static candidate => candidate.Succeeded)?.Capability;
            return new CompiledAuthoringSelectionSnapshot(
                DiscoveryCatalog(discovery),
                nearestCapability is null
                    ? ProjectRejectedSelection(
                        discovery, selectedSlotIds, prerequisiteLength, exact.Issues)
                    : ProjectExactSelection(
                        discovery, nearestCapability, authoringRevision,
                        selectedSlotIds, prerequisiteLength),
                exact.Issues);
        }

        ResolvedCapability capability = RetainEquivalentExactCapability(
            retainedSession?.ExactCapability,
            exact.Capability!);
        return new CompiledAuthoringSelectionSnapshot(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(
                capability,
                discovery.DiscoveryTransition),
            ProjectExactSelection(
                discovery,
                capability,
                authoringRevision,
                selectedSlotIds,
                prerequisiteLength),
            []);
    }

    /// <summary>Resolves and inspects one immutable selected-input batch without fallback.</summary>
    public CompiledAuthoringInspectionBatch InspectBatch(
        string icId,
        AuthoringRevision authoringRevision,
        IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs,
        ResolvedCapability? retainedCapability = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(inputs);
        CompiledAuthoringSelectedInput[] selected = [.. inputs];
        if (selected.Length == 0 ||
            selected.Any(static input => input is null) ||
            selected.Select(static input => input.SlotId)
                .Distinct(StringComparer.Ordinal).Count() != selected.Length)
        {
            throw new ArgumentException(
                "A compiled authoring inspection batch requires unique selected slots.",
                nameof(inputs));
        }

        string[] selectedSlotIds = [.. selected.Select(static input => input.SlotId)];
        if (retainedCapability is not null &&
            CanInspectRetainedExactCapability(
                retainedCapability,
                icId,
                selectedSlotIds,
                selected))
        {
            return InspectExactBatch(
                retainedCapability,
                authoringRevision,
                selected,
                selectedSlotIds);
        }

        CompiledAuthoringWorkflowDiscovery discovery = _resolver.Discover(icId);
        ValidateDiscovery(discovery);
        CompiledAuthoringSelectedInput? prerequisiteInput =
            discovery.CompilationPrerequisiteSlotId is { } prerequisiteSlotId
                ? selected.SingleOrDefault(input => StringComparer.Ordinal.Equals(
                    input.SlotId,
                    prerequisiteSlotId))
                : null;
        long? prerequisiteLength = prerequisiteInput?.Bytes?.Length;
        CompiledAuthoringWorkflowResolution exact =
            discovery.CompilationPrerequisiteSlotId is not null && prerequisiteLength is null
                ? new CompiledAuthoringWorkflowResolution(
                    null,
                    [new CompositionIssue(
                        InputArtifactInspectionIssueCodes.SourceUnreadable,
                        "The exact authoring compilation prerequisite is unreadable.",
                        discovery.CompilationPrerequisiteSlotId)])
                : _resolver.ResolveExact(icId, prerequisiteLength, selectedSlotIds);
        if (!exact.Succeeded || !ContainsEverySelectedSlot(exact.Capability, selectedSlotIds))
        {
            IReadOnlyList<CompositionIssue> issues = exact.Issues.Count == 0
                ? [new CompositionIssue(
                    InputSelectionReadinessIssueCodes.SelectionNotApplicable,
                    "The exact compilation does not contain every selected input.")]
                : exact.Issues;
            CompositionIssue primary = issues[0];
            return new CompiledAuthoringInspectionBatch(
                DiscoveryCatalog(discovery),
                selected.ToDictionary(
                    static input => input.SlotId,
                    input => AuthoringInputSlotInspectionService.BlockBeforeCompilation(
                        discovery.DiscoveryCapability,
                        authoringRevision,
                        input.SlotId,
                        input.SlotId,
                        primary.Code,
                        primary.Message,
                        input.Bytes is { } bytes ? FileStamp.FromBytes(bytes.Span) : null,
                        input.SelectedPathHint),
                    StringComparer.Ordinal),
                issues);
        }

        ResolvedCapability resolved = exact.Capability ??
            throw new InvalidOperationException(
                "A successful exact workflow resolution requires one capability.");
        ResolvedCapability capability = retainedCapability is null
            ? resolved
            : RetainEquivalentExactCapability(retainedCapability, resolved);
        return InspectExactBatch(
            capability,
            authoringRevision,
            selected,
            selectedSlotIds,
            discovery.DiscoveryTransition);
    }

    private static CompiledAuthoringInspectionBatch InspectExactBatch(
        ResolvedCapability capability,
        AuthoringRevision authoringRevision,
        IReadOnlyCollection<CompiledAuthoringSelectedInput> selected,
        IReadOnlyCollection<string> selectedSlotIds,
        ReviewedDiscoveryTransition? discoveryTransition = null)
    {
        var selectedBySlotId =
            selected.ToDictionary(
                static input => input.SlotId,
                StringComparer.Ordinal);
        CompiledInputSpaceBinding[] selectedBindings =
        [
            .. capability.CompiledComposition.V2Details.InputContract.SpaceBindings
                .Where(binding => selectedBySlotId.ContainsKey(binding.SlotId)),
        ];
        Dictionary<string, ReadOnlyMemory<byte>?> sources = selectedBindings.ToDictionary(
            static binding => binding.AddressSpaceId,
            binding => selectedBySlotId[binding.SlotId].Bytes,
            StringComparer.Ordinal);
        return new CompiledAuthoringInspectionBatch(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(
                capability,
                discoveryTransition),
            AuthoringInputSlotInspectionService.InspectBatch(
                capability,
                authoringRevision,
                sources,
                selectedBindings.ToDictionary(
                    static binding => binding.AddressSpaceId,
                    binding => selectedBySlotId[binding.SlotId].SelectedPathHint,
                    StringComparer.Ordinal),
                selectedSlotIds),
            []);
    }

    private bool CanInspectRetainedExactCapability(
        ResolvedCapability capability,
        string icId,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyCollection<CompiledAuthoringSelectedInput> selected)
    {
        if (!StringComparer.Ordinal.Equals(capability.Identity.IcId, icId) ||
            !StringComparer.Ordinal.Equals(
                capability.Identity.WorkflowId,
                _resolver.WorkflowId))
        {
            return false;
        }

        CompiledInputContract contract =
            capability.CompiledComposition.V2Details.InputContract;
        if (!contract.Slots.Select(static slot => slot.SlotId)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(selectedSlotIds))
        {
            return false;
        }

        foreach (CompiledAuthoringSelectedInput input in selected)
        {
            CompiledInputSpaceBinding? spaceBinding = contract.SpaceBindings
                .SingleOrDefault(binding => StringComparer.Ordinal.Equals(
                    binding.SlotId,
                    input.SlotId));
            if (spaceBinding is null || input.Bytes is not { } bytes)
            {
                continue;
            }
            AddressSpace? addressSpace = capability.CompiledComposition.Plan.AddressSpaces
                .SingleOrDefault(space => StringComparer.Ordinal.Equals(
                    space.AddressSpaceId,
                    spaceBinding.AddressSpaceId));
            if (addressSpace is null ||
                (addressSpace.AllowedInputLengths.Count > 0 &&
                    !addressSpace.AllowedInputLengths.Contains(bytes.Length)) ||
                (addressSpace.AllowedInputLengths.Count == 0 &&
                    addressSpace.InputPaddingByte is null &&
                    addressSpace.InputOversizePolicy == InputOversizePolicy.Reject &&
                    addressSpace.Length != bytes.Length))
            {
                return false;
            }
        }

        return true;
    }

    private ReadOnlyCollection<InputSelectionMemberReadiness> ProjectExactSelection(
        CompiledAuthoringWorkflowDiscovery discovery,
        ResolvedCapability capability,
        AuthoringRevision authoringRevision,
        IReadOnlyCollection<string> selectedSlotIds,
        long? prerequisiteLength)
    {
        CompiledInputContract contract = capability.CompiledComposition.V2Details.InputContract;
        var groupMembers = contract.SelectionGroups
            .SelectMany(static group => group.MemberSlotIds)
            .ToHashSet(StringComparer.Ordinal);
        InputSelectionReadinessSnapshot readiness = InputSelectionReadinessResolver.Resolve(
            authoringRevision,
            contract.SelectionGroups,
            selectedSlotIds.Where(groupMembers.Contains));
        var resolvedMembers = readiness.Groups
            .SelectMany(static group => group.Members)
            .ToDictionary(static member => member.SlotId, StringComparer.Ordinal);
        return Array.AsReadOnly(
        [
            .. discovery.AvailableSlotIds.Select(slotId =>
            {
                if (resolvedMembers.TryGetValue(slotId, out InputSelectionMemberReadiness? member) &&
                    (member.IsSelected ||
                        member.Readiness != ResolvedChildReadiness.NotApplicable ||
                        !_resolver.ResolveExact(
                            capability.Identity.IcId,
                            prerequisiteLength,
                            [.. selectedSlotIds, slotId]).Succeeded))
                {
                    return member;
                }
                CompiledInputSlotRequirement? slot = contract.Slots.SingleOrDefault(candidate =>
                    StringComparer.Ordinal.Equals(candidate.SlotId, slotId));
                if (slot is not null)
                {
                    return new InputSelectionMemberReadiness(
                        slotId,
                        selectedSlotIds.Contains(slotId, StringComparer.Ordinal),
                        ResolvedChildReadiness.Ready,
                        CanSelect: true,
                        Reason: null,
                        NextAction: null,
                        IsRequired: slot.Required);
                }

                bool selected = selectedSlotIds.Contains(slotId, StringComparer.Ordinal);
                bool alternate = !selected && _resolver.ResolveExact(
                    capability.Identity.IcId,
                    prerequisiteLength,
                    [.. selectedSlotIds, slotId]).Succeeded;
                return new InputSelectionMemberReadiness(
                    slotId,
                    selected,
                    selected ? ResolvedChildReadiness.Blocked : ResolvedChildReadiness.NotApplicable,
                    CanSelect: alternate,
                    alternate
                        ? "Selecting this input resolves another reviewed compilation."
                        : "The exact compilation does not apply this input.",
                    selected
                        ? new InputSelectionNextAction(
                            InputSelectionNextActionKind.CorrectSelection,
                            slotId)
                        : null,
                    selected ? InputSelectionReadinessIssueCodes.SelectionNotApplicable : null);
            }),
        ]);
    }

    private ResolvedCapability? TryRetainExactCapability(
        ActiveSessionSnapshot? session,
        string icId,
        IReadOnlyCollection<string> selectedSlotIds,
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps,
        ResolvedCapability? discoveredExactCapability)
    {
        if (session?.ExactCapability is not { } capability ||
            !StringComparer.Ordinal.Equals(session.WorkflowId, _resolver.WorkflowId) ||
            !StringComparer.Ordinal.Equals(session.SelectedIc, icId) ||
            (discoveredExactCapability is not null &&
                !IsEquivalentExactCapability(capability, discoveredExactCapability)) ||
            !session.Slots.Where(static slot => slot.SelectedPath is not null)
                .Select(static slot => slot.DefinitionId)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(selectedSlotIds) ||
            session.Slots.Where(static slot => slot.SelectedPath is not null)
                .Any(static slot => slot.FileStamp is null ||
                    slot.Lifecycle is not (
                        AuthoringSlotLifecycle.Verified or
                        AuthoringSlotLifecycle.Warning)))
        {
            return null;
        }

        var retainedStamps = session.Slots
            .Where(static slot => slot.FileStamp is not null)
            .ToDictionary(static slot => slot.DefinitionId, static slot => slot.FileStamp!.Value,
                StringComparer.Ordinal);
        return retainedStamps.Count == acceptedFileStamps.Count &&
            retainedStamps.All(pair => acceptedFileStamps.GetValueOrDefault(pair.Key) == pair.Value)
                ? capability
                : null;
    }

    private static ResolvedCapability RetainEquivalentExactCapability(
        ResolvedCapability? retained,
        ResolvedCapability resolved)
    {
        return retained is not null && IsEquivalentExactCapability(retained, resolved)
                ? retained
                : resolved;
    }

    private static bool IsEquivalentExactCapability(
        ResolvedCapability left,
        ResolvedCapability right)
    {
        return Equals(left.Identity, right.Identity) &&
            left.ResolutionToken == right.ResolutionToken &&
            StringComparer.Ordinal.Equals(
                left.CapabilityFingerprint,
                right.CapabilityFingerprint) &&
            StringComparer.Ordinal.Equals(
                left.CompiledComposition.CompilationFingerprint,
                right.CompiledComposition.CompilationFingerprint);
    }

    private static ReadOnlyCollection<InputSelectionMemberReadiness> ProjectPendingPrerequisite(
        CompiledAuthoringWorkflowDiscovery discovery,
        IReadOnlyCollection<string> selectedSlotIds,
        string prerequisiteSlotId)
    {
        return Array.AsReadOnly(
        [
            .. discovery.AvailableSlotIds.Select(slotId =>
                StringComparer.Ordinal.Equals(slotId, prerequisiteSlotId)
                    ? new InputSelectionMemberReadiness(
                        slotId,
                        selectedSlotIds.Contains(slotId, StringComparer.Ordinal),
                        ResolvedChildReadiness.Ready,
                        CanSelect: true,
                        Reason: null,
                        NextAction: null)
                    : new InputSelectionMemberReadiness(
                        slotId,
                        selectedSlotIds.Contains(slotId, StringComparer.Ordinal),
                        ResolvedChildReadiness.PendingInput,
                        CanSelect: false,
                        $"Load {prerequisiteSlotId} first to resolve this input.",
                        new InputSelectionNextAction(
                            InputSelectionNextActionKind.LoadArtifactFirst,
                            prerequisiteSlotId))),
        ]);
    }

    private ReadOnlyCollection<InputSelectionMemberReadiness> ProjectRejectedSelection(
        CompiledAuthoringWorkflowDiscovery discovery,
        IReadOnlyCollection<string> selectedSlotIds,
        long? prerequisiteLength,
        IReadOnlyList<CompositionIssue> issues)
    {
        string reason = issues.Count == 0
            ? "The exact selected-input state is not admitted."
            : issues[0].Message;
        return Array.AsReadOnly(
        [
            .. discovery.AvailableSlotIds.Select(slotId =>
            {
                bool selected = selectedSlotIds.Contains(slotId, StringComparer.Ordinal);
                bool alternate = !selected && _resolver.ResolveExact(
                    discovery.DiscoveryCapability.Identity.IcId,
                    prerequisiteLength,
                    [.. selectedSlotIds, slotId]).Succeeded;
                return new InputSelectionMemberReadiness(
                    slotId,
                    selected,
                    selected ? ResolvedChildReadiness.Blocked : ResolvedChildReadiness.NotApplicable,
                    CanSelect: selected || alternate,
                    alternate
                        ? "Selecting this input resolves another reviewed compilation."
                        : reason,
                    selected
                        ? new InputSelectionNextAction(
                            InputSelectionNextActionKind.CorrectSelection,
                            slotId)
                        : null,
                    selected && issues.Count != 0 ? issues[0].Code : null);
            }),
        ]);
    }

    private static AuthoringCapabilityCatalogSnapshot DiscoveryCatalog(
        CompiledAuthoringWorkflowDiscovery discovery)
    {
        return AuthoringCapabilityCatalogSnapshot.FromDiscovery(
            discovery.DiscoveryCapability,
            discovery.AvailableSlotIds,
            discovery.DiscoveryTransition);
    }

    private static bool ContainsEverySelectedSlot(
        ResolvedCapability? capability,
        IReadOnlyCollection<string> selectedSlotIds)
    {
        return capability is not null && selectedSlotIds.All(slotId =>
            capability.CompiledComposition.V2Details.InputContract.Slots.Any(slot =>
                StringComparer.Ordinal.Equals(slot.SlotId, slotId)));
    }

    private void ValidateDiscovery(CompiledAuthoringWorkflowDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        if (!StringComparer.Ordinal.Equals(
                discovery.DiscoveryCapability.Identity.WorkflowId,
                _resolver.WorkflowId) ||
            discovery.AvailableSlotIds.Count == 0 ||
            discovery.AvailableSlotIds.Any(string.IsNullOrWhiteSpace) ||
            discovery.AvailableSlotIds.Distinct(StringComparer.Ordinal).Count() !=
                discovery.AvailableSlotIds.Count ||
            (discovery.CompilationPrerequisiteSlotId is { } prerequisite &&
                (!discovery.AvailableSlotIds.Contains(prerequisite, StringComparer.Ordinal) ||
                    discovery.DiscoveryTransition is null ||
                    discovery.DiscoveryTransition.ResolutionToken !=
                        discovery.DiscoveryCapability.ResolutionToken ||
                    !StringComparer.Ordinal.Equals(
                        discovery.DiscoveryTransition.WorkflowId,
                        discovery.DiscoveryCapability.Identity.WorkflowId) ||
                    !StringComparer.Ordinal.Equals(
                        discovery.DiscoveryTransition.IcId,
                        discovery.DiscoveryCapability.Identity.IcId) ||
                    !StringComparer.Ordinal.Equals(
                        discovery.DiscoveryTransition.IcCountVariant,
                        discovery.DiscoveryCapability.Identity.IcCountVariant) ||
                    !StringComparer.Ordinal.Equals(
                        discovery.DiscoveryTransition.DiscoveryMember.RouteId,
                        discovery.DiscoveryCapability.Identity.RouteId) ||
                    !StringComparer.Ordinal.Equals(
                        discovery.DiscoveryTransition.DiscoveryMember.CapabilityFingerprint,
                        discovery.DiscoveryCapability.CapabilityFingerprint) ||
                    !StringComparer.Ordinal.Equals(
                        discovery.DiscoveryTransition.PrerequisiteSlotId,
                        prerequisite))))
        {
            throw new InvalidOperationException(
                "The compiler adapter returned an invalid authoring discovery contract.");
        }
    }
}
