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
        CompiledAuthoringWorkflowDiscovery preparationDiscovery = _resolver.Discover(icId);
        CompiledAuthoringSelectionSnapshot selection =
            preparationDiscovery.DiscoveryRoute is not null &&
            selected.SingleOrDefault(input => StringComparer.Ordinal.Equals(
                input.SlotId, preparationDiscovery.CompilationPrerequisiteSlotId))?.Bytes is { } capturedDp
                ? ProjectCapturedSelection(
                    icId,
                    session.CurrentSnapshot?.AuthoringRevision ?? new AuthoringRevision(1),
                    [.. selected.Select(static input => input.SlotId)],
                    capturedDp)
                : ProjectSelection(
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

        CompiledAuthoringInspectionBatch inspection =
            preparationDiscovery.DiscoveryRoute is not null
                ? InspectExactBatch(
                    started.Snapshot!.ExactCapability ?? throw new InvalidOperationException(
                        "Captured preparation lost its exact capability."),
                    started.Snapshot.AuthoringRevision,
                    selected,
                    [.. selected.Select(static input => input.SlotId)],
                    selection.Catalog.Routes.Single().DiscoveryTransition)
                : InspectBatch(
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
        bool retainedSourceEnvelope = retainedCapability?.CompiledComposition.V2Details
            .Provenance.Context is ResolvedMapV2CompilationContext { SourceEnvelope: not null };
        CompiledAuthoringSelectedInput[] retainedSelected = retainedCapability is null
            ? captured
            : NormalizeSelectedInputs(
                captured,
                [
                    .. retainedCapability.CompiledComposition.V2Details.InputContract.SpaceBindings
                        .Select(static binding => new CompiledAuthoringInputBinding(
                            binding.SlotId, binding.AddressSpaceId)),
                ]);
        if (!retainedSourceEnvelope && retainedCapability is not null &&
            CanInspectRetainedExactCapability(
                retainedCapability,
                icId,
                [.. retainedSelected.Select(static input => input.SlotId)],
                retainedSelected))
        {
            return InspectExactBatch(
                retainedCapability,
                authoringRevision,
                retainedSelected,
                [.. retainedSelected.Select(static input => input.SlotId)]);
        }

        CompiledAuthoringWorkflowDiscovery definitionDiscovery = _resolver.Discover(icId);
        CompiledAuthoringSelectedInput[] selected = retainedCapability is null ||
            definitionDiscovery.DiscoveryRoute is not null
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
        CompiledAuthoringWorkflowDiscovery discovery = definitionDiscovery;
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
                : discovery.DiscoveryRoute is not null
                    ? _resolver.ResolveExact(
                        icId,
                        authoringRevision,
                        prerequisiteInput!.Bytes!.Value,
                        selectedSlotIds)
                    : _resolver.ResolveExact(
                        icId,
                        authoringRevision,
                        prerequisiteLength,
                        selectedSlotIds);
        if (!exact.Succeeded || !ContainsEverySelectedSlot(exact.Capability, selectedSlotIds) ||
            !MatchesDiscovery(discovery, exact.Capability))
        {
            IReadOnlyList<CompositionIssue> issues = exact.Issues.Count != 0
                ? exact.Issues
                : exact.Capability is not null &&
                    !MatchesDiscovery(discovery, exact.Capability)
                    ? [new CompositionIssue(AuthoringSessionIssueCodes.StalePublication,
                        "The exact compilation belongs to a different canonical publication.")]
                    : [new CompositionIssue(
                        InputSelectionReadinessIssueCodes.SelectionNotApplicable,
                        "The exact compilation does not contain every selected input.")];
            CompositionIssue primary = issues[0];
            return discovery.DiscoveryRoute is not null
                ? new CompiledAuthoringInspectionBatch(
                    DiscoveryCatalog(discovery),
                    new Dictionary<string, AuthoringInputSlotStatus>(StringComparer.Ordinal),
                    issues)
                : new CompiledAuthoringInspectionBatch(
                DiscoveryCatalog(discovery),
                selected.ToDictionary(
                    static input => input.SlotId,
                    input => AuthoringInputSlotInspectionService.BlockBeforeCompilation(
                        discovery.DiscoveryCapability!,
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
        ResolvedCapability capability = retainedCapability is null ||
            discovery.DiscoveryRoute is not null
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
            : [.. selected.Select(static input => input with
            {
                Bytes = input.Bytes?.ToArray(),
            })];
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
        ResolvedCapability? discoveredExactCapability,
        string? routePrerequisiteSlotId = null)
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
            (routePrerequisiteSlotId is not null &&
                !session.Slots.Any(slot =>
                    StringComparer.Ordinal.Equals(slot.DefinitionId, routePrerequisiteSlotId) &&
                    slot.SelectedPath is not null)) ||
            session.Slots.Where(slot => slot.SelectedPath is not null &&
                    (routePrerequisiteSlotId is null ||
                        StringComparer.Ordinal.Equals(slot.DefinitionId, routePrerequisiteSlotId)))
                .Any(static slot => slot.FileStamp is null ||
                    slot.Lifecycle is not (
                        AuthoringSlotLifecycle.Verified or
                        AuthoringSlotLifecycle.Warning)))
        {
            return null;
        }

        var retainedStamps = session.Slots
            .Where(slot => slot.SelectedPath is not null && slot.FileStamp is not null &&
                (routePrerequisiteSlotId is null ||
                    StringComparer.Ordinal.Equals(slot.DefinitionId, routePrerequisiteSlotId)))
            .ToDictionary(static slot => slot.DefinitionId, static slot => slot.FileStamp!.Value,
                StringComparer.Ordinal);
        return (routePrerequisiteSlotId is null
                ? retainedStamps.Count == acceptedFileStamps.Count
                : retainedStamps.Count == 1 &&
                    retainedStamps.ContainsKey(routePrerequisiteSlotId)) &&
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

    internal static bool IsEquivalentExactCapability(
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
        CompiledInputContract contract = discovery.DiscoveryCapability!.CompiledComposition
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
        return discovery.DiscoveryRoute is { } route
            ? AuthoringCapabilityCatalogSnapshot.FromDynamicRoute(
                route, discovery.AvailableSlotIds, discovery.DiscoveryTransition)
            : AuthoringCapabilityCatalogSnapshot.FromDiscovery(
                discovery.DiscoveryCapability!,
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

    private static bool MatchesDiscovery(
        CompiledAuthoringWorkflowDiscovery discovery,
        ResolvedCapability? capability)
    {
        if (capability is null || discovery.DiscoveryRoute is null)
        {
            return true;
        }

        ReviewedDiscoveryTransition transition = discovery.DiscoveryTransition!;
        return capability.ResolutionToken == transition.ResolutionToken &&
            StringComparer.Ordinal.Equals(capability.Identity.WorkflowId, transition.WorkflowId) &&
            StringComparer.Ordinal.Equals(capability.Identity.IcId, transition.IcId) &&
            StringComparer.Ordinal.Equals(
                capability.Identity.IcCountVariant, transition.IcCountVariant) &&
            transition.Allows(
                capability.Identity.RouteId, capability.CapabilityFingerprint);
    }

    private void ValidateDiscovery(CompiledAuthoringWorkflowDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        bool hasDefinition = discovery.DiscoveryRoute is not null;
        if (hasDefinition == (discovery.DiscoveryCapability is not null))
        {
            throw new InvalidOperationException(
                "Authoring discovery must contain exactly one compiled or definition-level route.");
        }
        CapabilityRouteIdentity identity = hasDefinition
            ? discovery.DiscoveryRoute!.Identity
            : discovery.DiscoveryCapability!.Identity;
        ResolutionToken token = hasDefinition
            ? discovery.DiscoveryRoute!.ResolutionToken
            : discovery.DiscoveryCapability!.ResolutionToken;
        string fingerprint = hasDefinition
            ? discovery.DiscoveryRoute!.CapabilityFingerprint
            : discovery.DiscoveryCapability!.CapabilityFingerprint;
        if (!StringComparer.Ordinal.Equals(
                identity.WorkflowId,
                _resolver.WorkflowId) ||
            (hasDefinition && discovery.AvailableInputBindings is null) ||
            discovery.AvailableSlotIds.Count == 0 ||
            discovery.AvailableSlotIds.Any(string.IsNullOrWhiteSpace) ||
            discovery.AvailableSlotIds.Distinct(StringComparer.Ordinal).Count() !=
                discovery.AvailableSlotIds.Count ||
            (discovery.CompilationPrerequisiteSlotId is { } prerequisite &&
                (!discovery.AvailableSlotIds.Contains(prerequisite, StringComparer.Ordinal) ||
                    discovery.DiscoveryTransition is null ||
                    discovery.DiscoveryTransition.ResolutionToken !=
                        token ||
                    !StringComparer.Ordinal.Equals(
                        discovery.DiscoveryTransition.WorkflowId,
                        identity.WorkflowId) ||
                    !StringComparer.Ordinal.Equals(
                        discovery.DiscoveryTransition.IcId,
                        identity.IcId) ||
                    !StringComparer.Ordinal.Equals(
                        discovery.DiscoveryTransition.IcCountVariant,
                        identity.IcCountVariant) ||
                    !StringComparer.Ordinal.Equals(
                        discovery.DiscoveryTransition.DiscoveryMember.RouteId,
                        identity.RouteId) ||
                    !StringComparer.Ordinal.Equals(
                        discovery.DiscoveryTransition.DiscoveryMember.CapabilityFingerprint,
                        fingerprint) ||
                    !StringComparer.Ordinal.Equals(
                        discovery.DiscoveryTransition.PrerequisiteSlotId,
                        prerequisite))))
        {
            throw new InvalidOperationException(
                "The compiler adapter returned an invalid authoring discovery contract.");
        }
    }
}
