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
                new FirmwarePerfectLikeFamilyRelationshipDocument(
                    "synthetic-perfect-family",
                    ["NT00001", "NT00002"],
                    "Owner-confirmed synthetic equality.",
                    ["perfect-evidence"]),
            ],
        };

        FirmwareFamilyResolutionDefinition definition =
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash);

        FirmwareFamilyRelationship relationship = Assert.Single(definition.FamilyRelationships);
        Assert.Equal(FirmwareFamilyRelationshipKind.PerfectLikeFamily, relationship.Kind);
        Assert.Equal(["NT00001", "NT00002"], relationship.MemberIds);
        Assert.Empty(relationship.SharedRegionIds);
        Assert.Empty(relationship.MetadataDefinitions);
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
                new FirmwarePerfectLikeFamilyRelationshipDocument(
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
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareRegionSetDocument firstSet = Assert.Single(source.RegionSets);
        FirmwareRegionDocument other = firstSet.Regions.Single(region => region.RegionId == "other");
        FirmwareRegionSetDocument secondSet = firstSet with
        {
            RegionSetId = "physical-with-distinct-ldc",
            Regions =
            [
                .. firstSet.Regions.Where(region => region.RegionId != "other"),
                other with { Owner = "ldc", Kind = "code" },
            ],
            EvidenceRefs = ["second-region-evidence"],
        };
        FirmwareFamilyDocument document = source with
        {
            Members =
            [
                new FirmwareFamilyMemberDocument("NT00001", "Synthetic IC 1"),
                new FirmwareFamilyMemberDocument("NT00002", "Synthetic IC 2"),
            ],
            Capabilities = [],
            RegionSets = [firstSet, secondSet],
            ImageMaps =
            [
                map,
                map with
                {
                    MapId = "second-map",
                    Applicability = map.Applicability with { MemberIds = ["NT00002"] },
                    RegionSetIds = ["physical-with-distinct-ldc"],
                },
            ],
            FamilyRelationships =
            [
                new FirmwareInitialCodeSharedFamilyRelationshipDocument(
                    "shared-initial-code",
                    ["NT00001", "NT00002"],
                    ["config-region"],
                    ["config"],
                    "Owner-confirmed shared Initial Code.",
                    ["shared-initial-code-evidence"]),
            ],
        };

        FirmwareFamilyResolutionDefinition definition =
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash);

        FirmwareFamilyRelationship relationship = Assert.Single(definition.FamilyRelationships);
        FirmwareMetadataStructure first =
            Assert.Single(definition.GetStructuresForMap("map"));
        FirmwareMetadataStructure second =
            Assert.Single(definition.GetStructuresForMap("second-map"));
        Assert.Equal(FirmwareFamilyRelationshipKind.InitialCodeSharedFamily, relationship.Kind);
        Assert.Equal(["config-region"], relationship.SharedRegionIds);
        Assert.Same(first.Definition, second.Definition);
        Assert.Same(first.Definition, Assert.Single(relationship.MetadataDefinitions));
        Assert.NotEqual(
            definition.ImageMaps[0].Regions.Single(region => region.RegionId == "other").Owner,
            definition.ImageMaps[1].Regions.Single(region => region.RegionId == "other").Owner);
    }

    /// <summary>A shared part rejects different geometry for its declared region.</summary>
    [Fact]
    public void NormalizeRejectsSharedPartRegionOverride()
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareRegionSetDocument firstSet = Assert.Single(source.RegionSets);
        FirmwareRegionSetDocument secondSet = firstSet with
        {
            RegionSetId = "different-physical",
            Regions =
            [
                firstSet.Regions[0],
                firstSet.Regions[1] with { Kind = "data" },
                firstSet.Regions[2],
            ],
            EvidenceRefs = ["different-region-evidence"],
        };
        FirmwareFamilyDocument document = source with
        {
            Members =
            [
                new FirmwareFamilyMemberDocument("NT00001", "Synthetic IC 1"),
                new FirmwareFamilyMemberDocument("NT00002", "Synthetic IC 2"),
            ],
            Capabilities = [],
            RegionSets = [firstSet, secondSet],
            ImageMaps =
            [
                map,
                map with
                {
                    MapId = "second-map",
                    Applicability = map.Applicability with { MemberIds = ["NT00002"] },
                    RegionSetIds = ["different-physical"],
                },
            ],
            FamilyRelationships =
            [
                new FirmwareInitialCodeSharedFamilyRelationshipDocument(
                    "shared-initial-code",
                    ["NT00001", "NT00002"],
                    ["config-region"],
                    ["config"],
                    "Owner-confirmed shared Initial Code.",
                    ["shared-initial-code-evidence"]),
            ],
        };

        FirmwareFamilyNormalizationException exception =
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("familyRelationships[0].sharedRegionIds", exception.Path);
    }
}
