using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
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

        GeneralMappingRowViewModel? mapping = _merge.GeneralMergeMappings
            .Cast<GeneralMappingRowViewModel>()
            .Concat(_replace.GeneralReplaceMappings)
            .FirstOrDefault(row => StringComparer.Ordinal.Equals(row.MappingId, slotId));
        if (mapping is not null)
        {
            if (!mapping.CanSelectFile)
            {
                return;
            }

            bool rebindCurrentPath = StringComparer.Ordinal.Equals(mapping.FilePath, path);
            mapping.FilePath = path;
            if (rebindCurrentPath)
            {
                mapping.InvalidateFileInspection();
                switch (mapping)
                {
                    case GeneralMergeMappingViewModel:
                        _merge.RefreshMergeMemoryMapState();
                        break;
                    case GeneralReplaceMappingViewModel:
                        _replace.RefreshReplaceMemoryMapState();
                        break;
                    default:
                        throw new InvalidOperationException("Unknown General mapping row.");
                }
            }
            Task preparation = mapping switch
            {
                GeneralMergeMappingViewModel => _merge.GeneralMergeReadinessRefreshTask,
                GeneralReplaceMappingViewModel => _replace.GeneralReplaceReadinessRefreshTask,
                _ => throw new InvalidOperationException("Unknown General mapping row."),
            };
            await preparation.WaitAsync(cancellationToken);
            return;
        }

        FirmwareSlotViewModel? slot = SelectSlotFile(slotId, path);
        if (slot is null)
        {
            return;
        }

        FirmwareInspectionRequestContext context = CreateFirmwareInspectionRequestContext();
        if (context.IsDpReplace || context.IsCtrlRamReplace)
        {
            await RefreshSelectedReplaceFirmwareInspectionsAsync(slot.SlotId);
            return;
        }

        if ((context.IsStandardMerge && _merge.IsStandardMergeSlot(slot)) ||
            (context.IsAbMerge && context.AbAddressSpaceBySlotId.ContainsKey(slot.SlotId)))
        {
            await RefreshSelectedMergeFirmwareInspectionsAsync(IsAbMergeContextActive ? slot.SlotId : null);
            return;
        }

        IReadOnlyList<FirmwareInspectionItemRequest> items = FirmwareInspectionRequestFactory.CreateSelectionItems(
            slot,
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
            IsReplaceVisible && SelectedReplaceMode == ExperienceIds.DpReplace,
            IsNumberSelectorVisible,
            SelectedNumber,
            IsAbCodeMergeModeSelected,
            AbMergeAddressSpaceBySlotId,
            GetSelectedAbMergeTopologyToken(),
            CompositionSlotIds.MergeDp,
            CompositionSlotIds.MergeTp,
            CompositionSlotIds.ReplaceBase,
            IsStandardMergeModeSelected,
            [.. _merge.StandardMergeSlots.Select(static slot => slot.SlotId)]);
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
        AuthoringRevision authoringRevision = items.Any(static item =>
            item.StandardMergeAddressSpaceId is not null)
                ? _merge.StandardMergeAuthoringRevision
                : items.Any(static item => item.AbMergeAddressSpaceId is not null)
                    ? _merge.AbMergeAuthoringRevision
                : items.Any(static item => item.DpReplaceAddressSpaceId is not null ||
                    item.CtrlRamReplaceAddressSpaceId is not null)
                    ? _replace.ReplaceInputAuthoringRevision
                    : new AuthoringRevision(1);
        var request = new FirmwareInspectionBatchRequest(
            generation,
            authoringRevision,
            SelectedIc,
            SelectedNumber,
            SelectedMergeMode,
            SelectedReplaceMode,
            items);
        foreach (FirmwareInspectionItemRequest item in items.Where(static item =>
                     item.AbMergeAddressSpaceId is not null ||
                     item.DpReplaceAddressSpaceId is not null ||
                     item.CtrlRamReplaceAddressSpaceId is not null ||
                     item.StandardMergeAddressSpaceId is not null))
        {
            FindSlot(item.SlotId)?.SetInputInspectionPending(Text.FirmwareInspectionLoadingStatus);
        }

        SetFirmwareInspectionLoading(true);
        try
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            FirmwareInspectionBatchResult result = await InspectionSession
                .ReadBatchAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (FirmwareInspectionProjection.IsCurrent(
                    request, result, InspectionSession.CurrentGeneration,
                    SelectedIc, SelectedNumber, SelectedMergeMode, SelectedReplaceMode,
                    FindSlot, MergeTpSlot.FilePath))
            {
                ApplyFirmwareInspectionBatch(request, result);
                if (request.Items.Any(static item =>
                        item.CtrlRamReplaceAddressSpaceId is not null))
                {
                    await _replace.RefreshCtrlRamActionReadinessAsync(
                        cancellationToken);
                }
            }
            else if (generation == InspectionSession.CurrentGeneration &&
                !result.IsContentStable &&
                FirmwareInspectionProjection.ApplyStaleInputInspection(
                    MergeSlots.Concat(ReplaceSlots).Append(ReplaceBaseSlot),
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
        bool standardMergeAccepted =
            !request.Items.Any(static item => item.StandardMergeAddressSpaceId is not null) ||
            _merge.TryCompleteStandardMergeInputBatch(
                request.Items,
                result.InspectionsById);
        bool abMergeAccepted =
            !request.Items.Any(static item => item.AbMergeAddressSpaceId is not null) ||
            _merge.TryCompleteAbMergeInputBatch(
                request.Items,
                result.InspectionsById);
        bool replaceAccepted = _replace.TryCompleteReplaceInputBatch(
            request.Items,
            result.InspectionsById);
        FirmwareInspectionItemRequest ctrlRamBase = request.Items.FirstOrDefault(item =>
            item.SlotId == CompositionSlotIds.ReplaceBase && IsCtrlRamReplaceModeSelected);
        if (ctrlRamBase.SlotId == CompositionSlotIds.ReplaceBase)
        {
            ApplyCtrlRamDisplayFromInspection(result.InspectionsById[ctrlRamBase.SlotId]);
        }
        foreach (FirmwareInspectionItemRequest item in request.Items)
        {
            FirmwareInspectionSnapshot inspection = result.InspectionsById[item.SlotId];
            if (FindSlot(item.SlotId) is not { } slot)
            {
                continue;
            }

            slot.SetCurrentInspectionProjection(inspection);

            if (inspection.AbMergeFacts is not null)
            {
                FirmwareInspectionProjection.ApplyAbInputFacts(slot, inspection, Text);
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
                if ((item.StandardMergeAddressSpaceId is not null &&
                        !standardMergeAccepted) ||
                    (item.AbMergeAddressSpaceId is not null &&
                        !abMergeAccepted) ||
                    ((item.DpReplaceAddressSpaceId is not null ||
                        item.CtrlRamReplaceAddressSpaceId is not null) &&
                        !replaceAccepted))
                {
                    slot.SetInputInspection(
                        FirmwareInputInspectionSeverity.Blocking,
                        Text.FirmwareInspectionStaleFileStatus);
                }
                else
                {
                    FirmwareInspectionProjection.ApplyInputSlotInspection(slot, inputSlotStatus, Text);
                }
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

        }

        if (request.Items.Any(static item =>
                item.SlotId == CompositionSlotIds.MergeDp ||
                item.AbMergeAddressSpaceId is not null))
        {
            RefreshMergeMemoryMapState();
        }

        if (request.Items.Any(static item => item.StandardMergeAddressSpaceId is not null))
        {
            _merge.RefreshStandardMergeAuthoringState();
        }

        if (request.Items.Any(static item => item.AbMergeAddressSpaceId is not null))
        {
            _merge.RefreshAbMergeAuthoringState();
        }

        if (request.Items.Any(static item =>
                item.SlotId == CompositionSlotIds.ReplaceBase))
        {
            RefreshReplaceMemoryMapState();
        }

        RefreshCommandState();
    }

    internal void ApplyCtrlRamDisplayFromInspection(FirmwareInspectionSnapshot inspection)
    {
        ApplyCtrlRamInspectionDisplay(FirmwareInspectionProjection.ResolveCtrlRamDisplay(
            _compositionServices.FirmwareInspection,
            inspection,
            SelectedIc,
            SelectedNumber));
    }

    internal Task RefreshSelectedMergeFirmwareInspectionsAsync(
        string? applyVerifiedContextSlotId = null)
    {
        return RefreshSelectedFirmwareInspectionsAsync(
            IsStandardMergeModeSelected
                ? _merge.CurrentStandardMergeInspectionSlots()
                : MergeSlots,
            includeEverySelectedSlot: true,
            applyVerifiedContextSlotId);
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
        IReadOnlyDictionary<string, AuthoringSlotInspectionLease> standardMergeLeases =
            IsStandardMergeModeSelected
                ? _merge.BeginStandardMergeSlotInspections(
                    slots.Values.Where(_merge.IsStandardMergeSlot))
                : new Dictionary<string, AuthoringSlotInspectionLease>(StringComparer.Ordinal);
        if (IsStandardMergeModeSelected)
        {
            foreach (string slotId in slots.Values
                         .Where(_merge.IsStandardMergeSlot)
                         .Where(slot => slot.AddressSpaceId is null ||
                             !standardMergeLeases.ContainsKey(slot.AddressSpaceId))
                         .Select(static slot => slot.SlotId)
                         .ToArray())
            {
                _ = slots.Remove(slotId);
            }
        }

        IReadOnlyList<FirmwareInspectionItemRequest> items =
        [
            .. slots.Values.Select(slot =>
            {
                bool applyVerified = string.Equals(
                    slot.SlotId,
                    applyVerifiedContextSlotId,
                    StringComparison.Ordinal);
                FirmwareInspectionItemRequest item = CreateFirmwareInspectionItem(
                    slot,
                    FirmwareInspectionRequestFactory.SupportsFacts(slot),
                    applyVerified,
                    applyVerified && slot.SlotKind is FirmwareSlotKind.Tp or FirmwareSlotKind.Base);
                return item.StandardMergeAddressSpaceId is not null &&
                    standardMergeLeases.TryGetValue(
                        item.StandardMergeAddressSpaceId,
                        out AuthoringSlotInspectionLease? lease)
                            ? item with { StandardMergeInspectionLease = lease }
                            : item;
            }),
        ];
        items = AttachAbMergeInspectionLeases(items);
        items = _replace.AttachReplaceInspectionLeases(items, slots.Values);
        foreach (FirmwareSlotViewModel slot in slots.Values)
        {
            slot.ClearCurrentInspectionProjection();
        }

        NotifySlotFileOutputNames();
        if (slots.ContainsKey(CompositionSlotIds.MergeDp) ||
            (IsAbCodeMergeModeSelected && slots.Keys.Any(AbMergeAddressSpaceBySlotId.ContainsKey)))
        {
            RefreshMergeMemoryMapState();
        }

        if (slots.ContainsKey(CompositionSlotIds.ReplaceBase) && SelectedReplaceMode == ExperienceIds.DpReplace)
        {
            RefreshReplaceMemoryMapState();
        }

        return FirmwareInspectionRefreshTask = RunFirmwareInspectionAsync(items, CancellationToken.None);
    }

    private IReadOnlyList<FirmwareInspectionItemRequest> AttachAbMergeInspectionLeases(
        IReadOnlyList<FirmwareInspectionItemRequest> items)
    {
        if (!IsAbCodeMergeModeSelected)
        {
            return items;
        }

        FirmwareSlotViewModel[] slots =
        [
            .. items
                .Where(static item => item.AbMergeAddressSpaceId is not null)
                .Select(item => FindSlot(item.SlotId))
                .OfType<FirmwareSlotViewModel>(),
        ];
        if (slots.Length == 0)
        {
            return items;
        }

        IReadOnlyDictionary<string, AuthoringSlotInspectionLease> leases =
            _merge.BeginAbMergeSlotInspections(slots);
        return
        [
            .. items.Select(item =>
                item.AbMergeAddressSpaceId is not null &&
                leases.TryGetValue(item.SlotId, out AuthoringSlotInspectionLease? lease)
                    ? item with { AbMergeInspectionLease = lease }
                    : item),
        ];
    }

    internal void RefreshCtrlRamDisplayFromInspection()
    {
        if (!IsCtrlRamReplaceModeSelected || !ReplaceBaseSlot.HasFile)
        {
            return;
        }

        if (ReplaceBaseSlot.CurrentInspectionProjection is { } inspection)
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
        bool clearBaseProjection = false,
        bool clearSlotProjections = false)
    {
        InspectionSession.Invalidate();
        if (clearBaseProjection)
        {
            ReplaceBaseSlot.ClearCurrentInspectionProjection();
        }

        if (clearSlotProjections)
        {
            foreach (FirmwareSlotViewModel slot in MergeSlots
                         .Concat(ReplaceSlots)
                         .Concat([ReplaceBaseSlot])
                         .Concat(AbMergeSlots)
                         .Distinct())
            {
                slot.ClearCurrentInspectionProjection();
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
