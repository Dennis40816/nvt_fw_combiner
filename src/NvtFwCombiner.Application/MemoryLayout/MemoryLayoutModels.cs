using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.MemoryLayout;

/// <summary>Primary source-neutral content role for one projected segment.</summary>
public enum MemoryContentRole
{
    /// <summary>Display or Initial Code.</summary>
    Dp,
    /// <summary>Normal touch firmware.</summary>
    Tp,
    /// <summary>Backup touch firmware.</summary>
    TpBackup,
    /// <summary>LDC.</summary>
    Ldc,
    /// <summary>General or otherwise neutral data.</summary>
    General,
    /// <summary>Reserved or unmapped structure.</summary>
    Reserved,
    /// <summary>CtrlRAM; subtype remains a separate future fact.</summary>
    CtrlRam,
}

/// <summary>Planned workflow effect, independent from content and observed bytes.</summary>
public enum MemoryWorkflowDisposition
{
    /// <summary>Resolved physical structure without a selected effect.</summary>
    Resolved,
    /// <summary>Blank-initialized Merge structure.</summary>
    Blank,
    /// <summary>Selected Merge input will write this range.</summary>
    WillWrite,
    /// <summary>Reference bytes remain preserved by Replace.</summary>
    Kept,
    /// <summary>Selected Replace input will replace this range.</summary>
    WillReplace,
    /// <summary>DP AB seed range.</summary>
    DpAbBase,
    /// <summary>TP normal-code overlay in the A bank.</summary>
    TpaOverlay,
    /// <summary>TP backup-code overlay in the B bank.</summary>
    TpbOverlay,
}

/// <summary>Physical endpoint identity independent from content role.</summary>
public enum MemoryEndpointIdentity
{
    /// <summary>No endpoint distinction applies.</summary>
    NotApplicable,
    /// <summary>Single endpoint.</summary>
    SingleEndpoint,
    /// <summary>Master endpoint.</summary>
    Master,
    /// <summary>Slave endpoint.</summary>
    Slave,
}

/// <summary>A/B bank identity independent from content role.</summary>
public enum MemoryBankIdentity
{
    /// <summary>No bank distinction applies.</summary>
    NotApplicable,
    /// <summary>A bank.</summary>
    A,
    /// <summary>B bank.</summary>
    B,
}

/// <summary>Declared processor effect independent from workflow disposition.</summary>
public enum MemoryProcessorEffect
{
    /// <summary>No processor effect contributes.</summary>
    None,
    /// <summary>A declared processor has write authority.</summary>
    DeclaredWrite,
}

/// <summary>Highest diagnostic severity attached to one projected item.</summary>
public enum MemoryDiagnosticSeverity
{
    /// <summary>No diagnostic applies.</summary>
    None,
    /// <summary>Informational prerequisite.</summary>
    Information,
    /// <summary>Non-blocking warning.</summary>
    Warning,
    /// <summary>Blocking error.</summary>
    Error,
}

/// <summary>Observed byte-comparison state, available only after byte evidence exists.</summary>
public enum MemoryObservedChange
{
    /// <summary>No byte comparison has been performed.</summary>
    NotObserved,
    /// <summary>Compared bytes are unchanged.</summary>
    Unchanged,
    /// <summary>Compared bytes changed.</summary>
    Changed,
}

/// <summary>Selection state independent from content and workflow effects.</summary>
public enum MemorySelectionState
{
    /// <summary>No contributing authoring input is selected.</summary>
    NotSelected,
    /// <summary>The contributing authoring input is selected and admitted.</summary>
    Selected,
}

/// <summary>Focus state independent from content and workflow effects.</summary>
public enum MemoryFocusState
{
    /// <summary>The segment is not focused.</summary>
    NotFocused,
    /// <summary>The segment is focused.</summary>
    Focused,
}

/// <summary>Non-geometric readiness for an unresolved artifact or part.</summary>
public enum MemoryLayoutReadiness
{
    /// <summary>More authoring input or inspection is required.</summary>
    PendingInput,
    /// <summary>A supplied input has a blocking issue.</summary>
    Blocked,
}

/// <summary>Typed prerequisite for one non-geometric item.</summary>
public enum MemoryLayoutPrerequisite
{
    /// <summary>Select the required input.</summary>
    SelectInput,
    /// <summary>Complete inspection of the selected input.</summary>
    CompleteInspection,
    /// <summary>Resolve a blocking input issue.</summary>
    ResolveInputIssue,
}

/// <summary>Typed next action for one non-geometric item.</summary>
public enum MemoryLayoutNextAction
{
    /// <summary>Select an input file.</summary>
    SelectInput,
    /// <summary>Start input inspection.</summary>
    RunInspection,
    /// <summary>Wait for the active inspection.</summary>
    WaitForInspection,
    /// <summary>Review and correct the input issue.</summary>
    ReviewInputIssue,
}

/// <summary>
/// Identity-pinned reference to the actual issue that blocks one authoring slot.
/// The referenced inspection or validation result remains the diagnostic owner.
/// </summary>
public sealed record MemoryLayoutBlockedIssueReference
{
    internal MemoryLayoutBlockedIssueReference(
        ResolutionToken resolutionToken,
        AuthoringRevision authoringRevision,
        AuthoringSlotPublicationIdentity slotIdentity,
        AuthoringSlotIssueReference issue)
    {
        ArgumentNullException.ThrowIfNull(slotIdentity);
        ArgumentNullException.ThrowIfNull(issue);
        if (slotIdentity.SelectedPath is null || slotIdentity.FileStamp is null)
        {
            throw new ArgumentException(
                "A blocked issue requires the exact selected-file identity.",
                nameof(slotIdentity));
        }

        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        SlotIdentity = slotIdentity;
        Issue = issue;
    }

    /// <summary>Canonical catalog publication identity.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Exact authoring-input revision.</summary>
    public AuthoringRevision AuthoringRevision { get; }

    /// <summary>Exact slot, path, and host-captured file identity.</summary>
    public AuthoringSlotPublicationIdentity SlotIdentity { get; }

    /// <summary>Reference to the actual separately owned diagnostic issue.</summary>
    public AuthoringSlotIssueReference Issue { get; }
}

/// <summary>A kept range subordinate to a primary segment, not a canonical region.</summary>
public sealed class MemoryLayoutPreservationDetail
{
    /// <summary>Creates one checked kept-range detail from canonical caller facts.</summary>
    public MemoryLayoutPreservationDetail(
        string detailId,
        int blockIndex,
        MemoryEndpointIdentity endpoint,
        string sourceSpaceId,
        ByteRange artifactRelativeRange,
        ByteRange resolvedRange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detailId);
        ArgumentOutOfRangeException.ThrowIfNegative(blockIndex);
        MemoryLayoutGuard.Defined(endpoint, nameof(endpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSpaceId);
        DetailId = detailId;
        BlockIndex = blockIndex;
        Endpoint = endpoint;
        SourceSpaceId = sourceSpaceId;
        ArtifactRelativeRange = artifactRelativeRange;
        ResolvedRange = resolvedRange;
    }

    /// <summary>Stable detail identity.</summary>
    public string DetailId { get; }
    /// <summary>Zero-based DiffDLM block index when applicable.</summary>
    public int BlockIndex { get; }
    /// <summary>Endpoint owning the kept detail.</summary>
    public MemoryEndpointIdentity Endpoint { get; }
    /// <summary>Source artifact address-space identity.</summary>
    public string SourceSpaceId { get; }
    /// <summary>Half-open range relative to the source artifact.</summary>
    public ByteRange ArtifactRelativeRange { get; }
    /// <summary>Resolved half-open range inside the primary output segment.</summary>
    public ByteRange ResolvedRange { get; }
    /// <summary>Subordinate details always preserve reference bytes.</summary>
    public MemoryWorkflowDisposition Disposition { get; } = MemoryWorkflowDisposition.Kept;
}

/// <summary>One immutable checked segment in the canonical output address space.</summary>
public sealed class MemoryLayoutSegment
{
    private MemoryLayoutSegment(
        string segmentId,
        string addressSpaceId,
        ByteRange range,
        FirmwareRegion canonicalRegion,
        MemoryContentRole contentRole,
        MemoryWorkflowDisposition disposition,
        MemoryEndpointIdentity endpoint,
        MemoryBankIdentity bank,
        MemoryProcessorEffect processorEffect,
        MemoryDiagnosticSeverity diagnosticSeverity,
        MemoryObservedChange observedChange,
        MemorySelectionState selection,
        MemoryFocusState focus,
        string? sourceSpaceId,
        string? sourceSlotId,
        IEnumerable<string> contributingOperationIds,
        IEnumerable<MemoryLayoutPreservationDetail> preservationDetails)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segmentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentNullException.ThrowIfNull(canonicalRegion);
        if (!canonicalRegion.Range.Contains(range))
        {
            throw new ArgumentException(
                "Projected range must remain inside its canonical region.",
                nameof(range));
        }

        MemoryLayoutGuard.Defined(contentRole, nameof(contentRole));
        MemoryLayoutGuard.Defined(disposition, nameof(disposition));
        MemoryLayoutGuard.Defined(endpoint, nameof(endpoint));
        MemoryLayoutGuard.Defined(bank, nameof(bank));
        MemoryLayoutGuard.Defined(processorEffect, nameof(processorEffect));
        MemoryLayoutGuard.Defined(diagnosticSeverity, nameof(diagnosticSeverity));
        MemoryLayoutGuard.Defined(observedChange, nameof(observedChange));
        MemoryLayoutGuard.Defined(selection, nameof(selection));
        MemoryLayoutGuard.Defined(focus, nameof(focus));
        if (sourceSlotId is not null && sourceSpaceId is null)
        {
            throw new ArgumentException(
                "A source slot requires a source address space.",
                nameof(sourceSlotId));
        }

        string[] operationIds = [.. contributingOperationIds];
        if (operationIds.Any(string.IsNullOrWhiteSpace) ||
            operationIds.Distinct(StringComparer.Ordinal).Count() != operationIds.Length)
        {
            throw new ArgumentException(
                "Contributing operation ids must be non-empty and unique.",
                nameof(contributingOperationIds));
        }

        MemoryLayoutPreservationDetail[] details =
            MemoryLayoutGuard.PreservationDetails(preservationDetails, range);
        SegmentId = segmentId;
        AddressSpaceId = addressSpaceId;
        Range = range;
        CanonicalRegion = canonicalRegion;
        ContentRole = contentRole;
        Disposition = disposition;
        Endpoint = endpoint;
        Bank = bank;
        ProcessorEffect = processorEffect;
        DiagnosticSeverity = diagnosticSeverity;
        ObservedChange = observedChange;
        Selection = selection;
        Focus = focus;
        SourceSpaceId = sourceSpaceId;
        SourceSlotId = sourceSlotId;
        ContributingOperationIds = Array.AsReadOnly(operationIds);
        PreservationDetails = Array.AsReadOnly(details);
    }

    /// <summary>Stable projection-local identity.</summary>
    public string SegmentId { get; }
    /// <summary>Canonical output address-space identity.</summary>
    public string AddressSpaceId { get; }
    /// <summary>Resolved half-open output range.</summary>
    public ByteRange Range { get; }
    /// <summary>Exact canonical physical-region reference.</summary>
    public FirmwareRegion CanonicalRegion { get; }
    /// <summary>Primary content role.</summary>
    public MemoryContentRole ContentRole { get; }
    /// <summary>Planned workflow disposition.</summary>
    public MemoryWorkflowDisposition Disposition { get; }
    /// <summary>Endpoint identity.</summary>
    public MemoryEndpointIdentity Endpoint { get; }
    /// <summary>Bank identity.</summary>
    public MemoryBankIdentity Bank { get; }
    /// <summary>Declared processor effect.</summary>
    public MemoryProcessorEffect ProcessorEffect { get; }
    /// <summary>Highest attached diagnostic severity.</summary>
    public MemoryDiagnosticSeverity DiagnosticSeverity { get; }
    /// <summary>Observed byte-comparison state.</summary>
    public MemoryObservedChange ObservedChange { get; }
    /// <summary>Contributing authoring-selection state.</summary>
    public MemorySelectionState Selection { get; }
    /// <summary>Current focus state.</summary>
    public MemoryFocusState Focus { get; }
    /// <summary>Contributing source address space, if any.</summary>
    public string? SourceSpaceId { get; }
    /// <summary>Contributing canonical input slot, if any.</summary>
    public string? SourceSlotId { get; }
    /// <summary>Ordered operation identities contributing to this segment.</summary>
    public IReadOnlyList<string> ContributingOperationIds { get; }
    /// <summary>Typed kept details subordinate to this primary segment.</summary>
    public IReadOnlyList<MemoryLayoutPreservationDetail> PreservationDetails { get; }

    internal static MemoryLayoutSegment Create(
        string segmentId,
        string addressSpaceId,
        ByteRange range,
        FirmwareRegion canonicalRegion,
        MemoryContentRole contentRole,
        MemoryWorkflowDisposition disposition,
        MemoryEndpointIdentity endpoint,
        MemoryBankIdentity bank,
        MemoryProcessorEffect processorEffect,
        MemoryDiagnosticSeverity diagnosticSeverity,
        MemoryObservedChange observedChange,
        MemorySelectionState selection,
        MemoryFocusState focus,
        string? sourceSpaceId,
        string? sourceSlotId,
        IEnumerable<string> contributingOperationIds,
        IEnumerable<MemoryLayoutPreservationDetail> preservationDetails)
    {
        return new(
            segmentId,
            addressSpaceId,
            range,
            canonicalRegion,
            contentRole,
            disposition,
            endpoint,
            bank,
            processorEffect,
            diagnosticSeverity,
            observedChange,
            selection,
            focus,
            sourceSpaceId,
            sourceSlotId,
            contributingOperationIds,
            preservationDetails);
    }
}

/// <summary>One unresolved non-geometric artifact or part.</summary>
public sealed class MemoryLayoutPendingItem
{
    internal MemoryLayoutPendingItem(
        CompiledInputSlotRequirement requirement,
        MemoryLayoutReadiness readiness,
        MemoryLayoutPrerequisite prerequisite,
        MemoryLayoutNextAction nextAction,
        long? knownInputLength,
        MemoryDiagnosticSeverity diagnosticSeverity,
        MemoryLayoutBlockedIssueReference? blockedIssue)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        MemoryLayoutGuard.Defined(readiness, nameof(readiness));
        MemoryLayoutGuard.Defined(prerequisite, nameof(prerequisite));
        MemoryLayoutGuard.Defined(nextAction, nameof(nextAction));
        MemoryLayoutGuard.Defined(diagnosticSeverity, nameof(diagnosticSeverity));
        if (knownInputLength is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(knownInputLength));
        }

        bool hasBlockedIssue = blockedIssue is not null;
        if (hasBlockedIssue != (readiness == MemoryLayoutReadiness.Blocked))
        {
            throw new ArgumentException(
                "Only blocked items require one identity-pinned issue reference.",
                nameof(blockedIssue));
        }

        SlotId = requirement.SlotId;
        Role = requirement.Role;
        ArtifactClass = requirement.ArtifactClass;
        Readiness = readiness;
        Prerequisite = prerequisite;
        NextAction = nextAction;
        KnownInputLength = knownInputLength;
        DiagnosticSeverity = diagnosticSeverity;
        BlockedIssue = blockedIssue;
    }

    /// <summary>Canonical input-slot identity.</summary>
    public string SlotId { get; }
    /// <summary>Canonical source role.</summary>
    public string Role { get; }
    /// <summary>Closed source-artifact class.</summary>
    public CompiledInputArtifactClass ArtifactClass { get; }
    /// <summary>Pending or blocked readiness.</summary>
    public MemoryLayoutReadiness Readiness { get; }
    /// <summary>Typed prerequisite.</summary>
    public MemoryLayoutPrerequisite Prerequisite { get; }
    /// <summary>Typed next action.</summary>
    public MemoryLayoutNextAction NextAction { get; }
    /// <summary>Known input length without a fabricated destination.</summary>
    public long? KnownInputLength { get; }
    /// <summary>Typed diagnostic severity.</summary>
    public MemoryDiagnosticSeverity DiagnosticSeverity { get; }
    /// <summary>Actual identity-pinned issue reference for a blocked item.</summary>
    public MemoryLayoutBlockedIssueReference? BlockedIssue { get; }
}

/// <summary>One disposable immutable layout projection for an authoring revision.</summary>
public sealed class MemoryLayoutSnapshot
{
    internal MemoryLayoutSnapshot(
        ResolvedCapability capability,
        ActiveSessionSnapshot authoring,
        FirmwareImageMap map,
        long capacity,
        IReadOnlyList<MemoryLayoutSegment> beforeSegments,
        IReadOnlyList<MemoryLayoutSegment> afterSegments,
        IEnumerable<MemoryLayoutPendingItem> pendingItems)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(authoring);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        FirmwareRegion[] regions = [.. map.Regions];
        MemoryLayoutSegment[] before = [.. beforeSegments];
        MemoryLayoutSegment[] after = ReferenceEquals(beforeSegments, afterSegments)
            ? before
            : [.. afterSegments];
        MemoryLayoutPendingItem[] pending = [.. pendingItems];
        ValidateCoverage(before, map.AddressSpaceId, capacity, regions);
        ValidateCoverage(after, map.AddressSpaceId, capacity, regions);
        if (pending.Select(static item => item.SlotId)
            .Distinct(StringComparer.Ordinal).Count() != pending.Length)
        {
            throw new ArgumentException("Pending items must be unique by slot.", nameof(pendingItems));
        }

        RouteId = capability.Identity.RouteId;
        CapabilityFingerprint = capability.CapabilityFingerprint;
        CompilationFingerprint =
            capability.CompiledComposition.CompilationFingerprint;
        ResolutionToken = capability.ResolutionToken;
        AuthoringRevision = authoring.AuthoringRevision;
        MapId = map.MapId;
        AddressSpaceId = map.AddressSpaceId;
        Capacity = capacity;
        CanonicalRegions = Array.AsReadOnly(regions);
        BeforeSegments = Array.AsReadOnly(before);
        AfterSegments = ReferenceEquals(before, after)
            ? BeforeSegments
            : Array.AsReadOnly(after);
        PendingItems = Array.AsReadOnly(pending);
    }

    /// <summary>Exact canonical route identity.</summary>
    public string RouteId { get; }
    /// <summary>Reviewed capability-definition fingerprint.</summary>
    public string CapabilityFingerprint { get; }
    /// <summary>Exact compiled-composition fingerprint used by this projection.</summary>
    public string CompilationFingerprint { get; }
    /// <summary>Catalog publication identity.</summary>
    public ResolutionToken ResolutionToken { get; }
    /// <summary>Projected authoring revision.</summary>
    public AuthoringRevision AuthoringRevision { get; }
    /// <summary>Canonical image-map identity.</summary>
    public string MapId { get; }
    /// <summary>Canonical physical output address-space identity.</summary>
    public string AddressSpaceId { get; }
    /// <summary>Exact resolved output capacity.</summary>
    public long Capacity { get; }
    /// <summary>Exact canonical region references, including nested regions.</summary>
    public IReadOnlyList<FirmwareRegion> CanonicalRegions { get; }
    /// <summary>Coverage seeded from workflow initialization.</summary>
    public IReadOnlyList<MemoryLayoutSegment> BeforeSegments { get; }
    /// <summary>Coverage after admitted selected operations.</summary>
    public IReadOnlyList<MemoryLayoutSegment> AfterSegments { get; }
    /// <summary>Unresolved non-geometric items.</summary>
    public IReadOnlyList<MemoryLayoutPendingItem> PendingItems { get; }

    private static void ValidateCoverage(
        MemoryLayoutSegment[] segments,
        string addressSpaceId,
        long capacity,
        FirmwareRegion[] canonicalRegions)
    {
        if (segments.Length == 0 ||
            segments.Select(static segment => segment.SegmentId)
                .Distinct(StringComparer.Ordinal).Count() != segments.Length)
        {
            throw new ArgumentException("Coverage must be non-empty and unique.", nameof(segments));
        }

        long expectedStart = 0;
        foreach (MemoryLayoutSegment segment in segments)
        {
            if (!StringComparer.Ordinal.Equals(segment.AddressSpaceId, addressSpaceId) ||
                segment.Range.Start != expectedStart ||
                !canonicalRegions.Any(region => ReferenceEquals(region, segment.CanonicalRegion)))
            {
                throw new ArgumentException(
                    "Coverage must form an ordered canonical-region-backed partition.",
                    nameof(segments));
            }

            expectedStart = segment.Range.EndExclusive;
        }

        if (expectedStart != capacity)
        {
            throw new ArgumentException("Coverage must span the output capacity.", nameof(segments));
        }
    }
}

internal static class MemoryLayoutGuard
{
    internal static void Defined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown enum value.");
        }
    }

    internal static MemoryLayoutPreservationDetail[] PreservationDetails(
        IEnumerable<MemoryLayoutPreservationDetail> values,
        ByteRange primaryRange)
    {
        ArgumentNullException.ThrowIfNull(values);
        MemoryLayoutPreservationDetail[] details =
        [
            .. values.OrderBy(static detail => detail.ResolvedRange.Start)
                .ThenBy(static detail => detail.DetailId, StringComparer.Ordinal),
        ];
        return details.Select(static detail => detail.DetailId)
                .Distinct(StringComparer.Ordinal).Count() != details.Length ||
            details.Any(detail => !primaryRange.Contains(detail.ResolvedRange)) ||
            details.Zip(details.Skip(1)).Any(static pair =>
                pair.First.ResolvedRange.Overlaps(pair.Second.ResolvedRange))
            ? throw new ArgumentException(
                "Preservation details must be unique, contained, and non-overlapping.",
                nameof(values))
            : details;
    }
}
