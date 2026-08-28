using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

internal sealed partial class CtrlRamAuthoringExperience :
    ICtrlRamAuthoring,
    ICompiledInputSlotInspector<FirmwareInspectionStatusBatch>
{
    private readonly ICtrlRamAuthoringAdapter _adapter;
    private readonly IRuntimeDependencyReadinessLeaseProvider _runtimeLeases;

    internal CtrlRamAuthoringExperience(
        ICtrlRamAuthoringAdapter adapter,
        IRuntimeDependencyReadinessLeaseProvider runtimeLeases)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _runtimeLeases = runtimeLeases ?? throw new ArgumentNullException(nameof(runtimeLeases));
    }

    /// <summary>Gets the declared CtrlRAM regions and input slots before a base is accepted.</summary>
    public CtrlRamInspectionDisplay GetDiscoveryDisplay(
        string icId,
        string number)
    {
        return _adapter.GetDiscoveryDisplay(icId, number);
    }

    /// <inheritdoc />
    public CtrlRamInspectionDisplay GetDiscoveryDisplayFromAcceptedBase(
        string icId,
        string number,
        ReadOnlyMemory<byte> acceptedBaseBytes)
    {
        return _adapter.GetDiscoveryDisplayFromAcceptedBase(
            icId,
            number,
            acceptedBaseBytes);
    }

    /// <inheritdoc />
    public CompiledInputVersionObservation? ProjectFirmwareVersionConfirmationLease(ActiveSessionSnapshot session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.WorkflowId == ExperienceIds.CtrlRamReplace && session.HasCurrentInputInspection
                ? session.InputSlotStatuses.SingleOrDefault(static status => status.AddressSpaceId == CompositionAddressSpaceIds.ReferenceBase)?
                    .Observation.Versions.SingleOrDefault(static version =>
                        version.Kind == CompiledInputVersionKind.TpReferenceFirmwareConfig)
                : null;
    }

    /// <inheritdoc />
    public bool IsFirmwareVersionConfirmationLeaseCurrent(ActiveSessionSnapshot current, ActiveSessionSnapshot lease)
    {
        return ReferenceEquals(current, lease);
    }

    /// <summary>Atomically prepares one exact CtrlRAM Replace session from host-read immutable inputs.</summary>
    public CtrlRamAuthoringSessionPreparation PrepareSession(
        AuthoringSessionState session,
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        CtrlRamFirmwareVersionDraftState? firmwareVersionEdit = null)
    {
        _ = TryPrepareSession(
            icId,
            number,
            slotPaths,
            inputBytes,
            firmwareVersionEdit,
            session,
            out ActiveSessionSnapshot? acceptedSession,
            out IReadOnlyList<CompositionIssue> issues);
        return new CtrlRamAuthoringSessionPreparation(acceptedSession, issues);
    }

    /// <inheritdoc />
    public AuthoringSessionTransitionResult AdoptInspectedBatch(
        AuthoringSessionState session,
        AuthoringCapabilityCatalogSnapshot catalog,
        IReadOnlyCollection<AuthoringInputSlotStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.TryAdoptExactSlotFileInspectionBatch(catalog, statuses);
    }

    private bool TryPrepareSession(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        CtrlRamFirmwareVersionDraftState? firmwareVersionEdit,
        AuthoringSessionState session,
        out ActiveSessionSnapshot? acceptedSession,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(slotPaths);
        ArgumentNullException.ThrowIfNull(inputBytes);
        ArgumentNullException.ThrowIfNull(session);
        CtrlRamAuthoringCompilation compilation = _adapter.Resolve(
            icId,
            number,
            slotPaths,
            firmwareVersionEdit: null,
            inputBytes);
        if (compilation.Capability is not { } resolved)
        {
            acceptedSession = null;
            issues = compilation.Issues;
            return false;
        }

        CompiledInputContract inputContract =
            resolved.CompiledComposition.V2Details.InputContract;
        var sourcesByAddressSpace = new Dictionary<string, ReadOnlyMemory<byte>?>(
            StringComparer.Ordinal);
        var pathsByAddressSpace = new Dictionary<string, string>(StringComparer.Ordinal);
        var pathsByDefinition = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (CompiledInputSpaceBinding binding in inputContract.SpaceBindings)
        {
            string inputId = StringComparer.Ordinal.Equals(
                    binding.AddressSpaceId,
                    CompositionAddressSpaceIds.ReferenceBase)
                ? CompositionSlotIds.ReplaceBase
                : binding.AddressSpaceId;
            if (!inputBytes.TryGetValue(inputId, out byte[]? bytes) ||
                !slotPaths.TryGetValue(inputId, out string? path))
            {
                acceptedSession = null;
                issues = [new CompositionIssue(
                    InputSelectionReadinessIssueCodes.SelectionPending,
                    $"CtrlRAM input '{inputId}' has not been selected.",
                    inputId)];
                return false;
            }

            string definitionId = binding.InstancePolicy ==
                    CompiledInputInstancePolicy.PerBinding
                ? binding.AddressSpaceId
                : binding.SlotId;
            if (pathsByDefinition.TryGetValue(definitionId, out string? existingPath) &&
                !StringComparer.Ordinal.Equals(existingPath, path))
            {
                acceptedSession = null;
                issues = [new CompositionIssue(
                    InputSelectionReadinessIssueCodes.SelectionNotApplicable,
                    $"CtrlRAM singleton input '{definitionId}' selected different artifacts.",
                    definitionId)];
                return false;
            }

            sourcesByAddressSpace.Add(binding.AddressSpaceId, bytes);
            pathsByAddressSpace.Add(binding.AddressSpaceId, path);
            pathsByDefinition[definitionId] = path;
        }

        var catalog =
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(resolved);
        AuthoringSessionTransitionResult activated = session.Activate(catalog);
        if (!activated.Succeeded)
        {
            acceptedSession = activated.Snapshot;
            issues = [new CompositionIssue(
                activated.Issue!.Code,
                activated.Issue.Message,
                activated.Issue.Subject)];
            return false;
        }

        AuthoringSlotInspectionBatchStartResult started =
            session.BeginSlotFileInspections(pathsByDefinition);
        if (!started.Succeeded)
        {
            acceptedSession = started.Snapshot;
            issues = [new CompositionIssue(
                started.Issue!.Code,
                started.Issue.Message,
                started.Issue.Subject)];
            return false;
        }

        IReadOnlyDictionary<string, AuthoringInputSlotStatus> statuses =
            AuthoringInputSlotInspectionService.InspectBatch(
                resolved,
                started.Snapshot!.AuthoringRevision,
                sourcesByAddressSpace,
                pathsByAddressSpace);
        AuthoringSessionTransitionResult completed =
            session.TryCompleteSlotFileInspectionBatch(
                catalog,
                started.Leases,
                statuses.Values.ToDictionary(
                    static status => status.SlotId,
                    StringComparer.Ordinal));
        acceptedSession = completed.Snapshot;
        issues =
        [
            .. statuses.Values
            .Where(static status => status.BlocksBuild)
            .Select(static status => new CompositionIssue(
                status.InspectionIssueCode ??
                    InputSelectionReadinessIssueCodes.SelectionNotApplicable,
                "The selected CtrlRAM input failed its compiled artifact inspection.",
                status.SlotId)),
        ];
        if (issues.Count > 0)
        {
            acceptedSession = null;
            return false;
        }

        if (!completed.Succeeded || acceptedSession?.HasCurrentInputInspection != true)
        {
            if (issues.Count == 0 && completed.Issue is { } issue)
            {
                issues = [new CompositionIssue(issue.Code, issue.Message, issue.Subject)];
            }
            return false;
        }

        if (firmwareVersionEdit is not null)
        {
            CtrlRamAuthoringTransitionResult transitioned = TransitionFirmwareVersionCompilation(
                session,
                icId,
                number,
                slotPaths,
                firmwareVersionEdit);
            acceptedSession = transitioned.Session;
            issues = transitioned.Issues;
            return transitioned.Succeeded;
        }

        return true;
    }

}
