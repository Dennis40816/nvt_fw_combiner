using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>
/// Immutable headless projection of prerequisite readiness and selected-artifact
/// inspection health for one compiler-owned input slot.
/// </summary>
public sealed class AuthoringInputSlotStatus
{
    internal AuthoringInputSlotStatus(
        CapabilityRouteIdentity identity,
        ResolutionToken resolutionToken,
        AuthoringRevision authoringRevision,
        string capabilityFingerprint,
        string? compilationFingerprint,
        InputSelectionMemberReadiness selectionReadiness,
        string addressSpaceId,
        AuthoringSlotLifecycle? inspectionLifecycle,
        FileStamp? fileStamp,
        CompiledInputArtifactInspectionResult? inspection,
        string? selectedPathHint = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(selectionReadiness);
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityFingerprint);
        if (inspectionLifecycle is not null && compilationFingerprint is null)
        {
            throw new ArgumentException(
                "Selected-artifact inspection health requires one exact compilation fingerprint.",
                nameof(compilationFingerprint));
        }
        WorkflowId = identity.WorkflowId;
        RouteId = identity.RouteId;
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        CapabilityFingerprint = capabilityFingerprint;
        CompilationFingerprint = compilationFingerprint;
        SelectionReadiness = selectionReadiness;
        AddressSpaceId = addressSpaceId;
        SelectedPathHint = selectedPathHint;
        InspectionLifecycle = inspectionLifecycle;
        FileStamp = fileStamp;
        Inspection = inspection;
    }

    /// <summary>Canonical workflow owning this slot.</summary>
    public string WorkflowId { get; }

    /// <summary>Exact selected route identity.</summary>
    public string RouteId { get; }

    /// <summary>Catalog publication that owns this projection.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Authoring-input revision that owns this projection.</summary>
    public AuthoringRevision AuthoringRevision { get; }

    /// <summary>Reviewed capability-definition identity.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Exact compiled-composition identity, or null while a prerequisite prevents compilation.</summary>
    public string? CompilationFingerprint { get; }

    /// <summary>Compiler-owned immutable address-space binding.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Selected path hint used only to reject stale inspection publication.</summary>
    public string? SelectedPathHint { get; }

    /// <summary>Compiler-owned slot definition.</summary>
    public string SlotId => SelectionReadiness.SlotId;

    /// <summary>Complete prerequisite and selection readiness projection.</summary>
    public InputSelectionMemberReadiness SelectionReadiness { get; }

    /// <summary>Prerequisite and applicability state, independent from inspection health.</summary>
    public ResolvedChildReadiness Readiness => SelectionReadiness.Readiness;

    /// <summary>Whether an independent picker transition is currently admitted.</summary>
    public bool CanSelect => SelectionReadiness.CanSelect;

    /// <summary>Typed operator action for unresolved readiness.</summary>
    public InputSelectionNextAction? ReadinessNextAction => SelectionReadiness.NextAction;

    /// <summary>
    /// Null before inspection starts; otherwise Checking, Verified, Warning, or Error.
    /// Empty and Selected are session states and are never published here.
    /// </summary>
    public AuthoringSlotLifecycle? InspectionLifecycle { get; }

    /// <summary>Content-authoritative complete source identity after terminal inspection.</summary>
    public FileStamp? FileStamp { get; }

    /// <summary>Compiler-owned terminal diagnostic, or null while absent/checking.</summary>
    public CompiledInputArtifactInspectionResult? Inspection { get; }

    /// <summary>Stable terminal issue code, including source-access failures.</summary>
    public string? InspectionIssueCode => Inspection?.IssueCode ??
        (InspectionLifecycle == AuthoringSlotLifecycle.Error && FileStamp is null
            ? InputArtifactInspectionIssueCodes.SourceUnreadable
            : null);

    /// <summary>True when the terminal input health prevents Build.</summary>
    public bool BlocksBuild => Inspection?.BlocksBuild ??
        (InspectionLifecycle == AuthoringSlotLifecycle.Error);

    /// <summary>Typed corrective action for the terminal input diagnostic.</summary>
    public CompiledInputArtifactInspectionNextAction InspectionNextAction =>
        InspectionLifecycle == AuthoringSlotLifecycle.Error && Inspection is null
            ? CompiledInputArtifactInspectionNextAction.SelectReadableInput
            : Inspection?.NextAction ?? CompiledInputArtifactInspectionNextAction.None;

    /// <summary>True only for a completed immutable artifact inspection.</summary>
    public bool IsTerminal => InspectionLifecycle is
        AuthoringSlotLifecycle.Verified or
        AuthoringSlotLifecycle.Warning or
        AuthoringSlotLifecycle.Error;
}

/// <summary>Typed result of binding one terminal status to a captured publication lease.</summary>
public sealed record AuthoringInputSlotPublicationResult(
    AuthoringDerivedPublication? Publication,
    AuthoringSessionIssue? Issue)
{
    /// <summary>True only when the terminal status matches the captured lease.</summary>
    public bool Succeeded => Publication is not null && Issue is null;
}

/// <summary>
/// Projects and inspects authoring slots without file I/O or Presentation semantics.
/// Immutable bytes are always inspected by <see cref="CompiledInputArtifactInspectionService"/>.
/// </summary>
public static class AuthoringInputSlotInspectionService
{
    /// <summary>
    /// Projects readiness from a reviewed dynamic route before prerequisites
    /// permit one exact compilation.
    /// </summary>
    public static AuthoringInputSlotStatus ProjectReadiness(
        ResolvedCapabilityRoute route,
        AuthoringRevision authoringRevision,
        InputSelectionMemberReadiness selectionReadiness,
        CompiledInputSpaceBinding discoveryBinding)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(selectionReadiness);
        ArgumentNullException.ThrowIfNull(discoveryBinding);
        _ = StringComparer.Ordinal.Equals(
                discoveryBinding.SlotId,
                selectionReadiness.SlotId)
            ? true
            : throw new ArgumentException(
                "Selection readiness must identify the compiler-owned discovery binding.",
                nameof(selectionReadiness));

        return Create(
            route.Identity,
            route.ResolutionToken,
            authoringRevision,
            route.CapabilityFingerprint,
            compilationFingerprint: null,
            selectionReadiness,
            discoveryBinding.AddressSpaceId,
            inspectionLifecycle: null,
            fileStamp: null,
            inspection: null);
    }

    /// <summary>Projects readiness without fabricating selected-artifact health.</summary>
    public static AuthoringInputSlotStatus ProjectReadiness(
        ResolvedCapability capability,
        AuthoringRevision authoringRevision,
        InputSelectionMemberReadiness selectionReadiness,
        string addressSpaceId)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(selectionReadiness);
        ValidateBinding(capability, selectionReadiness, addressSpaceId);
        return Create(
            capability, authoringRevision, selectionReadiness, addressSpaceId,
            inspectionLifecycle: null, fileStamp: null, inspection: null);
    }

    /// <summary>Begins one admitted selected-artifact inspection as transient Checking.</summary>
    public static AuthoringInputSlotStatus BeginInspection(
        ResolvedCapability capability,
        AuthoringRevision authoringRevision,
        InputSelectionMemberReadiness selectionReadiness,
        string addressSpaceId)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(selectionReadiness);
        ValidateInspectable(capability, selectionReadiness, addressSpaceId);
        return Create(
            capability, authoringRevision, selectionReadiness, addressSpaceId,
            AuthoringSlotLifecycle.Checking, fileStamp: null, inspection: null);
    }

    /// <summary>Inspects immutable bytes, or publishes terminal Error when the selected source is unreadable.</summary>
    public static AuthoringInputSlotStatus Inspect(
        ResolvedCapability capability,
        AuthoringRevision authoringRevision,
        InputSelectionMemberReadiness selectionReadiness,
        string addressSpaceId,
        ReadOnlyMemory<byte>? sourceBytes,
        string? selectedPathHint = null)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(selectionReadiness);
        ValidateInspectable(capability, selectionReadiness, addressSpaceId);
        if (sourceBytes is null)
        {
            return Create(
                capability, authoringRevision, selectionReadiness, addressSpaceId,
                AuthoringSlotLifecycle.Error, fileStamp: null, inspection: null,
                selectedPathHint);
        }

        CompiledInputArtifactInspectionResult inspection =
            CompiledInputArtifactInspectionService.Inspect(
                capability.CompiledComposition,
                addressSpaceId,
                sourceBytes.Value);
        var fileStamp = new FileStamp(
            inspection.ActualLength,
            inspection.ActualSha256);
        return Create(
            capability, authoringRevision, selectionReadiness, addressSpaceId,
            MapLifecycle(inspection.Severity), fileStamp, inspection, selectedPathHint);
    }

    /// <summary>Inspects one coherent selected-input batch under one exact compilation identity.</summary>
    public static IReadOnlyDictionary<string, AuthoringInputSlotStatus> InspectBatch(
        ResolvedCapability capability,
        AuthoringRevision authoringRevision,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>?> sourceBytesByAddressSpaceId,
        IReadOnlyDictionary<string, string>? selectedPathsByAddressSpaceId = null,
        IReadOnlyCollection<string>? selectedSlotIds = null)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(sourceBytesByAddressSpaceId);
        CompiledInputContract contract = capability.CompiledComposition.V2Details.InputContract;
        var groupMemberIds = contract.SelectionGroups
            .SelectMany(static group => group.MemberSlotIds)
            .ToHashSet(StringComparer.Ordinal);
        InputSelectionReadinessSnapshot readiness = InputSelectionReadinessResolver.Resolve(
            authoringRevision,
            contract.SelectionGroups,
            selectedSlotIds is null
                ? contract.SelectionGroups.SelectMany(static group => group.SelectedSlotIds)
                : selectedSlotIds.Where(groupMemberIds.Contains));
        IReadOnlyDictionary<string, InputSelectionMemberReadiness> members = readiness.Groups
            .SelectMany(static group => group.Members).ToDictionary(static member => member.SlotId);
        Dictionary<string, AuthoringInputSlotStatus> statuses = new(StringComparer.Ordinal);
        foreach ((string addressSpaceId, ReadOnlyMemory<byte>? sourceBytes) in sourceBytesByAddressSpaceId)
        {
            CompiledInputSpaceBinding binding = contract.SpaceBindings.Single(candidate =>
                StringComparer.Ordinal.Equals(candidate.AddressSpaceId, addressSpaceId));
            InputSelectionMemberReadiness member = members.GetValueOrDefault(binding.SlotId) ??
                new InputSelectionMemberReadiness(
                    binding.SlotId, true, ResolvedChildReadiness.Ready, true, null, null);
            statuses.Add(
                addressSpaceId,
                member.IsSelected && member.Readiness == ResolvedChildReadiness.Ready
                    ? Inspect(
                        capability, authoringRevision, member, addressSpaceId, sourceBytes,
                        selectedPathsByAddressSpaceId?.GetValueOrDefault(addressSpaceId))
                    : ProjectReadiness(capability, authoringRevision, member, addressSpaceId));
        }

        return statuses;
    }

    /// <summary>Projects a selected input blocked before one exact compilation exists.</summary>
    public static AuthoringInputSlotStatus BlockBeforeCompilation(
        ResolvedCapability discoveryCapability, AuthoringRevision authoringRevision,
        CompiledInputSpaceBinding discoveryBinding, string issueCode, string reason)
    {
        ArgumentNullException.ThrowIfNull(discoveryCapability);
        ArgumentNullException.ThrowIfNull(discoveryBinding);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var readiness = new InputSelectionMemberReadiness(
            discoveryBinding.SlotId, true, ResolvedChildReadiness.Blocked, false, reason,
            new InputSelectionNextAction(InputSelectionNextActionKind.CorrectSelection, discoveryBinding.SlotId),
            issueCode);
        return Create(discoveryCapability.Identity, discoveryCapability.ResolutionToken, authoringRevision,
            discoveryCapability.CapabilityFingerprint, compilationFingerprint: null, readiness, discoveryBinding.AddressSpaceId,
            inspectionLifecycle: null, fileStamp: null, inspection: null);
    }

    /// <summary>Projects an explicitly identified selected input rejected before exact compilation.</summary>
    public static AuthoringInputSlotStatus BlockBeforeCompilation(
        ResolvedCapability discoveryCapability,
        AuthoringRevision authoringRevision,
        string slotId,
        string addressSpaceId,
        string issueCode,
        string reason,
        FileStamp? fileStamp,
        string selectedPathHint)
    {
        ArgumentNullException.ThrowIfNull(discoveryCapability);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPathHint);
        var readiness = new InputSelectionMemberReadiness(
            slotId,
            IsSelected: true,
            ResolvedChildReadiness.Blocked,
            CanSelect: false,
            reason,
            new InputSelectionNextAction(
                InputSelectionNextActionKind.CorrectSelection,
                slotId),
            issueCode);
        return Create(
            discoveryCapability.Identity,
            discoveryCapability.ResolutionToken,
            authoringRevision,
            discoveryCapability.CapabilityFingerprint,
            compilationFingerprint: null,
            readiness,
            addressSpaceId,
            inspectionLifecycle: null,
            fileStamp,
            inspection: null,
            selectedPathHint);
    }

    /// <summary>
    /// Binds terminal health to a captured session lease. The owning
    /// <see cref="AuthoringSessionState"/> performs the sole current-state
    /// publication check after this immutable result is created.
    /// </summary>
    public static AuthoringInputSlotPublicationResult TryCreatePublication(
        AuthoringInputSlotStatus status,
        AuthoringPublicationLease lease,
        string resultReference)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultReference);
        bool unreadableTerminal = status.InspectionLifecycle == AuthoringSlotLifecycle.Error &&
            status.Inspection is null && status.FileStamp is null;
        if (!status.IsTerminal ||
            (unreadableTerminal && string.IsNullOrWhiteSpace(status.SelectedPathHint)) ||
            (!unreadableTerminal && (status.Inspection is null || status.FileStamp is null)))
        {
            return Failure(
                AuthoringSessionIssueCodes.InvalidPublication,
                "Only terminal selected-artifact health may be published.",
                status.SlotId);
        }

        if (lease.Kind != AuthoringDerivedResultKind.Inspection)
        {
            return Failure(
                AuthoringSessionIssueCodes.InvalidPublication,
                "Per-slot health requires an inspection publication lease.",
                status.SlotId);
        }

        bool stale = status.ResolutionToken != lease.ResolutionToken ||
            status.AuthoringRevision != lease.AuthoringRevision ||
            !StringComparer.Ordinal.Equals(status.RouteId, lease.SelectedRouteId) ||
            !StringComparer.Ordinal.Equals(status.CapabilityFingerprint, lease.CapabilityFingerprint) ||
            !StringComparer.Ordinal.Equals(status.CompilationFingerprint, lease.CompilationFingerprint) ||
            !lease.Slots.Any(captured =>
                StringComparer.Ordinal.Equals(captured.DefinitionId, status.SlotId) &&
                (status.SelectedPathHint is null ||
                    StringComparer.Ordinal.Equals(captured.SelectedPath, status.SelectedPathHint)) &&
                captured.FileStamp == status.FileStamp);
        return stale
            ? Failure(
                AuthoringSessionIssueCodes.StaleInspection,
                "The selected-artifact inspection belongs to older authoring or compilation state.",
                status.SlotId)
            : new AuthoringInputSlotPublicationResult(
                new AuthoringDerivedPublication(
                    AuthoringDerivedResultKind.Inspection,
                    resultReference,
                    status.CompilationFingerprint),
                Issue: null);
    }

    private static AuthoringInputSlotStatus Create(
        ResolvedCapability capability,
        AuthoringRevision authoringRevision,
        InputSelectionMemberReadiness selectionReadiness,
        string addressSpaceId,
        AuthoringSlotLifecycle? inspectionLifecycle,
        FileStamp? fileStamp,
        CompiledInputArtifactInspectionResult? inspection,
        string? selectedPathHint = null)
    {
        return Create(
            capability.Identity, capability.ResolutionToken, authoringRevision,
            capability.CapabilityFingerprint, capability.CompiledComposition.CompilationFingerprint,
            selectionReadiness, addressSpaceId, inspectionLifecycle, fileStamp, inspection,
            selectedPathHint);
    }

    private static AuthoringInputSlotStatus Create(
        CapabilityRouteIdentity identity,
        ResolutionToken resolutionToken,
        AuthoringRevision authoringRevision,
        string capabilityFingerprint,
        string? compilationFingerprint,
        InputSelectionMemberReadiness selectionReadiness,
        string addressSpaceId,
        AuthoringSlotLifecycle? inspectionLifecycle,
        FileStamp? fileStamp,
        CompiledInputArtifactInspectionResult? inspection,
        string? selectedPathHint = null)
    {
        return new AuthoringInputSlotStatus(
            identity,
            resolutionToken,
            authoringRevision,
            capabilityFingerprint,
            compilationFingerprint,
            selectionReadiness,
            addressSpaceId,
            inspectionLifecycle,
            fileStamp,
            inspection,
            selectedPathHint);
    }

    private static void ValidateInspectable(
        ResolvedCapability capability,
        InputSelectionMemberReadiness selectionReadiness,
        string addressSpaceId)
    {
        ValidateBinding(capability, selectionReadiness, addressSpaceId);
        if (!selectionReadiness.IsSelected ||
            selectionReadiness.Readiness != ResolvedChildReadiness.Ready)
        {
            throw new ArgumentException(
                "Artifact inspection requires one selected, dependency-ready slot.",
                nameof(selectionReadiness));
        }
    }

    private static void ValidateBinding(
        ResolvedCapability capability,
        InputSelectionMemberReadiness selectionReadiness,
        string addressSpaceId)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(selectionReadiness);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        CompiledInputSpaceBinding? binding = capability.CompiledComposition.V2Details
            .InputContract.SpaceBindings.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.AddressSpaceId, addressSpaceId));
        if (binding is null ||
            !StringComparer.Ordinal.Equals(binding.SlotId, selectionReadiness.SlotId))
        {
            throw new ArgumentException(
                "Selection readiness must identify the compiler-owned slot for the requested address space.",
                nameof(selectionReadiness));
        }
    }

    private static AuthoringSlotLifecycle MapLifecycle(
        CompiledInputArtifactInspectionSeverity severity)
    {
        return severity switch
        {
            CompiledInputArtifactInspectionSeverity.Valid => AuthoringSlotLifecycle.Verified,
            CompiledInputArtifactInspectionSeverity.Warning => AuthoringSlotLifecycle.Warning,
            CompiledInputArtifactInspectionSeverity.Blocking => AuthoringSlotLifecycle.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };
    }

    private static AuthoringInputSlotPublicationResult Failure(
        string code,
        string message,
        string subject)
    {
        return new AuthoringInputSlotPublicationResult(
            Publication: null,
            new AuthoringSessionIssue(code, message, subject));
    }
}
