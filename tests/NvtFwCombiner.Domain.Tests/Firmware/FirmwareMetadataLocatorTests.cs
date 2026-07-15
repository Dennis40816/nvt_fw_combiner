using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests closed metadata locator declarations.</summary>
public sealed class FirmwareMetadataLocatorTests
{
    /// <summary>Verifies addressed ranges retain one explicit address-space identity.</summary>
    [Fact]
    public void AddressedRangePreservesNamedHalfOpenRange()
    {
        var range = new FirmwareAddressedRange("flash", new ByteRange(4, 8));

        Assert.Equal("flash", range.AddressSpaceId);
        Assert.Equal(new ByteRange(4, 8), range.Range);
        _ = Assert.Throws<ArgumentException>(() => new FirmwareAddressedRange(" ", new ByteRange(0, 1)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareAddressedRange("flash", default));
    }

    /// <summary>Verifies absolute and region-relative locators preserve closed declarations.</summary>
    [Fact]
    public void FixedLocatorsPreserveCanonicalDeclarations()
    {
        var addressedRange = new FirmwareAddressedRange("flash", new ByteRange(16, 4));
        var absolute = new FirmwareAbsoluteRangeLocator(addressedRange, "tp-image");
        var relative = new FirmwareRegionRelativeLocator("tp-image", 12, "tp-image");

        Assert.Equal(FirmwareMetadataLocatorKind.AbsoluteRange, absolute.Kind);
        Assert.Same(addressedRange, absolute.Range);
        Assert.Equal("tp-image", absolute.AllowedResultRegionId);
        Assert.Equal(FirmwareMetadataLocatorKind.RegionRelative, relative.Kind);
        Assert.Equal("tp-image", relative.RegionId);
        Assert.Equal(12, relative.Offset);
    }

    /// <summary>Verifies marker locators snapshot bytes and preserve signed result offsets.</summary>
    [Fact]
    public void MarkerLocatorCreatesImmutableBoundedDeclaration()
    {
        byte[] marker = [0x00, 0x4E, 0x56, 0x54];
        var searchRange = new FirmwareAddressedRange("flash", new ByteRange(0, 64));
        var selection = new FirmwareTerminalMarkerSelection(
            FirmwareMarkerTerminal.HighestAddress,
            expectedMatchCount: 2);

        var locator = new FirmwareMarkerRelativeLocator(
            searchRange,
            marker,
            selection,
            resultOffset: -12,
            allowedResultRegionId: "tp-image");
        marker[1] = 0;

        Assert.Equal(FirmwareMetadataLocatorKind.MarkerRelative, locator.Kind);
        Assert.Equal("004e5654", locator.MarkerBytes.Hex);
        Assert.Same(searchRange, locator.SearchRange);
        Assert.Same(selection, locator.Selection);
        Assert.Equal(-12, locator.ResultOffset);
        Assert.Equal(FirmwareMarkerSelectionKind.TerminalMatch, locator.Selection.Kind);
    }

    /// <summary>Verifies unique and terminal marker policies have closed cardinality semantics.</summary>
    [Fact]
    public void MarkerSelectionsPreserveClosedPolicies()
    {
        var unique = new FirmwareUniqueMarkerSelection();
        var lowest = new FirmwareTerminalMarkerSelection(
            FirmwareMarkerTerminal.LowestAddress,
            expectedMatchCount: 3);

        Assert.Equal(FirmwareMarkerSelectionKind.Unique, unique.Kind);
        Assert.Equal(FirmwareMarkerSelectionKind.TerminalMatch, lowest.Kind);
        Assert.Equal(FirmwareMarkerTerminal.LowestAddress, lowest.Terminal);
        Assert.Equal(3, lowest.ExpectedMatchCount);
    }

    /// <summary>Verifies independently constructed locator records use complete structural identity.</summary>
    [Fact]
    public void LocatorRecordsUseStructuralValueEquality()
    {
        FirmwareMarkerRelativeLocator first = MarkerLocator();
        FirmwareMarkerRelativeLocator equal = MarkerLocator();
        var firstRange = new FirmwareAddressedRange("flash", new ByteRange(0, 8));
        var equalRange = new FirmwareAddressedRange("flash", new ByteRange(0, 8));

        Assert.Equal(firstRange, equalRange);
        Assert.Equal(firstRange.GetHashCode(), equalRange.GetHashCode());
        Assert.Equal(first, equal);
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(first, MarkerLocator(marker: [0x41, 0x42]));
        Assert.NotEqual(first, MarkerLocator(resultOffset: -2));
        Assert.NotEqual(first, MarkerLocator(selection: new FirmwareTerminalMarkerSelection(
            FirmwareMarkerTerminal.HighestAddress,
            1)));
        Assert.NotEqual(first, MarkerLocator(selection: new FirmwareTerminalMarkerSelection(
            FirmwareMarkerTerminal.LowestAddress,
            2)));
        Assert.NotEqual(first, MarkerLocator(addressSpaceId: "other"));
        Assert.NotEqual(first, MarkerLocator(allowedResultRegionId: "other-region"));
        Assert.NotEqual(
            new FirmwareRegionRelativeLocator("base-a", 0, "allowed"),
            new FirmwareRegionRelativeLocator("base-b", 0, "allowed"));
    }

    /// <summary>Verifies markers cannot be empty or longer than the bounded search range.</summary>
    [Fact]
    public void MarkerLocatorRejectsInvalidMarkerBoundaries()
    {
        var searchRange = new FirmwareAddressedRange("flash", new ByteRange(0, 2));
        var selection = new FirmwareUniqueMarkerSelection();
        FirmwareMarkerRelativeLocator exactFit = new(
            searchRange,
            [1, 2],
            selection,
            0,
            "root");

        Assert.Equal(2, exactFit.MarkerBytes.Length);
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMarkerRelativeLocator(
            searchRange,
            [],
            selection,
            0,
            "root"));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMarkerRelativeLocator(
            searchRange,
            [1, 2, 3],
            selection,
            0,
            "root"));

        var widerSearch = new FirmwareAddressedRange("flash", new ByteRange(0, 4));
        FirmwareMarkerRelativeLocator maximumCount = new(
            widerSearch,
            [1, 1],
            new FirmwareTerminalMarkerSelection(FirmwareMarkerTerminal.HighestAddress, 3),
            0,
            "root");
        Assert.Equal(3, ((FirmwareTerminalMarkerSelection)maximumCount.Selection).ExpectedMatchCount);
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMarkerRelativeLocator(
            widerSearch,
            [1, 1],
            new FirmwareTerminalMarkerSelection(FirmwareMarkerTerminal.HighestAddress, 4),
            0,
            "root"));
    }

    /// <summary>Verifies locator identity, selection, and null boundaries fail closed.</summary>
    [Fact]
    public void ConstructorsRejectInvalidBoundaries()
    {
        var searchRange = new FirmwareAddressedRange("flash", new ByteRange(0, 4));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareAbsoluteRangeLocator(null!, "root"));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareAbsoluteRangeLocator(searchRange, " "));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareRegionRelativeLocator(" ", 0, "root"));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareRegionRelativeLocator(
            "root",
            -1,
            "root"));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareRegionRelativeLocator("root", 0, " "));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareMarkerRelativeLocator(
            null!,
            [1],
            new FirmwareUniqueMarkerSelection(),
            0,
            "root"));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareMarkerRelativeLocator(
            searchRange,
            [1],
            null!,
            0,
            "root"));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMarkerRelativeLocator(
            searchRange,
            [1],
            new FirmwareUniqueMarkerSelection(),
            0,
            " "));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareTerminalMarkerSelection(
            (FirmwareMarkerTerminal)int.MaxValue,
            1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareTerminalMarkerSelection(
            FirmwareMarkerTerminal.LowestAddress,
            0));
    }

    private static FirmwareMarkerRelativeLocator MarkerLocator(
        string addressSpaceId = "flash",
        byte[]? marker = null,
        FirmwareMarkerSelection? selection = null,
        long resultOffset = -1,
        string allowedResultRegionId = "root")
    {
        return new FirmwareMarkerRelativeLocator(
            new FirmwareAddressedRange(addressSpaceId, new ByteRange(0, 8)),
            marker ?? [0x41, 0x41],
            selection ?? new FirmwareTerminalMarkerSelection(FirmwareMarkerTerminal.LowestAddress, 1),
            resultOffset,
            allowedResultRegionId);
    }
}
