using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class FirmwareFamilyResolutionNormalizerTests
{
    /// <summary>
    /// A perfect-like family owns one map and one metadata definition while
    /// retaining member-specific effective bindings.
    /// </summary>
    [Fact]
    public void NormalizeMaterializesPerfectLikeFamilyWithoutCloningFacts()
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareFamilyDocument document = source with
        {
            Members =
            [
                new FirmwareFamilyMemberDocument("NT00001", "Synthetic IC 1"),
                new FirmwareFamilyMemberDocument("NT00002", "Synthetic IC 2"),
            ],
            Capabilities = [],
            ImageMaps =
            [
                map with
                {
                    Applicability = map.Applicability with
                    {
                        MemberIds = ["NT00001", "NT00002"],
                    },
                },
            ],
            FamilyRelationships =
            [
                new FirmwarePerfectFamilyRelationshipDocument(
                    "synthetic-perfect-family",
                    ["NT00001", "NT00002"],
                    "Owner-confirmed synthetic equality.",
                    ["perfect-evidence"]),
            ],
        };

        FirmwareFamilyResolutionDefinition definition =
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash);

        PerfectFamilyRelationship relationship =
            Assert.IsType<PerfectFamilyRelationship>(
                Assert.Single(definition.FamilyRelationships));
        Assert.Equal(["NT00001", "NT00002"], relationship.MemberIds);
        FirmwareImageMap normalizedMap = Assert.Single(definition.ImageMaps);
        Assert.Equal(
            ["NT00001", "NT00002"],
            normalizedMap.RegionSetBindings
                .Select(static binding => binding.EffectiveKey.MemberId)
                .Order(StringComparer.Ordinal));
        Assert.Same(
            normalizedMap.RegionSetBindings[0].Value,
            normalizedMap.RegionSetBindings[1].Value);
    }

    /// <summary>A perfect-like member cannot add a member-only semantic map.</summary>
    [Fact]
    public void NormalizeRejectsPerfectLikeMemberMapOverride()
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareFamilyDocument document = source with
        {
            Members =
            [
                new FirmwareFamilyMemberDocument("NT00001", "Synthetic IC 1"),
                new FirmwareFamilyMemberDocument("NT00002", "Synthetic IC 2"),
            ],
            Capabilities = [],
            ImageMaps =
            [
                map with
                {
                    Applicability = map.Applicability with
                    {
                        MemberIds = ["NT00001", "NT00002"],
                    },
                },
                map with
                {
                    MapId = "member-override-map",
                    Applicability = map.Applicability with { MemberIds = ["NT00001"] },
                },
            ],
            FamilyRelationships =
            [
                new FirmwarePerfectFamilyRelationshipDocument(
                    "synthetic-perfect-family",
                    ["NT00001", "NT00002"],
                    "Owner-confirmed synthetic equality.",
                    ["perfect-evidence"]),
            ],
        };

        FirmwareFamilyNormalizationException exception =
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("familyRelationships[0]", exception.Path);
        Assert.Contains("member-specific map", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Shared-part relationships compare only their named regions, retain the
    /// same logical metadata definition, and allow an unrelated region to differ.
    /// </summary>
    [Fact]
    public void NormalizeMaterializesSharedPartAndIsolatesUnrelatedRegions()
    {
        FirmwareFamilyDocument document =
            CreateTwoMapSharedFactDocument("initial-code-shared");

        FirmwareFamilyResolutionDefinition definition =
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash);

        SharedFactRelationship relationship =
            Assert.IsType<SharedFactRelationship>(
                Assert.Single(definition.FamilyRelationships));
        FirmwareMetadataStructure first =
            Assert.Single(definition.GetStructuresForMap("map"));
        FirmwareMetadataStructure second =
            Assert.Single(definition.GetStructuresForMap("second-map"));
        Assert.Equal(FirmwareSharedFactRole.InitialCodeShared, relationship.Role);
        Assert.Equal(
            ["map", "second-map"],
            relationship.ApplicableMaps.Select(static candidate => candidate.MapId));
        FirmwareSharedFactReference sharedRegion = Assert.Single(
            relationship.SharedFactReferences,
            static reference => reference.Kind == FirmwareSharedFactKind.Region);
        FirmwareSharedFactReference sharedDefinition = Assert.Single(
            relationship.SharedFactReferences,
            static reference => reference.Kind == FirmwareSharedFactKind.MetadataDefinition);
        Assert.Equal("config-region", sharedRegion.FactId);
        Assert.All(
            relationship.ApplicableMaps,
            map => Assert.Same(
                sharedRegion.Region,
                map.Regions.Single(region => region.RegionId == "config-region")));
        Assert.Same(first.Definition, second.Definition);
        Assert.Same(first.Definition, sharedDefinition.MetadataDefinition);
        Assert.NotEqual(
            definition.ImageMaps[0].Regions.Single(region => region.RegionId == "other").Owner,
            definition.ImageMaps[1].Regions.Single(region => region.RegionId == "other").Owner);
    }

    /// <summary>A shared part rejects different geometry for its declared region.</summary>
    [Fact]
    public void NormalizeRejectsSharedPartRegionOverride()
    {
        FirmwareFamilyDocument document = WithDuplicatedSharedRegion(
            CreateTwoMapSharedFactDocument("initial-code-shared"),
            changeKind: true);

        FirmwareFamilyNormalizationException exception =
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("familyRelationships[0].sharedFactReferences[0].factId", exception.Path);
    }

    /// <summary>Equal but separately declared regions are not one canonical shared fact.</summary>
    [Fact]
    public void NormalizeRejectsValueEqualButDistinctSharedRegion()
    {
        FirmwareFamilyDocument document = WithDuplicatedSharedRegion(
            CreateTwoMapSharedFactDocument("initial-code-shared"),
            changeKind: false);

        FirmwareFamilyNormalizationException exception =
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("familyRelationships[0].sharedFactReferences[0].factId", exception.Path);
        Assert.Contains("does not reuse one canonical region", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Readable role changes do not alter canonical map or fact resolution.</summary>
    [Fact]
    public void NormalizeTreatsSharedFactRoleAsDescriptiveOnly()
    {
        FirmwareFamilyDocument initialCodeDocument =
            CreateTwoMapSharedFactDocument("initial-code-shared");
        FirmwareFamilyDocument tpDocument =
            CreateTwoMapSharedFactDocument("tp-shared");

        SharedFactRelationship initialCode =
            Assert.IsType<SharedFactRelationship>(
                Assert.Single(FirmwareFamilyResolutionNormalizer.Normalize(
                    initialCodeDocument,
                    FamilyHash).FamilyRelationships));
        SharedFactRelationship tp =
            Assert.IsType<SharedFactRelationship>(
                Assert.Single(FirmwareFamilyResolutionNormalizer.Normalize(
                    tpDocument,
                    FamilyHash).FamilyRelationships));

        Assert.NotEqual(initialCode.Role, tp.Role);
        Assert.Equal(
            initialCode.ApplicableMaps.Select(static map => map.MapId),
            tp.ApplicableMaps.Select(static map => map.MapId));
        Assert.Equal(
            initialCode.SharedFactReferences.Select(static reference =>
                (reference.Kind, reference.FactId)),
            tp.SharedFactReferences.Select(static reference =>
                (reference.Kind, reference.FactId)));
    }

    /// <summary>The same fact cannot gain a second relationship by changing only its role.</summary>
    [Fact]
    public void NormalizeRejectsSharedFactConflictIndependentOfRole()
    {
        FirmwareFamilyDocument source =
            CreateTwoMapSharedFactDocument("initial-code-shared");
        FirmwareSharedFactRelationshipDocument first =
            Assert.IsType<FirmwareSharedFactRelationshipDocument>(
                Assert.Single(source.FamilyRelationships ?? []));
        FirmwareFamilyDocument document = source with
        {
            FamilyRelationships =
            [
                first,
                first with
                {
                    RelationshipId = "second-role",
                    Role = "tp-shared",
                },
            ],
        };

        FirmwareFamilyNormalizationException exception =
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("familyRelationships[1].sharedFactReferences", exception.Path);
        Assert.Contains("more than one shared relationship", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Applicability must name known maps and cover every relationship member.</summary>
    [Fact]
    public void NormalizeRejectsUnknownOrIncompleteSharedFactApplicability()
    {
        FirmwareFamilyDocument source =
            CreateTwoMapSharedFactDocument("initial-code-shared");
        FirmwareSharedFactRelationshipDocument relationship =
            Assert.IsType<FirmwareSharedFactRelationshipDocument>(
                Assert.Single(source.FamilyRelationships ?? []));

        FirmwareFamilyNormalizationException unknown =
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(
                    source with
                    {
                        FamilyRelationships =
                        [
                            relationship with
                            {
                                Applicability =
                                    new FirmwareSharedFactApplicabilityDocument(["missing-map"]),
                            },
                        ],
                    },
                    FamilyHash));
        Assert.Equal("familyRelationships[0].applicability.mapIds[0]", unknown.Path);

        FirmwareFamilyNormalizationException incomplete =
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(
                    source with
                    {
                        FamilyRelationships =
                        [
                            relationship with
                            {
                                Applicability =
                                    new FirmwareSharedFactApplicabilityDocument(["map"]),
                            },
                        ],
                    },
                    FamilyHash));
        Assert.Equal("familyRelationships[0].applicability.mapIds", incomplete.Path);
        Assert.Contains("NT00002", incomplete.Message, StringComparison.Ordinal);
    }

    /// <summary>A typed reference cannot resolve an identifier through the wrong fact kind.</summary>
    [Fact]
    public void NormalizeRejectsWrongKindSharedFactReference()
    {
        FirmwareFamilyDocument source =
            CreateTwoMapSharedFactDocument("initial-code-shared");
        FirmwareSharedFactRelationshipDocument relationship =
            Assert.IsType<FirmwareSharedFactRelationshipDocument>(
                Assert.Single(source.FamilyRelationships ?? []));
        FirmwareFamilyDocument document = source with
        {
            FamilyRelationships =
            [
                relationship with
                {
                    SharedFactReferences =
                    [
                        new FirmwareSharedFactReferenceDocument(
                            "metadata-definition",
                            "config-region"),
                    ],
                },
            ],
        };

        FirmwareFamilyNormalizationException exception =
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("familyRelationships[0].sharedFactReferences[0].factId", exception.Path);
        Assert.Contains("missing or ambiguous", exception.Message, StringComparison.Ordinal);
    }

    private static FirmwareFamilyDocument CreateTwoMapSharedFactDocument(string role)
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareRegionSetDocument originalSet = Assert.Single(source.RegionSets);
        FirmwareRegionDocument sharedRegion = originalSet.Regions.Single(
            region => region.RegionId == "config-region");
        FirmwareRegionDocument unrelatedRegion = originalSet.Regions.Single(
            region => region.RegionId == "other");
        FirmwareRegionSetDocument firstSet = originalSet with
        {
            Regions =
            [
                .. originalSet.Regions.Where(region => region.RegionId != "config-region"),
            ],
        };
        FirmwareRegionSetDocument secondSet = firstSet with
        {
            RegionSetId = "second-physical",
            Regions =
            [
                .. firstSet.Regions.Where(region => region.RegionId != "other"),
                unrelatedRegion with { Owner = "ldc", Kind = "code" },
            ],
            EvidenceRefs = ["second-region-evidence"],
        };
        var sharedSet = new FirmwareRegionSetDocument(
            "shared-config",
            "flash",
            [sharedRegion],
            ["shared-region-evidence"]);
        return source with
        {
            Members =
            [
                new FirmwareFamilyMemberDocument("NT00001", "Synthetic IC 1"),
                new FirmwareFamilyMemberDocument("NT00002", "Synthetic IC 2"),
            ],
            Capabilities = [],
            RegionSets = [firstSet, secondSet, sharedSet],
            ImageMaps =
            [
                map with { RegionSetIds = ["physical", "shared-config"] },
                map with
                {
                    MapId = "second-map",
                    Applicability = map.Applicability with { MemberIds = ["NT00002"] },
                    RegionSetIds = ["second-physical", "shared-config"],
                },
            ],
            FamilyRelationships =
            [
                new FirmwareSharedFactRelationshipDocument(
                    "shared-facts",
                    ["NT00001", "NT00002"],
                    role,
                    new FirmwareSharedFactApplicabilityDocument(["map", "second-map"]),
                    [
                        new FirmwareSharedFactReferenceDocument("region", "config-region"),
                        new FirmwareSharedFactReferenceDocument("metadata-definition", "config"),
                    ],
                    "Owner-confirmed exact shared facts.",
                    ["shared-fact-evidence"]),
            ],
        };
    }

    private static FirmwareFamilyDocument WithDuplicatedSharedRegion(
        FirmwareFamilyDocument source,
        bool changeKind)
    {
        FirmwareRegionSetDocument sharedSet = source.RegionSets.Single(
            set => set.RegionSetId == "shared-config");
        FirmwareRegionDocument sharedRegion = Assert.Single(sharedSet.Regions);
        FirmwareRegionSetDocument duplicateSet = sharedSet with
        {
            RegionSetId = "duplicate-shared-config",
            Regions =
            [
                changeKind
                    ? sharedRegion with { Kind = "data" }
                    : sharedRegion,
            ],
            EvidenceRefs = ["duplicate-shared-region-evidence"],
        };
        FirmwareImageMapDocument secondMap = source.ImageMaps.Single(
            map => map.MapId == "second-map");
        return source with
        {
            RegionSets = [.. source.RegionSets, duplicateSet],
            ImageMaps =
            [
                source.ImageMaps.Single(map => map.MapId == "map"),
                secondMap with
                {
                    RegionSetIds =
                    [
                        .. secondMap.RegionSetIds.Select(regionSetId =>
                            regionSetId == "shared-config"
                                ? duplicateSet.RegionSetId
                                : regionSetId),
                    ],
                },
            ],
        };
    }
}
