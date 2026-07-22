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
        string? abMergeAddressSpaceId = IsAbCodeMergeModeSelected
            ? _abMergeAddressSpaceBySlotId.GetValueOrDefault(slot.SlotId)
            : null;
        return new FirmwareInspectionItemRequest(
            slot.SlotId,
            slot.SlotKind,
            path,
            dependentTpPath,
            ctrlRamRequest,
            publishFacts,
            promptForMismatch,
            applyVerifiedContext,
            abMergeAddressSpaceId);
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
                item.CtrlRamRequest,
                item.AbMergeAddressSpaceId)),
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
            string.Equals(request.MergeMode, SelectedMergeMode, StringComparison.Ordinal) &&
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

            if (inspection.AbMergeInput is { } abInput)
            {
                ApplyAbInputInspection(slot, abInput);
            }
            else if (item.PublishFacts)
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
                PromptForFirmwareNumberMismatch(slot, inspection.ContextSuggestion);
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

    private static IReadOnlyList<FirmwareSlotFactViewModel> CreateAbFirmwareFacts(
        WorkbenchAbMergeInputInspection inspection)
    {
        return
        [
            .. inspection.Versions.Select(version => new FirmwareSlotFactViewModel(
                ShellTextResources.GetAbVersionLabel(version.Kind),
                version.JiraBadge is null ? version.Value : $"{version.Value} · {version.JiraBadge}",
                version.IsUnknown)),
        ];
    }

    private void ApplyAbInputInspection(
        FirmwareSlotViewModel slot,
        WorkbenchAbMergeInputInspection inspection)
    {
        slot.SetFirmwareFacts(CreateAbFirmwareFacts(inspection));
        slot.SetInputInspection(
            inspection.PrimaryIssue.Severity,
            Text.GetAbInputInspectionStatus(inspection));
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
                    SlotSupportsFirmwareFacts(slot) ||
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
                    SlotSupportsFirmwareFacts(slot),
                    applyVerified,
                    applyVerified && slot.SlotKind is FirmwareSlotKind.Tp or FirmwareSlotKind.Base);
            }),
        ];
        foreach (FirmwareSlotViewModel slot in slots.Values)
        {
            _ = _firmwareFileProjections.Remove(slot.SlotId);
        }

        _baseFirmwareInspectionCache = slots.ContainsKey(ReplaceBaseSlotId)
            ? null
            : _baseFirmwareInspectionCache;

        NotifySlotFileOutputNames();
        if (slots.ContainsKey(MergeDpSlotId))
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

    private readonly record struct FirmwareInspectionBatchRequest(
        long Generation,
        string IcId,
        string Number,
        string MergeMode,
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
        bool ApplyVerifiedContext,
        string? AbMergeAddressSpaceId);

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
