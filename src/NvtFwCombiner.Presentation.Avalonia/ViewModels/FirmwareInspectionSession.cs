using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed class FirmwareInspectionSession
{
    private readonly Func<
        string,
        IReadOnlyList<WorkbenchFirmwareInspectionInput>,
        IReadOnlyList<WorkbenchFirmwareInspectionResult>> _reader;
    private readonly Dictionary<string, FirmwareFileProjection> _fileProjections =
        new(StringComparer.Ordinal);
    private BaseFirmwareInspectionCache? _baseCache;
    private long _generation;

    internal FirmwareInspectionSession(Func<
        string,
        IReadOnlyList<WorkbenchFirmwareInspectionInput>,
        IReadOnlyList<WorkbenchFirmwareInspectionResult>> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    internal long CurrentGeneration => Volatile.Read(ref _generation);

    internal long NextGeneration()
    {
        return Interlocked.Increment(ref _generation);
    }

    internal FirmwareInspectionBatchResult ReadBatch(FirmwareInspectionBatchRequest request)
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
        IReadOnlyList<WorkbenchFirmwareInspectionResult> inspections = _reader(request.IcId, inputs);
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

    internal void StoreProjection(
        string slotId,
        string path,
        FirmwareFileIdentity identity,
        WorkbenchFirmwareInspection inspection)
    {
        _fileProjections[slotId] = new FirmwareFileProjection(path, identity, inspection);
    }

    internal void StoreBase(string icId, string path, WorkbenchFirmwareInspection inspection)
    {
        _baseCache = new BaseFirmwareInspectionCache(icId, path, inspection);
    }

    internal bool TryGetFileLength(FirmwareSlotViewModel slot, out long length)
    {
        if (slot.FilePath is { } path &&
            _fileProjections.TryGetValue(slot.SlotId, out FirmwareFileProjection projection) &&
            projection.Matches(path) &&
            projection.FileIdentity.Exists)
        {
            length = projection.FileIdentity.Length;
            return true;
        }

        length = 0;
        return false;
    }

    internal bool TryGetInspection(
        string slotId,
        string? path,
        out WorkbenchFirmwareInspection inspection)
    {
        if (path is not null &&
            _fileProjections.TryGetValue(slotId, out FirmwareFileProjection projection) &&
            projection.Matches(path))
        {
            inspection = projection.Inspection;
            return true;
        }

        inspection = default!;
        return false;
    }

    internal bool TryGetBase(
        string icId,
        string? path,
        out WorkbenchFirmwareInspection inspection)
    {
        if (_baseCache is { } cache && cache.MatchesContext(icId, path))
        {
            inspection = cache.Inspection;
            return true;
        }

        inspection = default!;
        return false;
    }

    internal void RemoveProjection(string slotId)
    {
        _ = _fileProjections.Remove(slotId);
    }

    internal void ClearBase()
    {
        _baseCache = null;
    }

    internal void Invalidate(bool clearBaseCache, bool clearFileProjections)
    {
        _ = NextGeneration();
        if (clearBaseCache)
        {
            ClearBase();
        }

        if (clearFileProjections)
        {
            _fileProjections.Clear();
        }
    }

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

internal static class FirmwareInspectionRequestFactory
{
    internal static bool SupportsFacts(FirmwareSlotViewModel slot)
    {
        return slot.SlotKind is FirmwareSlotKind.Base or FirmwareSlotKind.Dp or FirmwareSlotKind.Tp;
    }

    internal static IReadOnlyList<FirmwareInspectionItemRequest> CreateSelectionItems(
        FirmwareSlotViewModel selectedSlot,
        bool includeCtrlRamBase,
        FirmwareInspectionRequestContext context)
    {
        List<FirmwareInspectionItemRequest> items = [];
        if (includeCtrlRamBase && !ReferenceEquals(selectedSlot, context.ReplaceBaseSlot))
        {
            items.Add(CreateItem(
                context.ReplaceBaseSlot,
                context,
                publishFacts: true,
                promptForMismatch: true,
                applyVerifiedContext: true));
        }

        items.Add(CreateItem(
            selectedSlot,
            context,
            publishFacts: SupportsFacts(selectedSlot),
            promptForMismatch: true,
            applyVerifiedContext: selectedSlot.SlotKind is FirmwareSlotKind.Tp or FirmwareSlotKind.Base));
        if (selectedSlot.SlotId == context.MergeTpSlotId && context.MergeDpSlot.HasFile)
        {
            items.Add(CreateItem(
                context.MergeDpSlot,
                context,
                publishFacts: true,
                promptForMismatch: false,
                applyVerifiedContext: false,
                tpPath: selectedSlot.FilePath));
        }

        return items;
    }

    internal static FirmwareInspectionItemRequest CreateItem(
        FirmwareSlotViewModel slot,
        FirmwareInspectionRequestContext context,
        bool publishFacts,
        bool promptForMismatch,
        bool applyVerifiedContext,
        string? tpPath = null)
    {
        string path = slot.FilePath!;
        string? dependentTpPath = slot.SlotId == context.MergeDpSlotId
            ? tpPath ?? context.MergeTpSlot.FilePath
            : null;
        WorkbenchCtrlRamInspectionRequest? ctrlRamRequest =
            slot.SlotId == context.ReplaceBaseSlotId && context.IsCtrlRamReplace
                ? new WorkbenchCtrlRamInspectionRequest(context.SelectedNumber)
                : null;
        string? abMergeAddressSpaceId = context.IsAbMerge
            ? context.AbAddressSpaceBySlotId.GetValueOrDefault(slot.SlotId)
            : null;
        bool applyWorkflowContext = applyVerifiedContext && !context.IsAbMerge;
        return new FirmwareInspectionItemRequest(
            slot.SlotId,
            slot.SlotKind,
            path,
            dependentTpPath,
            ctrlRamRequest,
            publishFacts,
            promptForMismatch,
            applyWorkflowContext,
            abMergeAddressSpaceId);
    }
}

internal static class FirmwareInspectionProjection
{
    internal static WorkbenchCtrlRamInspectionDisplay ResolveCtrlRamDisplay(
        WorkbenchFirmwareInspection inspection,
        string icId,
        string number)
    {
        return inspection.CtrlRamDisplay is { } inspectedDisplay &&
            string.Equals(inspectedDisplay.NumberToken, number, StringComparison.Ordinal)
                ? inspectedDisplay
                : WorkbenchCompositionService.ProjectCtrlRamInspectionDisplay(
                    icId,
                    number,
                    inspection.FirmwareConfig);
    }

    internal static void ApplyAbInputInspection(
        FirmwareSlotViewModel slot,
        WorkbenchAbMergeInputInspection inspection,
        ShellTextResources text)
    {
        slot.SetFirmwareFacts(CreateAbFirmwareFacts(inspection));
        slot.SetInputInspection(
            inspection.PrimaryIssue.Severity,
            text.GetAbInputInspectionStatus(inspection));
    }

    internal static IReadOnlyList<FirmwareSlotFactViewModel> CreateAbFirmwareFacts(
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
}

internal static class FirmwareOutputNamingProjection
{
    internal static string CreateFlashCodeOutputFileName(
        string icId,
        IEnumerable<FirmwareSlotViewModel> slots,
        FirmwareInspectionSession inspectionSession,
        WorkbenchCtrlRamFirmwareVersionEdit? edit)
    {
        ArgumentNullException.ThrowIfNull(slots);
        WorkbenchOutputNameInspectionCandidate[] candidates =
            [.. slots.Select(slot => ToCandidate(slot, inspectionSession))];
        return edit is null
            ? WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(icId, candidates).FileName
            : WorkbenchCompositionService.CreateFlashCodeOutputFileNameFromInspections(icId, candidates, edit).FileName;
    }

    private static WorkbenchOutputNameInspectionCandidate ToCandidate(
        FirmwareSlotViewModel slot,
        FirmwareInspectionSession inspectionSession)
    {
        WorkbenchFirmwareInspection? inspection = inspectionSession.TryGetInspection(
            slot.SlotId,
            slot.FilePath,
            out WorkbenchFirmwareInspection projected)
                ? projected
                : null;
        return new WorkbenchOutputNameInspectionCandidate(
            slot.SlotKind switch
            {
                FirmwareSlotKind.Dp => WorkbenchOutputNameCandidateKind.Dp,
                FirmwareSlotKind.Tp => WorkbenchOutputNameCandidateKind.Tp,
                FirmwareSlotKind.CtrlRam => WorkbenchOutputNameCandidateKind.CtrlRam,
                FirmwareSlotKind.Base => WorkbenchOutputNameCandidateKind.Base,
                FirmwareSlotKind.Unknown => WorkbenchOutputNameCandidateKind.Unknown,
                _ => WorkbenchOutputNameCandidateKind.Unknown,
            },
            inspection);
    }
}

internal readonly record struct FirmwareInspectionRequestContext(
    FirmwareSlotViewModel MergeDpSlot,
    FirmwareSlotViewModel MergeTpSlot,
    FirmwareSlotViewModel ReplaceBaseSlot,
    bool IsCtrlRamReplace,
    string SelectedNumber,
    bool IsAbMerge,
    IReadOnlyDictionary<string, string> AbAddressSpaceBySlotId,
    string MergeDpSlotId,
    string MergeTpSlotId,
    string ReplaceBaseSlotId);

internal readonly record struct FirmwareInspectionBatchRequest(
    long Generation,
    string IcId,
    string Number,
    string MergeMode,
    string ReplaceMode,
    IReadOnlyList<FirmwareInspectionItemRequest> Items);

internal readonly record struct FirmwareInspectionItemRequest(
    string SlotId,
    FirmwareSlotKind SlotKind,
    string Path,
    string? TpPath,
    WorkbenchCtrlRamInspectionRequest? CtrlRamRequest,
    bool PublishFacts,
    bool PromptForMismatch,
    bool ApplyVerifiedContext,
    string? AbMergeAddressSpaceId);

internal readonly record struct FirmwareInspectionBatchResult(
    IReadOnlyDictionary<string, WorkbenchFirmwareInspection> InspectionsById,
    IReadOnlyDictionary<string, FirmwareFileIdentity> FileIdentities,
    bool IsFileIdentityStable);
