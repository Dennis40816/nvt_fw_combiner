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

/// <summary>Internal canonical declaration for one closed metadata locator.</summary>
internal abstract record FirmwareMetadataLocator
{
    private protected FirmwareMetadataLocator(
        FirmwareMetadataLocatorKind kind,
        string allowedResultRegionId)
    {
        AllowedResultRegionId = RequiredValue.NotBlank(allowedResultRegionId);
        Kind = kind;
    }

    internal FirmwareMetadataLocatorKind Kind { get; }

    internal string AllowedResultRegionId { get; }
}

/// <summary>Metadata structure located at one exact addressed range.</summary>
internal sealed record FirmwareAbsoluteRangeLocator : FirmwareMetadataLocator
{
    internal FirmwareAbsoluteRangeLocator(
        FirmwareAddressedRange range,
        string allowedResultRegionId)
        : base(FirmwareMetadataLocatorKind.AbsoluteRange, allowedResultRegionId)
    {
        Range = RequiredValue.NotNull(range);
    }

    internal FirmwareAddressedRange Range { get; }
}

/// <summary>Metadata structure located relative to one canonical map region.</summary>
internal sealed record FirmwareRegionRelativeLocator : FirmwareMetadataLocator
{
    internal FirmwareRegionRelativeLocator(
        string regionId,
        long offset,
        string allowedResultRegionId)
        : base(FirmwareMetadataLocatorKind.RegionRelative, allowedResultRegionId)
    {
        RegionId = RequiredValue.NotBlank(regionId);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        Offset = offset;
    }

    internal string RegionId { get; }

    internal long Offset { get; }
}

internal enum FirmwareMarkerSelectionKind
{
    Unique,
    TerminalMatch,
}

/// <summary>Base declaration for marker cardinality and selection.</summary>
internal abstract record FirmwareMarkerSelection
{
    private protected FirmwareMarkerSelection(FirmwareMarkerSelectionKind kind)
    {
        Kind = kind;
    }

    internal FirmwareMarkerSelectionKind Kind { get; }
}

/// <summary>Requires exactly one marker match.</summary>
internal sealed record FirmwareUniqueMarkerSelection : FirmwareMarkerSelection
{
    internal FirmwareUniqueMarkerSelection()
        : base(FirmwareMarkerSelectionKind.Unique)
    {
    }
}

internal enum FirmwareMarkerTerminal
{
    LowestAddress,
    HighestAddress,
}

/// <summary>Requires an exact evidenced match count and selects one terminal match.</summary>
internal sealed record FirmwareTerminalMarkerSelection : FirmwareMarkerSelection
{
    internal FirmwareTerminalMarkerSelection(
        FirmwareMarkerTerminal terminal,
        int expectedMatchCount)
        : base(FirmwareMarkerSelectionKind.TerminalMatch)
    {
        ClosedEnum.ThrowIfUndefined(terminal, "Unknown marker terminal.");

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedMatchCount);
        Terminal = terminal;
        ExpectedMatchCount = expectedMatchCount;
    }

    internal FirmwareMarkerTerminal Terminal { get; }

    internal int ExpectedMatchCount { get; }
}

/// <summary>Metadata structure located at a checked signed offset from a selected marker.</summary>
internal sealed record FirmwareMarkerRelativeLocator : FirmwareMetadataLocator
{
    internal FirmwareMarkerRelativeLocator(
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

    internal FirmwareAddressedRange SearchRange { get; }

    internal FirmwareMetadataBytes MarkerBytes { get; }

    internal FirmwareMarkerSelection Selection { get; }

    internal long ResultOffset { get; }
}

/// <summary>
/// One non-overlapping unsigned prerequisite value interval and its exact
/// logical-address anchor.
/// </summary>
internal sealed record FirmwareMetadataFieldSelectedBranch
{
    internal FirmwareMetadataFieldSelectedBranch(
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

    internal ulong MinimumValue { get; }

    internal ulong MaximumValue { get; }

    internal FirmwareAddressedRange AnchorRange { get; }

    internal bool Contains(ulong value)
    {
        return value >= MinimumValue && value <= MaximumValue;
    }
}

/// <summary>
/// Metadata structure located from one prerequisite field-selected logical
/// address anchor.
/// </summary>
internal sealed record FirmwareMetadataFieldSelectedLocator : FirmwareMetadataLocator
{
    private readonly FirmwareMetadataFieldSelectedBranch[] _branches;

    internal FirmwareMetadataFieldSelectedLocator(
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

    internal string PrerequisiteStructureId { get; }

    internal string PrerequisiteFieldId { get; }

    internal IReadOnlyList<FirmwareMetadataFieldSelectedBranch> Branches { get; }

    internal long ResultOffset { get; }

    /// <summary>Returns the unique branch for one decoded value.</summary>
    internal bool TrySelect(
        ulong value,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out FirmwareMetadataFieldSelectedBranch? branch)
    {
        branch = _branches.FirstOrDefault(candidate => candidate.Contains(value));
        return branch is not null;
    }
}
