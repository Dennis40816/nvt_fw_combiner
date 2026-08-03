using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MergePresentationViewModel
{
    internal AuthoringRevision AbMergeAuthoringRevision =>
        _authoringSessions.AbMerge.CurrentSnapshot?.AuthoringRevision ?? new AuthoringRevision(1);

    internal IReadOnlyDictionary<string, AuthoringSlotInspectionLease>
        BeginAbMergeSlotInspections(IEnumerable<FirmwareSlotViewModel> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        if (!IsAbCodeMergeModeSelected)
        {
            return new Dictionary<string, AuthoringSlotInspectionLease>(StringComparer.Ordinal);
        }

        WorkbenchAbMergeAuthoringSnapshot projection = ResolveAbMergeAuthoringSnapshot();
        AuthoringSessionTransitionResult activated =
            _authoringSessions.AbMerge.Activate(projection.Catalog);
        ApplyAbMergeReadiness(projection);
        SyncAbMergeMembership(activated.Snapshot);
        if (!activated.Succeeded)
        {
            return new Dictionary<string, AuthoringSlotInspectionLease>(StringComparer.Ordinal);
        }

        var members = activated.Snapshot!.Slots
            .Select(static slot => slot.DefinitionId)
            .ToHashSet(StringComparer.Ordinal);
        var selections = slots
            .Where(slot =>
                slot.FilePath is not null &&
                members.Contains(slot.SlotId))
            .ToDictionary(
                static slot => slot.SlotId,
                static slot => slot.FilePath!,
                StringComparer.Ordinal);
        AuthoringSlotInspectionBatchStartResult started =
            _authoringSessions.AbMerge.BeginSlotFileInspections(selections);
        return started.Succeeded
            ? started.Leases.ToDictionary(
                static lease => lease.DefinitionId,
                StringComparer.Ordinal)
            : new Dictionary<string, AuthoringSlotInspectionLease>(StringComparer.Ordinal);
    }

    internal bool TryCompleteAbMergeInputBatch(
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        IReadOnlyDictionary<string, WorkbenchFirmwareInspection> inspections)
    {
        FirmwareInspectionItemRequest[] selected =
        [
            .. items.Where(static item => item.AbMergeAddressSpaceId is not null),
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
            selected.Any(static item => item.AbMergeInspectionLease is null))
        {
            return false;
        }

        AuthoringSessionTransitionResult completed =
            _authoringSessions.AbMerge.TryCompleteSlotFileInspectionBatch(
                catalog,
                [
                    .. selected.Select(static item => item.AbMergeInspectionLease!),
                ],
                results.ToDictionary(
                    static result => result.InputSlotStatus!.SlotId,
                    static result => result.InputSlotStatus!,
                    StringComparer.Ordinal));
        if (!completed.Succeeded)
        {
            return false;
        }
        SyncAbMergeMembership(completed.Snapshot);
        return true;
    }

    internal void RefreshAbMergeAuthoringState()
    {
        if (!IsAbCodeMergeModeSelected)
        {
            return;
        }

        WorkbenchAbMergeAuthoringSnapshot projection = ResolveAbMergeAuthoringSnapshot();
        AuthoringSessionTransitionResult activated =
            _authoringSessions.AbMerge.Activate(projection.Catalog);
        ApplyAbMergeReadiness(projection);
        SyncAbMergeMembership(activated.Snapshot);
    }

    private WorkbenchAbMergeAuthoringSnapshot ResolveAbMergeAuthoringSnapshot()
    {
        string[] selectedSlotIds =
        [
            .. AbMergeSlots
                .Where(static slot => slot.HasFile)
                .Select(static slot => slot.SlotId),
        ];
        ActiveSessionSnapshot? current = _authoringSessions.AbMerge.CurrentSnapshot;
        Dictionary<string, FileStamp> accepted = current?.Slots
            .Where(slot =>
                slot.FileStamp is not null &&
                AbMergeSlots.Any(candidate =>
                    StringComparer.Ordinal.Equals(candidate.SlotId, slot.DefinitionId) &&
                    StringComparer.Ordinal.Equals(candidate.FilePath, slot.SelectedPath)))
            .ToDictionary(
                static slot => slot.DefinitionId,
                static slot => slot.FileStamp!.Value,
                StringComparer.Ordinal) ??
            new Dictionary<string, FileStamp>(StringComparer.Ordinal);
        return WorkbenchCompositionService.GetAbMergeAuthoringSnapshot(
            SelectedIc,
            GetSelectedAbMergeTopologyToken(),
            selectedSlotIds,
            accepted,
            AbMergeAuthoringRevision);
    }

    private void ApplyAbMergeReadiness(WorkbenchAbMergeAuthoringSnapshot projection)
    {
        foreach (FirmwareSlotViewModel slot in AbMergeSlots)
        {
            InputSelectionMemberReadiness? member = projection.Slots.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.SlotId, slot.SlotId));
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

    private void SyncAbMergeMembership(ActiveSessionSnapshot? snapshot)
    {
        var members = (snapshot?.Slots ?? [])
            .Select(static slot => slot.DefinitionId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (FirmwareSlotViewModel slot in AbMergeSlots.Where(slot =>
                     !members.Contains(slot.SlotId)))
        {
            slot.FilePath = null;
            slot.SetFirmwareFacts([]);
            slot.ClearInputInspection();
        }
    }
}
