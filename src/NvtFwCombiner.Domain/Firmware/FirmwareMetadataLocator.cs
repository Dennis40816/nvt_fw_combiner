namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed metadata locator declaration kind.</summary>
public enum FirmwareMetadataLocatorKind
{
    /// <summary>One exact addressed range.</summary>
    AbsoluteRange,

    /// <summary>One checked offset from a canonical map region.</summary>
    RegionRelative,

    /// <summary>One checked signed offset from a selected marker.</summary>
    MarkerRelative,
}

/// <summary>Base declaration for one closed metadata locator.</summary>
public abstract record FirmwareMetadataLocator
{
    private protected FirmwareMetadataLocator(
        FirmwareMetadataLocatorKind kind,
        string allowedResultRegionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedResultRegionId);
        Kind = kind;
        AllowedResultRegionId = allowedResultRegionId;
    }

    /// <summary>Closed locator kind.</summary>
    public FirmwareMetadataLocatorKind Kind { get; }

    /// <summary>Canonical candidate-map region that must contain the full result.</summary>
    public string AllowedResultRegionId { get; }
}

/// <summary>Metadata structure located at one exact addressed range.</summary>
public sealed record FirmwareAbsoluteRangeLocator : FirmwareMetadataLocator
{
    /// <summary>Creates an exact-range locator.</summary>
    public FirmwareAbsoluteRangeLocator(
        FirmwareAddressedRange range,
        string allowedResultRegionId)
        : base(FirmwareMetadataLocatorKind.AbsoluteRange, allowedResultRegionId)
    {
        ArgumentNullException.ThrowIfNull(range);
        Range = range;
    }

    /// <summary>Exact addressed structure range.</summary>
    public FirmwareAddressedRange Range { get; }
}

/// <summary>Metadata structure located relative to one canonical map region.</summary>
public sealed record FirmwareRegionRelativeLocator : FirmwareMetadataLocator
{
    /// <summary>Creates a nonnegative region-relative locator.</summary>
    public FirmwareRegionRelativeLocator(
        string regionId,
        long offset,
        string allowedResultRegionId)
        : base(FirmwareMetadataLocatorKind.RegionRelative, allowedResultRegionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        RegionId = regionId;
        Offset = offset;
    }

    /// <summary>Canonical base region id.</summary>
    public string RegionId { get; }

    /// <summary>Nonnegative offset from the base region start.</summary>
    public long Offset { get; }
}

/// <summary>Closed marker match-selection kind.</summary>
public enum FirmwareMarkerSelectionKind
{
    /// <summary>Exactly one match must exist.</summary>
    Unique,

    /// <summary>An evidenced count must exist and one terminal match is selected.</summary>
    TerminalMatch,
}

/// <summary>Base declaration for marker cardinality and selection.</summary>
public abstract record FirmwareMarkerSelection
{
    private protected FirmwareMarkerSelection(FirmwareMarkerSelectionKind kind)
    {
        Kind = kind;
    }

    /// <summary>Closed marker-selection kind.</summary>
    public FirmwareMarkerSelectionKind Kind { get; }
}

/// <summary>Requires exactly one marker match.</summary>
public sealed record FirmwareUniqueMarkerSelection : FirmwareMarkerSelection
{
    /// <summary>Creates the unique marker-selection policy.</summary>
    public FirmwareUniqueMarkerSelection()
        : base(FirmwareMarkerSelectionKind.Unique)
    {
    }
}

/// <summary>Terminal direction for an evidenced marker match set.</summary>
public enum FirmwareMarkerTerminal
{
    /// <summary>Select the lowest-address match.</summary>
    LowestAddress,

    /// <summary>Select the highest-address match.</summary>
    HighestAddress,
}

/// <summary>Requires an exact evidenced match count and selects one terminal match.</summary>
public sealed record FirmwareTerminalMarkerSelection : FirmwareMarkerSelection
{
    /// <summary>Creates a checked terminal marker-selection policy.</summary>
    public FirmwareTerminalMarkerSelection(
        FirmwareMarkerTerminal terminal,
        int expectedMatchCount)
        : base(FirmwareMarkerSelectionKind.TerminalMatch)
    {
        if (!Enum.IsDefined(terminal))
        {
            throw new ArgumentOutOfRangeException(nameof(terminal), terminal, "Unknown marker terminal.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedMatchCount);
        Terminal = terminal;
        ExpectedMatchCount = expectedMatchCount;
    }

    /// <summary>Selected terminal direction.</summary>
    public FirmwareMarkerTerminal Terminal { get; }

    /// <summary>Required exact match count.</summary>
    public int ExpectedMatchCount { get; }
}

/// <summary>Metadata structure located at a checked signed offset from a selected marker.</summary>
public sealed record FirmwareMarkerRelativeLocator : FirmwareMetadataLocator
{
    /// <summary>Creates a bounded marker-relative locator with immutable marker bytes.</summary>
    public FirmwareMarkerRelativeLocator(
        FirmwareAddressedRange searchRange,
        ReadOnlySpan<byte> markerBytes,
        FirmwareMarkerSelection selection,
        long resultOffset,
        string allowedResultRegionId)
        : base(FirmwareMetadataLocatorKind.MarkerRelative, allowedResultRegionId)
    {
        ArgumentNullException.ThrowIfNull(searchRange);
        ArgumentNullException.ThrowIfNull(selection);
        var marker = new FirmwareMetadataBytes(markerBytes);
        if (marker.Length > searchRange.Range.Length)
        {
            throw new ArgumentException("Metadata marker must fit its bounded search range.", nameof(markerBytes));
        }

        long maximumMatchCount = checked(searchRange.Range.Length - marker.Length + 1);
        if (selection is FirmwareTerminalMarkerSelection terminalSelection &&
            terminalSelection.ExpectedMatchCount > maximumMatchCount)
        {
            throw new ArgumentException(
                "Terminal marker count cannot exceed bounded candidate start positions.",
                nameof(selection));
        }

        SearchRange = searchRange;
        MarkerBytes = marker;
        Selection = selection;
        ResultOffset = resultOffset;
    }

    /// <summary>Bounded addressed marker search range.</summary>
    public FirmwareAddressedRange SearchRange { get; }

    /// <summary>Exact immutable marker bytes.</summary>
    public FirmwareMetadataBytes MarkerBytes { get; }

    /// <summary>Required marker cardinality and selection.</summary>
    public FirmwareMarkerSelection Selection { get; }

    /// <summary>Signed offset from the selected marker start to the structure start.</summary>
    public long ResultOffset { get; }
}
