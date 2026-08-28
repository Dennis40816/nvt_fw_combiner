using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MergePresentationViewModel
{
    internal AuthoringRevision StandardMergeAuthoringRevision =>
        _standardMergeSession.CurrentSnapshot?.AuthoringRevision ?? new AuthoringRevision(1);

    internal void InvalidateCanonicalCatalogSessions()
    {
        _standardMergeSession.InvalidateCanonicalPublication();
        _abMergeSession.InvalidateCanonicalPublication();
        _generalMergeSession.InvalidateCanonicalPublication();
        ClearAbMergeActionReadiness();
        _generalMergeAdmission = null;
        _generalMergeActionReadiness = null;
        InspectionLifecycles[NormalMergeMode].Invalidate();
        InspectionLifecycles[AbCodeMergeMode].Invalidate();
        InspectionLifecycles[GeneralMergeMode].Invalidate();
    }

    internal IReadOnlyDictionary<string, AuthoringSlotInspectionLease>
        BeginStandardMergeSlotInspections(IEnumerable<FirmwareSlotViewModel> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        if (!IsNormalMergeModeSelected)
        {
            return new Dictionary<string, AuthoringSlotInspectionLease>(StringComparer.Ordinal);
        }

        CompiledAuthoringSelectionSnapshot projection =
            ResolveStandardMergeAuthoringSnapshot();
        AuthoringSessionTransitionResult activated =
            _standardMergeSession.Activate(projection);
        ApplyStandardMergeReadiness(projection);
        SyncStandardMergeMembership(activated.Snapshot);
        return !activated.Succeeded
            ? EmptyInspectionLeases()
            : BeginInputInspections(
                _standardMergeSession,
                activated.Snapshot!,
                slots,
                static slot => slot.AddressSpaceId);
    }

    internal bool TryCompleteStandardMergeInputBatch(
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        IReadOnlyDictionary<string, FirmwareInspectionSnapshot> inspections)
    {
        FirmwareInspectionItemRequest[] selected =
        [
            .. items.Where(static item => item.StandardMergeAddressSpaceId is not null),
        ];
        bool completed = TryCompleteInputBatch(
            _standardMergeSession,
            selected,
            inspections,
            static item => item.InspectionLease,
            out ActiveSessionSnapshot? snapshot);
        if (completed && selected.Length > 0)
        {
            SyncStandardMergeMembership(snapshot);
        }
        return completed;
    }

    internal void ClearStandardMergeAuthoringSelections()
    {
        ActiveSessionSnapshot? snapshot = _standardMergeSession.CurrentSnapshot;
        if (snapshot is null)
        {
            return;
        }

        foreach (AuthoringSlotState slot in snapshot.Slots.Where(static slot => slot.SelectedPath is not null))
        {
            _ = _standardMergeSession.SetSlotFile(slot.DefinitionId, null, null);
        }
    }

    internal void RefreshStandardMergeAuthoringState()
    {
        if (!IsNormalMergeModeSelected || !HasSelectedIc)
        {
            if (!HasSelectedIc)
            {
                foreach (FirmwareSlotViewModel slot in StandardMergeSlots)
                {
                    slot.ClearSelectionReadiness();
                }
            }
            return;
        }

        CompiledAuthoringSelectionSnapshot projection =
            ResolveStandardMergeAuthoringSnapshot();
        AuthoringSessionTransitionResult activated =
            _standardMergeSession.Activate(projection);
        ApplyStandardMergeReadiness(projection);
        SyncStandardMergeMembership(activated.Snapshot);
    }

    internal void RelocalizeStandardMergeReadiness()
    {
        if (IsNormalMergeModeSelected && _standardMergeSession.CurrentSnapshot is { } snapshot)
        {
            ApplyInputReadiness(
                StandardMergeSlots,
                snapshot.InputSelectionReadiness,
                static slot => slot.AddressSpaceId);
        }
    }

    private CompiledAuthoringSelectionSnapshot ResolveStandardMergeAuthoringSnapshot()
    {
        return ResolveStandardMergeAuthoringSnapshot(SelectedIc);
    }

    private CompiledAuthoringSelectionSnapshot ResolveStandardMergeAuthoringSnapshot(
        string icId)
    {
        if (string.Equals(_preparedStandardMergeIc, icId, StringComparison.Ordinal) &&
            _preparedStandardMergeSnapshot is not null)
        {
            CompiledAuthoringSelectionSnapshot prepared = _preparedStandardMergeSnapshot;
            _preparedStandardMergeIc = null;
            _preparedStandardMergeSnapshot = null;
            return prepared;
        }

        return ResolveStandardMergeAuthoringSnapshotCore(icId);
    }

    private CompiledAuthoringSelectionSnapshot ResolveStandardMergeAuthoringSnapshotCore(
        string icId)
    {
        string[] selectedSlotIds =
        [
            .. StandardMergeSlots
                .Where(static slot => slot.HasFile)
                .Select(static slot => slot.AddressSpaceId)
                .Where(static slotId => slotId is not null)
                .Select(static slotId => slotId!),
        ];
        Dictionary<string, FileStamp> accepted = AcceptedInputStamps(
            _standardMergeSession,
            StandardMergeSlots,
            static slot => slot.AddressSpaceId);
        return _compositionServices.StandardMergeAuthoring.GetAuthoringSnapshot(
            icId,
            selectedSlotIds,
            accepted,
            StandardMergeAuthoringRevision,
            _standardMergeSession.CurrentSnapshot);
    }

    private void ApplyStandardMergeReadiness(CompiledAuthoringSelectionSnapshot projection)
    {
        ApplyInputReadiness(StandardMergeSlots, projection.Slots, static slot => slot.AddressSpaceId);
    }

    private void SyncStandardMergeMembership(ActiveSessionSnapshot? snapshot)
    {
        SyncInputMembership(snapshot, StandardMergeSlots, static slot => slot.AddressSpaceId);
    }

    private static Dictionary<string, AuthoringSlotInspectionLease> BeginInputInspections(
        AuthoringSessionState session,
        ActiveSessionSnapshot snapshot,
        IEnumerable<FirmwareSlotViewModel> slots,
        Func<FirmwareSlotViewModel, string?> inputId)
    {
        var members = snapshot.Slots.Select(static slot => slot.DefinitionId)
            .ToHashSet(StringComparer.Ordinal);
        var selections = slots
            .Where(slot => slot.FilePath is not null && inputId(slot) is { } id && members.Contains(id))
            .ToDictionary(slot => inputId(slot)!, static slot => slot.FilePath!, StringComparer.Ordinal);
        if (selections.Count == 0)
        {
            return EmptyInspectionLeases();
        }

        AuthoringSlotInspectionBatchStartResult started = session.BeginSlotFileInspections(selections);
        return started.Succeeded
            ? started.Leases.ToDictionary(static lease => lease.DefinitionId, StringComparer.Ordinal)
            : EmptyInspectionLeases();
    }

    private static bool TryCompleteInputBatch(
        AuthoringSessionState session,
        FirmwareInspectionItemRequest[] selected,
        IReadOnlyDictionary<string, FirmwareInspectionSnapshot> inspections,
        Func<FirmwareInspectionItemRequest, AuthoringSlotInspectionLease?> selectLease,
        out ActiveSessionSnapshot? snapshot)
    {
        snapshot = session.CurrentSnapshot;
        if (selected.Length == 0)
        {
            return true;
        }

        FirmwareInspectionSnapshot[] results = [.. selected.Select(item => inspections[item.SlotId])];
        AuthoringCapabilityCatalogSnapshot? catalog = results[0].InputSlotCatalog;
        AuthoringSlotInspectionLease?[] leases = [.. selected.Select(selectLease)];
        if (catalog is null || leases.Any(static lease => lease is null) || results.Any(
                static result => result.InputSlotCatalog is null || result.InputSlotStatus is null))
        {
            return false;
        }

        AuthoringSessionTransitionResult completed = session.TryCompleteSlotFileInspectionBatch(
            catalog,
            [.. leases.Select(static lease => lease!)],
            results.ToDictionary(static result => result.InputSlotStatus!.SlotId,
                static result => result.InputSlotStatus!, StringComparer.Ordinal));
        snapshot = completed.Snapshot;
        return completed.Succeeded;
    }

    private static Dictionary<string, FileStamp> AcceptedInputStamps(
        AuthoringSessionState session,
        IEnumerable<FirmwareSlotViewModel> slots,
        Func<FirmwareSlotViewModel, string?> inputId)
    {
        FirmwareSlotViewModel[] candidates = [.. slots];
        return session.CurrentSnapshot?.Slots.Where(slot => slot.FileStamp is not null &&
                candidates.Any(candidate => StringComparer.Ordinal.Equals(inputId(candidate), slot.DefinitionId) &&
                    StringComparer.Ordinal.Equals(candidate.FilePath, slot.SelectedPath)))
            .ToDictionary(static slot => slot.DefinitionId, static slot => slot.FileStamp!.Value,
                StringComparer.Ordinal) ?? [];
    }

    private void ApplyInputReadiness(
        IEnumerable<FirmwareSlotViewModel> slots,
        IReadOnlyList<InputSelectionMemberReadiness> readiness,
        Func<FirmwareSlotViewModel, string?> inputId)
    {
        foreach (FirmwareSlotViewModel slot in slots)
        {
            InputSelectionMemberReadiness? member = readiness.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.SlotId, inputId(slot)));
            if (member is null)
            {
                slot.ClearSelectionReadiness();
                continue;
            }

            string label = Text.GetDpInputSelectionReadinessLabel(member);
            string detail = Text.GetStandardMergeInputSelectionReadinessDetail(member);
            slot.SetSelectionReadiness(member.Readiness, label, detail,
                Text.GetInputSelectionReadinessAutomationText(label, detail), member.CanSelect);
        }
    }

    private static void SyncInputMembership(
        ActiveSessionSnapshot? snapshot,
        IEnumerable<FirmwareSlotViewModel> slots,
        Func<FirmwareSlotViewModel, string?> inputId)
    {
        var members = (snapshot?.Slots ?? []).Select(static slot => slot.DefinitionId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (FirmwareSlotViewModel slot in slots.Where(slot =>
                     inputId(slot) is { } id && !members.Contains(id)))
        {
            slot.FilePath = null;
            slot.SetFirmwareFacts([]);
            slot.ClearInputInspection();
        }
    }

    private static Dictionary<string, AuthoringSlotInspectionLease> EmptyInspectionLeases()
    {
        return new Dictionary<string, AuthoringSlotInspectionLease>(StringComparer.Ordinal);
    }
}
