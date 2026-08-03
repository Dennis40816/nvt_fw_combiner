using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MergePresentationViewModel
{
    internal AuthoringRevision StandardMergeAuthoringRevision =>
        _authoringSessions.StandardMerge.CurrentSnapshot?.AuthoringRevision ?? new AuthoringRevision(1);

    internal IReadOnlyDictionary<string, AuthoringSlotInspectionLease>
        BeginStandardMergeSlotInspections(IEnumerable<FirmwareSlotViewModel> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        if (!IsNormalMergeModeSelected)
        {
            return new Dictionary<string, AuthoringSlotInspectionLease>(StringComparer.Ordinal);
        }

        WorkbenchStandardMergeAuthoringSnapshot projection =
            ResolveStandardMergeAuthoringSnapshot();
        AuthoringSessionTransitionResult activated =
            _authoringSessions.StandardMerge.Activate(projection.Catalog);
        ApplyStandardMergeReadiness(projection);
        SyncStandardMergeMembership(activated.Snapshot);
        if (!activated.Succeeded)
        {
            return new Dictionary<string, AuthoringSlotInspectionLease>(StringComparer.Ordinal);
        }

        var members = activated.Snapshot!.Slots
            .Select(static slot => slot.DefinitionId)
            .ToHashSet(StringComparer.Ordinal);
        var selections = slots
            .Where(slot =>
                slot.AddressSpaceId is not null &&
                slot.FilePath is not null &&
                members.Contains(slot.AddressSpaceId))
            .ToDictionary(
                static slot => slot.AddressSpaceId!,
                static slot => slot.FilePath!,
                StringComparer.Ordinal);
        if (selections.Count == 0)
        {
            return new Dictionary<string, AuthoringSlotInspectionLease>(StringComparer.Ordinal);
        }

        AuthoringSlotInspectionBatchStartResult started =
            _authoringSessions.StandardMerge.BeginSlotFileInspections(selections);
        return started.Succeeded
            ? started.Leases.ToDictionary(
                static lease => lease.DefinitionId,
                StringComparer.Ordinal)
            : new Dictionary<string, AuthoringSlotInspectionLease>(StringComparer.Ordinal);
    }

    internal bool TryCompleteStandardMergeInputBatch(
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        IReadOnlyDictionary<string, WorkbenchFirmwareInspection> inspections)
    {
        FirmwareInspectionItemRequest[] selected =
        [
            .. items.Where(static item => item.StandardMergeAddressSpaceId is not null),
        ];
        if (selected.Length == 0)
        {
            return true;
        }

        WorkbenchFirmwareInspection[] results =
        [
            .. selected.Select(item => inspections[item.SlotId]),
        ];
        AuthoringCapabilityCatalogSnapshot? catalog = results[0].InputSlotCatalog;
        if (catalog is null ||
            results.Any(result => result.InputSlotCatalog is null || result.InputSlotStatus is null) ||
            selected.Any(static item => item.StandardMergeInspectionLease is null))
        {
            return false;
        }

        AuthoringSessionTransitionResult completed =
            _authoringSessions.StandardMerge.TryCompleteSlotFileInspectionBatch(
                catalog,
                [
                    .. selected.Select(static item => item.StandardMergeInspectionLease!),
                ],
                results.ToDictionary(
                    static result => result.InputSlotStatus!.SlotId,
                    static result => result.InputSlotStatus!,
                    StringComparer.Ordinal));
        if (!completed.Succeeded)
        {
            return false;
        }

        SyncStandardMergeMembership(completed.Snapshot);
        return true;
    }

    internal void ClearStandardMergeAuthoringSelections()
    {
        ActiveSessionSnapshot? snapshot = _authoringSessions.StandardMerge.CurrentSnapshot;
        if (snapshot is null)
        {
            return;
        }

        foreach (AuthoringSlotState slot in snapshot.Slots.Where(static slot => slot.SelectedPath is not null))
        {
            _ = _authoringSessions.StandardMerge.SetSlotFile(slot.DefinitionId, null, null);
        }
    }

    internal void RefreshStandardMergeAuthoringState()
    {
        if (!IsNormalMergeModeSelected)
        {
            return;
        }

        WorkbenchStandardMergeAuthoringSnapshot projection =
            ResolveStandardMergeAuthoringSnapshot();
        AuthoringSessionTransitionResult activated =
            _authoringSessions.StandardMerge.Activate(projection.Catalog);
        ApplyStandardMergeReadiness(projection);
        SyncStandardMergeMembership(activated.Snapshot);
    }

    internal IEnumerable<FirmwareSlotViewModel> CurrentStandardMergeInspectionSlots()
    {
        return StandardMergeSlots.Where(slot =>
            slot.HasFile &&
            slot.AddressSpaceId is not null);
    }

    private WorkbenchStandardMergeAuthoringSnapshot ResolveStandardMergeAuthoringSnapshot()
    {
        string[] selectedSlotIds =
        [
            .. StandardMergeSlots
                .Where(static slot => slot.HasFile)
                .Select(static slot => slot.AddressSpaceId)
                .Where(static slotId => slotId is not null)
                .Select(static slotId => slotId!),
        ];
        ActiveSessionSnapshot? current = _authoringSessions.StandardMerge.CurrentSnapshot;
        Dictionary<string, FileStamp> accepted = current?.Slots
            .Where(slot =>
                slot.FileStamp is not null &&
                StandardMergeSlots.Any(candidate =>
                    StringComparer.Ordinal.Equals(candidate.AddressSpaceId, slot.DefinitionId) &&
                    StringComparer.Ordinal.Equals(candidate.FilePath, slot.SelectedPath)))
            .ToDictionary(
                static slot => slot.DefinitionId,
                static slot => slot.FileStamp!.Value,
                StringComparer.Ordinal) ??
            new Dictionary<string, FileStamp>(StringComparer.Ordinal);
        return WorkbenchCompositionService.GetStandardMergeAuthoringSnapshot(
            SelectedIc,
            selectedSlotIds,
            accepted,
            StandardMergeAuthoringRevision);
    }

    private void ApplyStandardMergeReadiness(WorkbenchStandardMergeAuthoringSnapshot projection)
    {
        foreach (FirmwareSlotViewModel slot in StandardMergeSlots)
        {
            InputSelectionMemberReadiness? member = projection.Slots.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.SlotId, slot.AddressSpaceId));
            if (member is null)
            {
                slot.ClearSelectionReadiness();
                continue;
            }

            string label = Text.GetDpInputSelectionReadinessLabel(member.Readiness);
            string detail = Text.GetStandardMergeInputSelectionReadinessDetail(member);
            slot.SetSelectionReadiness(
                member.Readiness,
                label,
                detail,
                Text.GetInputSelectionReadinessAutomationText(label, detail),
                member.CanSelect);
        }
    }

    private void SyncStandardMergeMembership(ActiveSessionSnapshot? snapshot)
    {
        var members = (snapshot?.Slots ?? [])
            .Select(static slot => slot.DefinitionId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (FirmwareSlotViewModel slot in StandardMergeSlots.Where(slot =>
                     slot.AddressSpaceId is not null && !members.Contains(slot.AddressSpaceId)))
        {
            slot.FilePath = null;
            slot.SetFirmwareFacts([]);
            slot.ClearInputInspection();
        }
    }
}
