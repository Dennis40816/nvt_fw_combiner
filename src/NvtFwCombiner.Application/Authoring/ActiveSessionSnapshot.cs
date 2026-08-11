using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Coherent immutable state consumed by UI or CLI adapters.</summary>
public sealed class ActiveSessionSnapshot
{
    internal ActiveSessionSnapshot(
        string workflowId,
        ResolutionToken resolutionToken,
        AuthoringRevision authoringRevision,
        string selectedRouteId,
        string capabilityFingerprint,
        bool executionAdmitted,
        string selectedIc,
        string selectedIcCount,
        string selectedMapVariant,
        IEnumerable<string> icChoices,
        IEnumerable<string> icCountChoices,
        IEnumerable<AuthoringSlotState> slots,
        AuthoringDraftState? draftState,
        string? draftCapabilityFingerprint,
        IEnumerable<AuthoringDerivedPublication> derivedPublications,
        string? compilationFingerprint = null,
        ResolvedCapability? exactCapability = null,
        IEnumerable<AuthoringInputSlotStatus>? inputSlotStatuses = null,
        IEnumerable<InputSelectionMemberReadiness>? inputSelectionReadiness = null,
        MetadataInspectionSnapshot? metadataInspection = null)
    {
        WorkflowId = workflowId;
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        SelectedRouteId = selectedRouteId;
        CapabilityFingerprint = capabilityFingerprint;
        CompilationFingerprint = compilationFingerprint;
        ExactCapability = exactCapability;
        ExecutionAdmitted = executionAdmitted;
        SelectedIc = selectedIc;
        SelectedIcCount = selectedIcCount;
        SelectedMapVariant = selectedMapVariant;
        IcChoices = Array.AsReadOnly([.. icChoices]);
        IcCountChoices = Array.AsReadOnly([.. icCountChoices]);
        Slots = Array.AsReadOnly([.. slots]);
        DerivedPublications = Array.AsReadOnly([.. derivedPublications]);
        InputSlotStatuses = Array.AsReadOnly([.. inputSlotStatuses ?? []]);
        InputSelectionReadiness = Array.AsReadOnly([.. inputSelectionReadiness ?? []]);
        if (InputSlotStatuses.Select(static status => status.SlotId)
                .Distinct(StringComparer.Ordinal).Count() != InputSlotStatuses.Count)
        {
            throw new ArgumentException(
                "Session input-slot statuses must be uniquely identified.",
                nameof(inputSlotStatuses));
        }

        DraftState = draftState;
        DraftCapabilityFingerprint = draftCapabilityFingerprint;
        if (metadataInspection is not null &&
            (exactCapability is null ||
             metadataInspection.ResolutionToken != exactCapability.MetadataPlan.ResolutionToken ||
             metadataInspection.AuthoringRevision != authoringRevision.Value ||
             metadataInspection.Results.Count != exactCapability.MetadataPlan.Entries.Count ||
             metadataInspection.Results.Where((result, index) =>
                 !ReferenceEquals(
                     result.PlanEntry,
                     exactCapability.MetadataPlan.Entries[index])).Any()))
        {
            throw new ArgumentException(
                "Session metadata inspection must retain the exact capability plan and authoring revision.",
                nameof(metadataInspection));
        }

        MetadataInspection = metadataInspection;
    }

    /// <summary>Mode/workflow identity for this isolated session.</summary>
    public string WorkflowId { get; }

    /// <summary>Canonical catalog publication identity.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Current authoring-input revision.</summary>
    public AuthoringRevision AuthoringRevision { get; }

    /// <summary>Selected exact canonical route identity.</summary>
    public string SelectedRouteId { get; }

    /// <summary>Selected firmware-semantic identity.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Exact compiled-composition identity for the active slot definitions.</summary>
    public string? CompilationFingerprint { get; }

    /// <summary>The one exact immutable capability owned by this compiled session.</summary>
    public ResolvedCapability? ExactCapability { get; }

    /// <summary>Whether Build may proceed after remaining readiness checks.</summary>
    public bool ExecutionAdmitted { get; }

    /// <summary>Selected canonical IC.</summary>
    public string SelectedIc { get; }

    /// <summary>Selected IC Count variant.</summary>
    public string SelectedIcCount { get; }

    /// <summary>Resolved map variant retained for traceability, not user inference.</summary>
    public string SelectedMapVariant { get; }

    /// <summary>Current workflow IC choices.</summary>
    public IReadOnlyList<string> IcChoices { get; }

    /// <summary>IC Count choices for the selected IC.</summary>
    public IReadOnlyList<string> IcCountChoices { get; }

    /// <summary>Resolved slot states.</summary>
    public IReadOnlyList<AuthoringSlotState> Slots { get; }

    /// <summary>Current immutable typed draft, or null when this mode has none.</summary>
    public AuthoringDraftState? DraftState { get; }

    internal string? DraftCapabilityFingerprint { get; }

    /// <summary>Derived result references admitted for this exact snapshot.</summary>
    public IReadOnlyList<AuthoringDerivedPublication> DerivedPublications { get; }

    /// <summary>Complete per-slot readiness and terminal health for the current inspection batch.</summary>
    public IReadOnlyList<AuthoringInputSlotStatus> InputSlotStatuses { get; }

    /// <summary>Current compiler-owned picker readiness for this exact authoring revision.</summary>
    public IReadOnlyList<InputSelectionMemberReadiness> InputSelectionReadiness { get; }

    /// <summary>Canonical metadata inspection published with this exact input batch.</summary>
    public MetadataInspectionSnapshot? MetadataInspection { get; }

    /// <summary>True when every current slot has terminal accepted health under this exact compilation.</summary>
    public bool HasCurrentInputInspection
    {
        get
        {
            if (CompilationFingerprint is not { } fingerprint ||
                !DerivedPublications.Any(publication =>
                    publication.Kind == AuthoringDerivedResultKind.Inspection &&
                    StringComparer.Ordinal.Equals(publication.CompilationFingerprint, fingerprint)))
            {
                return false;
            }

            AuthoringSlotState[] inspectionSlots =
            [
                .. Slots.Where(IsRequiredOrSelectedInput),
            ];
            return inspectionSlots.Length > 0 && inspectionSlots.All(static slot =>
                    slot.SelectedPath is not null && slot.FileStamp is not null &&
                    slot.Lifecycle is AuthoringSlotLifecycle.Verified or AuthoringSlotLifecycle.Warning) &&
                InputSlotStatuses.Count == inspectionSlots.Length && InputSlotStatuses.All(status =>
                    status.IsTerminal &&
                    status.AcceptedByteArray is not null &&
                    StringComparer.Ordinal.Equals(status.CompilationFingerprint, fingerprint));
        }
    }

    /// <summary>Returns the exact capability only after the requested result is current.</summary>
    public ResolvedCapability? GetAcceptedCapability(AuthoringDerivedResultKind kind)
    {
        return ExactCapability is { } capability &&
            DerivedPublications.Any(publication =>
                publication.Kind == kind &&
                StringComparer.Ordinal.Equals(
                    publication.CompilationFingerprint,
                    capability.CompiledComposition.CompilationFingerprint))
                ? capability
                : null;
    }

    private bool IsRequiredOrSelectedInput(AuthoringSlotState slot)
    {
        if (slot.SelectedPath is not null)
        {
            return true;
        }

        CompiledInputContract? contract = ExactCapability?.CompiledComposition.V2Details.InputContract;
        return contract is not null && contract.SpaceBindings.Any(binding =>
            StringComparer.Ordinal.Equals(
                binding.InstancePolicy == CompiledInputInstancePolicy.PerBinding
                    ? binding.AddressSpaceId
                    : binding.SlotId,
                slot.DefinitionId) &&
            contract.Slots.Single(requirement => StringComparer.Ordinal.Equals(
                requirement.SlotId,
                binding.SlotId)).Required);
    }
}
