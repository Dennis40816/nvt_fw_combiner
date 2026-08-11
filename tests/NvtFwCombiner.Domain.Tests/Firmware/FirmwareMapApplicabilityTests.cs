using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests canonical map-applicability construction boundaries.</summary>
public sealed class FirmwareMapApplicabilityTests
{
    /// <summary>Verifies constructor snapshots remain immutable and ordinally sorted.</summary>
    [Fact]
    public void ConstructorCreatesImmutableCanonicalSnapshots()
    {
        string[] members = ["NT00002", "NT00001"];
        var applicability = new FirmwareMapApplicability(
            members,
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            64);
        members[0] = "changed";

        Assert.Equal(["NT00001", "NT00002"], applicability.MemberIds);
        IList<string> exposed = Assert.IsType<IList<string>>(applicability.MemberIds, exactMatch: false);
        Assert.True(exposed.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => exposed[0] = "changed");
    }

    /// <summary>Verifies duplicate, empty, null, and invalid capacity inputs fail closed.</summary>
    [Fact]
    public void ConstructorRejectsInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMapApplicability(
            ["NT00001", "NT00001"],
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            64));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMapApplicability(
            [],
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            64));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareMapApplicability(
            ["NT00001"],
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            0));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMapApplicability(
            ["NT00001"],
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            64,
            metadataPredicates: [null!]));
    }
}
