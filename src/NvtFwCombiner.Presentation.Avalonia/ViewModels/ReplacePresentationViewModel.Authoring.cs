using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReplacePresentationViewModel
{
    private CompiledAuthoringSelectionSnapshot? _dpReplaceSelection;
    private CapabilityActionReadinessSnapshot? _ctrlRamActionReadiness;
    private ActiveSessionSnapshot? _ctrlRamReadinessSession;
    private string? _ctrlRamReadinessIc;
    private string? _ctrlRamReadinessNumber;

    internal AuthoringRevision ReplaceInputAuthoringRevision =>
        CurrentReplaceInputSession?.CurrentSnapshot?.AuthoringRevision ?? new AuthoringRevision(1);

    internal void InvalidateCanonicalCatalogSessions()
    {
        _authoringSessions.DpReplace.InvalidateCanonicalPublication();
        _authoringSessions.CtrlRamReplace.InvalidateCanonicalPublication();
        _dpReplaceSelection = null;
        ClearCtrlRamActionReadiness();
    }

    private AuthoringSessionState? CurrentReplaceInputSession => SelectedReplaceMode switch
    {
        DpReplaceMode => _authoringSessions.DpReplace,
        CtrlRamReplaceMode => _authoringSessions.CtrlRamReplace,
        _ => null,
    };

    internal IReadOnlyList<FirmwareInspectionItemRequest> AttachReplaceInspectionLeases(
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        IEnumerable<FirmwareSlotViewModel> slots)
    {
        AuthoringSessionState? session = CurrentReplaceInputSession;
        if (session is null)
        {
            return items;
        }

        var requested = items.Where(static item => item.DpReplaceAddressSpaceId is not null ||
                item.CtrlRamReplaceAddressSpaceId is not null)
            .Select(ReplaceInspectionInputId)
            .ToHashSet(StringComparer.Ordinal);
        if (requested.Count == 0)
        {
            return items;
        }
        FirmwareSlotViewModel[] selected =
        [
            .. slots.Where(slot => slot.HasFile && requested.Contains(ReplaceInputId(slot))),
        ];
        if (SelectedReplaceMode == CtrlRamReplaceMode)
        {
            ClearCtrlRamActionReadiness();
        }
        AuthoringCapabilityCatalogSnapshot? catalog = SelectedReplaceMode == DpReplaceMode
            ? ResolveDpReplaceAuthoringSnapshot(selected).Catalog
            : WorkbenchCompositionService.GetCtrlRamReplaceAuthoringCatalog(
                SelectedIc,
                SelectedNumber,
                selected.ToDictionary(
                    slot => ReplaceInputId(slot) == WorkbenchAddressSpaceIds.ReferenceBase
                        ? WorkbenchSlotIds.ReplaceBase
                        : ReplaceInputId(slot),
                    static slot => slot.FilePath!,
                    StringComparer.Ordinal),
                _authoringSessions.CtrlRamReplace.CurrentSnapshot);
        if (catalog is null)
        {
            return items;
        }
        if (!session.Activate(catalog).Succeeded)
        {
            return items;
        }

        AuthoringSlotInspectionBatchStartResult started =
            session.BeginSlotFileInspections(
                selected.ToDictionary(ReplaceInputId, static slot => slot.FilePath!, StringComparer.Ordinal));
        if (!started.Succeeded)
        {
            return items;
        }

        var leases = started.Leases.ToDictionary(
            static lease => lease.DefinitionId,
            StringComparer.Ordinal);
        return
        [
            .. items.Select(item => (item.DpReplaceAddressSpaceId is not null ||
                item.CtrlRamReplaceAddressSpaceId is not null) &&
                leases.TryGetValue(ReplaceInspectionInputId(item), out AuthoringSlotInspectionLease? lease)
                    ? item with { ReplaceInspectionLease = lease }
                    : item),
        ];
    }

    internal bool TryCompleteReplaceInputBatch(
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        IReadOnlyDictionary<string, WorkbenchFirmwareInspection> inspections)
    {
        FirmwareInspectionItemRequest[] selected =
        [
            .. items.Where(static item => item.DpReplaceAddressSpaceId is not null ||
                item.CtrlRamReplaceAddressSpaceId is not null),
        ];
        if (selected.Length == 0)
        {
            return true;
        }

        WorkbenchFirmwareInspection[] results = [.. selected.Select(item => inspections[item.SlotId])];
        AuthoringCapabilityCatalogSnapshot? catalog = results[0].InputSlotCatalog;
        AuthoringSessionState? session = CurrentReplaceInputSession;
        if (catalog is null || session is null || results.Any(static result =>
                result.InputSlotCatalog is null || result.InputSlotStatus is null) ||
            selected.Any(static item => item.ReplaceInspectionLease is null))
        {
            return false;
        }
        AuthoringSessionTransitionResult completed = session.TryCompleteSlotFileInspectionBatch(
            catalog,
            [.. selected.Select(static item => item.ReplaceInspectionLease!)],
            results.ToDictionary(
                static result => result.InputSlotStatus!.SlotId,
                static result => result.InputSlotStatus!,
                StringComparer.Ordinal));
        return completed.Succeeded;
    }

    private bool CanRunCompiledReplaceSession(AuthoringSessionState session)
    {
        ActiveSessionSnapshot? snapshot = session.CurrentSnapshot;
        return snapshot?.HasCurrentInputInspection == true &&
            StringComparer.Ordinal.Equals(snapshot.SelectedIc, SelectedIc) &&
            snapshot.Slots.All(slot => CurrentReplaceInputSlots().Any(candidate =>
                StringComparer.Ordinal.Equals(ReplaceInputId(candidate), slot.DefinitionId) &&
                StringComparer.Ordinal.Equals(candidate.FilePath, slot.SelectedPath)));
    }

    private bool HasCurrentCtrlRamActionReadiness(bool build)
    {
        ActiveSessionSnapshot? current = _authoringSessions.CtrlRamReplace.CurrentSnapshot;
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
        ActiveSessionSnapshot? session = _authoringSessions.CtrlRamReplace.CurrentSnapshot;
        if (!IsCtrlRamReplaceModeSelected || session is null)
        {
            RefreshCommandState();
            return;
        }

        string icId = SelectedIc;
        string number = SelectedNumber;
        CapabilityActionReadinessSnapshot? readiness =
            await WorkbenchCompositionService.GetCtrlRamReplaceActionReadinessAsync(
                icId,
                number,
                CreateReplaceSlotPaths(),
                session,
                cancellationToken);
        if (readiness is not null &&
            ReferenceEquals(session, _authoringSessions.CtrlRamReplace.CurrentSnapshot) &&
            StringComparer.Ordinal.Equals(icId, SelectedIc) &&
            StringComparer.Ordinal.Equals(number, SelectedNumber))
        {
            _ctrlRamActionReadiness = readiness;
            _ctrlRamReadinessSession = session;
            _ctrlRamReadinessIc = icId;
            _ctrlRamReadinessNumber = number;
        }
        RefreshCommandState();
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
        return ReplaceSlots.Concat([ReplaceBaseSlot]).Where(static slot => slot.HasFile);
    }

    private CompiledAuthoringSelectionSnapshot ResolveDpReplaceAuthoringSnapshot(
        IReadOnlyCollection<FirmwareSlotViewModel> selected)
    {
        ActiveSessionSnapshot? current = _authoringSessions.DpReplace.CurrentSnapshot;
        Dictionary<string, FileStamp> accepted = current?.Slots.Where(slot =>
                slot.FileStamp is not null && selected.Any(candidate =>
                    StringComparer.Ordinal.Equals(ReplaceInputId(candidate), slot.DefinitionId) &&
                    StringComparer.Ordinal.Equals(candidate.FilePath, slot.SelectedPath)))
            .ToDictionary(static slot => slot.DefinitionId, static slot => slot.FileStamp!.Value,
                StringComparer.Ordinal) ?? [];
        return _dpReplaceSelection = WorkbenchCompositionService.GetDpReplaceAuthoringSnapshot(
            SelectedIc,
            [.. selected.Select(ReplaceInputId)],
            accepted,
            current?.AuthoringRevision ?? new AuthoringRevision(1),
            current);
    }

    private static string ReplaceInputId(FirmwareSlotViewModel slot)
    {
        return slot.SlotId == WorkbenchSlotIds.ReplaceBase
            ? WorkbenchAddressSpaceIds.ReferenceBase
            : slot.AddressSpaceId!;
    }

    private static string ReplaceInspectionInputId(FirmwareInspectionItemRequest item)
    {
        return item.DpReplaceAddressSpaceId ?? item.CtrlRamReplaceAddressSpaceId!;
    }

    private bool IsCurrentDpReplaceSelection(
        CompiledAuthoringSelectionSnapshot snapshot,
        IReadOnlyCollection<FirmwareSlotViewModel> selected)
    {
        return snapshot.Catalog.Routes.All(route => StringComparer.Ordinal.Equals(
                route.Identity.IcId, SelectedIc)) &&
            StringComparer.Ordinal.Equals(
                snapshot.Catalog.Routes.Single().CompilationFingerprint,
                _authoringSessions.DpReplace.CurrentSnapshot?.CompilationFingerprint) &&
            snapshot.Slots.Where(static slot => slot.IsSelected).Select(static slot => slot.SlotId)
                .ToHashSet(StringComparer.Ordinal).SetEquals(selected.Select(ReplaceInputId));
    }
}
