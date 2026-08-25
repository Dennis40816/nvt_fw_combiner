using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

internal sealed partial class CtrlRamAuthoringExperience
{
    /// <summary>Refreshes CtrlRAM Preview/Build readiness without creating a run report.</summary>
    public async ValueTask<CapabilityActionReadinessSnapshot?>
        GetActionReadinessAsync(
            string icId,
            string number,
            IReadOnlyDictionary<string, string> slotPaths,
            ActiveSessionSnapshot acceptedSession,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slotPaths);
        ArgumentNullException.ThrowIfNull(acceptedSession);
        ResolvedCapability? capability = acceptedSession.GetAcceptedCapability(
            AuthoringDerivedResultKind.Inspection);
        IReadOnlyDictionary<string, byte[]> acceptedInputBytes =
            GetAcceptedInputBytes(acceptedSession);
        if (capability is null ||
            !StringComparer.Ordinal.Equals(
                acceptedSession.WorkflowId,
                ExperienceIds.CtrlRamReplace) ||
            !_adapter.IsAcceptedCapability(
                icId,
                number,
                slotPaths,
                firmwareVersionEdit: null,
                acceptedInputBytes,
                capability,
                out IReadOnlyDictionary<string, string> expectedPaths,
                out _) ||
            !HasExpectedPaths(acceptedSession, expectedPaths))
        {
            return null;
        }

        RuntimeDependencyReadinessLease runtime = _runtimeLeases.AcquireCurrent();
        CapabilityActionReadinessSnapshot readiness =
            await CapabilityActionReadinessResolver.RefreshAndResolveAsync(
            CapabilityAdmissionSnapshot.FromResolvedCapability(
                capability,
                acceptedSession.AuthoringRevision),
            acceptedSession.InputSlotStatuses.Select(static status =>
                new CapabilityChildReadiness(
                    status.SlotId,
                    ResolvedChildReadiness.Ready)),
            RuntimeDependencyReadinessRequest.FromResolvedCapability(
                capability,
                acceptedSession.AuthoringRevision),
            runtime.ReadinessProvider,
            runtime.Generation,
            runtime.GenerationIsCurrent,
            cancellationToken).ConfigureAwait(false);
        return CapabilityActionReadinessResolver.RequireRuntimeDependenciesForPreview(
            readiness);
    }

    public FirmwareInspectionStatusBatch InspectInputSlots(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
        Func<string, byte[]?> readFirmwareImage)
    {
        FirmwareInspectionSnapshotInput[] selected =
        [
            .. inputs.Where(static input => input.CtrlRamReplaceAddressSpaceId is not null),
        ];
        if (selected.Length == 0)
        {
            return FirmwareInspectionStatusBatch.Empty;
        }

        FirmwareInspectionSnapshotInput? reference = selected.SingleOrDefault(static input =>
            StringComparer.Ordinal.Equals(
                input.CtrlRamReplaceAddressSpaceId,
                CompositionAddressSpaceIds.ReferenceBase));
        string? number = reference?.CtrlRamRequest?.NumberToken;
        if (reference is null || number is null)
        {
            return FirmwareInspectionStatusBatch.Empty;
        }

        Dictionary<string, string> slotPaths = selected.ToDictionary(
            input => StringComparer.Ordinal.Equals(
                input.CtrlRamReplaceAddressSpaceId,
                CompositionAddressSpaceIds.ReferenceBase)
                    ? CompositionSlotIds.ReplaceBase
                    : input.CtrlRamReplaceAddressSpaceId!,
            static input => input.Path,
            StringComparer.Ordinal);
        var selectedInputBytes = selected
            .Select(input => (
                SlotId: StringComparer.Ordinal.Equals(
                        input.CtrlRamReplaceAddressSpaceId,
                        CompositionAddressSpaceIds.ReferenceBase)
                    ? CompositionSlotIds.ReplaceBase
                    : input.CtrlRamReplaceAddressSpaceId!,
                Bytes: readFirmwareImage(input.Path)))
            .Where(static input => input.Bytes is not null)
            .ToDictionary(
                static input => input.SlotId,
                static input => input.Bytes!,
                StringComparer.Ordinal);
        ResolvedCapability? capability = selected[0].ExactCapability;
        if (selected.Any(input => !ReferenceEquals(input.ExactCapability, capability)))
        {
            throw new InvalidOperationException("CtrlRAM inspection leases disagree on the exact compilation.");
        }
        if (capability is null)
        {
            CtrlRamAuthoringCompilation compilation = _adapter.Resolve(
                icId,
                number,
                slotPaths,
                firmwareVersionEdit: null,
                selectedInputBytes);
            capability = compilation.Capability;
            if (capability is null)
            {
                CompositionIssue[] remainingIssues =
                [
                    .. compilation.Issues.Where(issue => selected.Length > 1 ||
                        issue.Code != CompositionPlanningIssueCodes.ReplaceCtrlRamNoRegionInput),
                ];
                bool baseOnlyDiscovery = selected.Length == 1 &&
                    selectedInputBytes.ContainsKey(CompositionSlotIds.ReplaceBase) &&
                    compilation.Issues.Count == 1 &&
                    compilation.Issues[0].Code ==
                        CompositionPlanningIssueCodes.ReplaceCtrlRamNoRegionInput;
                return FirmwareInspectionStatusBatch.Empty with
                {
                    Issues = remainingIssues,
                    CtrlRamBaseDiscovery = baseOnlyDiscovery
                        ? new CtrlRamBaseDiscoveryResult(
                            reference.InspectionId,
                            CtrlRamBaseDiscoveryReadiness.Inspected)
                        : null,
                };
            }
        }

        var revision = new AuthoringRevision(
            selected.Select(static input => input.AuthoringRevision).Distinct().Single());
        Dictionary<string, ReadOnlyMemory<byte>?> sources = selected.ToDictionary(
            static input => input.CtrlRamReplaceAddressSpaceId!,
            input => selectedInputBytes.GetValueOrDefault(StringComparer.Ordinal.Equals(
                    input.CtrlRamReplaceAddressSpaceId,
                    CompositionAddressSpaceIds.ReferenceBase)
                ? CompositionSlotIds.ReplaceBase
                : input.CtrlRamReplaceAddressSpaceId!) is { } image
                ? image
                : (ReadOnlyMemory<byte>?)null,
            StringComparer.Ordinal);
        IReadOnlyDictionary<string, AuthoringInputSlotStatus> statuses =
            AuthoringInputSlotInspectionService.InspectBatch(
                capability!,
                revision,
                sources,
                selected.ToDictionary(
                    static input => input.CtrlRamReplaceAddressSpaceId!,
                    static input => input.Path,
                    StringComparer.Ordinal));
        return new FirmwareInspectionStatusBatch(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(capability!),
            selected.ToDictionary(
                static input => input.InspectionId,
                input => statuses[input.CtrlRamReplaceAddressSpaceId!],
                StringComparer.Ordinal),
            []);
    }

    private static bool HasExpectedPaths(
        ActiveSessionSnapshot session,
        IReadOnlyDictionary<string, string> expectedPaths)
    {
        return session.Slots.Count == expectedPaths.Count &&
            session.Slots.All(slot => slot.SelectedPath is { } path &&
                expectedPaths.TryGetValue(slot.DefinitionId, out string? expected) &&
                StringComparer.Ordinal.Equals(path, expected));
    }

    private static Dictionary<string, byte[]> GetAcceptedInputBytes(
        ActiveSessionSnapshot session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.HasCurrentInputInspection
            ? session.InputSlotStatuses.ToDictionary(
                static status => StringComparer.Ordinal.Equals(
                        status.AddressSpaceId,
                        CompositionAddressSpaceIds.ReferenceBase)
                    ? CompositionSlotIds.ReplaceBase
                    : status.AddressSpaceId,
                static status => status.AcceptedByteArray ??
                    throw new InvalidOperationException(
                        $"CtrlRAM input '{status.SlotId}' has no accepted immutable bytes."),
                StringComparer.Ordinal)
            : throw new InvalidOperationException(
                "CtrlRAM execution requires one current immutable inspection publication.");
    }
}
