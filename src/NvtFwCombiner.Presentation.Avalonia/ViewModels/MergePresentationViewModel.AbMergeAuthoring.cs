using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MergePresentationViewModel
{
    internal AuthoringRevision AbMergeAuthoringRevision =>
        _abMergeSession.CurrentSnapshot?.AuthoringRevision ?? new AuthoringRevision(1);

    internal IReadOnlyDictionary<string, AuthoringSlotInspectionLease>
        BeginAbMergeSlotInspections(IEnumerable<FirmwareSlotViewModel> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        if (!IsAbCodeMergeModeSelected)
        {
            return EmptyInspectionLeases();
        }

        CompiledAuthoringSelectionSnapshot projection = ResolveAbMergeAuthoringSnapshot();
        AuthoringSessionTransitionResult activated =
            _abMergeSession.Activate(projection.Catalog);
        ApplyAbMergeReadiness(projection);
        SyncAbMergeMembership(activated.Snapshot);
        return !activated.Succeeded
            ? EmptyInspectionLeases()
            : BeginInputInspections(
                _abMergeSession,
                activated.Snapshot!,
                slots,
                static slot => slot.SlotId);
    }

    internal bool TryCompleteAbMergeInputBatch(
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        IReadOnlyDictionary<string, FirmwareInspectionSnapshot> inspections)
    {
        FirmwareInspectionItemRequest[] selected =
        [
            .. items.Where(static item => item.AbMergeAddressSpaceId is not null),
        ];
        bool completed = TryCompleteInputBatch(
            _abMergeSession,
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

        RefreshAbMergeAuthoringState(ResolveAbMergeAuthoringSnapshot());
    }

    private void RefreshAbMergeAuthoringState(CompiledAuthoringSelectionSnapshot projection)
    {
        AuthoringSessionTransitionResult activated =
            _abMergeSession.Activate(projection.Catalog);
        ApplyAbMergeReadiness(projection);
        SyncAbMergeMembership(activated.Snapshot);
    }

    private CompiledAuthoringSelectionSnapshot ResolveAbMergeAuthoringSnapshot()
    {
        string[] selectedSlotIds =
        [
            .. AbMergeSlots
                .Where(static slot => slot.HasFile)
                .Select(static slot => slot.SlotId),
        ];
        Dictionary<string, FileStamp> accepted = AcceptedInputStamps(
            _abMergeSession,
            AbMergeSlots,
            static slot => slot.SlotId);
        return _compositionServices.AbMergeAuthoring.GetAuthoringSnapshot(
            SelectedIc,
            GetSelectedAbMergeTopologyToken(),
            selectedSlotIds,
            accepted,
            AbMergeAuthoringRevision,
            _abMergeSession.CurrentSnapshot);
    }

    private void ApplyAbMergeReadiness(CompiledAuthoringSelectionSnapshot projection)
    {
        ApplyInputReadiness(AbMergeSlots, projection.Slots, static slot => slot.SlotId);
    }

    private void SyncAbMergeMembership(ActiveSessionSnapshot? snapshot)
    {
        SyncInputMembership(snapshot, AbMergeSlots, static slot => slot.SlotId);
    }
}
