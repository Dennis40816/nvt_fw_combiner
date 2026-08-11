using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

internal sealed partial class CtrlRamAuthoringExperience
{
    /// <summary>
    /// Compiles an owner-confirmed CtrlRAM firmware-version edit as a new
    /// authoring revision and re-inspects the unchanged accepted inputs.
    /// </summary>
    public CtrlRamAuthoringTransitionResult
        TransitionFirmwareVersionCompilation(
            AuthoringSessionState session,
            string icId,
            string number,
            IReadOnlyDictionary<string, string> slotPaths,
            CtrlRamFirmwareVersionDraftState? firmwareVersionEdit)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentNullException.ThrowIfNull(slotPaths);

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
                IcIdentifier.Normalize(icId)) ||
            !current.HasCurrentInputInspection)
        {
            return Failed([new CompositionIssue(
                AuthoringSessionIssueCodes.StaleInspection,
                "CtrlRAM requires one exact current accepted authoring compilation.")]);
        }
        IReadOnlyDictionary<string, byte[]> acceptedInputBytes = GetAcceptedInputBytes(current);
        bool acceptedCapability = _adapter.IsAcceptedCapability(
            icId,
            number,
            slotPaths,
            firmwareVersionEdit,
            acceptedInputBytes,
            accepted,
            out IReadOnlyDictionary<string, string> expectedPaths,
            out IReadOnlyList<CompositionIssue> validationIssues);
        if (validationIssues.Count != 0)
        {
            return Failed(validationIssues);
        }
        if (!acceptedCapability || !HasExpectedPaths(current, expectedPaths))
        {
            return Failed([new CompositionIssue(
                AuthoringSessionIssueCodes.StaleInspection,
                "The CtrlRAM inputs no longer match the accepted inspection.")]);
        }

        CtrlRamFirmwareVersionDraftState? desiredDraft = firmwareVersionEdit;
        if (HasSameCtrlRamVersionDraft(current.DraftState, desiredDraft))
        {
            return new CtrlRamAuthoringTransitionResult(current, []);
        }

        var inputBytes = new Dictionary<string, ReadOnlyMemory<byte>?>(StringComparer.Ordinal);
        var selectedPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (AuthoringInputSlotStatus status in current.InputSlotStatuses)
        {
            inputBytes[status.AddressSpaceId] = status.AcceptedByteArray;
            selectedPaths[status.SlotId] = status.SelectedPathHint ??
                throw new InvalidOperationException(
                    $"CtrlRAM input '{status.SlotId}' omitted its accepted path identity.");
        }

        CtrlRamAuthoringCompilation compilation = _adapter.Resolve(
            icId,
            number,
            slotPaths,
            desiredDraft,
            acceptedInputBytes);
        if (compilation.Capability is not { } transitioned)
        {
            return Failed(compilation.Issues);
        }
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
                ? new CtrlRamAuthoringTransitionResult(
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

    private static CtrlRamAuthoringTransitionResult Failed(
        AuthoringSessionIssue issue)
    {
        return Failed([new CompositionIssue(
            issue.Code,
            issue.Message,
            issue.Subject)]);
    }

    private static CtrlRamAuthoringTransitionResult Failed(
        IReadOnlyList<CompositionIssue> issues)
    {
        return new CtrlRamAuthoringTransitionResult(null, issues);
    }
}
