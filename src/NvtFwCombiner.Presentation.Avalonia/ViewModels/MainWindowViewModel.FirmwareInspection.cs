using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly Func<
        string,
        IReadOnlyList<WorkbenchFirmwareInspectionInput>,
        IReadOnlyList<WorkbenchFirmwareInspectionResult>> _firmwareInspectionReader;
    private long _firmwareInspectionGeneration;
    private bool _isApplyingFirmwareInspectionContext;
    private bool _isRefreshingFirmwareInspectionContext;
    private BaseFirmwareInspectionCache? _baseFirmwareInspectionCache;
    private readonly Dictionary<string, FirmwareFileProjection> _firmwareFileProjections =
        new(StringComparer.Ordinal);

    /// <summary>True while the latest selected-file inspection is executing outside the dispatcher.</summary>
    public bool IsFirmwareInspectionLoading { get; private set; }

    internal Task FirmwareInspectionRefreshTask { get; private set; } = Task.CompletedTask;

    private static bool SlotSupportsFirmwareFacts(FirmwareSlotViewModel slot)
    {
        return slot.SlotKind is FirmwareSlotKind.Base or FirmwareSlotKind.Dp or FirmwareSlotKind.Tp;
    }

    /// <summary>Selects a slot file, then projects all affected firmware facts outside the UI dispatcher.</summary>
    public async Task SetSlotFileAsync(
        string slotId,
        string path,
        CancellationToken cancellationToken = default)
    {
        bool preservePendingCtrlRamBase = IsFirmwareInspectionLoading &&
            IsCtrlRamReplaceModeSelected &&
            ReplaceBaseSlot.HasFile;
        FirmwareSlotViewModel? slot = SelectSlotFile(slotId, path);
        if (slot is null)
        {
            return;
        }

        List<FirmwareInspectionItemRequest> items = CreateSelectionInspectionItems(
            slot,
            preservePendingCtrlRamBase);
        FirmwareInspectionRefreshTask = RunFirmwareInspectionAsync(items, cancellationToken);
        await FirmwareInspectionRefreshTask;
    }

    private List<FirmwareInspectionItemRequest> CreateSelectionInspectionItems(
        FirmwareSlotViewModel selectedSlot,
        bool includeCtrlRamBase)
    {
        List<FirmwareInspectionItemRequest> items = [];
        if (includeCtrlRamBase && !ReferenceEquals(selectedSlot, ReplaceBaseSlot))
        {
            items.Add(CreateFirmwareInspectionItem(
                ReplaceBaseSlot,
                publishFacts: true,
                promptForMismatch: true,
                applyVerifiedContext: true));
        }

        items.Add(CreateFirmwareInspectionItem(
            selectedSlot,
            publishFacts: SlotSupportsFirmwareFacts(selectedSlot),
            promptForMismatch: true,
            applyVerifiedContext: selectedSlot.SlotKind is FirmwareSlotKind.Tp or FirmwareSlotKind.Base));
        if (selectedSlot.SlotId == MergeTpSlotId && _mergeDpSlot.HasFile)
        {
            items.Add(CreateFirmwareInspectionItem(
                _mergeDpSlot,
                publishFacts: true,
                promptForMismatch: false,
                applyVerifiedContext: false,
                tpPath: selectedSlot.FilePath));
        }

        return items;
    }

    private FirmwareInspectionItemRequest CreateFirmwareInspectionItem(
        FirmwareSlotViewModel slot,
        bool publishFacts,
        bool promptForMismatch,
        bool applyVerifiedContext,
        string? tpPath = null)
    {
        string path = slot.FilePath!;
        string? dependentTpPath = slot.SlotId == MergeDpSlotId
            ? tpPath ?? _mergeTpSlot.FilePath
            : null;
        WorkbenchCtrlRamInspectionRequest? ctrlRamRequest = slot.SlotId == ReplaceBaseSlotId &&
            IsCtrlRamReplaceModeSelected
                ? new WorkbenchCtrlRamInspectionRequest(SelectedNumber)
                : null;
        return new FirmwareInspectionItemRequest(
            slot.SlotId,
            slot.SlotKind,
            path,
            dependentTpPath,
            ctrlRamRequest,
            publishFacts,
            promptForMismatch,
            applyVerifiedContext);
    }

    private async Task RunFirmwareInspectionAsync(
        IReadOnlyList<FirmwareInspectionItemRequest> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        long generation = Interlocked.Increment(ref _firmwareInspectionGeneration);
        var request = new FirmwareInspectionBatchRequest(
            generation,
            SelectedIc,
            SelectedNumber,
            SelectedReplaceMode,
            items);
        SetFirmwareInspectionLoading(true);
        try
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            FirmwareInspectionBatchResult result = await Task.Run(
                () => ReadFirmwareInspectionBatch(request),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (IsCurrentFirmwareInspection(request, result))
            {
                ApplyFirmwareInspectionBatch(request, result);
            }
        }
        finally
        {
            if (generation == Volatile.Read(ref _firmwareInspectionGeneration))
            {
                NotifySlotFileOutputNames();
                SetFirmwareInspectionLoading(false);
            }
        }
    }

    private FirmwareInspectionBatchResult ReadFirmwareInspectionBatch(FirmwareInspectionBatchRequest request)
    {
        string[] distinctPaths =
        [
            .. request.Items
                .SelectMany(static item => new[] { item.Path, item.TpPath })
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(static path => path!)
                .Distinct(StringComparer.Ordinal),
        ];
        Dictionary<string, FirmwareFileIdentity> before = distinctPaths.ToDictionary(
            static path => path,
            FirmwareFileIdentity.Capture,
            StringComparer.Ordinal);
        WorkbenchFirmwareInspectionInput[] inputs =
        [
            .. request.Items.Select(static item => new WorkbenchFirmwareInspectionInput(
                item.SlotId,
                item.Path,
                item.TpPath,
                item.CtrlRamRequest)),
        ];
        IReadOnlyList<WorkbenchFirmwareInspectionResult> inspections =
            _firmwareInspectionReader(request.IcId, inputs);
        var inspectionsById = inspections.ToDictionary(
            static result => result.InspectionId,
            static result => result.Inspection,
            StringComparer.Ordinal);
        if (inspectionsById.Count != request.Items.Count ||
            request.Items.Any(item => !inspectionsById.ContainsKey(item.SlotId)))
        {
            throw new InvalidOperationException("Firmware inspection batch did not return every requested slot.");
        }

        Dictionary<string, FirmwareFileIdentity> after = distinctPaths.ToDictionary(
            static path => path,
            FirmwareFileIdentity.Capture,
            StringComparer.Ordinal);
        bool isFileIdentityStable = distinctPaths.All(path => before[path].Equals(after[path]));
        return new FirmwareInspectionBatchResult(inspectionsById, after, isFileIdentityStable);
    }

    private bool IsCurrentFirmwareInspection(
        FirmwareInspectionBatchRequest request,
        FirmwareInspectionBatchResult result)
    {
        return request.Generation == Volatile.Read(ref _firmwareInspectionGeneration) &&
            result.IsFileIdentityStable &&
            string.Equals(request.IcId, SelectedIc, StringComparison.Ordinal) &&
            string.Equals(request.Number, SelectedNumber, StringComparison.Ordinal) &&
            string.Equals(request.ReplaceMode, SelectedReplaceMode, StringComparison.Ordinal) &&
            request.Items.All(item =>
                FindSlot(item.SlotId) is { } slot &&
                string.Equals(slot.FilePath, item.Path, StringComparison.Ordinal) &&
                (item.TpPath is null || string.Equals(_mergeTpSlot.FilePath, item.TpPath, StringComparison.Ordinal)));
    }

    private void ApplyFirmwareInspectionBatch(
        FirmwareInspectionBatchRequest request,
        FirmwareInspectionBatchResult result)
    {
        foreach (FirmwareInspectionItemRequest item in request.Items)
        {
            WorkbenchFirmwareInspection inspection = result.InspectionsById[item.SlotId];
            FirmwareFileIdentity identity = result.FileIdentities[item.Path];
            _firmwareFileProjections[item.SlotId] = new FirmwareFileProjection(
                item.Path,
                identity,
                inspection);
            if (item.SlotId == ReplaceBaseSlotId)
            {
                _baseFirmwareInspectionCache = new BaseFirmwareInspectionCache(
                    request.IcId,
                    item.Path,
                    inspection);
            }

            if (FindSlot(item.SlotId) is not { } slot)
            {
                continue;
            }

            if (item.PublishFacts)
            {
                slot.SetFirmwareFacts(item.SlotKind == FirmwareSlotKind.Dp
                    ? UiCompositionRunner.GetDpFirmwareSlotFacts(inspection)
                    : UiCompositionRunner.GetFirmwareSlotFacts(
                        inspection,
                        includeBaseFacts: item.SlotKind == FirmwareSlotKind.Base));
            }

            if (item.PromptForMismatch)
            {
                PromptForFirmwareIcMismatch(slot, inspection.DetectedIcId);
            }

            if (item.ApplyVerifiedContext && !IsFirmwareIcMismatchModalOpen)
            {
                ApplyVerifiedFirmwareContext(inspection.ContextSuggestion);
            }

            if (item.SlotId == ReplaceBaseSlotId && IsCtrlRamReplaceModeSelected)
            {
                ApplyCtrlRamDisplayFromInspection(inspection);
            }
        }

        if (request.Items.Any(static item => item.SlotId == MergeDpSlotId))
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
        WorkbenchCtrlRamInspectionDisplay display =
            inspection.CtrlRamDisplay is { } inspectedDisplay &&
            string.Equals(inspectedDisplay.NumberToken, SelectedNumber, StringComparison.Ordinal)
                ? inspectedDisplay
                : WorkbenchCompositionService.ProjectCtrlRamInspectionDisplay(
                    SelectedIc,
                    SelectedNumber,
                    inspection.FirmwareConfig);
        ApplyCtrlRamInspectionDisplay(display);
    }

    private bool TryGetInspectedFileLength(FirmwareSlotViewModel slot, out long length)
    {
        if (slot.FilePath is { } path &&
            _firmwareFileProjections.TryGetValue(slot.SlotId, out FirmwareFileProjection projection) &&
            projection.Matches(path) &&
            projection.FileIdentity.Exists)
        {
            length = projection.FileIdentity.Length;
            return true;
        }

        length = 0;
        return false;
    }

    private void QueueAllSelectedFirmwareInspections(string? applyVerifiedContextSlotId = null)
    {
        var slots = new Dictionary<string, FirmwareSlotViewModel>(StringComparer.Ordinal);
        foreach (FirmwareSlotViewModel slot in MergeSlots.Concat(ReplaceSlots).Concat([ReplaceBaseSlot]))
        {
            if (slot.HasFile &&
                (SlotSupportsFirmwareFacts(slot) ||
                    string.Equals(slot.SlotId, applyVerifiedContextSlotId, StringComparison.Ordinal)))
            {
                slots[slot.SlotId] = slot;
            }
        }

        IReadOnlyList<FirmwareInspectionItemRequest> items =
        [
            .. slots.Values.Select(slot => CreateFirmwareInspectionItem(
                slot,
                publishFacts: SlotSupportsFirmwareFacts(slot),
                promptForMismatch: string.Equals(
                    slot.SlotId,
                    applyVerifiedContextSlotId,
                    StringComparison.Ordinal),
                applyVerifiedContext: string.Equals(
                    slot.SlotId,
                    applyVerifiedContextSlotId,
                    StringComparison.Ordinal) &&
                    slot.SlotKind is FirmwareSlotKind.Tp or FirmwareSlotKind.Base)),
        ];
        FirmwareInspectionRefreshTask = RunFirmwareInspectionAsync(items, CancellationToken.None);
    }

    private void RefreshCtrlRamDisplayFromInspection()
    {
        if (!IsCtrlRamReplaceModeSelected || !ReplaceBaseSlot.HasFile)
        {
            return;
        }

        if (_baseFirmwareInspectionCache is { } cache &&
            cache.MatchesContext(SelectedIc, ReplaceBaseSlot.FilePath))
        {
            ApplyCtrlRamDisplayFromInspection(cache.Inspection);
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

    private void ApplyVerifiedFirmwareContext(WorkbenchFirmwareContextSuggestion? suggestion)
    {
        if (suggestion is null || string.Equals(SelectedNumber, suggestion.NumberToken, StringComparison.Ordinal))
        {
            return;
        }

        _isApplyingFirmwareInspectionContext = true;
        try
        {
            SelectedNumber = suggestion.NumberToken;
        }
        finally
        {
            _isApplyingFirmwareInspectionContext = false;
        }

        string selectionLabel = NumberSelectionChoices.FirstOrDefault(choice =>
            string.Equals(choice.Token, suggestion.NumberToken, StringComparison.Ordinal))?.DisplayLabel ??
            suggestion.NumberToken;
        SetShellToast(
            Text.ContextUpdatedToastTitle,
            Text.FormatVerifiedFirmwareContextToast(selectionLabel, suggestion.ChipNumber));
    }

    private void InvalidateFirmwareInspection(
        bool clearBaseCache = false,
        bool clearFileProjections = false)
    {
        _ = Interlocked.Increment(ref _firmwareInspectionGeneration);
        if (clearBaseCache)
        {
            _baseFirmwareInspectionCache = null;
        }

        if (clearFileProjections)
        {
            _firmwareFileProjections.Clear();
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
        OnPropertyChanged(nameof(MergeBuildActionTip));
        OnPropertyChanged(nameof(ReplaceBuildActionTip));
        RefreshCommandState();
    }

    private readonly record struct FirmwareInspectionBatchRequest(
        long Generation,
        string IcId,
        string Number,
        string ReplaceMode,
        IReadOnlyList<FirmwareInspectionItemRequest> Items);

    private readonly record struct FirmwareInspectionItemRequest(
        string SlotId,
        FirmwareSlotKind SlotKind,
        string Path,
        string? TpPath,
        WorkbenchCtrlRamInspectionRequest? CtrlRamRequest,
        bool PublishFacts,
        bool PromptForMismatch,
        bool ApplyVerifiedContext);

    private readonly record struct FirmwareInspectionBatchResult(
        IReadOnlyDictionary<string, WorkbenchFirmwareInspection> InspectionsById,
        IReadOnlyDictionary<string, FirmwareFileIdentity> FileIdentities,
        bool IsFileIdentityStable);

    private readonly record struct FirmwareFileProjection(
        string Path,
        FirmwareFileIdentity FileIdentity,
        WorkbenchFirmwareInspection Inspection)
    {
        internal bool Matches(string path)
        {
            return string.Equals(Path, path, StringComparison.Ordinal);
        }
    }

    private readonly record struct BaseFirmwareInspectionCache(
        string IcId,
        string Path,
        WorkbenchFirmwareInspection Inspection)
    {
        internal bool MatchesContext(string icId, string? path)
        {
            return string.Equals(IcId, icId, StringComparison.Ordinal) &&
                string.Equals(Path, path, StringComparison.Ordinal);
        }
    }
}
