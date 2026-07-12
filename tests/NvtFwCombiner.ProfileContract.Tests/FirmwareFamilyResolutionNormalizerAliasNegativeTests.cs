using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class FirmwareFamilyResolutionNormalizerTests
{
    /// <summary>Verifies every physical alias endpoint is explicitly map/member/fact scoped.</summary>
    [Fact]
    public void NormalizeRejectsUnknownOrUndeclaredPhysicalAliasEndpoints()
    {
        FirmwareFamilyDocument source = PhysicalAliasDocument();
        FirmwareMetadataSetAliasDocument first = Assert.IsType<FirmwareMetadataSetAliasDocument>(
            source.FactAliases[0]);
        (FirmwareMetadataSetAliasDocument Alias, string ExpectedPath)[] cases =
        [
            (first with { TargetMapId = "missing-map" }, "factAliases[0].targetMapId"),
            (first with { TargetMemberId = "NT99999" }, "factAliases[0].targetMemberId"),
            (first with { TargetMetadataSetId = "undeclared-target" }, "factAliases[0].target"),
            (first with { SourceMetadataSetId = "undeclared-source" }, "factAliases[0].source"),
        ];

        foreach ((FirmwareMetadataSetAliasDocument alias, string expectedPath) in cases)
        {
            FirmwareFamilyDocument document = source with
            {
                FactAliases = [alias, source.FactAliases[1]],
            };

            FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

            Assert.Equal(expectedPath, exception.Path);
        }
    }

    /// <summary>Verifies alias ids and effective target keys never use declaration order as a tie breaker.</summary>
    [Fact]
    public void NormalizeRejectsDuplicateAliasIdsAndTargetProviders()
    {
        FirmwareFamilyDocument source = PhysicalAliasDocument();
        FirmwareMetadataSetAliasDocument first = Assert.IsType<FirmwareMetadataSetAliasDocument>(
            source.FactAliases[0]);
        FirmwareMetadataSetAliasDocument duplicateTarget = new(
            "second-provider",
            "NT00001",
            "target-map",
            "target-metadata",
            "NT00001",
            "source-map",
            "metadata",
            AliasApplicability(),
            "synthetic duplicate target",
            ["duplicate-target-evidence"]);
        FirmwareFamilyDocument duplicateId = source with
        {
            FactAliases = [first, source.FactAliases[1] with { AliasId = first.AliasId }],
        };
        FirmwareFamilyDocument duplicateProvider = source with
        {
            FactAliases = [first, duplicateTarget],
        };

        Assert.Equal(
            "factAliases[1].aliasId",
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(duplicateId, FamilyHash)).Path);
        Assert.Equal(
            "factAliases[1]",
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(duplicateProvider, FamilyHash)).Path);
    }

    /// <summary>Verifies a capability alias cannot cite an absent direct or aliased source provider.</summary>
    [Fact]
    public void NormalizeRejectsUnresolvedCapabilityAliasSource()
    {
        FirmwareFamilyDocument source = PhysicalAliasDocument();
        FirmwareFamilyDocument document = source with
        {
            FactAliases =
            [
                .. source.FactAliases,
                new FirmwareCapabilityAliasDocument(
                    "unresolved-capability-source",
                    "NT00001",
                    "target-map",
                    "target-capability",
                    "NT00001",
                    "source-map",
                    "missing-capability",
                    AliasApplicability(),
                    "synthetic missing capability source",
                    ["missing-capability-evidence"]),
            ],
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("factAliases[2].source", exception.Path);
    }

    /// <summary>Verifies a capability alias cannot widen from an intermediate capability provider.</summary>
    [Fact]
    public void NormalizeRejectsCapabilityAliasWiderThanImmediateSource()
    {
        FirmwareFamilyDocument source = PhysicalAliasDocument();
        FirmwareAliasApplicabilityDocument exactOne = new(
            ["standard"],
            new FirmwareTopologyRequirementDocument("exact-count", ChipCount: Number("1")),
            Number("16"));
        FirmwareFamilyDocument document = source with
        {
            Capabilities =
            [
                new FirmwareCapabilityFactDocument(
                    "source-capability",
                    "ab-code",
                    "NT00001",
                    "source-map",
                    exactOne,
                    "confirmed-present",
                    "source evidence",
                    ["source-capability-evidence"]),
            ],
            FactAliases =
            [
                .. source.FactAliases,
                new FirmwareCapabilityAliasDocument(
                    "middle-capability",
                    "NT00001",
                    "middle-map",
                    "middle-capability",
                    "NT00001",
                    "source-map",
                    "source-capability",
                    exactOne,
                    "middle evidence",
                    ["middle-capability-evidence"]),
                new FirmwareCapabilityAliasDocument(
                    "target-capability",
                    "NT00001",
                    "target-map",
                    "target-capability",
                    "NT00001",
                    "middle-map",
                    "middle-capability",
                    AliasApplicability(),
                    "widened target evidence",
                    ["target-capability-evidence"]),
            ],
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("factAliases[3].applicability", exception.Path);
    }

    /// <summary>Verifies an aliased region graph is revalidated for the target map address space.</summary>
    [Fact]
    public void NormalizeRevalidatesAliasedRegionSetAgainstTargetAddressSpace()
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        FirmwareRegionSetDocument regionSet = Assert.Single(source.RegionSets);
        FirmwareFamilyDocument document = source with
        {
            Capabilities = [],
            RegionSets = [regionSet with { RegionSetId = "source-regions", AddressSpaceId = "other" }],
            MetadataSets = [],
            ImageMaps =
            [
                map with { MapId = "target-map", RegionSetIds = ["target-regions"], MetadataSetIds = [] },
                map with
                {
                    MapId = "source-map",
                    AddressSpaceId = "other",
                    RegionSetIds = ["source-regions"],
                    MetadataSetIds = [],
                },
            ],
            FactAliases =
            [
                new FirmwareRegionSetAliasDocument(
                    "target-to-source-regions",
                    "NT00001",
                    "target-map",
                    "target-regions",
                    "NT00001",
                    "source-map",
                    "source-regions",
                    AliasApplicability(),
                    "synthetic wrong target address space",
                    ["region-target-evidence"]),
            ],
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("imageMaps[0]", exception.Path);
        Assert.Contains("address space", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies an aliased metadata set is revalidated against target-map region geometry.</summary>
    [Fact]
    public void NormalizeRevalidatesAliasedMetadataSetAgainstTargetRegions()
    {
        FirmwareFamilyDocument source = PhysicalAliasDocument();
        FirmwareImageMapDocument targetMap = source.ImageMaps.Single(map => map.MapId == "target-map");
        FirmwareRegionSetDocument physical = Assert.Single(source.RegionSets);
        FirmwareRegionDocument root = Assert.Single(physical.Regions, region => region.ParentRegionId is null);
        FirmwareFamilyDocument document = source with
        {
            RegionSets =
            [
                physical,
                new FirmwareRegionSetDocument(
                    "target-regions",
                    "flash",
                    [root],
                    ["target-region-evidence"]),
            ],
            ImageMaps =
            [
                .. source.ImageMaps.Select(map => map.MapId == "target-map"
                    ? targetMap with { RegionSetIds = ["target-regions"] }
                    : map),
            ],
        };

        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal("$", exception.Path);
        Assert.Contains("allowed region", exception.Message, StringComparison.Ordinal);
    }
}
