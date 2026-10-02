using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;

namespace NvtFwCombiner.Bootstrap.Tests;

internal sealed class CliChoiceRecordingCtrlRamAuthoring(ICtrlRamAuthoring inner) : ICtrlRamAuthoring
{
    internal List<CtrlRamAuthoringDraftState?> Drafts { get; } = [];

    public bool IsCurrentBaseInspection(CtrlRamBaseInspection inspection)
    {
        return inner.IsCurrentBaseInspection(inspection);
    }

    public CapabilityWorkflowReadiness GetAbReferenceReadiness(string icId, string number)
    {
        return inner.GetAbReferenceReadiness(icId, number);
    }

    public CtrlRamInspectionDisplay GetDiscoveryDisplay(string icId, string number)
    {
        return inner.GetDiscoveryDisplay(icId, number);
    }

    public CtrlRamInspectionDisplay GetDiscoveryDisplayFromAcceptedBase(string icId, string number,
        ReadOnlyMemory<byte> acceptedBaseBytes)
    {
        return inner.GetDiscoveryDisplayFromAcceptedBase(icId, number, acceptedBaseBytes);
    }

    public CtrlRamAuthoringSessionPreparation PrepareSession(AuthoringSessionState session, string icId,
        string number, IReadOnlyDictionary<string, string> slotPaths, IReadOnlyDictionary<string, byte[]> inputBytes,
        CtrlRamAuthoringDraftState? firmwareVersionEdit = null)
    {
        return inner.PrepareSession(session, icId, number, slotPaths, inputBytes, firmwareVersionEdit);
    }

    public AuthoringSessionTransitionResult AdoptInspectedBatch(AuthoringSessionState session,
        AuthoringCapabilityCatalogSnapshot catalog, IReadOnlyCollection<AuthoringInputSlotStatus> statuses,
        CtrlRamBaseInspection? baseInspection = null)
    {
        return inner.AdoptInspectedBatch(session, catalog, statuses, baseInspection);
    }

    public ValueTask<CapabilityActionReadinessSnapshot?> GetActionReadinessAsync(string icId, string number,
        IReadOnlyDictionary<string, string> slotPaths, ActiveSessionSnapshot acceptedSession,
        CancellationToken cancellationToken)
    {
        return inner.GetActionReadinessAsync(icId, number, slotPaths, acceptedSession, cancellationToken);
    }

    public CtrlRamAuthoringTransitionResult TransitionFirmwareVersionCompilation(AuthoringSessionState session,
        string icId, string number, IReadOnlyDictionary<string, string> slotPaths,
        CtrlRamAuthoringDraftState? firmwareVersionEdit)
    {
        Drafts.Add(firmwareVersionEdit);
        return inner.TransitionFirmwareVersionCompilation(session, icId, number, slotPaths, firmwareVersionEdit);
    }

    public CompiledInputVersionObservation? ProjectFirmwareVersionConfirmationLease(ActiveSessionSnapshot session)
    {
        return inner.ProjectFirmwareVersionConfirmationLease(session);
    }

    public bool IsFirmwareVersionConfirmationLeaseCurrent(ActiveSessionSnapshot current, ActiveSessionSnapshot lease)
    {
        return inner.IsFirmwareVersionConfirmationLeaseCurrent(current, lease);
    }
}
