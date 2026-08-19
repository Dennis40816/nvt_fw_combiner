using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
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
                GeneralMergeMappingViewModel => _merge.InspectionLifecycles[ExperienceIds.GeneralMerge].ActiveTask,
                GeneralReplaceMappingViewModel => _replace.InspectionLifecycles[ExperienceIds.GeneralReplace].ActiveTask,
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

        if ((IsReplaceVisible && SelectedReplaceMode == ExperienceIds.DpReplace) ||
            IsCtrlRamReplaceModeSelected)
        {
            await RefreshSelectedReplaceFirmwareInspectionsAsync(slot.SlotId);
            return;
        }

        if ((IsStandardMergeModeSelected && _merge.IsStandardMergeSlot(slot)) ||
            (IsAbCodeMergeModeSelected && AbMergeAddressSpaceBySlotId.ContainsKey(slot.SlotId)))
        {
            await RefreshSelectedMergeFirmwareInspectionsAsync(IsAbMergeContextActive ? slot.SlotId : null);
            return;
        }

        List<FirmwareInspectionItemRequest> items =
        [
            CreateFirmwareInspectionItem(
                slot,
                FirmwareInspectionProjection.SupportsFacts(slot),
                promptForMismatch: true,
                applyVerifiedContext: slot.SlotKind is FirmwareSlotKind.Tp or FirmwareSlotKind.Base),
        ];
        if (slot.SlotId == CompositionSlotIds.MergeTp && MergeDpSlot.HasFile)
        {
            items.Add(CreateFirmwareInspectionItem(
                MergeDpSlot,
                publishFacts: true,
                promptForMismatch: false,
                applyVerifiedContext: false,
                slot.FilePath));
        }
        await RunFirmwareInspectionAsync(items, cancellationToken);
    }

    private FirmwareInspectionItemRequest CreateFirmwareInspectionItem(
        FirmwareSlotViewModel slot,
        bool publishFacts,
        bool promptForMismatch,
        bool applyVerifiedContext,
        string? tpPath = null)
    {
        string path = slot.FilePath!;
        bool ctrlRamReplace = IsCtrlRamReplaceModeSelected;
        bool dpReplace = IsReplaceVisible && SelectedReplaceMode == ExperienceIds.DpReplace;
        return new FirmwareInspectionItemRequest(
            slot.SlotId,
            slot.SlotKind,
            path,
            slot.SlotId == CompositionSlotIds.MergeDp ? tpPath ?? MergeTpSlot.FilePath : null,
            slot.SlotId == CompositionSlotIds.ReplaceBase && ctrlRamReplace
                ? new CtrlRamInspectionRequest(SelectedNumber)
                : null,
            publishFacts,
            promptForMismatch,
            applyVerifiedContext && IsNumberSelectorVisible,
            IsAbCodeMergeModeSelected ? AbMergeAddressSpaceBySlotId.GetValueOrDefault(slot.SlotId) : null,
            _merge.GetSelectedAbMergeTopologyToken(),
            dpReplace
                ? ReferenceEquals(slot, ReplaceBaseSlot)
                    ? CompositionAddressSpaceIds.ReferenceBase
                    : slot.AddressSpaceId ?? throw new InvalidOperationException(
                        $"DP Replace slot '{slot.SlotId}' has no canonical address-space id.")
                : null,
            IsStandardMergeModeSelected && _merge.IsStandardMergeSlot(slot) ? slot.AddressSpaceId : null,
            ctrlRamReplace && (ReferenceEquals(slot, ReplaceBaseSlot) ||
                slot.ReplaceInputRole == ReplaceInputRole.CtrlRam)
                ? ReferenceEquals(slot, ReplaceBaseSlot)
                    ? CompositionAddressSpaceIds.ReferenceBase
                    : slot.AddressSpaceId
                : null);
    }

    private Task RunFirmwareInspectionAsync(
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return Task.CompletedTask;
        }

        bool mergeInspection = items.Any(static item =>
            item.StandardMergeAddressSpaceId is not null || item.AbMergeAddressSpaceId is not null) ||
            (!items.Any(static item =>
                    item.DpReplaceAddressSpaceId is not null || item.CtrlRamReplaceAddressSpaceId is not null) &&
                !IsReplaceVisible);
        string mergeMode = SelectedMergeMode;
        string replaceMode = SelectedReplaceMode;
        WorkflowInspectionLifecycle lifecycle = mergeInspection
            ? _merge.InspectionLifecycles[mergeMode]
            : _replace.InspectionLifecycles[replaceMode];
        AuthoringRevision authoringRevision = items.Any(static item =>
            item.StandardMergeAddressSpaceId is not null)
                ? _merge.StandardMergeAuthoringRevision
                : items.Any(static item => item.AbMergeAddressSpaceId is not null)
                    ? _merge.AbMergeAuthoringRevision
                : items.Any(static item => item.DpReplaceAddressSpaceId is not null ||
                    item.CtrlRamReplaceAddressSpaceId is not null)
                    ? _replace.ReplaceInputAuthoringRevision
                    : new AuthoringRevision(1);
        string icId = SelectedIc;
        string number = SelectedNumber;
        return lifecycle.StartAsync(
            Text,
            async (progress, isCurrent, cancellationToken) =>
            {
                var request = new FirmwareInspectionBatchRequest(
                    authoringRevision,
                    icId,
                    number,
                    mergeMode,
                    replaceMode,
                    items);
                foreach (FirmwareInspectionItemRequest item in items.Where(static item =>
                             item.AbMergeAddressSpaceId is not null ||
                             item.DpReplaceAddressSpaceId is not null ||
                             item.CtrlRamReplaceAddressSpaceId is not null ||
                             item.StandardMergeAddressSpaceId is not null))
                {
                    FindSlot(item.SlotId)?.SetInputInspectionPending(Text.FirmwareInspectionLoadingStatus);
                }
                try
                {
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                    FirmwareInspectionSnapshotInput[] inputs =
                    [
                        .. request.Items.Select(item => new FirmwareInspectionSnapshotInput(
                            item.SlotId,
                            item.Path,
                            item.TpPath,
                            item.CtrlRamRequest,
                            item.AbMergeAddressSpaceId,
                            item.AbMergeTopologyToken,
                            item.DpReplaceAddressSpaceId,
                            request.AuthoringRevision.Value,
                            item.StandardMergeAddressSpaceId,
                            item.CtrlRamReplaceAddressSpaceId,
                            item.InspectionLease?.ExactCapability)),
                    ];
                    FirmwareInspectionBatchResult result = await _compositionServices.FirmwareInspection
                        .InspectFirmwareBatchAsync(
                            request.IcId, inputs, cancellationToken, progress);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (result.InspectionsById.Count != request.Items.Count ||
                        request.Items.Any(item => !result.InspectionsById.ContainsKey(item.SlotId)))
                    {
                        throw new InvalidDataException("Firmware inspection returned an incomplete or unexpected result set.");
                    }
                    bool sourceFailed = result.FileStamps.Values.Any(static stamp => stamp is null);
                    if (isCurrent() && FirmwareInspectionProjection.IsCurrent(
                            request, result,
                            SelectedIc, SelectedNumber, SelectedMergeMode, SelectedReplaceMode,
                            FindSlot, MergeTpSlot.FilePath))
                    {
                        if (sourceFailed)
                        {
                            return new(false, "input.artifact.read-failed");
                        }
                        bool inputBatchAccepted = ApplyFirmwareInspectionBatch(request, result);
                        if (!inputBatchAccepted)
                        {
                            return new(false, "input.inspection.result-unavailable");
                        }
                        if (request.Items.Any(static item =>
                                item.CtrlRamReplaceAddressSpaceId is not null))
                        {
                            await _replace.RefreshCtrlRamActionReadinessAsync(
                                cancellationToken);
                        }
                        return new(true);
                    }
                    else if (isCurrent() && !result.IsContentStable &&
                        FirmwareInspectionProjection.ApplyStaleInputInspection(
                            MergeSlots.Concat(ReplaceSlots).Append(ReplaceBaseSlot),
                            request,
                            result,
                            Text))
                    {
                        _stateBindings.RefreshCommandState();
                        return new(false, "input.artifact.changed-during-inspection");
                    }
                    throw new OperationCanceledException(cancellationToken);
                }
                finally
                {
                    if (isCurrent())
                    {
                        foreach (FirmwareInspectionItemRequest item in items)
                        {
                            if (FindSlot(item.SlotId) is { IsInputInspectionPending: true } pending)
                            {
                                pending.SetInputInspection(
                                    FirmwareInputInspectionSeverity.Blocking,
                                    Text.FirmwareInspectionFailedTitle);
                            }
                        }
                        NotifySlotFileOutputNames();
                        _stateBindings.RefreshCommandState();
                    }
                }
            },
            cancellationToken);
    }

    private bool ApplyFirmwareInspectionBatch(
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
        FirmwareInspectionItemRequest[] replaceItems =
        [
            .. request.Items.Where(static item => item.DpReplaceAddressSpaceId is not null ||
                item.CtrlRamReplaceAddressSpaceId is not null),
        ];
        bool replaceFactsOnly = replaceItems.Length > 0 && replaceItems.All(item =>
            IsCtrlRamBaseFactsOnly(item, result.InspectionsById[item.SlotId]));
        bool replaceAccepted = replaceFactsOnly || _replace.TryCompleteReplaceInputBatch(
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
            else if (IsCtrlRamBaseFactsOnly(item, inspection))
            {
                slot.SetInputInspection(
                    FirmwareInputInspectionSeverity.Valid,
                    Text.FirmwareSlotVerifiedLabel);
            }

            if (item.PromptForMismatch)
            {
                if (ReconcileFirmwareIcMismatch(slot, inspection.DetectedIcId))
                {
                    return standardMergeAccepted && abMergeAccepted && replaceAccepted;
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
            _merge.RefreshMergeMemoryMapState();
        }

        if (request.Items.Any(static item => item.StandardMergeAddressSpaceId is not null))
        {
            _merge.RefreshStandardMergeAuthoringState();
        }

        if (request.Items.Any(static item => item.AbMergeAddressSpaceId is not null))
        {
            _merge.RefreshAbMergeAuthoringState();
        }

        if (ctrlRamBase.SlotId != CompositionSlotIds.ReplaceBase &&
            request.Items.Any(static item =>
                item.SlotId == CompositionSlotIds.ReplaceBase))
        {
            _replace.RefreshReplaceMemoryMapState();
        }

        _stateBindings.RefreshCommandState();
        return standardMergeAccepted && abMergeAccepted && replaceAccepted;
    }

    private static bool IsCtrlRamBaseFactsOnly(
        FirmwareInspectionItemRequest item,
        FirmwareInspectionSnapshot inspection)
    {
        return item.SlotId == CompositionSlotIds.ReplaceBase &&
            item.CtrlRamRequest is not null &&
            item.CtrlRamReplaceAddressSpaceId == CompositionAddressSpaceIds.ReferenceBase &&
            item.InspectionLease is null &&
            inspection.InputSlotStatus is null &&
            inspection.InputSlotCatalog is null;
    }

    internal void ApplyCtrlRamDisplayFromInspection(FirmwareInspectionSnapshot inspection)
    {
        _replace.ApplyCtrlRamInspectionDisplay(FirmwareInspectionProjection.ResolveCtrlRamDisplay(
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
                    FirmwareInspectionProjection.SupportsFacts(slot) ||
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
                    FirmwareInspectionProjection.SupportsFacts(slot),
                    applyVerified,
                    applyVerified && slot.SlotKind is FirmwareSlotKind.Tp or FirmwareSlotKind.Base);
                return item.StandardMergeAddressSpaceId is not null &&
                    standardMergeLeases.TryGetValue(
                        item.StandardMergeAddressSpaceId,
                        out AuthoringSlotInspectionLease? lease)
                            ? item with { InspectionLease = lease }
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
            _merge.RefreshMergeMemoryMapState();
        }

        if (slots.ContainsKey(CompositionSlotIds.ReplaceBase) && SelectedReplaceMode == ExperienceIds.DpReplace)
        {
            _replace.RefreshReplaceMemoryMapState();
        }

        return RunFirmwareInspectionAsync(items, CancellationToken.None);
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
                    ? item with { InspectionLease = lease }
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
            _stateBindings.RefreshCommandState();
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
        _ = RunFirmwareInspectionAsync(items, CancellationToken.None);
    }

    internal void InvalidateFirmwareInspection(
        bool clearBaseProjection = false,
        bool clearSlotProjections = false)
    {
        _merge.InspectionLifecycles.ForEach(static lifecycle => lifecycle.Invalidate());
        _replace.InspectionLifecycles.ForEach(static lifecycle => lifecycle.Invalidate());
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
    }

}
