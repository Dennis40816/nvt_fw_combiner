using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests the single atomic run-input boundary for firmware-map resolution.</summary>
public sealed class FirmwareMapResolutionInputsTests
{
    /// <summary>Verifies payload identity is computed from a private byte snapshot.</summary>
    [Fact]
    public void PayloadComputesIdentityFromSnapshottedBytes()
    {
        byte[] source = [1, 2, 3];

        var payload = new FirmwareArtifactPayload("tp-firmware", source);
        source[0] = 9;

        Assert.Equal("tp-firmware", payload.ArtifactId);
        Assert.Equal(3, payload.LengthBytes);
        Assert.Equal(
            "039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81",
            payload.Sha256);
        Assert.Equal(payload.ArtifactId, payload.Identity.ArtifactId);
        Assert.Equal(payload.Sha256, payload.Identity.Sha256);
        Assert.Equal(payload.LengthBytes, payload.Identity.LengthBytes);
    }

    /// <summary>Verifies payload identity and byte boundaries fail closed.</summary>
    [Fact]
    public void PayloadRejectsInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareArtifactPayload(" ", [1]));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareArtifactPayload("tp-firmware", []));
    }

    /// <summary>Verifies artifact collections are snapshotted, sorted, and read-only.</summary>
    [Fact]
    public void ConstructorCreatesImmutableArtifactSnapshot()
    {
        FirmwareArtifactPayload[] artifacts = [Payload("tp-firmware", 2), Payload("dp-firmware", 1)];
        FirmwareMapResolutionInputs inputs = Inputs(artifacts: artifacts);
        artifacts[0] = Payload("changed", 3);

        Assert.Equal(["dp-firmware", "tp-firmware"],
            inputs.Artifacts.Select(static artifact => artifact.ArtifactId));
        IList<FirmwareArtifactPayload> exposed = Assert.IsType<IList<FirmwareArtifactPayload>>(
            inputs.Artifacts,
            exactMatch: false);
        Assert.True(exposed.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => exposed[0] = Payload("changed", 3));
    }

    /// <summary>Verifies caller-requested topology is retained without deriving metadata.</summary>
    [Fact]
    public void ConstructorPreservesRequestedTopology()
    {
        TopologySelection requested = RequestedTopology(2, "cascade");

        FirmwareMapResolutionInputs inputs = Inputs(requestedTopology: requested);

        TopologySelection actual = Assert.IsType<TopologySelection>(inputs.RequestedTopology);
        Assert.Same(requested, actual);
        Assert.Equal(TopologySelectionSource.Requested, actual.Source);
    }

    /// <summary>Verifies callers cannot inject a derived topology selection.</summary>
    [Fact]
    public void ConstructorRejectsCallerSuppliedDerivedTopology()
    {
        var derived = new TopologySelection(
            2,
            "cascade",
            TopologySelectionSource.Derived,
            "firmware-config-chip-number");

        _ = Assert.Throws<ArgumentException>(() => Inputs(requestedTopology: derived));
    }

    /// <summary>Verifies selection and artifact collection boundaries fail closed.</summary>
    [Fact]
    public void ConstructorRejectsInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => Inputs(memberId: " "));
        _ = Assert.Throws<ArgumentException>(() => Inputs(modeId: " "));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Inputs(capacityBytes: 0));
        _ = Assert.Throws<ArgumentException>(() => Inputs(artifacts: []));
        _ = Assert.Throws<ArgumentException>(() => Inputs(artifacts: [null!]));
        _ = Assert.Throws<ArgumentException>(() => Inputs(artifacts:
            [Payload("same", 1), Payload("same", 2)]));
        _ = Assert.Throws<ArgumentNullException>(() => new FirmwareMapResolutionInputs(
            "NT00001",
            "standard",
            64,
            requestedTopology: null,
            null!));
    }

    private static FirmwareMapResolutionInputs Inputs(
        string memberId = "NT00001",
        string modeId = "standard",
        long capacityBytes = 64,
        TopologySelection? requestedTopology = null,
        IEnumerable<FirmwareArtifactPayload>? artifacts = null)
    {
        return new FirmwareMapResolutionInputs(
            memberId,
            modeId,
            capacityBytes,
            requestedTopology,
            artifacts ?? [Payload("tp-firmware", 1)]);
    }

    private static FirmwareArtifactPayload Payload(string artifactId, byte value)
    {
        return new FirmwareArtifactPayload(artifactId, [value]);
    }

    private static TopologySelection RequestedTopology(int chipCount, string label)
    {
        return new TopologySelection(
            chipCount,
            label,
            TopologySelectionSource.Requested,
            "compile-request");
    }
}
