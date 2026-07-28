using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests owner-declared firmware family relationship invariants.</summary>
public sealed class FirmwareFamilyRelationshipTests
{
    /// <summary>The runtime relationship vocabulary contains only perfect and shared-fact forms.</summary>
    [Fact]
    public void RuntimeRelationshipVocabularyHasExactlyTwoSealedForms()
    {
        Type[] forms =
        [
            .. typeof(FirmwareFamilyRelationship).Assembly
                .GetTypes()
                .Where(type =>
                    type != typeof(FirmwareFamilyRelationship) &&
                    typeof(FirmwareFamilyRelationship).IsAssignableFrom(type))
                .OrderBy(static type => type.Name, StringComparer.Ordinal),
        ];

        Assert.Equal(
            [typeof(PerfectFamilyRelationship), typeof(SharedFactRelationship)],
            forms);
        Assert.All(forms, static type => Assert.True(type.IsSealed));
    }

    /// <summary>A family relationship cannot collapse to a single member.</summary>
    [Fact]
    public void PerfectFamilyRejectsFewerThanTwoMembers()
    {
        _ = Assert.Throws<ArgumentException>(() => new PerfectFamilyRelationship(
            "relationship",
            ["NT51929"],
            "Owner-confirmed perfect family.",
            ["SPEC.md"]));
    }

    /// <summary>Every relationship requires an explicit evidence reference.</summary>
    [Fact]
    public void PerfectFamilyRejectsMissingEvidence()
    {
        _ = Assert.Throws<ArgumentException>(() => new PerfectFamilyRelationship(
            "relationship",
            ["NT51919", "NT51929"],
            "Owner-confirmed perfect family.",
            []));
    }

    /// <summary>A shared-fact relationship retains exact canonical maps and typed facts.</summary>
    [Fact]
    public void SharedFactRelationshipSnapshotsCanonicalReferences()
    {
        FirmwareImageMap map = Map("map-b");
        FirmwareMetadataStructureDefinition definition =
            new("metadata", 1, [], []);
        FirmwareMetadataStructureDefinition otherDefinition =
            new("other-metadata", 1, [], []);
        var relationship = new SharedFactRelationship(
            "relationship",
            FirmwareSharedFactRole.InitialCodeShared,
            ["NT51929", "NT51919"],
            [map],
            [
                FirmwareSharedFactReference.ForMetadataDefinition(otherDefinition),
                FirmwareSharedFactReference.ForMetadataDefinition(definition),
                FirmwareSharedFactReference.ForRegion(map.Regions[0]),
            ],
            "Only the declared facts are shared.",
            ["SPEC.md"]);

        Assert.Equal(FirmwareSharedFactRole.InitialCodeShared, relationship.Role);
        Assert.Equal(["NT51919", "NT51929"], relationship.MemberIds);
        Assert.Equal("Only the declared facts are shared.", relationship.Reason);
        Assert.Equal(["SPEC.md"], relationship.EvidenceRefs);
        Assert.Same(map, Assert.Single(relationship.ApplicableMaps));
        Assert.Equal(
            [
                FirmwareSharedFactKind.Region,
                FirmwareSharedFactKind.MetadataDefinition,
                FirmwareSharedFactKind.MetadataDefinition,
            ],
            relationship.SharedFactReferences.Select(static reference => reference.Kind));
        Assert.Same(
            map.Regions[0],
            relationship.SharedFactReferences[0].Region);
        Assert.Same(
            definition,
            relationship.SharedFactReferences[1].MetadataDefinition);
        Assert.Same(
            otherDefinition,
            relationship.SharedFactReferences[2].MetadataDefinition);
    }

    /// <summary>Shared-fact scope must name at least one exact map and one typed fact.</summary>
    [Fact]
    public void SharedFactRelationshipRejectsMissingScope()
    {
        FirmwareImageMap map = Map("map");
        var reference =
            FirmwareSharedFactReference.ForRegion(map.Regions[0]);

        _ = Assert.Throws<ArgumentException>(() => Shared(
            applicableMaps: [],
            sharedFactReferences: [reference]));
        _ = Assert.Throws<ArgumentException>(() => Shared(
            applicableMaps: [map],
            sharedFactReferences: []));
    }

    /// <summary>Shared-fact scope cannot contain duplicate maps or typed facts.</summary>
    [Fact]
    public void SharedFactRelationshipRejectsDuplicateScope()
    {
        FirmwareImageMap map = Map("map");
        var reference =
            FirmwareSharedFactReference.ForRegion(map.Regions[0]);

        _ = Assert.Throws<ArgumentException>(() => Shared(
            applicableMaps: [map, map],
            sharedFactReferences: [reference]));
        _ = Assert.Throws<ArgumentException>(() => Shared(
            applicableMaps: [map],
            sharedFactReferences: [reference, reference]));
    }

    /// <summary>Typed shared-fact factories and roles fail closed on invalid runtime values.</summary>
    [Fact]
    public void SharedFactRelationshipRejectsInvalidTypedInputs()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            FirmwareSharedFactReference.ForRegion(null!));
        _ = Assert.Throws<ArgumentNullException>(() =>
            FirmwareSharedFactReference.ForMetadataDefinition(null!));

        FirmwareImageMap map = Map("map");
        var reference =
            FirmwareSharedFactReference.ForRegion(map.Regions[0]);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new SharedFactRelationship(
            "relationship",
            (FirmwareSharedFactRole)int.MaxValue,
            ["NT51919", "NT51929"],
            [map],
            [reference],
            "Only the declared facts are shared.",
            ["SPEC.md"]));
    }

    /// <summary>Set-like relationship inputs reject blank and duplicate identities.</summary>
    [Fact]
    public void RelationshipsRejectInvalidSetValues()
    {
        _ = Assert.Throws<ArgumentException>(() => new PerfectFamilyRelationship(
            "relationship",
            ["NT51929", " "],
            "Owner-confirmed perfect family.",
            ["SPEC.md"]));
        _ = Assert.Throws<ArgumentException>(() => new PerfectFamilyRelationship(
            "relationship",
            ["NT51929", "NT51929"],
            "Owner-confirmed perfect family.",
            ["SPEC.md"]));
    }

    private static SharedFactRelationship Shared(
        IEnumerable<FirmwareImageMap> applicableMaps,
        IEnumerable<FirmwareSharedFactReference> sharedFactReferences)
    {
        return new SharedFactRelationship(
            "relationship",
            FirmwareSharedFactRole.TpShared,
            ["NT51919", "NT51929"],
            applicableMaps,
            sharedFactReferences,
            "Only the declared facts are shared.",
            ["SPEC.md"]);
    }

    private static FirmwareImageMap Map(string mapId)
    {
        FirmwareRegionSet regionSet = new(
            "regions",
            "flash",
            [
                new FirmwareRegion(
                    "root",
                    null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 16),
                    FirmwareWriteConstraint.Forbidden),
            ],
            ["region-evidence"]);
        string[] members = ["NT51919", "NT51929"];
        FirmwareMapFactBinding<FirmwareRegionSet>[] bindings =
        [
            .. members.Select(memberId => DirectBinding(memberId, mapId, regionSet)),
        ];

        return new FirmwareImageMap(
            mapId,
            "flash",
            new FirmwareMapApplicability(
                members,
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                16),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            bindings,
            [],
            ["map-evidence"]);
    }

    private static FirmwareMapFactBinding<TFact> DirectBinding<TFact>(
        string memberId,
        string mapId,
        TFact value)
        where TFact : class, IFirmwareMapFact
    {
        FirmwareMapFactKey key =
            new(memberId, mapId, value.FactKind, value.CanonicalFactId);
        var applicability = new FirmwareFactApplicability(
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            16);
        return new FirmwareMapFactBinding<TFact>(
            key,
            key,
            value.CanonicalFactId,
            value,
            applicability,
            new FirmwareFactProvenance(key, key, [], value.EvidenceRefs));
    }
}
