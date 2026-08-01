using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly FirmwareInspectionSession _firmwareInspectionSession;
    private bool _isApplyingFirmwareInspectionContext;
    private bool _isRefreshingFirmwareInspectionContext;

    /// <summary>True while the latest selected-file inspection is executing outside the dispatcher.</summary>
    public bool IsFirmwareInspectionLoading { get; private set; }

    internal Task FirmwareInspectionRefreshTask { get; private set; } = Task.CompletedTask;

    /// <summary>Selects a slot file, then projects all affected firmware facts outside the UI dispatcher.</summary>
    public async Task SetSlotFileAsync(
        string slotId,
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!_deferredState.IsWorkflowLoaded)
        {
            RefreshContextState();
        }

        bool preservePendingCtrlRamBase = IsFirmwareInspectionLoading &&
            IsCtrlRamReplaceModeSelected &&
            ReplaceBaseSlot.HasFile;
        FirmwareSlotViewModel? slot = SelectSlotFile(slotId, path);
        if (slot is null)
        {
            return;
        }

        IReadOnlyList<FirmwareInspectionItemRequest> items = FirmwareInspectionRequestFactory.CreateSelectionItems(
            slot,
            preservePendingCtrlRamBase,
            CreateFirmwareInspectionRequestContext());
        FirmwareInspectionRefreshTask = RunFirmwareInspectionAsync(items, cancellationToken);
        await FirmwareInspectionRefreshTask;
    }

    private FirmwareInspectionItemRequest CreateFirmwareInspectionItem(
        FirmwareSlotViewModel slot,
        bool publishFacts,
        bool promptForMismatch,
        bool applyVerifiedContext,
        string? tpPath = null)
    {
        return FirmwareInspectionRequestFactory.CreateItem(
            slot,
            CreateFirmwareInspectionRequestContext(),
            publishFacts,
            promptForMismatch,
            applyVerifiedContext,
            tpPath);
    }

    private FirmwareInspectionRequestContext CreateFirmwareInspectionRequestContext()
    {
        return new FirmwareInspectionRequestContext(
            _mergeDpSlot,
            _mergeTpSlot,
            ReplaceBaseSlot,
            IsCtrlRamReplaceModeSelected,
            IsNumberSelectorVisible,
            SelectedNumber,
            IsAbCodeMergeModeSelected,
            _abMergeAddressSpaceBySlotId,
            GetSelectedAbMergeTopologyToken(),
            MergeDpSlotId,
            MergeTpSlotId,
            ReplaceBaseSlotId);
    }

    private async Task RunFirmwareInspectionAsync(
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        long generation = _firmwareInspectionSession.NextGeneration();
        var request = new FirmwareInspectionBatchRequest(
            generation,
            SelectedIc,
            SelectedNumber,
            SelectedMergeMode,
            SelectedReplaceMode,
            items);
        foreach (FirmwareInspectionItemRequest item in items.Where(static item =>
                     item.AbMergeAddressSpaceId is not null))
        {
            FindSlot(item.SlotId)?.SetInputInspectionPending(Text.FirmwareInspectionLoadingStatus);
        }

        SetFirmwareInspectionLoading(true);
        try
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            FirmwareInspectionBatchResult result = await Task.Run(
                () => _firmwareInspectionSession.ReadBatch(request),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (FirmwareInspectionProjection.IsCurrent(
                    request, result, _firmwareInspectionSession.CurrentGeneration,
                    SelectedIc, SelectedNumber, SelectedMergeMode, SelectedReplaceMode,
                    FindSlot, _mergeTpSlot.FilePath))
            {
                ApplyFirmwareInspectionBatch(request, result);
            }
            else if (generation == _firmwareInspectionSession.CurrentGeneration &&
                !result.IsFileIdentityStable &&
                FirmwareInspectionProjection.ApplyStaleAbInputInspection(
                    MergeSlots,
                    request,
                    result,
                    Text))
            {
                RefreshCommandState();
            }
        }
        finally
        {
            if (generation == _firmwareInspectionSession.CurrentGeneration)
            {
                NotifySlotFileOutputNames();
                SetFirmwareInspectionLoading(false);
            }
        }
    }

    private void ApplyFirmwareInspectionBatch(
        FirmwareInspectionBatchRequest request,
        FirmwareInspectionBatchResult result)
    {
        foreach (FirmwareInspectionItemRequest item in request.Items)
        {
            WorkbenchFirmwareInspection inspection = result.InspectionsById[item.SlotId];
            FirmwareFileIdentity identity = result.FileIdentities[item.Path];
            _firmwareInspectionSession.StoreProjection(
                item.SlotId,
                item.Path,
                identity,
                inspection);
            if (item.SlotId == ReplaceBaseSlotId)
            {
                _firmwareInspectionSession.StoreBase(
                    request.IcId,
                    item.Path,
                    inspection);
            }

            if (FindSlot(item.SlotId) is not { } slot)
            {
                continue;
            }

            if (inspection.AbMergeInput is not null)
            {
                FirmwareInspectionProjection.ApplyAbInputInspection(slot, inspection, Text);
            }
            else if (item.PublishFacts)
            {
                slot.SetFirmwareFacts(item.SlotKind == FirmwareSlotKind.Dp
                    ? UiCompositionRunner.GetDpFirmwareSlotFacts(inspection, Text)
                    : UiCompositionRunner.GetFirmwareSlotFacts(
                        inspection,
                        includeBaseFacts: item.SlotKind == FirmwareSlotKind.Base,
                        text: Text));
            }

            if (item.PromptForMismatch)
            {
                if (ReconcileFirmwareIcMismatch(slot, inspection.DetectedIcId))
                {
                    return;
                }
            }

            if (item.ApplyVerifiedContext && !IsFirmwareIcMismatchModalOpen)
            {
                PromptForFirmwareNumberMismatch(slot, inspection.ContextSuggestion);
            }

            if (item.SlotId == ReplaceBaseSlotId && IsCtrlRamReplaceModeSelected)
            {
                ApplyCtrlRamDisplayFromInspection(inspection);
            }
        }

        if (request.Items.Any(static item =>
                item.SlotId == MergeDpSlotId ||
                item.AbMergeAddressSpaceId is not null))
        {
            RefreshMergeMemoryMapState();
        }

        if (request.Items.Any(item =>
                item.SlotId == ReplaceBaseSlotId &&
                string.Equals(SelectedReplaceMode, DpReplaceMode, StringComparison.Ordinal)))
        {
            RefreshReplaceMemoryMapState();
        }

        RefreshCommandState();
    }

    private void ApplyCtrlRamDisplayFromInspection(WorkbenchFirmwareInspection inspection)
    {
        ApplyCtrlRamInspectionDisplay(FirmwareInspectionProjection.ResolveCtrlRamDisplay(
            inspection,
            SelectedIc,
            SelectedNumber));
    }

    internal Task RefreshSelectedMergeFirmwareInspectionsAsync()
    {
        return RefreshSelectedFirmwareInspectionsAsync(MergeSlots, includeEverySelectedSlot: true);
    }

    internal Task RefreshSelectedReplaceFirmwareInspectionsAsync()
    {
        return RefreshSelectedFirmwareInspectionsAsync(
            ReplaceSlots.Concat([ReplaceBaseSlot]),
            includeEverySelectedSlot: true);
    }

    internal Task RefreshAllSelectedFirmwareInspectionsAsync(string? applyVerifiedContextSlotId = null)
    {
        return RefreshSelectedFirmwareInspectionsAsync(
            MergeSlots.Concat(ReplaceSlots).Concat([ReplaceBaseSlot]),
            includeEverySelectedSlot: false,
            applyVerifiedContextSlotId);
    }

    private Task RefreshSelectedFirmwareInspectionsAsync(
        IEnumerable<FirmwareSlotViewModel> candidateSlots,
        bool includeEverySelectedSlot,
        string? applyVerifiedContextSlotId = null)
    {
        var slots = candidateSlots
            .Where(slot => slot.HasFile &&
                (includeEverySelectedSlot ||
                    FirmwareInspectionRequestFactory.SupportsFacts(slot) ||
                    string.Equals(slot.SlotId, applyVerifiedContextSlotId, StringComparison.Ordinal)))
            .DistinctBy(static slot => slot.SlotId, StringComparer.Ordinal)
            .ToDictionary(static slot => slot.SlotId, StringComparer.Ordinal);

        IReadOnlyList<FirmwareInspectionItemRequest> items =
        [
            .. slots.Values.Select(slot =>
            {
                bool applyVerified = string.Equals(
                    slot.SlotId,
                    applyVerifiedContextSlotId,
                    StringComparison.Ordinal);
                return CreateFirmwareInspectionItem(
                    slot,
                    FirmwareInspectionRequestFactory.SupportsFacts(slot),
                    applyVerified,
                    applyVerified && slot.SlotKind is FirmwareSlotKind.Tp or FirmwareSlotKind.Base);
            }),
        ];
        foreach (FirmwareSlotViewModel slot in slots.Values)
        {
            _firmwareInspectionSession.RemoveProjection(slot.SlotId);
        }

        if (slots.ContainsKey(ReplaceBaseSlotId))
        {
            _firmwareInspectionSession.ClearBase();
        }

        NotifySlotFileOutputNames();
        if (slots.ContainsKey(MergeDpSlotId) ||
            (IsAbCodeMergeModeSelected && slots.Keys.Any(_abMergeAddressSpaceBySlotId.ContainsKey)))
        {
            RefreshMergeMemoryMapState();
        }

        if (slots.ContainsKey(ReplaceBaseSlotId) && SelectedReplaceMode == DpReplaceMode)
        {
            RefreshReplaceMemoryMapState();
        }

        return FirmwareInspectionRefreshTask = RunFirmwareInspectionAsync(items, CancellationToken.None);
    }

    private void RefreshCtrlRamDisplayFromInspection()
    {
        if (!IsCtrlRamReplaceModeSelected || !ReplaceBaseSlot.HasFile)
        {
            return;
        }

        if (_firmwareInspectionSession.TryGetBase(
                SelectedIc,
                ReplaceBaseSlot.FilePath,
                out WorkbenchFirmwareInspection inspection))
        {
            ApplyCtrlRamDisplayFromInspection(inspection);
            RefreshCommandState();
            return;
        }

        IReadOnlyList<FirmwareInspectionItemRequest> items =
        [
            CreateFirmwareInspectionItem(
                ReplaceBaseSlot,
                publishFacts: false,
                promptForMismatch: false,
                applyVerifiedContext: false),
        ];
        FirmwareInspectionRefreshTask = RunFirmwareInspectionAsync(items, CancellationToken.None);
    }

    private void InvalidateFirmwareInspection(
        bool clearBaseCache = false,
        bool clearFileProjections = false)
    {
        _firmwareInspectionSession.Invalidate(clearBaseCache, clearFileProjections);
        if (clearFileProjections)
        {
            foreach (FirmwareSlotViewModel slot in _abMergeSlotsByAddressSpace.Values)
            {
                slot.ClearInputInspection();
            }
        }

        SetFirmwareInspectionLoading(false);
    }

    private void SetFirmwareInspectionLoading(bool isLoading)
    {
        if (IsFirmwareInspectionLoading == isLoading)
        {
            return;
        }

        IsFirmwareInspectionLoading = isLoading;
        OnPropertyChanged(nameof(IsFirmwareInspectionLoading));
        OnPropertyChanged(nameof(MergeReadinessStatus));
        OnPropertyChanged(nameof(ReplaceReadinessStatus));
        RefreshCommandState();
    }

}
