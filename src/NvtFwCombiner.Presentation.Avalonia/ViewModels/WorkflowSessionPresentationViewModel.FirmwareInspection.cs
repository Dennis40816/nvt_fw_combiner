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
        if (ActiveInspectionContext is not { } context)
        {
            return;
        }

        GeneralMappingRowViewModel? mapping = _merge.GeneralMergeMappings
            .Cast<GeneralMappingRowViewModel>()
            .Concat(_replace.GeneralReplaceMappings)
            .FirstOrDefault(row => StringComparer.Ordinal.Equals(row.MappingId, slotId));
        if (mapping is not null)
        {
            bool isCurrentMapping = mapping switch
            {
                GeneralMergeMappingViewModel => context.IsMerge &&
                    context.Mode == ExperienceIds.GeneralMerge,
                GeneralReplaceMappingViewModel => context.IsReplace &&
                    context.Mode == ExperienceIds.GeneralReplace,
                _ => false,
            };
            if (!isCurrentMapping)
            {
                return;
            }

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

        FirmwareSlotViewModel? slot = SelectSlotFile(context, slotId, path);
        if (slot is null)
        {
            return;
        }

        if (context.IsReplace)
        {
            await RefreshSelectedReplaceFirmwareInspectionsAsync(slot.SlotId);
            return;
        }

        if ((context.IsStandardMerge && _merge.IsStandardMergeSlot(slot)) ||
            (context.IsAbMerge && AbMergeAddressSpaceBySlotId.ContainsKey(slot.SlotId)))
        {
            await RefreshSelectedMergeFirmwareInspectionsAsync(IsAbMergeContextActive ? slot.SlotId : null);
            return;
        }

        List<FirmwareInspectionItemRequest> items =
        [
            CreateFirmwareInspectionItem(
                context,
                slot,
                FirmwareInspectionProjection.SupportsFacts(slot),
                promptForMismatch: true,
                applyVerifiedContext: slot.SlotKind is FirmwareSlotKind.Tp or FirmwareSlotKind.Base),
        ];
        if (slot.SlotId == CompositionSlotIds.MergeTp && MergeDpSlot.HasFile)
        {
            items.Add(CreateFirmwareInspectionItem(
                context,
                MergeDpSlot,
                publishFacts: true,
                promptForMismatch: false,
                applyVerifiedContext: false,
                slot.FilePath));
        }
        await RunFirmwareInspectionAsync(context, items, cancellationToken);
    }

    private IEnumerable<FirmwareSlotViewModel> InspectionSlots(WorkflowInspectionContext context)
    {
        return context switch
        {
            { IsStandardMerge: true } or { IsAbMerge: true } => MergeSlots,
            { IsDpReplace: true } or { IsCtrlRamReplace: true } =>
                ReplaceSlots.Append(ReplaceBaseSlot).Distinct(),
            { IsGeneralReplace: true } => [ReplaceBaseSlot],
            _ => [],
        };
    }

    private IEnumerable<FirmwareSlotViewModel> AllInspectionSlots(WorkflowInspectionOwner owner)
    {
        return owner == WorkflowInspectionOwner.Merge
            ? _merge.StandardMergeSlots.Concat(AbMergeSlots).Distinct()
            : ReplaceSlots.Append(ReplaceBaseSlot).Distinct();
    }

    private FirmwareSlotViewModel? FindInspectionSlot(
        WorkflowInspectionContext context,
        string slotId)
    {
        return InspectionSlots(context).FirstOrDefault(slot =>
            string.Equals(slot.SlotId, slotId, StringComparison.Ordinal));
    }

    private FirmwareInspectionItemRequest CreateFirmwareInspectionItem(
        WorkflowInspectionContext context,
        FirmwareSlotViewModel slot,
        bool publishFacts,
        bool promptForMismatch,
        bool applyVerifiedContext,
        string? tpPath = null)
    {
        string path = slot.FilePath!;
        return new FirmwareInspectionItemRequest(
            slot.SlotId,
            slot.SlotKind,
            path,
            slot.SlotId == CompositionSlotIds.MergeDp ? tpPath ?? MergeTpSlot.FilePath : null,
            slot.SlotId == CompositionSlotIds.ReplaceBase && context.IsCtrlRamReplace
                ? new CtrlRamInspectionRequest(SelectedNumber)
                : null,
            publishFacts,
            promptForMismatch,
            applyVerifiedContext && IsNumberSelectorVisible,
            context.IsAbMerge ? AbMergeAddressSpaceBySlotId.GetValueOrDefault(slot.SlotId) : null,
            context.IsAbMerge ? _merge.GetSelectedAbMergeTopologyToken() : null,
            context.IsDpReplace
                ? ReferenceEquals(slot, ReplaceBaseSlot)
                    ? CompositionAddressSpaceIds.ReferenceBase
                    : slot.AddressSpaceId ?? throw new InvalidOperationException(
                        $"DP Replace slot '{slot.SlotId}' has no canonical address-space id.")
                : null,
            context.IsStandardMerge && _merge.IsStandardMergeSlot(slot) ? slot.AddressSpaceId : null,
            context.IsCtrlRamReplace && (ReferenceEquals(slot, ReplaceBaseSlot) ||
                slot.ReplaceInputRole == ReplaceInputRole.CtrlRam)
                ? ReferenceEquals(slot, ReplaceBaseSlot)
                    ? CompositionAddressSpaceIds.ReferenceBase
                    : slot.AddressSpaceId
                : null);
    }

    private Task RunFirmwareInspectionAsync(
        WorkflowInspectionContext context,
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return Task.CompletedTask;
        }

        WorkflowInspectionLifecycle lifecycle = context.IsMerge
            ? _merge.InspectionLifecycles[context.Mode]
            : _replace.InspectionLifecycles[context.Mode];
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
                    context,
                    items);
                foreach (FirmwareInspectionItemRequest item in items.Where(static item =>
                             item.AbMergeAddressSpaceId is not null ||
                             item.DpReplaceAddressSpaceId is not null ||
                             item.CtrlRamReplaceAddressSpaceId is not null ||
                             item.StandardMergeAddressSpaceId is not null))
                {
                    FindInspectionSlot(context, item.SlotId)?
                        .SetInputInspectionPending(Text.FirmwareInspectionLoadingStatus);
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
                            SelectedIc, SelectedNumber, InspectionContext(request.Context.Owner),
                            slotId => FindInspectionSlot(request.Context, slotId),
                            MergeTpSlot.FilePath))
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
                            InspectionSlots(request.Context),
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
                            if (FindInspectionSlot(context, item.SlotId) is
                                { IsInputInspectionPending: true } pending)
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
        FirmwareInspectionItemRequest ctrlRamBase = request.Items.FirstOrDefault(static item =>
            item.SlotId == CompositionSlotIds.ReplaceBase &&
            item.CtrlRamReplaceAddressSpaceId == CompositionAddressSpaceIds.ReferenceBase);
        if (ctrlRamBase.SlotId == CompositionSlotIds.ReplaceBase)
        {
            ApplyCtrlRamDisplayFromInspection(result.InspectionsById[ctrlRamBase.SlotId]);
        }
        foreach (FirmwareInspectionItemRequest item in request.Items)
        {
            FirmwareInspectionSnapshot inspection = result.InspectionsById[item.SlotId];
            if (FindInspectionSlot(request.Context, item.SlotId) is not { } slot)
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
                if (ReconcileFirmwareIcMismatch(request.Context, slot, inspection.DetectedIcId))
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
        return ActiveInspectionContext is { IsMerge: true } context
            ? RefreshSelectedFirmwareInspectionsAsync(
                context,
                context is { IsStandardMerge: true } or { IsAbMerge: true }
                    ? MergeSlots
                    : [],
                applyVerifiedContextSlotId)
            : Task.CompletedTask;
    }

    internal Task RefreshSelectedReplaceFirmwareInspectionsAsync(
        string? applyVerifiedContextSlotId = null)
    {
        return ActiveInspectionContext is { IsReplace: true } context
            ? RefreshSelectedFirmwareInspectionsAsync(
                context,
                ReplaceSlots.Concat([ReplaceBaseSlot]),
                applyVerifiedContextSlotId)
            : Task.CompletedTask;
    }

    private Task RefreshSelectedFirmwareInspectionsAsync(
        WorkflowInspectionContext context,
        IEnumerable<FirmwareSlotViewModel> candidateSlots,
        string? applyVerifiedContextSlotId = null)
    {
        var slots = candidateSlots
            .Where(static slot => slot.HasFile)
            .DistinctBy(static slot => slot.SlotId, StringComparer.Ordinal)
            .ToDictionary(static slot => slot.SlotId, StringComparer.Ordinal);
        IReadOnlyDictionary<string, AuthoringSlotInspectionLease> standardMergeLeases =
            context.IsStandardMerge
                ? _merge.BeginStandardMergeSlotInspections(
                    slots.Values.Where(_merge.IsStandardMergeSlot))
                : new Dictionary<string, AuthoringSlotInspectionLease>(StringComparer.Ordinal);
        if (context.IsStandardMerge)
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
                    context,
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
        items = AttachAbMergeInspectionLeases(context, items);
        if (context.IsReplace)
        {
            items = _replace.AttachReplaceInspectionLeases(items, slots.Values);
        }
        foreach (FirmwareSlotViewModel slot in slots.Values)
        {
            slot.ClearCurrentInspectionProjection();
        }

        NotifySlotFileOutputNames();
        if (context.IsMerge &&
            (slots.ContainsKey(CompositionSlotIds.MergeDp) ||
                (context.IsAbMerge &&
                    slots.Keys.Any(AbMergeAddressSpaceBySlotId.ContainsKey))))
        {
            _merge.RefreshMergeMemoryMapState();
        }

        if (context.IsReplace &&
            slots.ContainsKey(CompositionSlotIds.ReplaceBase) &&
            context.IsDpReplace)
        {
            _replace.RefreshReplaceMemoryMapState();
        }

        return RunFirmwareInspectionAsync(context, items, CancellationToken.None);
    }

    private IReadOnlyList<FirmwareInspectionItemRequest> AttachAbMergeInspectionLeases(
        WorkflowInspectionContext context,
        IReadOnlyList<FirmwareInspectionItemRequest> items)
    {
        if (!context.IsAbMerge)
        {
            return items;
        }

        FirmwareSlotViewModel[] slots =
        [
            .. items
                .Where(static item => item.AbMergeAddressSpaceId is not null)
                .Select(item => FindInspectionSlot(context, item.SlotId))
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
        if (ActiveInspectionContext is not { IsCtrlRamReplace: true } context ||
            !ReplaceBaseSlot.HasFile)
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
                context,
                ReplaceBaseSlot,
                publishFacts: false,
                promptForMismatch: false,
                applyVerifiedContext: false),
        ];
        _ = RunFirmwareInspectionAsync(context, items, CancellationToken.None);
    }

    internal void InvalidateFirmwareInspection(
        WorkflowInspectionOwner? owner = null,
        bool clearBaseProjection = false,
        bool clearSlotProjections = false)
    {
        if (owner is null or WorkflowInspectionOwner.Merge)
        {
            _merge.InspectionLifecycles.ForEach(static lifecycle => lifecycle.Invalidate());
        }
        if (owner is null or WorkflowInspectionOwner.Replace)
        {
            _replace.InspectionLifecycles.ForEach(static lifecycle => lifecycle.Invalidate());
        }

        if (owner != WorkflowInspectionOwner.Merge && clearBaseProjection)
        {
            ReplaceBaseSlot.ClearCurrentInspectionProjection();
        }

        if (clearSlotProjections)
        {
            IEnumerable<FirmwareSlotViewModel> slots = owner is { } pageOwner
                ? AllInspectionSlots(pageOwner)
                : AllInspectionSlots(WorkflowInspectionOwner.Merge)
                    .Concat(AllInspectionSlots(WorkflowInspectionOwner.Replace));
            foreach (FirmwareSlotViewModel slot in slots)
            {
                slot.ClearCurrentInspectionProjection();
                slot.ClearInputInspection();
            }
        }
    }

}
