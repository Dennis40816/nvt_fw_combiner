namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed state of one candidate-scoped metadata structure evaluation.</summary>
public enum FirmwareMetadataStructureResolutionStatus
{
    /// <summary>The exact required artifact binding is not present.</summary>
    Pending,

    /// <summary>The supplied artifact contradicts the locator or structure declaration.</summary>
    Rejected,

    /// <summary>The locator and complete structure decode both succeeded.</summary>
    Resolved,
}

/// <summary>Closed reason for a pending or rejected structure evaluation.</summary>
public enum FirmwareMetadataStructureResolutionFailure
{
    /// <summary>The exact artifact binding is not present in the atomic resolution inputs.</summary>
    MissingArtifact,

    /// <summary>A static locator or marker search range exceeds the bound artifact payload.</summary>
    ArtifactRangeOutOfBounds,

    /// <summary>Observed marker matches do not satisfy the declared selection cardinality.</summary>
    MarkerCardinalityMismatch,

    /// <summary>The marker-selected result is outside the artifact or allowed result region.</summary>
    ResolvedRangeOutOfBounds,

    /// <summary>Structure assertions or typed field decoding failed atomically.</summary>
    StructureDecodeFailed,
}

/// <summary>Immutable successful outcome of one closed metadata locator declaration.</summary>
public sealed class FirmwareMetadataLocatorOutcome
{
    internal FirmwareMetadataLocatorOutcome(
        FirmwareMetadataLocatorKind locatorKind,
        FirmwareAddressedRange resolvedRange,
        int? markerMatchCount = null,
        long? selectedMarkerStart = null)
    {
        if (!Enum.IsDefined(locatorKind))
        {
            throw new ArgumentOutOfRangeException(nameof(locatorKind), locatorKind, "Unknown metadata locator kind.");
        }

        ArgumentNullException.ThrowIfNull(resolvedRange);
        bool isMarker = locatorKind == FirmwareMetadataLocatorKind.MarkerRelative;
        bool hasMarkerMatchCount = markerMatchCount is not null;
        bool hasSelectedMarkerStart = selectedMarkerStart is not null;
        if (hasMarkerMatchCount != hasSelectedMarkerStart || isMarker != hasMarkerMatchCount)
        {
            throw new ArgumentException("Marker evidence must exist only for marker-relative outcomes.");
        }

        if (markerMatchCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(markerMatchCount), "Marker match count must be positive.");
        }

        if (selectedMarkerStart < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedMarkerStart), "Selected marker start cannot be negative.");
        }

        LocatorKind = locatorKind;
        ResolvedRange = resolvedRange;
        MarkerMatchCount = markerMatchCount;
        SelectedMarkerStart = selectedMarkerStart;
    }

    /// <summary>Closed locator kind that produced this outcome.</summary>
    public FirmwareMetadataLocatorKind LocatorKind { get; }

    /// <summary>Exact resolved structure range in the candidate map address space.</summary>
    public FirmwareAddressedRange ResolvedRange { get; }

    /// <summary>Observed overlapping marker-match count for marker-relative outcomes.</summary>
    public int? MarkerMatchCount { get; }

    /// <summary>Selected absolute marker start for marker-relative outcomes.</summary>
    public long? SelectedMarkerStart { get; }
}

/// <summary>Immutable successful locator and typed-decode payload with no source bytes.</summary>
public sealed class FirmwareResolvedMetadataStructure
{
    internal FirmwareResolvedMetadataStructure(
        string mapId,
        FirmwareArtifactIdentity artifactIdentity,
        FirmwareMetadataLocatorOutcome locatorOutcome,
        FirmwareDecodedMetadataStructure decodedStructure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        ArgumentNullException.ThrowIfNull(artifactIdentity);
        ArgumentNullException.ThrowIfNull(locatorOutcome);
        ArgumentNullException.ThrowIfNull(decodedStructure);
        if (!StringComparer.Ordinal.Equals(
            artifactIdentity.ArtifactId,
            decodedStructure.ArtifactBindingId))
        {
            throw new ArgumentException("Resolved artifact identity must match decoded structure binding.");
        }

        MapId = mapId;
        ArtifactIdentity = artifactIdentity;
        LocatorOutcome = locatorOutcome;
        DecodedStructure = decodedStructure;
    }

    /// <summary>Candidate image map that scoped this metadata structure.</summary>
    public string MapId { get; }

    /// <summary>Identity derived from the immutable artifact payload snapshot.</summary>
    public FirmwareArtifactIdentity ArtifactIdentity { get; }

    /// <summary>Exact locator outcome without source payload bytes.</summary>
    public FirmwareMetadataLocatorOutcome LocatorOutcome { get; }

    /// <summary>Atomic typed structure decode.</summary>
    public FirmwareDecodedMetadataStructure DecodedStructure { get; }
}

/// <summary>Pending, rejected, or successful evaluation of one candidate-selected structure.</summary>
public sealed class FirmwareMetadataStructureResolution
{
    private FirmwareMetadataStructureResolution(
        string mapId,
        string artifactBindingId,
        string metadataStructureId,
        FirmwareMetadataStructureResolutionStatus status,
        FirmwareMetadataStructureResolutionFailure? failure,
        FirmwareResolvedMetadataStructure? resolved)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactBindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataStructureId);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown structure resolution status.");
        }

        if (failure is { } knownFailure && !Enum.IsDefined(knownFailure))
        {
            throw new ArgumentOutOfRangeException(nameof(failure), failure, "Unknown structure resolution failure.");
        }

        bool isResolved = status == FirmwareMetadataStructureResolutionStatus.Resolved;
        if (isResolved && (failure is not null || resolved is null))
        {
            throw new ArgumentException("Resolved status requires only a resolved payload.");
        }

        if (!isResolved && (failure is null || resolved is not null))
        {
            throw new ArgumentException("Unresolved status requires only a failure reason.");
        }

        if (status == FirmwareMetadataStructureResolutionStatus.Pending &&
            failure != FirmwareMetadataStructureResolutionFailure.MissingArtifact)
        {
            throw new ArgumentException("Pending structure resolution requires a missing artifact.");
        }

        if (status == FirmwareMetadataStructureResolutionStatus.Rejected &&
            failure == FirmwareMetadataStructureResolutionFailure.MissingArtifact)
        {
            throw new ArgumentException("A missing artifact is pending, not rejected.");
        }

        MapId = mapId;
        ArtifactBindingId = artifactBindingId;
        MetadataStructureId = metadataStructureId;
        Status = status;
        Failure = failure;
        Resolved = resolved;
    }

    /// <summary>Candidate image-map identifier.</summary>
    public string MapId { get; }

    /// <summary>Exact required artifact binding identifier.</summary>
    public string ArtifactBindingId { get; }

    /// <summary>Candidate-selected metadata structure identifier.</summary>
    public string MetadataStructureId { get; }

    /// <summary>Closed evaluation state.</summary>
    public FirmwareMetadataStructureResolutionStatus Status { get; }

    /// <summary>Pending or rejection reason; null only when resolved.</summary>
    public FirmwareMetadataStructureResolutionFailure? Failure { get; }

    /// <summary>Successful locator and decode payload; null unless resolved.</summary>
    public FirmwareResolvedMetadataStructure? Resolved { get; }

    internal static FirmwareMetadataStructureResolution Pending(
        string mapId,
        FirmwareMetadataStructure structure)
    {
        return new FirmwareMetadataStructureResolution(
            mapId,
            structure.ArtifactBindingId,
            structure.StructureId,
            FirmwareMetadataStructureResolutionStatus.Pending,
            FirmwareMetadataStructureResolutionFailure.MissingArtifact,
            resolved: null);
    }

    internal static FirmwareMetadataStructureResolution Rejected(
        string mapId,
        FirmwareMetadataStructure structure,
        FirmwareMetadataStructureResolutionFailure failure)
    {
        return new FirmwareMetadataStructureResolution(
            mapId,
            structure.ArtifactBindingId,
            structure.StructureId,
            FirmwareMetadataStructureResolutionStatus.Rejected,
            failure,
            resolved: null);
    }

    internal static FirmwareMetadataStructureResolution Success(
        FirmwareResolvedMetadataStructure resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        return new FirmwareMetadataStructureResolution(
            resolved.MapId,
            resolved.DecodedStructure.ArtifactBindingId,
            resolved.DecodedStructure.MetadataStructureId,
            FirmwareMetadataStructureResolutionStatus.Resolved,
            failure: null,
            resolved);
    }
}
