using NvtFwCombiner.Application.Authoring;
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

            mapping.FilePath = path;
            GeneralSelectedFileLengthResult length =
                await _compositionServices.Authoring.ObserveGeneralSelectedFileLengthAsync(
                    mapping.MappingId,
                    path,
                    cancellationToken);
            if (!length.Succeeded)
            {
                if (StringComparer.Ordinal.Equals(mapping.FilePath, path))
                {
                    mapping.ApplyFileInspection(null, length.Issue);
                }
                return;
            }

            AuthoringSlotInspectionStartResult started = mapping switch
            {
                GeneralMergeMappingViewModel merge =>
                    _merge.BeginGeneralMergeFileInspection(merge, length.ObservedLength!.Value),
                GeneralReplaceMappingViewModel replace =>
                    _replace.BeginGeneralReplaceFileInspection(replace, length.ObservedLength!.Value),
                _ => throw new InvalidOperationException("Unknown General mapping row."),
            };
            if (!started.Succeeded)
            {
                AuthoringPublicationLease? lease = mapping switch
                {
                    GeneralMergeMappingViewModel merge =>
                        _merge.CaptureGeneralMergePrebindingLease(merge, path),
                    GeneralReplaceMappingViewModel replace =>
                        _replace.CaptureGeneralReplacePrebindingLease(replace, path),
                    _ => throw new InvalidOperationException("Unknown General mapping row."),
                };
                if (lease is null)
                {
                    return;
                }
                GeneralSelectedFileInspectionResult cached =
                    await _compositionServices.Authoring.InspectGeneralSelectedFileAsync(
                        mapping.MappingId,
                        path,
                        lease.AuthoringRevision,
                        length.ObservedLength.Value,
                        cancellationToken);
                _ = mapping switch
                {
                    GeneralMergeMappingViewModel merge =>
                        _merge.TryCacheGeneralMergeInspection(merge, lease, cached),
                    GeneralReplaceMappingViewModel replace =>
                        _replace.TryCacheGeneralReplaceInspection(replace, lease, cached),
                    _ => false,
                };
                return;
            }

            GeneralSelectedFileInspectionResult inspection =
                await _compositionServices.Authoring.InspectGeneralSelectedFileAsync(
                    mapping.MappingId,
                    path,
                    started.Snapshot!.AuthoringRevision,
                    length.ObservedLength.Value,
                    cancellationToken);
            _ = mapping switch
            {
                GeneralMergeMappingViewModel merge =>
                    _merge.TryPublishGeneralMergeFileInspection(merge, started.Lease!, inspection),
                GeneralReplaceMappingViewModel replace =>
                    _replace.TryPublishGeneralReplaceFileInspection(replace, started.Lease!, inspection),
                _ => false,
            };
            if (mapping is GeneralReplaceMappingViewModel)
            {
                await _replace.GeneralReplaceReadinessRefreshTask;
            }
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
            IsReplaceVisible && SelectedReplaceMode == WorkbenchReplaceModes.Dp,
            IsNumberSelectorVisible,
            SelectedNumber,
            IsAbCodeMergeModeSelected,
            AbMergeAddressSpaceBySlotId,
            GetSelectedAbMergeTopologyToken(),
            WorkbenchSlotIds.MergeDp,
            WorkbenchSlotIds.MergeTp,
            WorkbenchSlotIds.ReplaceBase,
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
                if (request.Items.Any(static item =>
                        item.CtrlRamReplaceAddressSpaceId is not null))
                {
                    await _replace.RefreshCtrlRamActionReadinessAsync(
                        cancellationToken);
                }
            }
            else if (generation == InspectionSession.CurrentGeneration &&
                !result.IsFileIdentityStable &&
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
            item.SlotId == WorkbenchSlotIds.ReplaceBase && IsCtrlRamReplaceModeSelected);
        if (ctrlRamBase.SlotId == WorkbenchSlotIds.ReplaceBase)
        {
            ApplyCtrlRamDisplayFromInspection(result.InspectionsById[ctrlRamBase.SlotId]);
        }
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
                        WorkbenchInputInspectionSeverity.Blocking,
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
                item.SlotId == WorkbenchSlotIds.MergeDp ||
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
                item.SlotId == WorkbenchSlotIds.ReplaceBase))
        {
            RefreshReplaceMemoryMapState();
        }

        RefreshCommandState();
    }

    internal void ApplyCtrlRamDisplayFromInspection(WorkbenchFirmwareInspection inspection)
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
