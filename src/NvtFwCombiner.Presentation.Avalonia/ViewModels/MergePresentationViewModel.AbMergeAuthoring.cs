using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MergePresentationViewModel
{
    private CapabilityActionReadinessSnapshot? _abMergeActionReadiness;
    private ActiveSessionSnapshot? _abMergeReadinessSession;

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

        ClearAbMergeActionReadiness();
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
            static item => item.InspectionLease,
            out ActiveSessionSnapshot? snapshot);
        if (completed && selected.Length > 0)
        {
            SyncAbMergeMembership(snapshot);
        }
        return completed;
    }

    internal void RefreshAbMergeAuthoringState()
    {
        ClearAbMergeActionReadiness();
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

    internal async Task RefreshAbMergeActionReadinessAsync(
        CancellationToken cancellationToken)
    {
        ClearAbMergeActionReadiness();
        ActiveSessionSnapshot? session = _abMergeSession.CurrentSnapshot;
        if (!IsAbCodeMergeModeSelected || session is null)
        {
            RefreshCommandState();
            return;
        }

        CapabilityActionReadinessSnapshot? readiness =
            await _compositionServices.AbMergeAuthoring.GetActionReadinessAsync(
                    session,
                    cancellationToken)
                .ConfigureAwait(false);
        if (readiness is not null &&
            ReferenceEquals(session, _abMergeSession.CurrentSnapshot) &&
            IsAbCodeMergeModeSelected)
        {
            _abMergeActionReadiness = readiness;
            _abMergeReadinessSession = session;
        }
        RefreshCommandState();
    }

    internal bool HasCurrentAbMergeActionReadiness(bool build)
    {
        ActiveSessionSnapshot? current = _abMergeSession.CurrentSnapshot;
        CapabilityActionReadinessSnapshot? readiness = _abMergeActionReadiness;
        return readiness is not null &&
            ReferenceEquals(current, _abMergeReadinessSession) &&
            readiness.ResolutionToken == current?.ResolutionToken &&
            readiness.AuthoringRevision == current?.AuthoringRevision &&
            StringComparer.Ordinal.Equals(
                readiness.CapabilityFingerprint,
                current?.ExactCapability?.CapabilityFingerprint) &&
            StringComparer.Ordinal.Equals(
                readiness.CompilationFingerprint,
                current?.CompilationFingerprint) &&
            (build ? readiness.Build : readiness.Preview).IsAvailable;
    }

    private void ClearAbMergeActionReadiness()
    {
        _abMergeActionReadiness = null;
        _abMergeReadinessSession = null;
    }
}
