using NvtFwCombiner.Application.Capabilities;
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
        AuthoringRevision authoringRevision,
        long? prerequisiteLength,
        IReadOnlyCollection<string> selectedSlotIds);
}

/// <summary>Reviewed pre-compilation facts exposed by a compiler adapter.</summary>
public sealed record CompiledAuthoringWorkflowDiscovery(
    ResolvedCapability DiscoveryCapability,
    IReadOnlyList<string> AvailableSlotIds,
    string? CompilationPrerequisiteSlotId,
    ReviewedDiscoveryTransition? DiscoveryTransition = null,
    IReadOnlyList<CompiledAuthoringInputBinding>? AvailableInputBindings = null);

/// <summary>One exact compiler result with its original issues retained.</summary>
public sealed record CompiledAuthoringWorkflowResolution(
    ResolvedCapability? Capability,
    IReadOnlyList<CompositionIssue> Issues,
    InputSelectionReadinessSnapshot? SelectionReadiness = null)
{
    /// <summary>True only when one exact compiled capability exists without issues.</summary>
    public bool Succeeded => Capability is not null && Issues.Count == 0;
}

/// <summary>One already-read immutable selected input supplied by a host adapter.</summary>
public sealed record CompiledAuthoringSelectedInput(
    string SlotId,
    string SelectedPathHint,
    ReadOnlyMemory<byte>? Bytes);

/// <summary>Compiler-owned slot identity and immutable input contract projected for authoring clients.</summary>
public sealed record CompiledAuthoringInputBinding(
    string SlotId,
    string AddressSpaceId,
    string Role = "",
    long? RequiredEndExclusive = null,
    IReadOnlyList<long>? ExpectedOuterLengths = null);

/// <summary>Application-owned picker projection for one authoring revision.</summary>
public sealed record CompiledAuthoringSelectionSnapshot(
    AuthoringCapabilityCatalogSnapshot Catalog,
    IReadOnlyList<InputSelectionMemberReadiness> Slots,
    IReadOnlyList<CompiledAuthoringInputBinding> InputBindings,
    IReadOnlyList<CompositionIssue> Issues);

/// <summary>Application-owned coherent inspection result for one exact selected batch.</summary>
public sealed record CompiledAuthoringInspectionBatch(
    AuthoringCapabilityCatalogSnapshot Catalog,
    IReadOnlyDictionary<string, AuthoringInputSlotStatus> Statuses,
    IReadOnlyList<CompositionIssue> Issues,
    MetadataInspectionSnapshot? MetadataInspection = null);

/// <summary>One exact selection and inspection atomically published to its owning session.</summary>
public sealed record CompiledAuthoringSessionPreparation(
    ActiveSessionSnapshot? Snapshot,
    CompiledAuthoringSelectionSnapshot Selection,
    CompiledAuthoringInspectionBatch? Inspection,
    AuthoringSessionIssue? SessionIssue)
{
    /// <summary>True only when the exact inspected bytes are current and execution-ready.</summary>
    public bool Succeeded => SessionIssue is null &&
        Selection.Issues.Count == 0 &&
        Inspection is not null &&
        Snapshot?.HasCurrentInputInspection == true;

    /// <summary>Exact compiler issues from selection or inspection, in their original order.</summary>
    public IReadOnlyList<CompositionIssue> Issues =>
        Inspection?.Issues.Count > 0
            ? Inspection.Issues
            : Inspection is not null
                ?
                [
                    .. Inspection.Statuses.Values
                        .Where(static status =>
                            status.BlocksBuild &&
                            status.InspectionIssueCode is not null)
                        .Select(static status => new CompositionIssue(
                            status.InspectionIssueCode!,
                            "The selected input failed its compiled artifact inspection.",
                            status.SlotId)),
                ]
                : Selection.Issues;
}
