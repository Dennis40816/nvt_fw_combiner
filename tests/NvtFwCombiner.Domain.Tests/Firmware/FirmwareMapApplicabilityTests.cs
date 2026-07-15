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
        FirmwareMapApplicabilityEvaluation evaluation = applicability.Evaluate(Inputs());
        Assert.Equal(FirmwareApplicabilityResult.Match, evaluation.Result);
        Assert.Empty(evaluation.PendingRequirements);
    }

    /// <summary>Verifies known member, mode, and capacity contradictions reject the shape.</summary>
    [Fact]
    public void EvaluateRejectsKnownIdentityContradictions()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.NoTopologyConstraint());

        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(memberId: "NT00002")).Result);
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(modeId: "ab")).Result);
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(capacityBytes: 128)).Result);
    }

    /// <summary>Verifies requested topology matches exactly and missing topology remains pending.</summary>
    [Fact]
    public void EvaluateHandlesRequestedTopologyWithoutGuessing()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.RequireSingleChip());

        FirmwareMapApplicabilityEvaluation pending = applicability.Evaluate(Inputs());
        Assert.Equal(FirmwareApplicabilityResult.Pending, pending.Result);
        Assert.Equal(
            [FirmwareMapPendingRequirementKind.RequestedTopologyMissing],
            pending.PendingRequirements);
        Assert.Equal(
            FirmwareApplicabilityResult.Match,
            applicability.Evaluate(Inputs(requestedTopology: Selection(1, "single"))).Result);
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(requestedTopology: Selection(2, "cascade"))).Result);
    }

    /// <summary>Verifies Common FW category remains pending until resolver-owned derivation.</summary>
    [Fact]
    public void EvaluateDefersCommonFirmwareCategoryToResolver()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.NoTopologyConstraint(),
            commonFirmwareCategoryIds: ["standard"]);

        FirmwareMapApplicabilityEvaluation evaluation = applicability.Evaluate(Inputs());
        Assert.Equal(FirmwareApplicabilityResult.Pending, evaluation.Result);
        Assert.Equal(
            [FirmwareMapPendingRequirementKind.CommonFirmwareCategoryDerivationUnavailable],
            evaluation.PendingRequirements);
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
            applicability.Evaluate(Inputs(artifacts: [Payload("tp-firmware", 2)])).Result);
        Assert.Equal(
            [FirmwareMapPendingRequirementKind.MetadataResolutionRequired],
            applicability.Evaluate(Inputs()).PendingRequirements);
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
            applicability.Evaluate(Inputs(memberId: "NT00002")).Result);
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(requestedTopology: Selection(2, "cascade"))).Result);
        Assert.Empty(applicability.Evaluate(Inputs(memberId: "NT00002")).PendingRequirements);
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
            applicability.Evaluate(Inputs(artifacts: artifacts)).Result);
    }

    /// <summary>Verifies simultaneous unresolved requirements are complete, canonical, and immutable.</summary>
    [Fact]
    public void EvaluateReturnsCanonicalPendingRequirementSet()
    {
        FirmwareMetadataPredicate[] predicates =
        [
            Equal("chip-number", 2),
            Equal("panel-count", 3),
        ];
        string[] categories = ["z-category", "a-category"];
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.RequireCascade(),
            categories,
            predicates);
        categories[0] = "changed";
        predicates[0] = Equal("changed", 9);

        FirmwareMapApplicabilityEvaluation evaluation = applicability.Evaluate(Inputs());

        Assert.Equal(FirmwareApplicabilityResult.Pending, evaluation.Result);
        Assert.Equal(
            [
                FirmwareMapPendingRequirementKind.RequestedTopologyMissing,
                FirmwareMapPendingRequirementKind.CommonFirmwareCategoryDerivationUnavailable,
                FirmwareMapPendingRequirementKind.MetadataResolutionRequired,
            ],
            evaluation.PendingRequirements);
        IList<FirmwareMapPendingRequirementKind> exposed = Assert.IsType<IList<FirmwareMapPendingRequirementKind>>(
            evaluation.PendingRequirements,
            exactMatch: false);
        Assert.True(exposed.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() =>
            exposed[0] = FirmwareMapPendingRequirementKind.MetadataResolutionRequired);
    }

    /// <summary>Verifies equivalent declarations produce equal detailed evaluations.</summary>
    [Fact]
    public void EvaluateUsesValueEqualityIndependentOfDeclarationOrder()
    {
        var first = new FirmwareMapApplicability(
            ["NT00002", "NT00001"],
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            64,
            ["category-b", "category-a"],
            [Equal("field-b", 2), Equal("field-a", 1)]);
        var second = new FirmwareMapApplicability(
            ["NT00001", "NT00002"],
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            64,
            ["category-a", "category-b"],
            [Equal("field-a", 1), Equal("field-b", 2)]);

        FirmwareMapApplicabilityEvaluation firstEvaluation = first.Evaluate(Inputs());
        FirmwareMapApplicabilityEvaluation secondEvaluation = second.Evaluate(Inputs());

        Assert.Equal(firstEvaluation, secondEvaluation);
        Assert.Equal(firstEvaluation.GetHashCode(), secondEvaluation.GetHashCode());
        Assert.Empty(typeof(FirmwareMapApplicabilityEvaluation).GetConstructors());
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
