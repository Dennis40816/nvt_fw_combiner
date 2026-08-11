using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReplacePresentationViewModel
{
    private CapabilityActionReadinessSnapshot? _ctrlRamActionReadiness;
    private ActiveSessionSnapshot? _ctrlRamReadinessSession;
    private string? _ctrlRamReadinessIc;
    private string? _ctrlRamReadinessNumber;

    internal AuthoringRevision ReplaceInputAuthoringRevision =>
        CurrentReplaceInputSession?.CurrentSnapshot?.AuthoringRevision ?? new AuthoringRevision(1);

    internal void InvalidateCanonicalCatalogSessions()
    {
        _dpReplaceSession.InvalidateCanonicalPublication();
        _ctrlRamReplaceSession.InvalidateCanonicalPublication();
        ClearCtrlRamActionReadiness();
    }

    private AuthoringSessionState? CurrentReplaceInputSession => SelectedReplaceMode switch
    {
        DpReplaceMode => _dpReplaceSession,
        CtrlRamReplaceMode => _ctrlRamReplaceSession,
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
        CompiledAuthoringSelectionSnapshot? dpProjection = SelectedReplaceMode == DpReplaceMode
            ? ResolveDpReplaceAuthoringSnapshot(selected)
            : null;
        AuthoringCapabilityCatalogSnapshot? catalog = dpProjection is not null
            ? dpProjection.Catalog
            : _compositionServices.CtrlRamAuthoring.GetAuthoringCatalog(
                SelectedIc,
                SelectedNumber,
                selected.ToDictionary(
                    slot => ReplaceInputId(slot) == CompositionAddressSpaceIds.ReferenceBase
                        ? CompositionSlotIds.ReplaceBase
                        : ReplaceInputId(slot),
                    static slot => slot.FilePath!,
                    StringComparer.Ordinal),
                _ctrlRamReplaceSession.CurrentSnapshot);
        if (catalog is null)
        {
            return items;
        }
        AuthoringSessionTransitionResult activated = dpProjection is null
            ? session.Activate(catalog)
            : session.Activate(dpProjection);
        if (!activated.Succeeded)
        {
            return items;
        }

        AuthoringSlotInspectionBatchStartResult started =
            session.BeginSlotFileInspections(
                selected.ToDictionary(
                    slot => ReplaceDefinitionId(slot, dpProjection),
                    static slot => slot.FilePath!,
                    StringComparer.Ordinal));
        if (!started.Succeeded)
        {
            return items;
        }

        var leases = started.Leases.ToDictionary(
            static lease => lease.DefinitionId,
            StringComparer.Ordinal);
        Dictionary<string, AuthoringSlotInspectionLease> leasesByInputId = selected.ToDictionary(
            ReplaceInputId,
            slot => leases[ReplaceDefinitionId(slot, dpProjection)],
            StringComparer.Ordinal);
        return
        [
            .. items.Select(item => (item.DpReplaceAddressSpaceId is not null ||
                item.CtrlRamReplaceAddressSpaceId is not null) &&
                leasesByInputId.TryGetValue(
                    ReplaceInspectionInputId(item),
                    out AuthoringSlotInspectionLease? lease)
                    ? item with { ReplaceInspectionLease = lease }
                    : item),
        ];
    }

    internal bool TryCompleteReplaceInputBatch(
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        IReadOnlyDictionary<string, FirmwareInspectionSnapshot> inspections)
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

        FirmwareInspectionSnapshot[] results = [.. selected.Select(item => inspections[item.SlotId])];
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
            RefreshCommandState();
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
        return ReplaceSlots.Concat([ReplaceBaseSlot])
            .Where(static slot => slot.HasFile)
            .DistinctBy(ReplaceInputId);
    }

    private CompiledAuthoringSelectionSnapshot ResolveDpReplaceAuthoringSnapshot(
        IReadOnlyCollection<FirmwareSlotViewModel> selected)
    {
        ActiveSessionSnapshot? current = _dpReplaceSession.CurrentSnapshot;
        Dictionary<string, FileStamp> accepted = current?.Slots.Where(slot =>
                slot.FileStamp is not null && selected.Any(candidate =>
                    StringComparer.Ordinal.Equals(candidate.FilePath, slot.SelectedPath)))
            .ToDictionary(static slot => slot.DefinitionId, static slot => slot.FileStamp!.Value,
                StringComparer.Ordinal) ?? [];
        return _compositionServices.DpReplaceAuthoring.GetAuthoringSnapshot(
            SelectedIc,
            [.. selected.Select(ReplaceInputId)],
            accepted,
            current?.AuthoringRevision ?? new AuthoringRevision(1),
            current);
    }

    private static string ReplaceInputId(FirmwareSlotViewModel slot)
    {
        return slot.SlotId == CompositionSlotIds.ReplaceBase
            ? CompositionAddressSpaceIds.ReferenceBase
            : slot.AddressSpaceId!;
    }

    private static string ReplaceInspectionInputId(FirmwareInspectionItemRequest item)
    {
        return item.DpReplaceAddressSpaceId ?? item.CtrlRamReplaceAddressSpaceId!;
    }

    private string ReplaceDefinitionId(
        FirmwareSlotViewModel slot,
        CompiledAuthoringSelectionSnapshot? dpProjection)
    {
        if (SelectedReplaceMode != DpReplaceMode)
        {
            return ReferenceEquals(slot, ReplaceBaseSlot)
                ? SelectedReplaceMode == CtrlRamReplaceMode
                    ? CompositionAddressSpaceIds.ReferenceBase
                    : CompositionSlotIds.ReplaceBase
                : ReplaceInputId(slot);
        }

        if (slot.CompiledSlotId is { } compiledSlotId)
        {
            return compiledSlotId;
        }

        string addressSpaceId = ReplaceInputId(slot);
        return dpProjection?.InputBindings.Single(binding => StringComparer.Ordinal.Equals(
                binding.AddressSpaceId,
                addressSpaceId)).SlotId ??
            throw new InvalidOperationException(
                $"DP Replace input '{addressSpaceId}' has no current compiled slot binding.");
    }

}
