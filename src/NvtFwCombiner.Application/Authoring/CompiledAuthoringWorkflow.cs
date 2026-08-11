using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>
/// Owns prerequisite readiness, exact compilation selection, and immutable
/// input inspection for one fixed workflow. Hosts only read bytes and adapt
/// compiler calls; Presentation only renders these typed results.
/// </summary>
public sealed partial class CompiledAuthoringWorkflowService
{
    private readonly ICompiledAuthoringWorkflowResolver _resolver;

    /// <summary>Creates one workflow use case over its compiler adapter.</summary>
    public CompiledAuthoringWorkflowService(ICompiledAuthoringWorkflowResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolver.WorkflowId);
        _resolver = resolver;
    }

    /// <summary>
    /// Resolves one exact selected-input state, advances one session inspection
    /// revision, and atomically publishes the immutable inspected bytes.
    /// </summary>
    public CompiledAuthoringSessionPreparation PrepareExactSession(
        string icId,
        AuthoringSessionState session,
        IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(session);
        if (!StringComparer.Ordinal.Equals(session.WorkflowId, _resolver.WorkflowId))
        {
            throw new ArgumentException(
                "A compiled authoring service can prepare only its own workflow session.",
                nameof(session));
        }

        CompiledAuthoringSelectedInput[] selected = SnapshotInputs(inputs);
        var acceptedFileStamps = selected
            .Where(static input => input.Bytes is not null)
            .ToDictionary(
                static input => input.SlotId,
                static input => FileStamp.FromBytes(input.Bytes!.Value.Span),
                StringComparer.Ordinal);
        CompiledAuthoringSelectionSnapshot selection = ProjectSelection(
            icId,
            session.CurrentSnapshot?.AuthoringRevision ?? new AuthoringRevision(1),
            [.. selected.Select(static input => input.SlotId)],
            acceptedFileStamps,
            session.CurrentSnapshot);
        selected = NormalizeSelectedInputs(selected, selection.InputBindings);
        if (selection.Issues.Count != 0 ||
            selection.Catalog.Routes.SingleOrDefault()?.ExactCapability is null)
        {
            return new CompiledAuthoringSessionPreparation(
                session.CurrentSnapshot,
                selection,
                Inspection: null,
                SessionIssue: null);
        }

        AuthoringSessionTransitionResult activated = session.Activate(selection);
        if (!activated.Succeeded)
        {
            return new CompiledAuthoringSessionPreparation(
                activated.Snapshot,
                selection,
                Inspection: null,
                activated.Issue);
        }

        AuthoringSlotInspectionBatchStartResult started = session.BeginSlotFileInspections(
            selected.ToDictionary(
                static input => input.SlotId,
                static input => input.SelectedPathHint,
                StringComparer.Ordinal));
        if (!started.Succeeded)
        {
            return new CompiledAuthoringSessionPreparation(
                started.Snapshot,
                selection,
                Inspection: null,
                started.Issue);
        }

        CompiledAuthoringInspectionBatch inspection = InspectBatch(
            icId,
            started.Snapshot!.AuthoringRevision,
            selected,
            started.Snapshot.ExactCapability);
        AuthoringSessionTransitionResult completed =
            session.TryCompleteSlotFileInspectionBatch(
                inspection.Catalog,
                started.Leases,
                inspection.Statuses.Values.ToDictionary(
                    static status => status.SlotId,
                    StringComparer.Ordinal),
                inspection.MetadataInspection);
        return new CompiledAuthoringSessionPreparation(
            completed.Snapshot,
            selection,
            inspection,
            completed.Issue);
    }

    /// <summary>Resolves and inspects one immutable selected-input batch without fallback.</summary>
    public CompiledAuthoringInspectionBatch InspectBatch(
        string icId,
        AuthoringRevision authoringRevision,
        IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs,
        ResolvedCapability? retainedCapability = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        CompiledAuthoringSelectedInput[] captured = SnapshotInputs(inputs);
        CompiledAuthoringSelectedInput[] selected = retainedCapability is null
            ? captured
            : NormalizeSelectedInputs(
                captured,
                [
                    .. retainedCapability.CompiledComposition.V2Details.InputContract.SpaceBindings
                        .Select(static binding => new CompiledAuthoringInputBinding(
                            binding.SlotId,
                            binding.AddressSpaceId)),
                ]);

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
        selected = NormalizeSelectedInputs(captured, ProjectInputBindings(discovery));
        selectedSlotIds = [.. selected.Select(static input => input.SlotId)];
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
                : _resolver.ResolveExact(
                    icId,
                    authoringRevision,
                    prerequisiteLength,
                    selectedSlotIds);
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

    private static CompiledAuthoringSelectedInput[] SnapshotInputs(
        IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        CompiledAuthoringSelectedInput[] selected = [.. inputs];
        bool invalid = selected.Length == 0 ||
            selected.Any(static input => input is null) ||
            selected.Any(static input =>
                string.IsNullOrWhiteSpace(input.SlotId) ||
                string.IsNullOrWhiteSpace(input.SelectedPathHint)) ||
            selected.Select(static input => input.SlotId)
                .Distinct(StringComparer.Ordinal).Count() != selected.Length;
        return invalid
            ? throw new ArgumentException(
                "A compiled authoring inspection batch requires unique selected slots and paths.",
                nameof(inputs))
            : selected;
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
        IReadOnlyDictionary<string, AuthoringInputSlotStatus> statuses =
            AuthoringInputSlotInspectionService.InspectBatch(
                capability,
                authoringRevision,
                sources,
                selectedBindings.ToDictionary(
                    static binding => binding.AddressSpaceId,
                    binding => selectedBySlotId[binding.SlotId].SelectedPathHint,
                    StringComparer.Ordinal),
                selectedSlotIds);
        FirmwareArtifactPayload[] acceptedArtifacts =
        [
            .. statuses.Values
                .Where(static status => status.AcceptedBytes is { Length: > 0 })
                .Select(static status => new FirmwareArtifactPayload(
                    status.AddressSpaceId,
                    status.AcceptedBytes!.Value.Span)),
        ];
        MetadataInspectionSnapshot metadataInspection = FirmwareMetadataInspector.Inspect(
            new MetadataInspectionRequest(
                capability.MetadataPlan,
                authoringRevision.Value,
                acceptedArtifacts));
        return new CompiledAuthoringInspectionBatch(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(
                capability,
                discoveryTransition),
            statuses,
            [],
            metadataInspection);
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
            .SelectMany(static group => group.Members.Select(member =>
            {
                InputSelectionReadinessIssue? issue = group.Issue;
                bool ownsIssue = issue is not null &&
                    (StringComparer.Ordinal.Equals(issue.SubjectId, member.SlotId) ||
                        (StringComparer.Ordinal.Equals(issue.SubjectId, group.GroupId) &&
                            StringComparer.Ordinal.Equals(
                                member.SlotId,
                                group.Members[0].SlotId)));
                return ownsIssue
                    ? member with
                    {
                        Reason = issue!.Message,
                        NextAction = issue.NextAction,
                        IssueCode = issue.Code,
                    }
                    : member;
            }))
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
                            authoringRevision,
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
                    authoringRevision,
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
        AuthoringRevision authoringRevision,
        IReadOnlyCollection<string> selectedSlotIds,
        long? prerequisiteLength,
        IReadOnlyList<CompositionIssue> issues,
        InputSelectionReadinessSnapshot? exactSelectionReadiness)
    {
        CompiledInputContract contract = discovery.DiscoveryCapability.CompiledComposition
            .V2Details.InputContract;
        var groupMemberIds = contract.SelectionGroups
            .SelectMany(static group => group.MemberSlotIds)
            .ToHashSet(StringComparer.Ordinal);
        InputSelectionReadinessSnapshot selectionReadiness =
            exactSelectionReadiness ?? InputSelectionReadinessResolver.Resolve(
                authoringRevision,
                contract.SelectionGroups,
                selectedSlotIds.Where(groupMemberIds.Contains));
        var groupReadiness =
            selectionReadiness.Groups.SelectMany(group =>
            {
                InputSelectionMemberReadiness? issueMember = group.Issue is null
                    ? null
                    : group.Members.FirstOrDefault(member => StringComparer.Ordinal.Equals(
                            member.SlotId,
                            group.Issue.SubjectId)) ??
                        group.Members.FirstOrDefault(static member => member.IsSelected) ??
                        group.Members[0];
                return group.Members.Select(member => ReferenceEquals(member, issueMember)
                    ? member with
                    {
                        Reason = group.Issue!.Message,
                        NextAction = group.Issue.NextAction,
                        IssueCode = group.Issue.Code,
                    }
                    : member);
            }).ToDictionary(static member => member.SlotId, StringComparer.Ordinal);
        string reason = issues.Count == 0
            ? "The exact selected-input state is not admitted."
            : issues[0].Message;
        return Array.AsReadOnly(
        [
            .. discovery.AvailableSlotIds.Select(slotId =>
            {
                if (groupReadiness.TryGetValue(
                        slotId,
                        out InputSelectionMemberReadiness? readiness))
                {
                    return readiness;
                }

                bool selected = selectedSlotIds.Contains(slotId, StringComparer.Ordinal);
                bool alternate = !selected && _resolver.ResolveExact(
                    discovery.DiscoveryCapability.Identity.IcId,
                    authoringRevision,
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
