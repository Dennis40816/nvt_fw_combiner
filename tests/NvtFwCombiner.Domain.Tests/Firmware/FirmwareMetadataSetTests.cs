using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests immutable evidence-backed metadata sets.</summary>
public sealed class FirmwareMetadataSetTests
{
    /// <summary>Verifies structures and evidence are snapshotted and ordinally sorted.</summary>
    [Fact]
    public void ConstructorCreatesImmutableDeterministicSnapshots()
    {
        FirmwareMetadataStructure[] structures = [Structure("version"), Structure("config")];
        string[] evidenceRefs = ["evidence-z", "evidence-a"];

        var metadataSet = new FirmwareMetadataSet("primary-metadata", structures, evidenceRefs);
        structures[0] = Structure("changed");
        evidenceRefs[0] = "changed";

        Assert.Equal("primary-metadata", metadataSet.MetadataSetId);
        Assert.Equal(["config", "version"],
            metadataSet.Structures.Select(static structure => structure.StructureId));
        Assert.Equal(["evidence-a", "evidence-z"], metadataSet.EvidenceRefs);

        IList<FirmwareMetadataStructure> structureView =
            Assert.IsType<IList<FirmwareMetadataStructure>>(
                metadataSet.Structures,
                exactMatch: false);
        IList<string> evidenceView = Assert.IsType<IList<string>>(
            metadataSet.EvidenceRefs,
            exactMatch: false);
        Assert.True(structureView.IsReadOnly);
        Assert.True(evidenceView.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => structureView[0] = Structure("changed"));
        _ = Assert.Throws<NotSupportedException>(() => evidenceView[0] = "changed");
    }

    /// <summary>Verifies metadata-set identity, structure, and evidence boundaries fail closed.</summary>
    [Fact]
    public void ConstructorRejectsInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => Create(metadataSetId: " "));
        _ = Assert.Throws<ArgumentException>(() => Create(structures: []));
        _ = Assert.Throws<ArgumentException>(() => Create(structures: [null!]));
        _ = Assert.Throws<ArgumentException>(() => Create(structures:
            [Structure("same"), Structure("same")]));
        _ = Assert.Throws<ArgumentException>(() => Create(evidenceRefs: []));
        _ = Assert.Throws<ArgumentException>(() => Create(evidenceRefs: ["evidence", "evidence"]));
        _ = Assert.Throws<ArgumentException>(() => Create(evidenceRefs: [" "]));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareMetadataSet(
            "primary-metadata",
            null!,
            ["evidence"]));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareMetadataSet(
            "primary-metadata",
            [Structure("config")],
            null!));
    }

    private static FirmwareMetadataSet Create(
        string metadataSetId = "primary-metadata",
        IEnumerable<FirmwareMetadataStructure>? structures = null,
        IEnumerable<string>? evidenceRefs = null)
    {
        return new FirmwareMetadataSet(
            metadataSetId,
            structures ?? [Structure("config")],
            evidenceRefs ?? ["evidence"]);
    }

    private static FirmwareMetadataStructure Structure(string structureId)
    {
        return new FirmwareMetadataStructure(
            structureId,
            "tp-firmware",
            4,
            new FirmwareAbsoluteRangeLocator(
                new FirmwareAddressedRange("flash", new ByteRange(0, 4)),
                "root"),
            [],
            []);
    }
}
