using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class WorkflowSessionPresentationViewModel
{
    /// <summary>True while the latest selected-file inspection is executing outside the dispatcher.</summary>
    public bool IsFirmwareInspectionLoading { get; private set; }

    internal Task FirmwareInspectionRefreshTask { get; private set; } = Task.CompletedTask;

    /// <summary>Selects a slot file, then projects all affected firmware facts outside the UI dispatcher.</summary>
    public async Task SetSlotFileAsync(
        string slotId,
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!IsWorkflowLoaded)
        {
            EnsureWorkflowLoaded();
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

        FirmwareInspectionRequestContext context = CreateFirmwareInspectionRequestContext();
        if (context.IsDpReplace)
        {
            await RefreshSelectedReplaceFirmwareInspectionsAsync(slot.SlotId);
            return;
        }

        IReadOnlyList<FirmwareInspectionItemRequest> items = FirmwareInspectionRequestFactory.CreateSelectionItems(
            slot,
            preservePendingCtrlRamBase,
            context);
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
            MergeDpSlot,
            MergeTpSlot,
            ReplaceBaseSlot,
            IsCtrlRamReplaceModeSelected,
            IsReplaceVisible && SelectedReplaceMode == WorkbenchReplaceModes.Dp,
            IsNumberSelectorVisible,
            SelectedNumber,
            IsAbCodeMergeModeSelected,
            AbMergeAddressSpaceBySlotId,
            GetSelectedAbMergeTopologyToken(),
            WorkbenchSlotIds.MergeDp,
            WorkbenchSlotIds.MergeTp,
            WorkbenchSlotIds.ReplaceBase);
    }

    private async Task RunFirmwareInspectionAsync(
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        long generation = InspectionSession.NextGeneration();
        var request = new FirmwareInspectionBatchRequest(
            generation,
            InspectionSession.CurrentAuthoringRevision,
            SelectedIc,
            SelectedNumber,
            SelectedMergeMode,
            SelectedReplaceMode,
            items);
        foreach (FirmwareInspectionItemRequest item in items.Where(static item =>
                     item.AbMergeAddressSpaceId is not null || item.DpReplaceAddressSpaceId is not null))
        {
            FindSlot(item.SlotId)?.SetInputInspectionPending(Text.FirmwareInspectionLoadingStatus);
        }

        SetFirmwareInspectionLoading(true);
        try
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            FirmwareInspectionBatchResult result = await Task.Run(
                () => InspectionSession.ReadBatch(request),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (FirmwareInspectionProjection.IsCurrent(
                    request, result, InspectionSession.CurrentGeneration,
                    SelectedIc, SelectedNumber, SelectedMergeMode, SelectedReplaceMode,
                    FindSlot, MergeTpSlot.FilePath))
            {
                ApplyFirmwareInspectionBatch(request, result);
            }
            else if (generation == InspectionSession.CurrentGeneration &&
                !result.IsFileIdentityStable &&
                FirmwareInspectionProjection.ApplyStaleInputInspection(
                    MergeSlots.Concat(ReplaceSlots),
                    request,
                    result,
                    Text))
            {
                RefreshCommandState();
            }
        }
        finally
        {
            if (generation == InspectionSession.CurrentGeneration)
            {
                foreach (FirmwareInspectionItemRequest item in items)
                {
                    if (FindSlot(item.SlotId) is { IsInputInspectionPending: true } pending)
                    {
                        pending.ClearInputInspection();
                    }
                }
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
            InspectionSession.StoreProjection(
                item.SlotId,
                item.Path,
                identity,
                inspection);
            if (item.SlotId == WorkbenchSlotIds.ReplaceBase)
            {
                InspectionSession.StoreBase(
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

            if (inspection.InputSlotStatus is { } inputSlotStatus)
            {
                FirmwareInspectionProjection.ApplyInputSlotInspection(slot, inputSlotStatus, Text);
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

            if (item.SlotId == WorkbenchSlotIds.ReplaceBase && IsCtrlRamReplaceModeSelected)
            {
                ApplyCtrlRamDisplayFromInspection(inspection);
            }
        }

        if (request.Items.Any(static item =>
                item.SlotId == WorkbenchSlotIds.MergeDp ||
                item.AbMergeAddressSpaceId is not null))
        {
            RefreshMergeMemoryMapState();
        }

        if (request.Items.Any(item =>
                item.SlotId == WorkbenchSlotIds.ReplaceBase &&
                string.Equals(SelectedReplaceMode, WorkbenchReplaceModes.Dp, StringComparison.Ordinal)))
        {
            RefreshReplaceMemoryMapState();
        }

        RefreshCommandState();
    }

    internal void ApplyCtrlRamDisplayFromInspection(WorkbenchFirmwareInspection inspection)
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

    internal Task RefreshSelectedReplaceFirmwareInspectionsAsync(
        string? applyVerifiedContextSlotId = null)
    {
        return RefreshSelectedFirmwareInspectionsAsync(
            ReplaceSlots.Concat([ReplaceBaseSlot]),
            includeEverySelectedSlot: true,
            applyVerifiedContextSlotId);
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
            InspectionSession.RemoveProjection(slot.SlotId);
        }

        if (slots.ContainsKey(WorkbenchSlotIds.ReplaceBase))
        {
            InspectionSession.ClearBase();
        }

        NotifySlotFileOutputNames();
        if (slots.ContainsKey(WorkbenchSlotIds.MergeDp) ||
            (IsAbCodeMergeModeSelected && slots.Keys.Any(AbMergeAddressSpaceBySlotId.ContainsKey)))
        {
            RefreshMergeMemoryMapState();
        }

        if (slots.ContainsKey(WorkbenchSlotIds.ReplaceBase) && SelectedReplaceMode == WorkbenchReplaceModes.Dp)
        {
            RefreshReplaceMemoryMapState();
        }

        return FirmwareInspectionRefreshTask = RunFirmwareInspectionAsync(items, CancellationToken.None);
    }

    internal void RefreshCtrlRamDisplayFromInspection()
    {
        if (!IsCtrlRamReplaceModeSelected || !ReplaceBaseSlot.HasFile)
        {
            return;
        }

        if (InspectionSession.TryGetBase(
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

    internal void InvalidateFirmwareInspection(
        bool clearBaseCache = false,
        bool clearFileProjections = false)
    {
        InspectionSession.Invalidate(clearBaseCache, clearFileProjections);
        if (clearFileProjections)
        {
            foreach (FirmwareSlotViewModel slot in MergeSlots
                         .Concat(ReplaceSlots)
                         .Concat([ReplaceBaseSlot])
                         .Concat(AbMergeSlots)
                         .Distinct())
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
        RefreshCommandState();
    }

}
