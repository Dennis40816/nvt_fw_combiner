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
        FirmwareMetadataStructureDefinition definition =
            new("metadata", 1, [], []);
        FirmwareMetadataStructureDefinition otherDefinition =
            new("other-metadata", 1, [], []);
        FirmwareImageMap map = Map(
            "map-b",
            metadataDefinitions: [definition, otherDefinition]);
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

    /// <summary>Every applicable map is contained by and collectively covers the relationship members.</summary>
    [Fact]
    public void SharedFactRelationshipRejectsInvalidMemberCoverage()
    {
        FirmwareImageMap missingMemberMap = Map(
            "missing-member",
            members: ["NT51919"]);
        FirmwareImageMap outsideMemberMap = Map(
            "outside-member",
            members: ["NT51919", "NT51929", "NT51932"]);

        _ = Assert.Throws<ArgumentException>(() => Shared(
            applicableMaps: [missingMemberMap],
            sharedFactReferences:
            [
                FirmwareSharedFactReference.ForRegion(missingMemberMap.Regions[0]),
            ]));
        _ = Assert.Throws<ArgumentException>(() => Shared(
            applicableMaps: [outsideMemberMap],
            sharedFactReferences:
            [
                FirmwareSharedFactReference.ForRegion(outsideMemberMap.Regions[0]),
            ]));
    }

    /// <summary>Every referenced fact is the exact canonical instance exposed by every applicable map.</summary>
    [Fact]
    public void SharedFactRelationshipRejectsMissingOrDivergentCanonicalReferences()
    {
        FirmwareImageMap firstMap = Map("first-map");
        FirmwareImageMap secondMap = Map("second-map");
        _ = Assert.Throws<ArgumentException>(() => Shared(
            applicableMaps: [firstMap, secondMap],
            sharedFactReferences:
            [
                FirmwareSharedFactReference.ForRegion(firstMap.Regions[0]),
            ]));

        FirmwareMetadataStructureDefinition definition =
            new("metadata", 1, [], []);
        FirmwareImageMap mapWithoutMetadata = Map("without-metadata");
        _ = Assert.Throws<ArgumentException>(() => Shared(
            applicableMaps: [mapWithoutMetadata],
            sharedFactReferences:
            [
                FirmwareSharedFactReference.ForMetadataDefinition(definition),
            ]));

        FirmwareMetadataStructureDefinition sameIdDifferentInstance =
            new("metadata", 1, [], []);
        FirmwareImageMap mapWithDifferentDefinition = Map(
            "different-definition",
            metadataDefinitions: [sameIdDifferentInstance]);
        _ = Assert.Throws<ArgumentException>(() => Shared(
            applicableMaps: [mapWithDifferentDefinition],
            sharedFactReferences:
            [
                FirmwareSharedFactReference.ForMetadataDefinition(definition),
            ]));
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

    /// <summary>The family definition, not Profiles, owns perfect-family map coverage.</summary>
    [Fact]
    public void DefinitionRejectsPerfectFamilyMemberMapOverride()
    {
        PerfectFamilyRelationship relationship = Perfect();

        _ = Assert.ThrowsAny<ArgumentException>(() => Definition(
            [Map("family-map"), Map("member-map", ["NT51919"])],
            [relationship]));
    }

    /// <summary>Perfect-family members cannot carry aliases or capability overrides.</summary>
    [Fact]
    public void DefinitionRejectsPerfectFamilyMemberFactOverrides()
    {
        PerfectFamilyRelationship relationship = Perfect();
        FirmwareImageMap aliasedMap = Map("family-map", aliasedMember: "NT51919");
        FirmwareCapabilityFact capability = new(
            "capability-fact",
            "synthetic-capability",
            FirmwareCapabilityState.ConfirmedPresent,
            "Synthetic evidence.",
            ["capability-evidence"]);

        _ = Assert.ThrowsAny<ArgumentException>(() => Definition([aliasedMap], [relationship]));
        _ = Assert.ThrowsAny<ArgumentException>(() => Definition(
            [Map("family-map")],
            [relationship],
            [DirectBinding("NT51919", "family-map", capability)]));
    }

    /// <summary>One canonical map fact cannot belong to two shared relationships.</summary>
    [Fact]
    public void DefinitionRejectsDuplicateSharedFactBindings()
    {
        FirmwareImageMap map = Map("family-map");
        var reference =
            FirmwareSharedFactReference.ForRegion(map.Regions[0]);

        _ = Assert.ThrowsAny<ArgumentException>(() => Definition(
            [map],
            [
                Shared("first", map, reference),
                Shared("second", map, reference),
            ]));
    }

    private static PerfectFamilyRelationship Perfect()
    {
        return new PerfectFamilyRelationship(
            "perfect",
            ["NT51919", "NT51929"],
            "Owner-confirmed perfect family.",
            ["SPEC.md"]);
    }

    private static SharedFactRelationship Shared(
        string relationshipId,
        FirmwareImageMap map,
        FirmwareSharedFactReference reference)
    {
        return new SharedFactRelationship(
            relationshipId,
            FirmwareSharedFactRole.TpShared,
            ["NT51919", "NT51929"],
            [map],
            [reference],
            "Only the declared facts are shared.",
            ["SPEC.md"]);
    }

    private static FirmwareFamilyResolutionDefinition Definition(
        IEnumerable<FirmwareImageMap> maps,
        IEnumerable<FirmwareFamilyRelationship> relationships,
        IEnumerable<FirmwareMapFactBinding<FirmwareCapabilityFact>>? capabilities = null)
    {
        return new FirmwareFamilyResolutionDefinition(
            "test-family",
            "1.0.0",
            new string('a', 64),
            maps,
            [],
            capabilities ?? [],
            relationships);
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

    private static FirmwareImageMap Map(
        string mapId,
        IReadOnlyList<string>? members = null,
        IReadOnlyList<FirmwareMetadataStructureDefinition>? metadataDefinitions = null,
        string? aliasedMember = null)
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
        IReadOnlyList<string> selectedMembers = members ?? ["NT51919", "NT51929"];
        FirmwareMapFactBinding<FirmwareRegionSet>[] bindings =
        [
            .. selectedMembers.Select(memberId => StringComparer.Ordinal.Equals(memberId, aliasedMember)
                ? AliasedBinding(memberId, selectedMembers.First(other => other != memberId), mapId, regionSet)
                : DirectBinding(memberId, mapId, regionSet)),
        ];
        FirmwareMapFactBinding<FirmwareMetadataSet>[] metadataBindings =
            CreateMetadataBindings(mapId, selectedMembers, metadataDefinitions ?? []);

        return new FirmwareImageMap(
            mapId,
            "flash",
            new FirmwareMapApplicability(
                selectedMembers,
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                16),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            bindings,
            metadataBindings,
            ["map-evidence"]);
    }

    private static FirmwareMapFactBinding<FirmwareMetadataSet>[] CreateMetadataBindings(
        string mapId,
        IReadOnlyList<string> members,
        IReadOnlyList<FirmwareMetadataStructureDefinition> definitions)
    {
        if (definitions.Count == 0)
        {
            return [];
        }

        FirmwareMetadataStructure[] structures =
        [
            .. definitions.Select(definition => new FirmwareMetadataStructure(
                definition.DefinitionId,
                "tp-input",
                definition,
                new FirmwareAbsoluteRangeLocator(
                    new FirmwareAddressedRange(
                        "flash",
                        new ByteRange(0, definition.LengthBytes)),
                    "root"))),
        ];
        var metadataSet = new FirmwareMetadataSet(
            "metadata",
            structures,
            ["metadata-evidence"]);
        return
        [
            .. members.Select(memberId => DirectBinding(memberId, mapId, metadataSet)),
        ];
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

    private static FirmwareMapFactBinding<TFact> AliasedBinding<TFact>(
        string memberId,
        string sourceMemberId,
        string mapId,
        TFact value)
        where TFact : class, IFirmwareMapFact
    {
        FirmwareMapFactKey target = new(memberId, mapId, value.FactKind, value.CanonicalFactId);
        FirmwareMapFactKey source = new(sourceMemberId, mapId, value.FactKind, value.CanonicalFactId);
        var applicability = new FirmwareFactApplicability(
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            16);
        var hop = new FirmwareFactAliasHop(
            "member-alias",
            target,
            source,
            applicability,
            "Synthetic alias.",
            ["alias-evidence"]);
        return new FirmwareMapFactBinding<TFact>(
            target,
            source,
            value.CanonicalFactId,
            value,
            applicability,
            new FirmwareFactProvenance(target, source, [hop], value.EvidenceRefs));
    }
}
