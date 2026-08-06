using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Bridges canonical authoring sessions to focused host inspection and readiness adapters.</summary>
public static partial class CompositionAuthoringSessionAdapter
{
    /// <summary>
    /// Compiles an owner-confirmed CtrlRAM firmware-version edit as a new
    /// authoring revision and re-inspects the unchanged accepted inputs.
    /// </summary>
    public static WorkbenchCtrlRamAuthoringTransitionResult
        TransitionCtrlRamFirmwareVersionCompilation(
            AuthoringSessionState session,
            string icId,
            string number,
            IReadOnlyDictionary<string, string> slotPaths,
            WorkbenchCtrlRamFirmwareVersionEdit? firmwareVersionEdit)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentNullException.ThrowIfNull(slotPaths);

        CompositionPlanningAdapter.CtrlRamReplaceRunContext context =
            CompositionPlanningAdapter.CreateCtrlRamReplaceRunContext(
            icId,
            number,
            slotPaths,
            firmwareVersionEdit);
        if (context.ValidationIssues.Count != 0)
        {
            return Failed(context.ValidationIssues);
        }
        ActiveSessionSnapshot? current = session.CurrentSnapshot;
        ResolvedCapability? accepted = current?.GetAcceptedCapability(
            AuthoringDerivedResultKind.Inspection);
        if (current is null ||
            accepted is null ||
            !StringComparer.Ordinal.Equals(
                current.WorkflowId,
                ExperienceIds.CtrlRamReplace) ||
            !StringComparer.Ordinal.Equals(
                accepted.Identity.IcId,
                Profiles.IcIdentifier.Normalize(icId)) ||
            !current.HasCurrentInputInspection)
        {
            return Failed([new CompositionIssue(
                AuthoringSessionIssueCodes.StaleInspection,
                "CtrlRAM requires one exact current accepted authoring compilation.")]);
        }
        if (!IsAcceptedCtrlRamSession(context, slotPaths, current, accepted) ||
            !HasCurrentCtrlRamSessionFiles(context, current))
        {
            return Failed([new CompositionIssue(
                AuthoringSessionIssueCodes.StaleInspection,
                "The CtrlRAM inputs no longer match the accepted inspection.")]);
        }

        CtrlRamFirmwareVersionDraftState? desiredDraft = firmwareVersionEdit is null
            ? null
            : new CtrlRamFirmwareVersionDraftState(
                firmwareVersionEdit.FirmwareVersion,
                firmwareVersionEdit.FirmwareSubVersion);
        if (HasSameCtrlRamVersionDraft(current.DraftState, desiredDraft))
        {
            return new WorkbenchCtrlRamAuthoringTransitionResult(current, []);
        }

        InputArtifactBinding[] acceptedBindings = CompositionPlanningAdapter.CreateCtrlRamReplaceBindings(
            accepted.CompiledComposition,
            context,
            slotPaths,
            current);
        var inputBytes = new Dictionary<string, ReadOnlyMemory<byte>?>(StringComparer.Ordinal);
        var selectedPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (InputArtifactBinding binding in acceptedBindings)
        {
            byte[]? bytes = FirmwareInspectionAdapter.TryReadFirmwareImage(binding.ArtifactId);
            if (bytes is null ||
                binding.AcceptedContentStamp is not { } acceptedStamp ||
                FileStamp.FromBytes(bytes) != acceptedStamp)
            {
                return Failed([new CompositionIssue(
                    AuthoringSessionIssueCodes.StaleInspection,
                    $"CtrlRAM input '{binding.AddressSpaceId}' changed after inspection.",
                    binding.AddressSpaceId)]);
            }
            inputBytes[binding.AddressSpaceId] = bytes;
            selectedPaths[binding.AddressSpaceId] = binding.ArtifactId;
        }

        if (!CompositionPlanningAdapter.TryResolveCtrlRamCapability(
                context,
                out ResolvedCapability? transitionedCapability,
                out IReadOnlyList<CompositionIssue> compilationIssues))
        {
            return Failed(compilationIssues);
        }
        ResolvedCapability transitioned = transitionedCapability!;
        if (!Equals(accepted.Identity, transitioned.Identity) ||
            accepted.ResolutionToken != transitioned.ResolutionToken ||
            !StringComparer.Ordinal.Equals(
                accepted.CapabilityFingerprint,
                transitioned.CapabilityFingerprint))
        {
            return Failed([new CompositionIssue(
                AuthoringSessionIssueCodes.StaleInspection,
                "The CtrlRAM authoring transition resolved different capability authority.")]);
        }

        var catalog =
            AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(transitioned);
        AuthoringSessionTransitionResult activated = session.Activate(catalog);
        if (!activated.Succeeded)
        {
            return Failed(activated.Issue!);
        }
        AuthoringSessionTransitionResult drafted = session.SetDraft(desiredDraft);
        if (!drafted.Succeeded ||
            !ReferenceEquals(drafted.Snapshot!.ExactCapability, transitioned))
        {
            return Failed(drafted.Issue ?? new AuthoringSessionIssue(
                AuthoringSessionIssueCodes.InvalidPublication,
                "The CtrlRAM version draft could not bind to its exact compilation."));
        }

        AuthoringSlotInspectionBatchStartResult started =
            session.BeginSlotFileInspections(selectedPaths);
        if (!started.Succeeded)
        {
            return Failed(started.Issue!);
        }
        IReadOnlyDictionary<string, AuthoringInputSlotStatus> statuses =
            AuthoringInputSlotInspectionService.InspectBatch(
                transitioned,
                started.Snapshot!.AuthoringRevision,
                inputBytes,
                selectedPaths,
                selectedPaths.Keys);
        AuthoringSessionTransitionResult completed =
            session.TryCompleteSlotFileInspectionBatch(
                catalog,
                started.Leases,
                statuses);
        return completed.Succeeded &&
            completed.Snapshot!.GetAcceptedCapability(
                AuthoringDerivedResultKind.Inspection) is not null
                ? new WorkbenchCtrlRamAuthoringTransitionResult(
                    completed.Snapshot,
                    [])
                : Failed(completed.Issue ?? new AuthoringSessionIssue(
                    AuthoringSessionIssueCodes.InvalidPublication,
                    "The CtrlRAM version compilation did not accept every input inspection."));
    }

    internal static bool HasSameCtrlRamVersionDraft(
        AuthoringDraftState? current,
        CtrlRamFirmwareVersionDraftState? desired)
    {
        return current is null
            ? desired is null
            : current is CtrlRamFirmwareVersionDraftState accepted &&
                desired is not null &&
                accepted.FirmwareVersion == desired.FirmwareVersion &&
                accepted.FirmwareSubVersion == desired.FirmwareSubVersion;
    }

    private static WorkbenchCtrlRamAuthoringTransitionResult Failed(
        AuthoringSessionIssue issue)
    {
        return Failed([new CompositionIssue(
            issue.Code,
            issue.Message,
            issue.Subject)]);
    }

    private static WorkbenchCtrlRamAuthoringTransitionResult Failed(
        IReadOnlyList<CompositionIssue> issues)
    {
        return new WorkbenchCtrlRamAuthoringTransitionResult(null, issues);
    }
}
