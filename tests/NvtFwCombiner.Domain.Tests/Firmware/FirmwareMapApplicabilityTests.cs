using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests deterministic, three-state firmware-map applicability.</summary>
public sealed class FirmwareMapApplicabilityTests
{
    /// <summary>Verifies a topology-independent shape matches exact member, mode, and capacity.</summary>
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

    /// <summary>Verifies a required topology is pending until selected and rejects a wrong count.</summary>
    [Fact]
    public void EvaluateHandlesTopologyWithoutGuessing()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.RequireSingleChip());

        Assert.Equal(FirmwareApplicabilityResult.Pending, applicability.Evaluate(Inputs()));
        Assert.Equal(
            FirmwareApplicabilityResult.Match,
            applicability.Evaluate(Inputs(topology: Selection(1, "single"))));
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(topology: Selection(2, "cascade"))));
    }

    /// <summary>Verifies Common FW category matching is ordinal and missing data remains pending.</summary>
    [Fact]
    public void EvaluateHandlesCommonFirmwareCategory()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.NoTopologyConstraint(),
            commonFirmwareCategoryIds: ["standard"]);

        Assert.Equal(FirmwareApplicabilityResult.Pending, applicability.Evaluate(Inputs()));
        Assert.Equal(
            FirmwareApplicabilityResult.Match,
            applicability.Evaluate(Inputs(commonFirmwareCategoryId: "standard")));
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(commonFirmwareCategoryId: "STANDARD")));
    }

    /// <summary>Verifies metadata predicates remain pending until a map scopes their source.</summary>
    [Fact]
    public void EvaluateDefersMetadataPredicatesUntilMapScopesFacts()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.NoTopologyConstraint(),
            metadataPredicates: [Equal("chip-number", 2)]);
        FirmwareDecodedMetadataFact matchingUnrelatedFact = new(
            "unrelated-chip-number",
            "firmware",
            "unrelated-config",
            "chip-number",
            FirmwareMetadataValue.FromInteger(2));
        FirmwareDecodedMetadataFact contradictingUnrelatedFact = new(
            "unrelated-chip-number",
            "firmware",
            "unrelated-config",
            "chip-number",
            FirmwareMetadataValue.FromInteger(1));

        Assert.Equal(FirmwareApplicabilityResult.Pending, applicability.Evaluate(Inputs()));
        Assert.Equal(
            FirmwareApplicabilityResult.Pending,
            applicability.Evaluate(Inputs(decodedFacts: [matchingUnrelatedFact])));
        Assert.Equal(
            FirmwareApplicabilityResult.Pending,
            applicability.Evaluate(Inputs(decodedFacts: [contradictingUnrelatedFact])));
    }

    /// <summary>Verifies known non-metadata contradictions outrank deferred metadata predicates.</summary>
    [Fact]
    public void EvaluatePrioritizesKnownContradictionOverDeferredMetadata()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.NoTopologyConstraint(),
            metadataPredicates:
        [
            Equal("chip-number", 2),
            Equal("common-version", 7),
        ]);

        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(memberId: "NT00002")));
    }

    /// <summary>Verifies contradictions across discriminator groups outrank missing facts.</summary>
    [Fact]
    public void EvaluatePrioritizesAnyKnownDiscriminatorContradiction()
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
            applicability.Evaluate(Inputs(commonFirmwareCategoryId: "other")));
        Assert.Equal(
            FirmwareApplicabilityResult.NoMatch,
            applicability.Evaluate(Inputs(topology: Selection(2, "cascade"))));
    }

    /// <summary>Verifies duplicate field ids from different artifacts remain unresolved until map scoping.</summary>
    [Fact]
    public void EvaluateDoesNotGuessBetweenArtifactScopedFacts()
    {
        FirmwareMapApplicability applicability = CreateApplicability(
            TopologyRequirement.NoTopologyConstraint(),
            metadataPredicates: [Equal("chip-number", 2)]);
        FirmwareArtifactIdentity[] artifacts =
        [
            Artifact(),
            new FirmwareArtifactIdentity("backup", new string('1', 64), 64),
        ];
        FirmwareDecodedMetadataFact[] facts =
        [
            new("primary-chip-number", "firmware", "primary-config", "chip-number",
                FirmwareMetadataValue.FromInteger(2)),
            new("backup-chip-number", "backup", "backup-config", "chip-number",
                FirmwareMetadataValue.FromInteger(2)),
        ];

        Assert.Equal(
            FirmwareApplicabilityResult.Pending,
            applicability.Evaluate(Inputs(artifacts: artifacts, decodedFacts: facts)));
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
        TopologySelection? topology = null,
        string? commonFirmwareCategoryId = null,
        IEnumerable<FirmwareArtifactIdentity>? artifacts = null,
        IEnumerable<FirmwareDecodedMetadataFact>? decodedFacts = null)
    {
        List<FirmwareDecodedMetadataFact> facts = [.. decodedFacts ?? []];
        FirmwareCommonCategorySelection? category = null;
        if (commonFirmwareCategoryId is not null)
        {
            const string categoryFactId = "common-category-selection";
            facts.Add(new FirmwareDecodedMetadataFact(
                categoryFactId,
                "firmware",
                "firmware-config",
                "common-category",
                FirmwareMetadataValue.FromText(commonFirmwareCategoryId)));
            category = new FirmwareCommonCategorySelection(
                commonFirmwareCategoryId,
                categoryFactId);
        }

        return new FirmwareMapResolutionInputs(
            memberId,
            modeId,
            capacityBytes,
            topology,
            category,
            artifacts ?? [Artifact()],
            facts);
    }

    private static FirmwareArtifactIdentity Artifact()
    {
        return new FirmwareArtifactIdentity("firmware", new string('0', 64), 64);
    }

    private static TopologySelection Selection(int chipCount, string label)
    {
        return new TopologySelection(
            chipCount,
            label,
            TopologySelectionSource.Requested,
            "compile-request");
    }

    private static FirmwareMetadataPredicate Equal(string fieldId, long value)
    {
        return new FirmwareMetadataPredicate(
            fieldId,
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromInteger(value)]);
    }

}
