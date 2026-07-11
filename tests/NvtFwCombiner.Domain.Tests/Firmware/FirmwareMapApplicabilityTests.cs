using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests deterministic pre-resolution firmware-map applicability.</summary>
public sealed class FirmwareMapApplicabilityTests
{
    /// <summary>Verifies a topology-independent shape matches exact static identity.</summary>
    [Fact]
    public void EvaluateMatchesTopologyIndependentShape()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.NoTopologyConstraint());

        Assert.Equal(FirmwareApplicabilityResult.Pending, default);
        Assert.Equal(FirmwareApplicabilityResult.Match, applicability.Evaluate(Inputs()));
    }

    /// <summary>Verifies known member, mode, and capacity contradictions reject the shape.</summary>
    [Fact]
    public void EvaluateRejectsKnownIdentityContradictions()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.NoTopologyConstraint());

        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(memberId: "NT00002")));
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(modeId: "ab")));
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(capacityBytes: 128)));
    }

    /// <summary>Verifies requested topology matches exactly and missing topology remains pending.</summary>
    [Fact]
    public void EvaluateHandlesRequestedTopologyWithoutGuessing()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.RequireSingleChip());

        Assert.Equal(FirmwareApplicabilityResult.Pending, applicability.Evaluate(Inputs()));
        Assert.Equal(
            FirmwareApplicabilityResult.Match,
            applicability.Evaluate(Inputs(requestedTopology: Selection(1, "single"))));
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(requestedTopology: Selection(2, "cascade"))));
    }

    /// <summary>Verifies Common FW category remains pending until resolver-owned derivation.</summary>
    [Fact]
    public void EvaluateDefersCommonFirmwareCategoryToResolver()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.NoTopologyConstraint(),
            commonFirmwareCategoryIds: ["standard"]);

        Assert.Equal(FirmwareApplicabilityResult.Pending, applicability.Evaluate(Inputs()));
    }

    /// <summary>Verifies metadata predicates remain pending even when matching artifact bytes exist.</summary>
    [Fact]
    public void EvaluateDefersMetadataPredicatesToResolver()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.NoTopologyConstraint(),
            metadataPredicates: [Equal("chip-number", 2)]);

        Assert.Equal(
            FirmwareApplicabilityResult.Pending,
            applicability.Evaluate(Inputs(artifacts: [Payload("tp-firmware", 2)])));
    }

    /// <summary>Verifies known contradictions outrank resolver-owned pending discriminators.</summary>
    [Fact]
    public void EvaluatePrioritizesKnownContradictions()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.RequireSingleChip(),
            commonFirmwareCategoryIds: ["standard"],
            metadataPredicates: [Equal("chip-number", 2)]);

        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(memberId: "NT00002")));
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(requestedTopology: Selection(2, "cascade"))));
    }

    /// <summary>Verifies multiple artifact bindings never trigger pre-resolver metadata guessing.</summary>
    [Fact]
    public void EvaluateDoesNotGuessBetweenArtifactPayloads()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.NoTopologyConstraint(),
            metadataPredicates: [Equal("chip-number", 2)]);
        FirmwareArtifactPayload[] artifacts =
        [
            Payload("tp-firmware", 2),
            Payload("dp-firmware", 2),
        ];

        Assert.Equal(
            FirmwareApplicabilityResult.Pending,
            applicability.Evaluate(Inputs(artifacts: artifacts)));
    }

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

    private static FirmwareMapApplicability CreateApplicability(
        TopologyRequirement topologyRequirement,
        IEnumerable<string>? commonFirmwareCategoryIds = null,
        IEnumerable<FirmwareMetadataPredicate>? metadataPredicates = null)
    {
        return new FirmwareMapApplicability(
            ["NT00001"],
            ["standard"],
            topologyRequirement,
            64,
            commonFirmwareCategoryIds,
            metadataPredicates);
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

    private static TopologySelection Selection(int chipCount, string label)
    {
        return new TopologySelection(
            chipCount,
            label,
            TopologySelectionSource.Requested,
            "compile-request");
    }

    private static FirmwareMetadataPredicate Equal(string fieldId, ulong value)
    {
        return new FirmwareMetadataPredicate(
            "firmware-config",
            fieldId,
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(value)]);
    }
}
