using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReplacePresentationViewModel
{
    private CapabilityActionReadinessSnapshot? _ctrlRamActionReadiness;
    private ActiveSessionSnapshot? _ctrlRamReadinessSession;
    private string? _ctrlRamReadinessIc;
    private string? _ctrlRamReadinessNumber;

    internal AuthoringRevision ReplaceInputAuthoringRevision =>
        CurrentReplaceInputSession?.CurrentSnapshot?.AuthoringRevision ?? new AuthoringRevision(1);

    internal void InvalidateCanonicalCatalogSessions()
    {
        _ctrlRamReplaceSession.InvalidateCanonicalPublication();
        _generalReplaceSession.InvalidateCanonicalPublication();
        ClearCtrlRamActionReadiness();
        _generalReplaceAdmission = null;
        _generalReplaceActionReadiness = null;
        _generalReplaceDiagnosticPreviewReport = null;
        InspectionLifecycles[CtrlRamReplaceMode].Invalidate();
        InspectionLifecycles[GeneralReplaceMode].Invalidate();
    }

    private AuthoringSessionState? CurrentReplaceInputSession => SelectedReplaceMode switch
    {
        CtrlRamReplaceMode => _ctrlRamReplaceSession,
        _ => null,
    };

    internal void BeginReplaceInputInspection(IReadOnlyList<FirmwareInspectionItemRequest> items)
    {
        if (SelectedReplaceMode == CtrlRamReplaceMode &&
            items.Any(static item => item.CtrlRamReplaceAddressSpaceId is not null))
        {
            ClearCtrlRamActionReadiness();
        }
    }

    internal bool TryCompleteReplaceInputBatch(
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        IReadOnlyDictionary<string, FirmwareInspectionSnapshot> inspections)
    {
        FirmwareInspectionItemRequest[] selected =
        [
            .. items.Where(static item => item.CtrlRamReplaceAddressSpaceId is not null),
        ];
        if (selected.Length == 0)
        {
            return true;
        }

        FirmwareInspectionSnapshot[] results = [.. selected.Select(item => inspections[item.SlotId])];
        AuthoringCapabilityCatalogSnapshot? catalog = results[0].InputSlotCatalog;
        AuthoringSessionState? session = CurrentReplaceInputSession;
        return catalog is not null && session is not null && results.All(static result =>
                result.InputSlotCatalog is not null && result.InputSlotStatus is not null) &&
            _compositionServices.CtrlRamAuthoring.AdoptInspectedBatch(
            session,
            catalog,
            [.. results.Select(static result => result.InputSlotStatus!)]).Succeeded;
    }

    private bool CanRunCompiledReplaceSession(AuthoringSessionState session)
    {
        ActiveSessionSnapshot? snapshot = session.CurrentSnapshot;
        if (snapshot?.HasCurrentInputInspection != true ||
            !StringComparer.Ordinal.Equals(snapshot.SelectedIc, SelectedIc))
        {
            return false;
        }

        string[] selectedPaths =
        [
            .. CurrentReplaceInputSlots().Select(static slot => slot.FilePath!),
        ];
        return snapshot.Slots.Count == selectedPaths.Length &&
            snapshot.Slots.All(slot => selectedPaths.Contains(
                slot.SelectedPath,
                OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal));
    }

    private bool HasCurrentCtrlRamActionReadiness(bool build)
    {
        ActiveSessionSnapshot? current = _ctrlRamReplaceSession.CurrentSnapshot;
        CapabilityActionReadinessSnapshot? readiness = _ctrlRamActionReadiness;
        return readiness is not null && ReferenceEquals(current, _ctrlRamReadinessSession) &&
            StringComparer.Ordinal.Equals(SelectedIc, _ctrlRamReadinessIc) &&
            StringComparer.Ordinal.Equals(SelectedNumber, _ctrlRamReadinessNumber) &&
            StringComparer.Ordinal.Equals(
                readiness.CompilationFingerprint,
                current?.CompilationFingerprint) &&
            readiness.AuthoringRevision == current?.AuthoringRevision &&
            (build ? readiness.Build : readiness.Preview).IsAvailable;
    }

    internal async Task RefreshCtrlRamActionReadinessAsync(
        CancellationToken cancellationToken)
    {
        ClearCtrlRamActionReadiness();
        ActiveSessionSnapshot? session = _ctrlRamReplaceSession.CurrentSnapshot;
        if (!IsCtrlRamReplaceModeSelected || session is null)
        {
            PresentationObserver.Invoke(NotifyCommandAvailabilityChanged);
            return;
        }

        string icId = SelectedIc;
        string number = SelectedNumber;
        CapabilityActionReadinessSnapshot? readiness =
            await _compositionServices.CtrlRamAuthoring.GetActionReadinessAsync(
                icId,
                number,
                CreateReplaceSlotPaths(),
                session,
                cancellationToken);
        if (readiness is not null &&
            ReferenceEquals(session, _ctrlRamReplaceSession.CurrentSnapshot) &&
            StringComparer.Ordinal.Equals(icId, SelectedIc) &&
            StringComparer.Ordinal.Equals(number, SelectedNumber))
        {
            _ctrlRamActionReadiness = readiness;
            _ctrlRamReadinessSession = session;
            _ctrlRamReadinessIc = icId;
            _ctrlRamReadinessNumber = number;
        }
        PresentationObserver.Invoke(NotifyCommandAvailabilityChanged);
    }

    private void ClearCtrlRamActionReadiness()
    {
        _ctrlRamActionReadiness = null;
        _ctrlRamReadinessSession = null;
        _ctrlRamReadinessIc = null;
        _ctrlRamReadinessNumber = null;
    }

    private IEnumerable<FirmwareSlotViewModel> CurrentReplaceInputSlots()
    {
        return ReplaceSlots.Concat([ReplaceBaseSlot])
            .Where(static slot => slot.HasFile)
            .DistinctBy(ReplaceInputId);
    }

    private static string ReplaceInputId(FirmwareSlotViewModel slot)
    {
        return slot.SlotId == CompositionSlotIds.ReplaceBase
            ? CompositionAddressSpaceIds.ReferenceBase
            : slot.AddressSpaceId!;
    }

}
