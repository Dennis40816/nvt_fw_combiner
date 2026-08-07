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

    /// <summary>
    /// One checked offset from an anchor selected by a decoded prerequisite
    /// metadata field.
    /// </summary>
    MetadataFieldSelected,
}

/// <summary>Base declaration for one closed metadata locator.</summary>
public abstract record FirmwareMetadataLocator
{
    private protected FirmwareMetadataLocator(
        FirmwareMetadataLocatorKind kind,
        string allowedResultRegionId)
    {
        AllowedResultRegionId = RequiredValue.NotBlank(allowedResultRegionId);
        Kind = kind;
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
        Range = RequiredValue.NotNull(range);
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
        RegionId = RequiredValue.NotBlank(regionId);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
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
        ClosedEnum.ThrowIfUndefined(terminal, "Unknown marker terminal.");

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
        SearchRange = RequiredValue.NotNull(searchRange);
        Selection = RequiredValue.NotNull(selection);
        var marker = new FirmwareMetadataBytes(markerBytes);
        DomainInvariant.Reject(
            marker.Length > searchRange.Range.Length,
            "Metadata marker must fit its bounded search range.", nameof(markerBytes));

        long maximumMatchCount = checked(searchRange.Range.Length - marker.Length + 1);
        DomainInvariant.Reject(
            selection is FirmwareTerminalMarkerSelection terminalSelection &&
            terminalSelection.ExpectedMatchCount > maximumMatchCount,
            "Terminal marker count cannot exceed bounded candidate start positions.",
            nameof(selection));

        MarkerBytes = marker;
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

/// <summary>
/// One non-overlapping unsigned prerequisite value interval and its exact
/// logical-address anchor.
/// </summary>
public sealed record FirmwareMetadataFieldSelectedBranch
{
    /// <summary>Creates one checked inclusive value interval.</summary>
    public FirmwareMetadataFieldSelectedBranch(
        ulong minimumValue,
        ulong maximumValue,
        FirmwareAddressedRange anchorRange)
    {
        DomainInvariant.Reject(
            minimumValue > maximumValue,
            "Metadata-selected branch minimum cannot exceed its maximum.");

        AnchorRange = RequiredValue.NotNull(anchorRange);
        MinimumValue = minimumValue;
        MaximumValue = maximumValue;
    }

    /// <summary>Inclusive minimum prerequisite value.</summary>
    public ulong MinimumValue { get; }

    /// <summary>Inclusive maximum prerequisite value.</summary>
    public ulong MaximumValue { get; }

    /// <summary>Exact map-relative logical-address anchor.</summary>
    public FirmwareAddressedRange AnchorRange { get; }

    /// <summary>Whether this branch accepts one decoded unsigned value.</summary>
    public bool Contains(ulong value)
    {
        return value >= MinimumValue && value <= MaximumValue;
    }
}

/// <summary>
/// Metadata structure located from one prerequisite field-selected logical
/// address anchor.
/// </summary>
public sealed record FirmwareMetadataFieldSelectedLocator : FirmwareMetadataLocator
{
    private readonly FirmwareMetadataFieldSelectedBranch[] _branches;

    /// <summary>Creates one deterministic prerequisite-selected locator.</summary>
    public FirmwareMetadataFieldSelectedLocator(
        string prerequisiteStructureId,
        string prerequisiteFieldId,
        IEnumerable<FirmwareMetadataFieldSelectedBranch> branches,
        long resultOffset,
        string allowedResultRegionId)
        : base(
            FirmwareMetadataLocatorKind.MetadataFieldSelected,
            allowedResultRegionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prerequisiteStructureId);
        ArgumentException.ThrowIfNullOrWhiteSpace(prerequisiteFieldId);
        ArgumentNullException.ThrowIfNull(branches);
        _branches = Composition.ImmutableReferenceSnapshot.Create(
            branches,
            "Metadata-selected locators cannot contain null branches.");
        DomainInvariant.Reject(
            _branches.Length == 0,
            "Metadata-selected locators require at least one branch.",
            nameof(branches));

        Array.Sort(_branches, static (left, right) =>
        {
            int minimum = left.MinimumValue.CompareTo(right.MinimumValue);
            return minimum != 0
                ? minimum
                : left.MaximumValue.CompareTo(right.MaximumValue);
        });
        for (int index = 1; index < _branches.Length; index++)
        {
            DomainInvariant.Reject(
                _branches[index].MinimumValue <=
                _branches[index - 1].MaximumValue,
                "Metadata-selected locator branch intervals cannot overlap.",
                nameof(branches));
        }

        PrerequisiteStructureId = prerequisiteStructureId;
        PrerequisiteFieldId = prerequisiteFieldId;
        ResultOffset = resultOffset;
        Branches = Array.AsReadOnly(_branches);
    }

    /// <summary>Exact prerequisite structure binding selected by the same map.</summary>
    public string PrerequisiteStructureId { get; }

    /// <summary>Unsigned field that selects one logical-address anchor.</summary>
    public string PrerequisiteFieldId { get; }

    /// <summary>Non-overlapping value branches in ascending order.</summary>
    public IReadOnlyList<FirmwareMetadataFieldSelectedBranch> Branches { get; }

    /// <summary>Checked signed offset from the selected anchor start.</summary>
    public long ResultOffset { get; }

    /// <summary>Returns the unique branch for one decoded value.</summary>
    public bool TrySelect(
        ulong value,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out FirmwareMetadataFieldSelectedBranch? branch)
    {
        branch = _branches.FirstOrDefault(candidate => candidate.Contains(value));
        return branch is not null;
    }
}
