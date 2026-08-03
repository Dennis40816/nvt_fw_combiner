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
        IReadOnlyDictionary<string, FileStamp> acceptedFileStamps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(selectedSlotIds);
        ArgumentNullException.ThrowIfNull(acceptedFileStamps);
        CompiledAuthoringWorkflowDiscovery discovery = _resolver.Discover(icId);
        ValidateDiscovery(discovery);
        if (discovery.CompilationPrerequisiteSlotId is { } prerequisite &&
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
        CompiledAuthoringWorkflowResolution exact = _resolver.ResolveExact(
            icId,
            prerequisiteLength,
            selectedSlotIds);
        if (!exact.Succeeded)
        {
            return new CompiledAuthoringSelectionSnapshot(
                DiscoveryCatalog(discovery),
                ProjectRejectedSelection(
                    discovery,
                    selectedSlotIds,
                    prerequisiteLength,
                    exact.Issues),
                exact.Issues);
        }

        ResolvedCapability capability = exact.Capability!;
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
        IReadOnlyCollection<CompiledAuthoringSelectedInput> inputs)
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

        CompiledAuthoringWorkflowDiscovery discovery = _resolver.Discover(icId);
        ValidateDiscovery(discovery);
        CompiledAuthoringSelectedInput? prerequisiteInput =
            discovery.CompilationPrerequisiteSlotId is { } prerequisiteSlotId
                ? selected.SingleOrDefault(input => StringComparer.Ordinal.Equals(
                    input.SlotId,
                    prerequisiteSlotId))
                : null;
        long? prerequisiteLength = prerequisiteInput?.Bytes?.Length;
        string[] selectedSlotIds = [.. selected.Select(static input => input.SlotId)];
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

        ResolvedCapability capability = exact.Capability!;
        Dictionary<string, ReadOnlyMemory<byte>?> sources = selected.ToDictionary(
            static input => input.SlotId,
            static input => input.Bytes,
            StringComparer.Ordinal);
        return new CompiledAuthoringInspectionBatch(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(
                capability,
                discovery.DiscoveryTransition),
            AuthoringInputSlotInspectionService.InspectBatch(
                capability,
                authoringRevision,
                sources,
                selected.ToDictionary(
                    static input => input.SlotId,
                    static input => input.SelectedPathHint,
                    StringComparer.Ordinal),
                selectedSlotIds),
            []);
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
        IReadOnlyDictionary<string, InputSelectionMemberReadiness> resolvedMembers = readiness.Groups
            .SelectMany(static group => group.Members)
            .ToDictionary(static member => member.SlotId, StringComparer.Ordinal);
        return Array.AsReadOnly(
        [
            .. discovery.AvailableSlotIds.Select(slotId =>
            {
                if (contract.Slots.Any(slot => StringComparer.Ordinal.Equals(slot.SlotId, slotId)))
                {
                    return resolvedMembers.GetValueOrDefault(slotId) ??
                        new InputSelectionMemberReadiness(
                            slotId,
                            selectedSlotIds.Contains(slotId, StringComparer.Ordinal),
                            ResolvedChildReadiness.Ready,
                            CanSelect: true,
                            Reason: null,
                            NextAction: null);
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
