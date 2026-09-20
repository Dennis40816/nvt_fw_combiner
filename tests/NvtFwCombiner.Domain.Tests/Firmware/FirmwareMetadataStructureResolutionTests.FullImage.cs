using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

public sealed partial class FirmwareMetadataStructureResolutionTests
{
    /// <summary>The exact view entrance uses the same locator/decode result without any execution-mode selection.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullImageViewReusesCanonicalResolutionAndRejectsForeignAuthority(bool invalidBytes)
    {
        FirmwareMetadataStructure structure = Structure(Absolute(4, 2, "allowed"),
            fields: [BytesField("value", 0, 2)],
            assertions: [FirmwareMetadataByteAssertion.Exact(0, [0x41])]);
        FirmwareMetadataSet set = MetadataSet("metadata", structure);
        FirmwareImageMap map = Map("map", [set]);
        var binding = new FirmwareFullImageMetadataBinding("selected", structure,
            [new(FirmwareMetadataReferenceTargetKind.Field, "value")], ["evidence"]);
        var view = new FirmwareFullImageMetadataView("full", map, ["NT00001"], [binding], ["evidence"]);
        var family = new FirmwareFamilyResolutionDefinition("family", "1.0.0", FamilyHash,
            [map], [set], [], [], null, [view]);
        byte[] bytes = new byte[32];
        bytes[4] = invalidBytes ? (byte)0 : (byte)0x41;
        bytes[5] = 0x12;
        var captured = new FirmwareArtifactPayload("captured-reference", bytes);
        FirmwareMetadataStructureResolution old = family.ResolveMetadataStructure("map", "config",
            Inputs(new FirmwareArtifactPayload("tp-firmware", bytes)));
        bytes[4] = 0xFF;
        FirmwareMetadataStructureResolution actual = family.ResolveMetadataStructure(view, "NT00001", binding, captured);
        Assert.Equal(old.Status, actual.Status);
        Assert.Equal(old.Failure, actual.Failure);
        Assert.Equal(old.Resolved?.LocatorOutcome.ResolvedRange, actual.Resolved?.LocatorOutcome.ResolvedRange);
        Assert.Equal(old.Resolved?.ArtifactIdentity, actual.Resolved?.ArtifactIdentity);
        if (!invalidBytes)
        {
            Assert.Same(structure, actual.Resolved!.StructureDefinition);
        }

        var foreignView = new FirmwareFullImageMetadataView("full", map, ["NT00001"], [binding], ["evidence"]);
        var foreignBinding = new FirmwareFullImageMetadataBinding("selected", structure,
            binding.TargetReferences, ["evidence"]);
        _ = Assert.Throws<ArgumentException>(() => family.ResolveMetadataStructure(foreignView, "NT00001", binding, captured));
        _ = Assert.Throws<ArgumentException>(() => family.ResolveMetadataStructure(view, "NT99999", binding, captured));
        _ = Assert.Throws<ArgumentException>(() => family.ResolveMetadataStructure(view, "NT00001", foreignBinding, captured));
    }
}
