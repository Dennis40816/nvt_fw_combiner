using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Bridges canonical authoring sessions to focused host inspection and readiness adapters.</summary>
public static partial class CompositionAuthoringSessionAdapter
{
    /// <summary>Refreshes CtrlRAM Preview/Build readiness without creating a run report.</summary>
    public static async ValueTask<CapabilityActionReadinessSnapshot?>
        GetCtrlRamReplaceActionReadinessAsync(
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
        CompositionPlanningAdapter.CtrlRamReplaceRunContext context =
            CompositionPlanningAdapter.CreateCtrlRamReplaceRunContext(
                icId, number, slotPaths, firmwareVersionEdit: null);
        if (capability is null ||
            !StringComparer.Ordinal.Equals(
                acceptedSession.WorkflowId,
                Profiles.IcWorkflowIds.CtrlRamReplace) ||
            !IsAcceptedCtrlRamSession(context, slotPaths, acceptedSession, capability))
        {
            return null;
        }

        ExternalProcessorGenerationLease runtime =
            ExternalProcessorFactory.AcquireCurrent();
        return await CapabilityActionReadinessResolver.RefreshAndResolveAsync(
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
            ExternalProcessorFactory.IsCurrent,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Resolves the exact CtrlRAM Replace authoring catalog for selected paths.</summary>
    public static AuthoringCapabilityCatalogSnapshot? GetCtrlRamReplaceAuthoringCatalog(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot? retainedSession = null)
    {
        ArgumentNullException.ThrowIfNull(slotPaths);
        CompositionPlanningAdapter.CtrlRamReplaceRunContext context =
            CompositionPlanningAdapter.CreateCtrlRamReplaceRunContext(
            icId, number, slotPaths, firmwareVersionEdit: null);
        ResolvedCapability? retained = retainedSession?.ExactCapability;
        return retainedSession is not null && retained is not null &&
            StringComparer.Ordinal.Equals(
                retainedSession.WorkflowId,
                Profiles.IcWorkflowIds.CtrlRamReplace) &&
            IsAcceptedCtrlRamSession(context, slotPaths, retainedSession, retained)
            ? AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(retained)
            : CompositionPlanningAdapter.TryResolveCtrlRamCapability(context, out ResolvedCapability? capability, out _)
            ? AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(capability!)
            : null;
    }

    internal static WorkbenchCompiledAuthoringInspectionBatch InspectCtrlRamReplaceInputSlots(
        string icId,
        IReadOnlyList<WorkbenchFirmwareInspectionInput> inputs,
        Func<string, byte[]?> readFirmwareImage)
    {
        WorkbenchFirmwareInspectionInput[] selected =
        [
            .. inputs.Where(static input => input.CtrlRamReplaceAddressSpaceId is not null),
        ];
        if (selected.Length == 0)
        {
            return WorkbenchCompiledAuthoringInspectionBatch.Empty;
        }

        WorkbenchFirmwareInspectionInput? reference = selected.SingleOrDefault(static input =>
            StringComparer.Ordinal.Equals(
                input.CtrlRamReplaceAddressSpaceId,
                CompositionAddressSpaceIds.ReferenceBase));
        string? number = reference?.CtrlRamRequest?.NumberToken;
        if (reference is null || number is null)
        {
            return WorkbenchCompiledAuthoringInspectionBatch.Empty;
        }

        Dictionary<string, string> slotPaths = selected.ToDictionary(
            input => StringComparer.Ordinal.Equals(
                input.CtrlRamReplaceAddressSpaceId,
                CompositionAddressSpaceIds.ReferenceBase)
                    ? WorkbenchSlotIds.ReplaceBase
                    : input.CtrlRamReplaceAddressSpaceId!,
            static input => input.Path,
            StringComparer.Ordinal);
        ResolvedCapability? capability = selected[0].ExactCapability;
        if (selected.Any(input => !ReferenceEquals(input.ExactCapability, capability)))
        {
            throw new InvalidOperationException("CtrlRAM inspection leases disagree on the exact compilation.");
        }
        CompositionPlanningAdapter.CtrlRamReplaceRunContext context =
            CompositionPlanningAdapter.CreateCtrlRamReplaceRunContext(
            icId, number, slotPaths, firmwareVersionEdit: null);
        if (capability is null &&
            !CompositionPlanningAdapter.TryResolveCtrlRamCapability(context, out capability, out _))
        {
            return WorkbenchCompiledAuthoringInspectionBatch.Empty;
        }

        var revision = new AuthoringRevision(
            selected.Select(static input => input.AuthoringRevision).Distinct().Single());
        Dictionary<string, ReadOnlyMemory<byte>?> sources = selected.ToDictionary(
            static input => input.CtrlRamReplaceAddressSpaceId!,
            input => readFirmwareImage(input.Path) is { } image
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
        return new WorkbenchCompiledAuthoringInspectionBatch(
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(capability!),
            selected.ToDictionary(
                static input => input.InspectionId,
                input => statuses[input.CtrlRamReplaceAddressSpaceId!],
                StringComparer.Ordinal),
            []);
    }

    internal static bool IsAcceptedCtrlRamSession(
        CompositionPlanningAdapter.CtrlRamReplaceRunContext context,
        IReadOnlyDictionary<string, string> slotPaths,
        ActiveSessionSnapshot session,
        ResolvedCapability capability)
    {
        if (!CompositionPlanningAdapter.IsAcceptedCtrlRamCapability(context, capability) ||
            context.BasePath is null)
        {
            return false;
        }

        var expectedPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.ReferenceBase] = context.BasePath,
        };
        foreach (TpCtrlRamPostbuildSource source in context.SelectedSources)
        {
            string slotId = CompositionMemoryProjection.CtrlRamSlotId(source.SourceId);
            if (!slotPaths.TryGetValue(slotId, out string? path))
            {
                return false;
            }
            expectedPaths[slotId] = path;
        }

        return session.Slots.Count == expectedPaths.Count &&
            session.Slots.All(slot => slot.SelectedPath is { } path &&
                expectedPaths.TryGetValue(slot.DefinitionId, out string? expected) &&
                StringComparer.Ordinal.Equals(path, expected));
    }

    internal static bool HasCurrentCtrlRamSessionFiles(
        CompositionPlanningAdapter.CtrlRamReplaceRunContext context,
        ActiveSessionSnapshot session)
    {
        if (context.BasePath is null || context.BaseBytes is null)
        {
            return false;
        }

        Dictionary<string, ReadOnlyMemory<byte>> currentBytes = new(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.ReferenceBase] = context.BaseBytes,
        };
        foreach (TpCtrlRamPostbuildSource source in context.SelectedSources)
        {
            string slotId = CompositionMemoryProjection.CtrlRamSlotId(source.SourceId);
            AuthoringSlotState? slot = session.Slots.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.DefinitionId, slotId));
            if (slot?.SelectedPath is not { } path || FirmwareInspectionAdapter.TryReadFirmwareImage(path) is not { } bytes)
            {
                return false;
            }
            currentBytes[slotId] = bytes;
        }

        return session.Slots.Count == currentBytes.Count && session.Slots.All(slot =>
            slot.FileStamp is { } accepted &&
            currentBytes.TryGetValue(slot.DefinitionId, out ReadOnlyMemory<byte> bytes) &&
            FileStamp.FromBytes(bytes.Span) == accepted);
    }
}
