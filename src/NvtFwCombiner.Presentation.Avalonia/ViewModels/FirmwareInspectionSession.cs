using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Metadata;
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

    internal AuthoringRevision CurrentAuthoringRevision { get; private set; } = new(1);

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
            .. request.Items.Select(item => new WorkbenchFirmwareInspectionInput(
                item.SlotId,
                item.Path,
                item.TpPath,
                item.CtrlRamRequest,
                item.AbMergeAddressSpaceId,
                item.AbMergeTopologyToken,
                item.DpReplaceAddressSpaceId,
                request.AuthoringRevision.Value,
                item.StandardMergeAddressSpaceId)),
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
        var unstableFilePaths = new HashSet<string>(
            distinctPaths.Where(path => !before[path].Equals(after[path])),
            StringComparer.Ordinal);
        return new FirmwareInspectionBatchResult(inspectionsById, after, unstableFilePaths);
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
        CurrentAuthoringRevision = CurrentAuthoringRevision.Next();
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
        string? dpReplaceAddressSpaceId = context.IsDpReplace
            ? ReferenceEquals(slot, context.ReplaceBaseSlot)
                ? WorkbenchAddressSpaceIds.ReferenceBase
                : slot.AddressSpaceId ?? throw new InvalidOperationException(
                    $"DP Replace slot '{slot.SlotId}' has no canonical address-space id.")
            : null;
        string? standardMergeAddressSpaceId = context.IsStandardMerge &&
            context.StandardMergeSlotIds.Contains(slot.SlotId, StringComparer.Ordinal)
            ? slot.AddressSpaceId
            : null;
        // Firmware metadata can request confirmation only when the current page exposes an
        // operator-selectable Number. A hidden control cannot be changed by a modal.
        bool applyWorkflowContext = applyVerifiedContext && context.IsNumberSelectorVisible;
        return new FirmwareInspectionItemRequest(
            slot.SlotId,
            slot.SlotKind,
            path,
            dependentTpPath,
            ctrlRamRequest,
            publishFacts,
            promptForMismatch,
            applyWorkflowContext,
            abMergeAddressSpaceId,
            context.AbMergeTopologyToken,
            dpReplaceAddressSpaceId,
            standardMergeAddressSpaceId);
    }
}

internal static class FirmwareInspectionProjection
{
    internal static bool IsCurrent(
        FirmwareInspectionBatchRequest request,
        FirmwareInspectionBatchResult result,
        long currentGeneration,
        string selectedIc,
        string selectedNumber,
        string selectedMergeMode,
        string selectedReplaceMode,
        Func<string, FirmwareSlotViewModel?> findSlot,
        string? currentTpPath)
    {
        return request.Generation == currentGeneration &&
            result.IsFileIdentityStable &&
            string.Equals(request.IcId, selectedIc, StringComparison.Ordinal) &&
            string.Equals(request.Number, selectedNumber, StringComparison.Ordinal) &&
            string.Equals(request.MergeMode, selectedMergeMode, StringComparison.Ordinal) &&
            string.Equals(request.ReplaceMode, selectedReplaceMode, StringComparison.Ordinal) &&
            request.Items.All(item =>
                findSlot(item.SlotId) is { } slot &&
                string.Equals(slot.FilePath, item.Path, StringComparison.Ordinal) &&
                (item.TpPath is null || string.Equals(currentTpPath, item.TpPath, StringComparison.Ordinal)));
    }

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
        WorkbenchFirmwareInspection inspection,
        ShellTextResources text)
    {
        slot.SetFirmwareFacts(CreateAbFirmwareFacts(inspection, text));
        slot.SetInputInspection(
            inspection.AbMergeInput!.PrimaryIssue.Severity,
            text.GetAbInputInspectionStatus(inspection.AbMergeInput));
    }

    internal static void ApplyInputSlotInspection(
        FirmwareSlotViewModel slot,
        AuthoringInputSlotStatus status,
        ShellTextResources text)
    {
        string readinessLabel = text.GetDpInputSelectionReadinessLabel(status.Readiness);
        string readinessDetail = text.GetDpInputSelectionReadinessDetail(status.SelectionReadiness);
        slot.SetSelectionReadiness(
            status.Readiness,
            readinessLabel,
            readinessDetail,
            text.GetInputSelectionReadinessAutomationText(readinessLabel, readinessDetail),
            status.CanSelect);

        if (!status.IsTerminal)
        {
            if (status.Readiness == ResolvedChildReadiness.Blocked)
            {
                slot.SetInputInspection(
                    WorkbenchInputInspectionSeverity.Blocking,
                    readinessDetail);
            }
            else
            {
                slot.ClearInputInspection();
            }

            return;
        }

        WorkbenchInputInspectionSeverity severity = status.InspectionLifecycle == AuthoringSlotLifecycle.Verified
            ? WorkbenchInputInspectionSeverity.Valid
            : status.InspectionLifecycle == AuthoringSlotLifecycle.Warning
                ? WorkbenchInputInspectionSeverity.Warning
                : WorkbenchInputInspectionSeverity.Blocking;
        slot.SetInputInspection(severity, text.GetInputSlotInspectionStatus(status));
    }

    internal static bool ApplyStaleInputInspection(
        IEnumerable<FirmwareSlotViewModel> slots,
        FirmwareInspectionBatchRequest request,
        FirmwareInspectionBatchResult result,
        ShellTextResources text)
    {
        bool applied = false;
        foreach (FirmwareInspectionItemRequest item in request.Items.Where(static item =>
                     item.AbMergeAddressSpaceId is not null ||
                     item.DpReplaceAddressSpaceId is not null ||
                     item.StandardMergeAddressSpaceId is not null))
        {
            FirmwareSlotViewModel? slot = slots.FirstOrDefault(candidate =>
                string.Equals(candidate.SlotId, item.SlotId, StringComparison.Ordinal));
            if (!result.UnstableFilePaths.Contains(item.Path) ||
                slot is null ||
                !string.Equals(slot.FilePath, item.Path, StringComparison.Ordinal) ||
                !slot.IsInputInspectionPending)
            {
                continue;
            }

            slot.SetInputInspection(
                WorkbenchInputInspectionSeverity.Blocking,
                text.FirmwareInspectionStaleFileStatus);
            applied = true;
        }

        return applied;
    }

    internal static IReadOnlyList<FirmwareSlotFactViewModel> CreateAbFirmwareFacts(
        WorkbenchFirmwareInspection inspection,
        ShellTextResources text)
    {
        WorkbenchAbMergeInputInspection abInput = inspection.AbMergeInput ??
            throw new ArgumentException("AB firmware facts require an AB input inspection.", nameof(inspection));
        return
        [
            .. abInput.Versions.Select(version => new FirmwareSlotFactViewModel(
                ShellTextResources.GetAbVersionLabel(version.Kind),
                version.IsUnknown
                    ? text.FirmwareSlotUnknownValueLabel
                    : version.JiraBadge is null ? version.Value : $"{version.Value} · {version.JiraBadge}",
                version.IsUnknown ? FirmwareSlotFactState.Unknown : FirmwareSlotFactState.Ordinary,
                version.IsUnknown ? text.FirmwareSlotUnknownValueLabel : null,
                version.IsUnknown ? text.FirmwareSlotUnknownFactDetail : null)),
            // AB owns the bank-specific TP A/TP B version labels. Reuse the standard
            // typed FWConfig projection for the remaining per-input TP identity facts.
            .. UiCompositionRunner.GetFirmwareSlotFacts(inspection).Where(static fact =>
                !string.Equals(fact.Label, "TP", StringComparison.Ordinal)),
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

    internal static string CreateCtrlRamReplaceOutputFileName(
        string icId,
        IEnumerable<FirmwareSlotViewModel> slots,
        FirmwareInspectionSession inspectionSession,
        WorkbenchCtrlRamFirmwareVersionEdit? edit)
    {
        ArgumentNullException.ThrowIfNull(slots);
        WorkbenchOutputNameInspectionCandidate[] candidates =
            [.. slots.Select(slot => ToCandidate(slot, inspectionSession))];
        return WorkbenchCompositionService.CreateCtrlRamReplaceOutputFileNameFromInspections(
            icId,
            candidates,
            edit).FileName;
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
    bool IsDpReplace,
    bool IsNumberSelectorVisible,
    string SelectedNumber,
    bool IsAbMerge,
    IReadOnlyDictionary<string, string> AbAddressSpaceBySlotId,
    string? AbMergeTopologyToken,
    string MergeDpSlotId,
    string MergeTpSlotId,
    string ReplaceBaseSlotId,
    bool IsStandardMerge,
    IReadOnlyList<string> StandardMergeSlotIds);

internal readonly record struct FirmwareInspectionBatchRequest(
    long Generation,
    AuthoringRevision AuthoringRevision,
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
    string? AbMergeAddressSpaceId,
    string? AbMergeTopologyToken,
    string? DpReplaceAddressSpaceId,
    string? StandardMergeAddressSpaceId,
    AuthoringSlotInspectionLease? StandardMergeInspectionLease = null);

internal readonly record struct FirmwareInspectionBatchResult(
    IReadOnlyDictionary<string, WorkbenchFirmwareInspection> InspectionsById,
    IReadOnlyDictionary<string, FirmwareFileIdentity> FileIdentities,
    IReadOnlySet<string> UnstableFilePaths)
{
    internal bool IsFileIdentityStable => UnstableFilePaths.Count == 0;
}
