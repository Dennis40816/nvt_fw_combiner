using NvtFwCombiner.Application.Authoring;
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
            return EmptyInspectionLeases();
        }

        WorkbenchAbMergeAuthoringSnapshot projection = ResolveAbMergeAuthoringSnapshot();
        AuthoringSessionTransitionResult activated =
            _authoringSessions.AbMerge.Activate(projection.Catalog);
        ApplyAbMergeReadiness(projection);
        SyncAbMergeMembership(activated.Snapshot);
        return !activated.Succeeded
            ? EmptyInspectionLeases()
            : BeginInputInspections(
                _authoringSessions.AbMerge,
                activated.Snapshot!,
                slots,
                static slot => slot.SlotId);
    }

    internal bool TryCompleteAbMergeInputBatch(
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        IReadOnlyDictionary<string, WorkbenchFirmwareInspection> inspections)
    {
        FirmwareInspectionItemRequest[] selected =
        [
            .. items.Where(static item => item.AbMergeAddressSpaceId is not null),
        ];
        bool completed = TryCompleteInputBatch(
            _authoringSessions.AbMerge,
            selected,
            inspections,
            static item => item.AbMergeInspectionLease,
            out ActiveSessionSnapshot? snapshot);
        if (completed && selected.Length > 0)
        {
            SyncAbMergeMembership(snapshot);
        }
        return completed;
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
        Dictionary<string, FileStamp> accepted = AcceptedInputStamps(
            _authoringSessions.AbMerge,
            AbMergeSlots,
            static slot => slot.SlotId);
        return WorkbenchCompositionService.GetAbMergeAuthoringSnapshot(
            SelectedIc,
            GetSelectedAbMergeTopologyToken(),
            selectedSlotIds,
            accepted,
            AbMergeAuthoringRevision,
            _authoringSessions.AbMerge.CurrentSnapshot);
    }

    private void ApplyAbMergeReadiness(WorkbenchAbMergeAuthoringSnapshot projection)
    {
        ApplyInputReadiness(AbMergeSlots, projection.Slots, static slot => slot.SlotId);
    }

    private void SyncAbMergeMembership(ActiveSessionSnapshot? snapshot)
    {
        SyncInputMembership(snapshot, AbMergeSlots, static slot => slot.SlotId);
    }
}
