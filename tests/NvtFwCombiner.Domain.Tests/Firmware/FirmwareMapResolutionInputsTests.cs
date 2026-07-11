using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests the single atomic input boundary for firmware-map resolution.</summary>
public sealed class FirmwareMapResolutionInputsTests
{
    /// <summary>Verifies artifact identities retain exact hash and length provenance.</summary>
    [Fact]
    public void ArtifactIdentityPreservesVerifiedProvenance()
    {
        FirmwareArtifactIdentity artifact = Artifact("tp", 'a', 64);

        Assert.Equal("tp", artifact.ArtifactId);
        Assert.Equal(new string('a', 64), artifact.Sha256);
        Assert.Equal(64, artifact.LengthBytes);
    }

    /// <summary>Verifies malformed hashes, empty ids, and nonpositive lengths are rejected.</summary>
    [Fact]
    public void ArtifactIdentityRejectsInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareArtifactIdentity(" ", new string('a', 64), 1));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareArtifactIdentity("tp", new string('A', 64), 1));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareArtifactIdentity("tp", new string('a', 63), 1));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareArtifactIdentity("tp", $"{new string('a', 63)}g", 1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareArtifactIdentity(
            "tp",
            new string('a', 64),
            0));
    }

    /// <summary>Verifies decoded facts retain artifact, structure, field, and typed value provenance.</summary>
    [Fact]
    public void DecodedFactPreservesLocatorProvenance()
    {
        FirmwareDecodedMetadataFact fact = Fact(
            "tp-chip-number",
            "tp",
            "firmware-config",
            "chip-number",
            2);

        Assert.Equal("tp-chip-number", fact.FactId);
        Assert.Equal("tp", fact.ArtifactId);
        Assert.Equal("firmware-config", fact.MetadataStructureId);
        Assert.Equal("chip-number", fact.FieldId);
        Assert.Equal(FirmwareMetadataValue.FromInteger(2), fact.Value);
    }

    /// <summary>Verifies decoded facts reject missing identity, locator, field, or scalar data.</summary>
    [Fact]
    public void DecodedFactRejectsInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareDecodedMetadataFact(
            " ",
            "tp",
            "firmware-config",
            "chip-number",
            FirmwareMetadataValue.FromInteger(2)));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareDecodedMetadataFact(
            "tp-chip-number",
            "tp",
            " ",
            "chip-number",
            FirmwareMetadataValue.FromInteger(2)));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareDecodedMetadataFact(
            "tp-chip-number",
            "tp",
            "firmware-config",
            "chip-number",
            null!));
    }

    /// <summary>Verifies artifact and decoded-fact snapshots are sorted and immutable.</summary>
    [Fact]
    public void ConstructorCreatesImmutableProvenanceSnapshots()
    {
        FirmwareArtifactIdentity[] artifacts = [Artifact("tp", 'b', 32), Artifact("dp", 'a', 64)];
        FirmwareDecodedMetadataFact[] facts =
        [
            Fact("tp-chip-number", "tp", "firmware-config-copy", "chip-number", 2),
            Fact("dp-chip-number", "dp", "firmware-config-primary", "chip-number", 1),
        ];
        FirmwareMapResolutionInputs inputs = Inputs(artifacts: artifacts, decodedFacts: facts);
        artifacts[0] = Artifact("changed", 'c', 1);
        facts[0] = Fact("changed", "dp", "metadata", "changed", 9);

        Assert.Equal(["dp", "tp"], inputs.Artifacts.Select(static artifact => artifact.ArtifactId));
        Assert.Equal(["dp-chip-number", "tp-chip-number"],
            inputs.DecodedFacts.Select(static fact => fact.FactId));
        Assert.All(inputs.DecodedFacts, fact => Assert.Equal("chip-number", fact.FieldId));

        IList<FirmwareArtifactIdentity> artifactView = Assert.IsType<IList<FirmwareArtifactIdentity>>(
            inputs.Artifacts,
            exactMatch: false);
        IList<FirmwareDecodedMetadataFact> factView =
            Assert.IsType<IList<FirmwareDecodedMetadataFact>>(
                inputs.DecodedFacts,
                exactMatch: false);
        Assert.True(artifactView.IsReadOnly);
        Assert.True(factView.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => artifactView[0] = Artifact("changed", 'c', 1));
        _ = Assert.Throws<NotSupportedException>(() =>
            factView[0] = Fact("changed", "dp", "metadata", "changed", 9));
    }

    /// <summary>Verifies one field may be decoded from either a distinct structure or artifact.</summary>
    [Fact]
    public void ConstructorPreservesSameFieldFromDistinctPhysicalSources()
    {
        FirmwareDecodedMetadataFact[] distinctStructureFacts =
        [
            Fact("tp-primary", "tp", "firmware-config-primary", "chip-number", 1),
            Fact("tp-copy", "tp", "firmware-config-copy", "chip-number", 1),
        ];
        FirmwareDecodedMetadataFact[] distinctArtifactFacts =
        [
            Fact("tp-primary", "tp", "firmware-config", "chip-number", 1),
            Fact("dp-primary", "dp", "firmware-config", "chip-number", 2),
        ];

        FirmwareMapResolutionInputs distinctStructures = Inputs(decodedFacts: distinctStructureFacts);
        FirmwareMapResolutionInputs distinctArtifacts = Inputs(
            artifacts: [Artifact("tp", 'a', 64), Artifact("dp", 'b', 64)],
            decodedFacts: distinctArtifactFacts);

        Assert.Equal(2, distinctStructures.DecodedFacts.Count);
        Assert.Equal(2, distinctArtifacts.DecodedFacts.Count);
        Assert.All(distinctStructures.DecodedFacts, fact => Assert.Equal("chip-number", fact.FieldId));
        Assert.All(distinctArtifacts.DecodedFacts, fact => Assert.Equal("chip-number", fact.FieldId));
    }

    /// <summary>Verifies derived topology and category sources resolve to declared decoded facts.</summary>
    [Fact]
    public void ConstructorValidatesDerivedSelectionProvenance()
    {
        FirmwareDecodedMetadataFact[] facts =
        [
            Fact("chip-count-source", "tp", "firmware-config", "chip-number", 2),
            Fact("category-source", "tp", "firmware-config", "common-version", 7),
        ];
        var topology = new TopologySelection(
            2,
            "cascade",
            TopologySelectionSource.Derived,
            "chip-count-source");
        var category = new FirmwareCommonCategorySelection("standard", "category-source");

        FirmwareMapResolutionInputs inputs = Inputs(
            topologySelection: topology,
            commonFirmwareCategory: category,
            decodedFacts: facts);

        Assert.Same(topology, inputs.TopologySelection);
        Assert.Same(category, inputs.CommonFirmwareCategory);
    }

    /// <summary>Verifies unknown derived topology and category fact references are rejected.</summary>
    [Fact]
    public void ConstructorRejectsUnknownDerivedSelectionSources()
    {
        var topology = new TopologySelection(
            2,
            "cascade",
            TopologySelectionSource.Derived,
            "missing-fact");
        var category = new FirmwareCommonCategorySelection("standard", "missing-fact");

        _ = Assert.Throws<ArgumentException>(() => Inputs(topologySelection: topology));
        _ = Assert.Throws<ArgumentException>(() => Inputs(commonFirmwareCategory: category));
    }

    /// <summary>Verifies selection identity and category boundaries fail before resolution.</summary>
    [Fact]
    public void ConstructorRejectsInvalidSelectionBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => Inputs(memberId: " "));
        _ = Assert.Throws<ArgumentException>(() => Inputs(modeId: " "));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Inputs(capacityBytes: 0));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareCommonCategorySelection(" ", "fact"));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareCommonCategorySelection("standard", " "));
    }

    /// <summary>Verifies missing, duplicate, null, and cross-artifact provenance is rejected.</summary>
    [Fact]
    public void ConstructorRejectsInvalidProvenanceBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => Inputs(artifacts: []));
        _ = Assert.Throws<ArgumentException>(() => Inputs(artifacts: [null!]));
        _ = Assert.Throws<ArgumentException>(() => Inputs(artifacts:
            [Artifact("same", 'a', 1), Artifact("same", 'b', 1)]));
        _ = Assert.Throws<ArgumentException>(() => Inputs(decodedFacts: [null!]));
        _ = Assert.Throws<ArgumentException>(() => Inputs(decodedFacts:
            [
                Fact("same", "tp", "one", "chip-number", 1),
                Fact("same", "tp", "two", "chip-number", 2),
            ]));
        _ = Assert.Throws<ArgumentException>(() => Inputs(decodedFacts:
            [Fact("unknown-source", "unknown", "metadata", "chip-number", 2)]));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareMapResolutionInputs(
            "NT00001",
            "standard",
            64,
            topologySelection: null,
            commonFirmwareCategory: null,
            [Artifact("tp", 'a', 64)],
            null!));
    }

    /// <summary>Verifies duplicate decodes of one physical field are rejected.</summary>
    [Fact]
    public void ConstructorRejectsDuplicatePhysicalFactSources()
    {
        FirmwareDecodedMetadataFact[] duplicateSourceFacts =
        [
            Fact("first", "tp", "firmware-config", "chip-number", 1),
            Fact("second", "tp", "firmware-config", "chip-number", 2),
        ];

        _ = Assert.Throws<ArgumentException>(() => Inputs(decodedFacts: duplicateSourceFacts));
    }

    private static FirmwareMapResolutionInputs Inputs(
        string memberId = "NT00001",
        string modeId = "standard",
        long capacityBytes = 64,
        TopologySelection? topologySelection = null,
        FirmwareCommonCategorySelection? commonFirmwareCategory = null,
        IEnumerable<FirmwareArtifactIdentity>? artifacts = null,
        IEnumerable<FirmwareDecodedMetadataFact>? decodedFacts = null)
    {
        return new FirmwareMapResolutionInputs(
            memberId,
            modeId,
            capacityBytes,
            topologySelection,
            commonFirmwareCategory,
            artifacts ?? [Artifact("tp", 'a', capacityBytes)],
            decodedFacts ?? []);
    }

    private static FirmwareArtifactIdentity Artifact(string artifactId, char hashCharacter, long lengthBytes)
    {
        return new FirmwareArtifactIdentity(artifactId, new string(hashCharacter, 64), lengthBytes);
    }

    private static FirmwareDecodedMetadataFact Fact(
        string factId,
        string artifactId,
        string structureId,
        string fieldId,
        long value)
    {
        return new FirmwareDecodedMetadataFact(
            factId,
            artifactId,
            structureId,
            fieldId,
            FirmwareMetadataValue.FromInteger(value));
    }
}
