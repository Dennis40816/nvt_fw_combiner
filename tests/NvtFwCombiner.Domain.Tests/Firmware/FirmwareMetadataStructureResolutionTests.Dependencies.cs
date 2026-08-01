using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

public sealed partial class FirmwareMetadataStructureResolutionTests
{
    /// <summary>Inclusive value branches reject inverted or overlapping intervals before use.</summary>
    [Fact]
    public void MetadataSelectedBranchesRejectInvalidIntervals()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new FirmwareMetadataFieldSelectedBranch(
                2,
                1,
                AddressedRange(0, 16)));
        _ = Assert.Throws<ArgumentException>(() =>
            new FirmwareMetadataFieldSelectedLocator(
                "prerequisite",
                "count",
                [
                    new FirmwareMetadataFieldSelectedBranch(
                        1,
                        2,
                        AddressedRange(0, 16)),
                    new FirmwareMetadataFieldSelectedBranch(
                        2,
                        3,
                        AddressedRange(16, 16)),
                ],
                0,
                "root"));
    }

    /// <summary>Selected locators require one same-map unsigned prerequisite field.</summary>
    [Fact]
    public void MetadataSelectedLocatorRejectsInvalidPrerequisite()
    {
        FirmwareMetadataStructure selected = SelectedStructure(
            "selected",
            "prerequisite",
            "count");
        FirmwareMetadataStructure missingField = PrerequisiteStructure(
            fields: [UnsignedField("other")]);
        FirmwareMetadataStructure nonUnsigned = PrerequisiteStructure(
            fields: [BytesField("count", 0, 1)]);

        _ = Assert.Throws<ArgumentException>(() => Definition(selected));
        _ = Assert.Throws<ArgumentException>(() =>
            Definition(missingField, selected));
        _ = Assert.Throws<ArgumentException>(() =>
            Definition(nonUnsigned, selected));

        FirmwareMetadataSet selectedSet = MetadataSet(
            "selected-metadata",
            selected);
        FirmwareMetadataSet prerequisiteSet = MetadataSet(
            "prerequisite-metadata",
            PrerequisiteStructure());
        _ = Assert.Throws<ArgumentException>(() =>
            Definition(
                [Map("map", [selectedSet])],
                [selectedSet, prerequisiteSet]));
    }

    /// <summary>Selected anchors and results must remain in the exact map address space and declared envelopes.</summary>
    [Fact]
    public void MetadataSelectedLocatorRejectsInvalidAnchorOrResultRange()
    {
        FirmwareMetadataStructure prerequisite = PrerequisiteStructure();
        FirmwareMetadataFieldSelectedLocator[] invalidLocators =
        [
            SelectedLocator(AddressedRange(0, 16, "other-space")),
            SelectedLocator(AddressedRange(24, 16)),
            SelectedLocator(AddressedRange(0, 2), resultOffset: 1),
            SelectedLocator(
                AddressedRange(16, 16),
                allowedResultRegionId: "allowed"),
        ];

        foreach (FirmwareMetadataFieldSelectedLocator locator in invalidLocators)
        {
            _ = Assert.Throws<ArgumentException>(() =>
                Definition(
                    prerequisite,
                    SelectedStructure("selected", locator)));
        }

        _ = Assert.Throws<OverflowException>(() =>
            Definition(
                prerequisite,
                SelectedStructure(
                    "selected",
                    SelectedLocator(
                        AddressedRange(1, 16),
                        resultOffset: long.MaxValue))));
    }

    /// <summary>Direct and transitive metadata dependency cycles are invalid family declarations.</summary>
    [Fact]
    public void MetadataSelectedLocatorRejectsDependencyCycles()
    {
        FirmwareMetadataStructure direct = SelectedStructure(
            "direct",
            SelectedLocator(
                AddressedRange(0, 16),
                prerequisiteStructureId: "direct"),
            fields: [UnsignedField("count")],
            lengthBytes: 1);
        FirmwareMetadataStructure first = SelectedStructure(
            "first",
            SelectedLocator(
                AddressedRange(0, 16),
                prerequisiteStructureId: "second"),
            fields: [UnsignedField("count")],
            lengthBytes: 1);
        FirmwareMetadataStructure second = SelectedStructure(
            "second",
            SelectedLocator(
                AddressedRange(16, 16),
                prerequisiteStructureId: "first"),
            fields: [UnsignedField("count")],
            lengthBytes: 1);

        _ = Assert.Throws<ArgumentException>(() => Definition(direct));
        _ = Assert.Throws<ArgumentException>(() => Definition(first, second));
    }

    /// <summary>An unsupported prerequisite value rejects the target without selecting a branch.</summary>
    [Fact]
    public void MetadataSelectedLocatorRejectsUnsupportedValue()
    {
        FirmwareFamilyResolutionDefinition definition = Definition(
            PrerequisiteStructure(),
            SelectedStructure(
                "selected",
                "prerequisite",
                "count"));
        byte[] source = new byte[32];
        source[0] = 3;

        FirmwareMetadataStructureResolution result =
            definition.ResolveMetadataStructure(
                "map",
                "selected",
                Inputs(new FirmwareArtifactPayload("tp-firmware", source)));

        Assert.Equal(
            FirmwareMetadataStructureResolutionStatus.Rejected,
            result.Status);
        Assert.Equal(
            FirmwareMetadataStructureResolutionFailure
                .PrerequisiteValueUnsupported,
            result.Failure);
        Assert.Equal("prerequisite", result.Prerequisite?.StructureId);
        Assert.Null(result.Resolved);
    }

    /// <summary>A malformed prerequisite blocks its dependent structure without a physical fallback.</summary>
    [Fact]
    public void MetadataSelectedLocatorPropagatesPrerequisiteRejection()
    {
        FirmwareMetadataStructure prerequisite = PrerequisiteStructure(
            assertions: [FirmwareMetadataByteAssertion.Exact(0, [0xAA])]);
        FirmwareFamilyResolutionDefinition definition = Definition(
            prerequisite,
            SelectedStructure(
                "selected",
                "prerequisite",
                "count"));

        FirmwareMetadataStructureResolution result =
            definition.ResolveMetadataStructure(
                "map",
                "selected",
                Inputs(new FirmwareArtifactPayload(
                    "tp-firmware",
                    new byte[32])));

        Assert.Equal(
            FirmwareMetadataStructureResolutionStatus.Rejected,
            result.Status);
        Assert.Equal(
            FirmwareMetadataStructureResolutionFailure.PrerequisiteRejected,
            result.Failure);
        Assert.Equal("prerequisite", result.Prerequisite?.StructureId);
        Assert.Null(result.Resolved);
    }

    private static FirmwareMetadataStructure PrerequisiteStructure(
        IEnumerable<FirmwareMetadataField>? fields = null,
        IEnumerable<FirmwareMetadataByteAssertion>? assertions = null)
    {
        return new FirmwareMetadataStructure(
            "prerequisite",
            "tp-firmware",
            1,
            Absolute(0, 1, "allowed"),
            fields ?? [UnsignedField("count")],
            assertions ?? []);
    }

    private static FirmwareMetadataStructure SelectedStructure(
        string structureId,
        string prerequisiteStructureId,
        string prerequisiteFieldId)
    {
        return SelectedStructure(
            structureId,
            SelectedLocator(
                AddressedRange(16, 16),
                prerequisiteStructureId,
                prerequisiteFieldId));
    }

    private static FirmwareMetadataStructure SelectedStructure(
        string structureId,
        FirmwareMetadataFieldSelectedLocator locator,
        IEnumerable<FirmwareMetadataField>? fields = null,
        long lengthBytes = 2)
    {
        return new FirmwareMetadataStructure(
            structureId,
            "tp-firmware",
            lengthBytes,
            locator,
            fields ?? [BytesField("value", 0, checked((int)lengthBytes))],
            []);
    }

    private static FirmwareMetadataFieldSelectedLocator SelectedLocator(
        FirmwareAddressedRange anchorRange,
        string prerequisiteStructureId = "prerequisite",
        string prerequisiteFieldId = "count",
        long resultOffset = 0,
        string allowedResultRegionId = "root")
    {
        return new FirmwareMetadataFieldSelectedLocator(
            prerequisiteStructureId,
            prerequisiteFieldId,
            [
                new FirmwareMetadataFieldSelectedBranch(
                    1,
                    2,
                    anchorRange),
            ],
            resultOffset,
            allowedResultRegionId);
    }

    private static FirmwareAddressedRange AddressedRange(
        long start,
        long length,
        string addressSpaceId = "flash")
    {
        return new FirmwareAddressedRange(
            addressSpaceId,
            new ByteRange(start, length));
    }

    private static FirmwareMetadataField UnsignedField(string fieldId)
    {
        return new FirmwareMetadataField(
            fieldId,
            0,
            1,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian);
    }
}
