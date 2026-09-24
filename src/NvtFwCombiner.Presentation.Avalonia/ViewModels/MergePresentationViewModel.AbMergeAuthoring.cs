using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MergePresentationViewModel
{
    private string? _preparedAbMergeIc;
    private string? _preparedAbMergeTopology;
    private bool _hasPreparedAbMergeSnapshot;
    private CompiledAuthoringSelectionSnapshot? _preparedAbMergeSnapshot;

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
            out ActiveSessionSnapshot? snapshot,
            (catalog, leases, statuses) => _compositionServices.AbMergeAuthoring.AdoptInspectedBatch(
                _abMergeSession, catalog, leases, statuses));
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
        return ResolveAbMergeAuthoringSnapshot(
            SelectedIc,
            GetSelectedAbMergeTopologyToken());
    }

    private CompiledAuthoringSelectionSnapshot ResolveAbMergeAuthoringSnapshot(
        string icId,
        string? topologyToken)
    {
        if (_hasPreparedAbMergeSnapshot &&
            string.Equals(_preparedAbMergeIc, icId, StringComparison.Ordinal) &&
            string.Equals(_preparedAbMergeTopology, topologyToken, StringComparison.Ordinal) &&
            _preparedAbMergeSnapshot is not null)
        {
            CompiledAuthoringSelectionSnapshot prepared = _preparedAbMergeSnapshot;
            _preparedAbMergeIc = null;
            _preparedAbMergeTopology = null;
            _hasPreparedAbMergeSnapshot = false;
            _preparedAbMergeSnapshot = null;
            return prepared;
        }

        return ResolveAbMergeAuthoringSnapshotCore(icId, topologyToken);
    }

    private CompiledAuthoringSelectionSnapshot ResolveAbMergeAuthoringSnapshotCore(
        string icId,
        string? topologyToken)
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
            icId,
            topologyToken,
            selectedSlotIds,
            accepted,
            AbMergeAuthoringRevision,
            _abMergeSession.CurrentSnapshot,
            AbDpMode);
    }

    private void ApplyAbMergeReadiness(CompiledAuthoringSelectionSnapshot projection)
    {
        ApplyInputReadiness(AbMergeSlots, projection.Slots, static slot => slot.SlotId);
        if (_abMergeSession.CurrentSnapshot is not { } current) { return; }
        foreach (AuthoringInputSlotStatus status in current.InputSlotStatuses.Where(static status => status.Readiness == NvtFwCombiner.Application.Metadata.ResolvedChildReadiness.Blocked))
        {
            FirmwareSlotViewModel? slot = AbMergeSlots.SingleOrDefault(slot => slot.SlotId == status.SlotId);
            if (slot is not null && StringComparer.Ordinal.Equals(slot.FilePath, status.SelectedPathHint))
            {
                FirmwareInspectionProjection.ApplyInputSlotInspection(slot, status, Text);
            }
        }
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
            PresentationObserver.Invoke(RefreshCommandState);
            return;
        }

        CapabilityActionReadinessSnapshot? readiness =
            await _compositionServices.AbMergeAuthoring.GetActionReadinessAsync(
                    session,
                    cancellationToken);
        if (readiness is not null &&
            ReferenceEquals(session, _abMergeSession.CurrentSnapshot) &&
            IsAbCodeMergeModeSelected)
        {
            _abMergeActionReadiness = readiness;
            _abMergeReadinessSession = session;
        }
        PresentationObserver.Invoke(RefreshCommandState);
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

    internal async Task<bool> ReapplyAbMergeConfigurationAsync()
    {
        ClearAbMergeActionReadiness();
        foreach (FirmwareSlotViewModel slot in AbMergeSlots)
        {
            if (slot.CurrentInspectionProjection is not { AbMergeFacts.EventBufferFormat: not null } previous) { continue; }
            FirmwareInspectionSnapshot pending = previous with
            {
                AbMergeFacts = previous.AbMergeFacts with { EventBufferFormat = null },
            };
            slot.SetCurrentInspectionProjection(pending);
            FirmwareInspectionProjection.ApplyAbInputFacts(slot, pending, Text);
        }
        RefreshCommandState();
        try
        {
            CompiledAuthoringSessionPreparation? result = await _compositionServices.AbMergeAuthoring
                .ReapplyAcceptedInputsAsync(_abMergeSession, CancellationToken.None);
            if (result is null)
            {
                await RefreshAbMergeActionReadinessAsync(CancellationToken.None);
                return true;
            }
            if (!ReferenceEquals(result.Snapshot, _abMergeSession.CurrentSnapshot)) { return false; }
            if (!result.Succeeded)
            {
                if (result.Issues.Count > 0)
                {
                    foreach (FirmwareSlotViewModel slot in AbMergeSlots.Where(static slot => slot.HasFile))
                    {
                        if (slot.CurrentInspectionProjection is { } previous)
                        {
                            AuthoringInputSlotStatus? blocked = result.Inspection?.Statuses.GetValueOrDefault(slot.SlotId);
                            if (blocked is not null && !StringComparer.Ordinal.Equals(slot.FilePath, blocked.SelectedPathHint)) { continue; }
                            slot.SetCurrentInspectionProjection(previous with
                            {
                                InputSlotStatus = blocked ?? previous.InputSlotStatus,
                                InputSlotCatalog = result.Inspection?.Catalog ?? previous.InputSlotCatalog,
                                AuthoringCompilationIssues = result.Issues,
                            });
                        }
                        FirmwareInspectionProjection.ApplyAuthoringIssues(slot, result.Issues, Text);
                    }
                }
                await RefreshAbMergeActionReadinessAsync(CancellationToken.None);
                return false;
            }

            foreach (FirmwareSlotViewModel slot in AbMergeSlots)
            {
                if (slot.CurrentInspectionProjection is not { } previous ||
                    !result.Inspection!.Statuses.TryGetValue(slot.SlotId, out AuthoringInputSlotStatus? status) ||
                    !StringComparer.Ordinal.Equals(slot.FilePath, status.SelectedPathHint)) { continue; }
                FirmwareInspectionSnapshot updated = previous with
                {
                    InputSlotStatus = status,
                    InputSlotCatalog = result.Inspection.Catalog,
                    AbMergeFacts = result.AbMergeFacts.GetValueOrDefault(slot.SlotId) ??
                        new(status.AddressSpaceId, status.Observation.Versions),
                    AuthoringCompilationIssues = [],
                };
                slot.SetCurrentInspectionProjection(updated);
                FirmwareInspectionProjection.ApplyAbInputFacts(slot, updated, Text);
                FirmwareInspectionProjection.ApplyInputSlotInspection(slot, status, Text);
            }
            SyncAbMergeMembership(result.Snapshot);
            if (IsAbCodeMergeModeSelected)
            {
                RefreshMergeMemoryMapState(refreshAuthoring: false);
                if (!_stateBindings.IsRunInProgress()) { _stateBindings.ResetRunResult(CaptureRunContext(AbCodeMergeMode)); }
            }
            await RefreshAbMergeActionReadinessAsync(CancellationToken.None);
            return true;
        }
        finally
        {
            RefreshCommandState();
        }
    }

    private void ClearAbMergeActionReadiness()
    {
        _abMergeActionReadiness = null;
        _abMergeReadinessSession = null;
    }
}
