using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.MemoryLayout;

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
        if (slotIdentity.SelectedPath is null)
        {
            throw new ArgumentException(
                "A blocked issue requires the exact selected-path identity.",
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

    /// <summary>Exact slot and path; content identity is null before bytes are accepted.</summary>
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
        string regionId,
        FirmwareRegion? canonicalRegion,
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
        IEnumerable<CompositionOperation> contributingOperations,
        IEnumerable<MemoryLayoutPreservationDetail> preservationDetails,
        ReplaceRegionGroup regionGroup,
        CtrlRamRegionRole ctrlRamRegionRole)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segmentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        if (canonicalRegion is not null &&
            (!StringComparer.Ordinal.Equals(canonicalRegion.RegionId, regionId) ||
             !canonicalRegion.Range.Contains(range)))
        {
            throw new ArgumentException(
                "A physical projected range must retain its exact containing canonical region.",
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
        MemoryLayoutGuard.Defined(regionGroup, nameof(regionGroup));
        MemoryLayoutGuard.Defined(ctrlRamRegionRole, nameof(ctrlRamRegionRole));
        if (sourceSlotId is not null && sourceSpaceId is null)
        {
            throw new ArgumentException(
                "A source slot requires a source address space.",
                nameof(sourceSlotId));
        }

        ArgumentNullException.ThrowIfNull(contributingOperations);
        CompositionOperation[] operations = [.. contributingOperations];
        if (operations.Any(static operation => operation is null))
        {
            throw new ArgumentException(
                "Contributing operations must be non-null.",
                nameof(contributingOperations));
        }

        MemoryLayoutPreservationDetail[] details =
            MemoryLayoutGuard.PreservationDetails(preservationDetails, range);
        SegmentId = segmentId;
        AddressSpaceId = addressSpaceId;
        Range = range;
        RegionId = regionId;
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
        ContributingOperations = Array.AsReadOnly(operations);
        PreservationDetails = Array.AsReadOnly(details);
        RegionGroup = regionGroup;
        CtrlRamRegionRole = ctrlRamRegionRole;
    }

    /// <summary>Stable projection-local identity.</summary>
    public string SegmentId { get; }
    /// <summary>Canonical output address-space identity.</summary>
    public string AddressSpaceId { get; }
    /// <summary>Resolved half-open output range.</summary>
    public ByteRange Range { get; }
    /// <summary>Stable physical-region or logical-output identity.</summary>
    public string RegionId { get; }
    /// <summary>Exact canonical physical-region reference, or null for logical output.</summary>
    public FirmwareRegion? CanonicalRegion { get; }
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
    /// <summary>Exact ordered compiled operations contributing to this segment.</summary>
    public IReadOnlyList<CompositionOperation> ContributingOperations { get; }
    /// <summary>Typed kept details subordinate to this primary segment.</summary>
    public IReadOnlyList<MemoryLayoutPreservationDetail> PreservationDetails { get; }
    /// <summary>Application-owned CtrlRAM grouping, or Common for ungrouped geometry.</summary>
    public ReplaceRegionGroup RegionGroup { get; }
    /// <summary>Closed detailed CtrlRAM family role; Other outside detailed CtrlRAM geometry.</summary>
    public CtrlRamRegionRole CtrlRamRegionRole { get; }

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
        IEnumerable<CompositionOperation> contributingOperations,
        IEnumerable<MemoryLayoutPreservationDetail> preservationDetails,
        ReplaceRegionGroup regionGroup = ReplaceRegionGroup.Common,
        CtrlRamRegionRole ctrlRamRegionRole = CtrlRamRegionRole.Other)
    {
        return new(
            segmentId,
            addressSpaceId,
            range,
            canonicalRegion.RegionId,
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
            contributingOperations,
            preservationDetails,
            regionGroup,
            ctrlRamRegionRole);
    }

    internal static MemoryLayoutSegment CreateLogical(
        string segmentId,
        string addressSpaceId,
        ByteRange range,
        string logicalRegionId,
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
        IEnumerable<CompositionOperation> contributingOperations,
        IEnumerable<MemoryLayoutPreservationDetail> preservationDetails,
        ReplaceRegionGroup regionGroup = ReplaceRegionGroup.Common,
        CtrlRamRegionRole ctrlRamRegionRole = CtrlRamRegionRole.Other)
    {
        return new(
            segmentId,
            addressSpaceId,
            range,
            logicalRegionId,
            canonicalRegion: null,
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
            contributingOperations,
            preservationDetails,
            regionGroup,
            ctrlRamRegionRole);
    }
}

/// <summary>One unresolved non-geometric artifact or part.</summary>
public sealed class MemoryLayoutPendingItem
{
    internal MemoryLayoutPendingItem(
        string slotId,
        CompiledInputSlotRequirement requirement,
        MemoryLayoutReadiness readiness,
        MemoryLayoutPrerequisite prerequisite,
        MemoryLayoutNextAction nextAction,
        long? knownInputLength,
        MemoryDiagnosticSeverity diagnosticSeverity,
        MemoryLayoutBlockedIssueReference? blockedIssue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
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

        SlotId = slotId;
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
        : this(
            capability,
            authoring,
            MemoryLayoutGeometryKind.PhysicalMap,
            map,
            map.MapId,
            map.AddressSpaceId,
            capacity,
            beforeSegments,
            afterSegments,
            pendingItems)
    {
    }

    internal MemoryLayoutSnapshot(
        ResolvedCapability capability,
        ActiveSessionSnapshot authoring,
        string logicalAddressSpaceId,
        long capacity,
        IReadOnlyList<MemoryLayoutSegment> beforeSegments,
        IReadOnlyList<MemoryLayoutSegment> afterSegments,
        IEnumerable<MemoryLayoutPendingItem> pendingItems)
        : this(
            capability,
            authoring,
            MemoryLayoutGeometryKind.LogicalOutput,
            map: null,
            mapId: null,
            logicalAddressSpaceId,
            capacity,
            beforeSegments,
            afterSegments,
            pendingItems)
    {
    }

    private MemoryLayoutSnapshot(
        ResolvedCapability capability,
        ActiveSessionSnapshot authoring,
        MemoryLayoutGeometryKind geometryKind,
        FirmwareImageMap? map,
        string? mapId,
        string addressSpaceId,
        long capacity,
        IReadOnlyList<MemoryLayoutSegment> beforeSegments,
        IReadOnlyList<MemoryLayoutSegment> afterSegments,
        IEnumerable<MemoryLayoutPendingItem> pendingItems)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(authoring);
        MemoryLayoutGuard.Defined(geometryKind, nameof(geometryKind));
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        if (geometryKind == MemoryLayoutGeometryKind.PhysicalMap != (map is not null) ||
            geometryKind == MemoryLayoutGeometryKind.PhysicalMap != (mapId is not null))
        {
            throw new ArgumentException(
                "Only physical memory-layout geometry can retain a canonical map.",
                nameof(map));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        FirmwareRegion[] regions = map is null ? [] : [.. map.Regions];
        MemoryLayoutSegment[] before = [.. beforeSegments];
        MemoryLayoutSegment[] after = ReferenceEquals(beforeSegments, afterSegments)
            ? before
            : [.. afterSegments];
        MemoryLayoutPendingItem[] pending = [.. pendingItems];
        ValidateCoverage(before, geometryKind, addressSpaceId, capacity, regions);
        ValidateCoverage(after, geometryKind, addressSpaceId, capacity, regions);
        if (pending.Select(static item => item.SlotId)
            .Distinct(StringComparer.Ordinal).Count() != pending.Length)
        {
            throw new ArgumentException("Pending items must be unique by slot.", nameof(pendingItems));
        }

        RouteId = capability.Identity.RouteId;
        CapabilityFingerprint = capability.CapabilityFingerprint;
        CompilationFingerprint =
            capability.CompiledComposition.CompilationFingerprint;
        ImageInitialization initialization =
            capability.CompiledComposition.Plan.OutputInitialization;
        if (initialization.Capacity != capacity)
        {
            throw new ArgumentException(
                "Memory-layout capacity must match the exact compiled initialization.",
                nameof(capacity));
        }

        ResolutionToken = capability.ResolutionToken;
        AuthoringRevision = authoring.AuthoringRevision;
        GeometryKind = geometryKind;
        MapId = mapId;
        AddressSpaceId = addressSpaceId;
        Capacity = capacity;
        BlankFillByte = initialization.Kind == ImageInitializationKind.Blank
            ? initialization.FillByte
            : null;
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
    /// <summary>Closed geometry kind for this projection.</summary>
    public MemoryLayoutGeometryKind GeometryKind { get; }
    /// <summary>Canonical image-map identity, or null for logical output.</summary>
    public string? MapId { get; }
    /// <summary>Canonical physical or compiler-owned logical output address-space identity.</summary>
    public string AddressSpaceId { get; }
    /// <summary>Exact resolved output capacity.</summary>
    public long Capacity { get; }
    /// <summary>Exact compiled blank fill byte, or null for reference-clone initialization.</summary>
    public byte? BlankFillByte { get; }
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
        MemoryLayoutGeometryKind geometryKind,
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
            bool retainsExpectedGeometry = geometryKind == MemoryLayoutGeometryKind.PhysicalMap
                ? segment.CanonicalRegion is not null &&
                    canonicalRegions.Any(region => ReferenceEquals(region, segment.CanonicalRegion))
                : segment.CanonicalRegion is null;
            if (!StringComparer.Ordinal.Equals(segment.AddressSpaceId, addressSpaceId) ||
                segment.Range.Start != expectedStart ||
                !retainsExpectedGeometry)
            {
                throw new ArgumentException(
                    "Coverage must form an ordered partition backed by its declared geometry.",
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
