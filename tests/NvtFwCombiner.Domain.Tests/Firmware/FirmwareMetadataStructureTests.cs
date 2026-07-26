using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests canonical metadata structure aggregates.</summary>
public sealed class FirmwareMetadataStructureTests
{
    /// <summary>Verifies fields/assertions are immutable, deterministic, and may overlap.</summary>
    [Fact]
    public void ConstructorCreatesDeterministicImmutableStructure()
    {
        FirmwareMetadataField[] fields =
        [
            UnsignedField("high-nibble", 1, new FirmwareMetadataBitSlice(4, 4)),
            BytesField("header", 0, 2),
            UnsignedField("low-nibble", 1, new FirmwareMetadataBitSlice(0, 4)),
        ];
        FirmwareMetadataByteAssertion[] assertions =
        [
            FirmwareMetadataByteAssertion.Exact(1, [0x20]),
            FirmwareMetadataByteAssertion.Exact(0, [0x10]),
        ];

        FirmwareMetadataStructure structure = Structure(
            lengthBytes: 4,
            fields: fields,
            assertions: assertions);
        fields[0] = BytesField("changed", 0, 1);
        assertions[0] = FirmwareMetadataByteAssertion.Exact(0, [0xFF]);

        Assert.Equal("firmware-config", structure.StructureId);
        Assert.Equal("tp-firmware", structure.ArtifactBindingId);
        Assert.Equal(4, structure.LengthBytes);
        Assert.Equal(["header", "high-nibble", "low-nibble"],
            structure.Fields.Select(static field => field.FieldId));
        Assert.Equal([0L, 1L], structure.Assertions.Select(static assertion => assertion.Range.Start));

        IList<FirmwareMetadataField> fieldView = Assert.IsType<IList<FirmwareMetadataField>>(
            structure.Fields,
            exactMatch: false);
        IList<FirmwareMetadataByteAssertion> assertionView =
            Assert.IsType<IList<FirmwareMetadataByteAssertion>>(
                structure.Assertions,
                exactMatch: false);
        Assert.True(fieldView.IsReadOnly);
        Assert.True(assertionView.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => fieldView[0] = BytesField("changed", 0, 1));
        _ = Assert.Throws<NotSupportedException>(() =>
            assertionView[0] = FirmwareMetadataByteAssertion.Exact(0, [0]));
    }

    /// <summary>Verifies fields and assertions may end at, but not pass, structure length.</summary>
    [Fact]
    public void ConstructorChecksFieldAndAssertionBounds()
    {
        FirmwareMetadataStructure exact = Structure(
            lengthBytes: 4,
            fields: [BytesField("tail", 3, 1)],
            assertions: [FirmwareMetadataByteAssertion.Exact(3, [1])]);

        Assert.Equal(4, exact.Fields[0].Range.EndExclusive);
        _ = Assert.Throws<ArgumentException>(() => Structure(
            lengthBytes: 4,
            fields: [BytesField("past", 3, 2)]));
        _ = Assert.Throws<ArgumentException>(() => Structure(
            lengthBytes: 4,
            assertions: [FirmwareMetadataByteAssertion.Exact(3, [1, 2])]));
    }

    /// <summary>Verifies absolute locator length exactly equals structure length.</summary>
    [Fact]
    public void ConstructorChecksAbsoluteLocatorLength()
    {
        FirmwareMetadataStructure exact = Structure(lengthBytes: 4);

        _ = Assert.IsType<FirmwareAbsoluteRangeLocator>(exact.Locator);
        _ = Assert.Throws<ArgumentException>(() => Structure(
            lengthBytes: 4,
            locator: AbsoluteLocator(length: 3)));
        _ = Assert.Throws<ArgumentException>(() => Structure(
            lengthBytes: 4,
            locator: AbsoluteLocator(length: 5)));
    }

    /// <summary>Verifies equal-start assertions have one canonical overlap order.</summary>
    [Fact]
    public void ConstructorOrdersEqualStartAssertionsDeterministically()
    {
        FirmwareMetadataStructure structure = Structure(
            assertions:
            [
                FirmwareMetadataByteAssertion.Exact(0, [0x20]),
                FirmwareMetadataByteAssertion.Masked(0, [0x00], [0xF0]),
                FirmwareMetadataByteAssertion.Exact(0, [0x99, 0x99]),
                FirmwareMetadataByteAssertion.Exact(0, [0x10]),
                FirmwareMetadataByteAssertion.Masked(0, [0x00], [0x0F]),
            ]);

        Assert.Equal(
            [
                (2L, "9999", "ffff"),
                (1L, "00", "0f"),
                (1L, "00", "f0"),
                (1L, "10", "ff"),
                (1L, "20", "ff"),
            ],
            structure.Assertions.Select(static assertion =>
                (assertion.Range.Length, assertion.ExpectedBytes.Hex, assertion.MaskBytes.Hex)));
    }

    /// <summary>Verifies relative locator arithmetic is checked before artifact evaluation.</summary>
    [Fact]
    public void ConstructorChecksRelativeLocatorArithmetic()
    {
        FirmwareMetadataStructure relative = Structure(
            lengthBytes: 4,
            locator: new FirmwareRegionRelativeLocator("root", 8, "root"));

        _ = Assert.IsType<FirmwareRegionRelativeLocator>(relative.Locator);
        _ = Assert.Throws<OverflowException>(() => Structure(
            lengthBytes: 2,
            locator: new FirmwareRegionRelativeLocator("root", long.MaxValue, "root")));
        _ = Assert.Throws<OverflowException>(() => Structure(
            lengthBytes: 2,
            locator: MarkerLocator(resultOffset: long.MaxValue),
            assertions: [FirmwareMetadataByteAssertion.Exact(0, [1])]));
    }

    /// <summary>Verifies an exact-one marker is sufficient location evidence, while terminal selection needs an assertion.</summary>
    [Fact]
    public void ConstructorAllowsUniqueMarkerWithoutAssertionButRequiresTerminalAssertion()
    {
        FirmwareMetadataStructure unique = Structure(
            locator: MarkerLocator(),
            assertions: []);
        var terminalSelection = new FirmwareTerminalMarkerSelection(
            FirmwareMarkerTerminal.HighestAddress,
            expectedMatchCount: 1);
        _ = Assert.Throws<ArgumentException>(() => Structure(
            locator: MarkerLocator(selection: terminalSelection),
            assertions: []));
        FirmwareMetadataStructure asserted = Structure(
            locator: MarkerLocator(selection: terminalSelection),
            assertions: [FirmwareMetadataByteAssertion.Exact(0, [1])]);

        Assert.Empty(unique.Assertions);
        _ = Assert.Single(asserted.Assertions);
    }

    /// <summary>Verifies structure identity, collection, and duplicate-field boundaries fail closed.</summary>
    [Fact]
    public void ConstructorRejectsInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => Structure(structureId: " "));
        _ = Assert.Throws<ArgumentException>(() => Structure(artifactBindingId: " "));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareMetadataStructure(
            "firmware-config",
            "tp-firmware",
            0,
            AbsoluteLocator(1),
            [],
            []));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareMetadataStructure(
            "firmware-config",
            "tp-firmware",
            -1,
            AbsoluteLocator(1),
            [],
            []));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareMetadataStructure(
            "firmware-config",
            "tp-firmware",
            4,
            null!,
            [],
            []));
        _ = Assert.Throws<ArgumentException>(() => Structure(fields: [null!]));
        _ = Assert.Throws<ArgumentException>(() => Structure(assertions: [null!]));
        _ = Assert.Throws<ArgumentException>(() => Structure(fields:
            [BytesField("same", 0, 1), BytesField("same", 1, 1)]));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareMetadataStructure(
            "firmware-config",
            "tp-firmware",
            4,
            AbsoluteLocator(4),
            null!,
            []));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareMetadataStructure(
            "firmware-config",
            "tp-firmware",
            4,
            AbsoluteLocator(4),
            [],
            null!));
    }

    private static FirmwareMetadataStructure Structure(
        string structureId = "firmware-config",
        string artifactBindingId = "tp-firmware",
        long lengthBytes = 4,
        FirmwareMetadataLocator? locator = null,
        IEnumerable<FirmwareMetadataField>? fields = null,
        IEnumerable<FirmwareMetadataByteAssertion>? assertions = null)
    {
        return new FirmwareMetadataStructure(
            structureId,
            artifactBindingId,
            lengthBytes,
            locator ?? AbsoluteLocator(lengthBytes),
            fields ?? [],
            assertions ?? []);
    }

    private static FirmwareAbsoluteRangeLocator AbsoluteLocator(long length)
    {
        return new FirmwareAbsoluteRangeLocator(
            new FirmwareAddressedRange("flash", new ByteRange(0, length)),
            "root");
    }

    private static FirmwareMarkerRelativeLocator MarkerLocator(
        long resultOffset = 0,
        FirmwareMarkerSelection? selection = null)
    {
        return new FirmwareMarkerRelativeLocator(
            new FirmwareAddressedRange("flash", new ByteRange(0, 8)),
            [0xAA],
            selection ?? new FirmwareUniqueMarkerSelection(),
            resultOffset,
            "root");
    }

    private static FirmwareMetadataField BytesField(string fieldId, long offset, int width)
    {
        return new FirmwareMetadataField(fieldId, offset, width, FirmwareMetadataEncoding.Bytes);
    }

    private static FirmwareMetadataField UnsignedField(
        string fieldId,
        long offset,
        FirmwareMetadataBitSlice bitSlice)
    {
        return new FirmwareMetadataField(
            fieldId,
            offset,
            1,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian,
            bitSlice);
    }
}
