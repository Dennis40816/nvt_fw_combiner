using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class FirmwareFamilyResolutionNormalizerTests
{
    /// <summary>Format discovery cannot borrow a primary location from a different output variant.</summary>
    [Fact]
    public void NormalizeRejectsAbPrimaryContextThatMovesBetweenFormatVariants()
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) =
            PrimaryContextDocument(movePrimary: true);
        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash, resolver));
        Assert.Equal("$", exception.Path); // Cross-map family invariant, after individual policy normalization.
        Assert.Contains("A/B primary discovery", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Equal absolute read envelopes do not hide different canonical allowed-result regions.</summary>
    [Fact]
    public void NormalizeRejectsAbPrimaryAllowedRegionDriftWithIdenticalReadEnvelope()
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) = PrimaryContextDocument(false);
        FirmwareMetadataSetDocument metadata = Assert.Single(document.MetadataSets);
        document = document with
        {
            MetadataSets = [metadata with
            {
                Structures = [.. metadata.Structures.Select(structure => structure with
                {
                    Locator = new FirmwareMetadataLocatorDocument("absolute-range", "config-region", Range: AddressedRange(0, 13)),
                })],
            }],
            RegionSets = [document.RegionSets[0], document.RegionSets[1] with
            {
                Regions = [.. document.RegionSets[1].Regions.Select(region => region.RegionId switch
                {
                    "config-region" => region with { Range = Range(0, 14) },
                    "other" => region with { Range = Range(14, 2) },
                    _ => region,
                })],
            }],
        };
        // Both maps independently contain the complete same absolute primary slice.
        FirmwareFamilyResolutionDefinition withoutPolicy = FirmwareFamilyResolutionNormalizer.Normalize(
            document with { AbFormatPolicy = null }, FamilyHash, resolver);
        Assert.Equal(2, withoutPolicy.ImageMaps.Count);
        FirmwareFamilyNormalizationException exception = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash, resolver));
        Assert.Contains("region 'config-region' must have the same range", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Output-only region classification is not part of primary discovery.</summary>
    [Fact]
    public void NormalizeAllowsUnrelatedOutputDifferencesBetweenFormatVariants()
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) =
            PrimaryContextDocument(movePrimary: false);
        document = document with
        {
            ImageMaps = [document.ImageMaps[0], document.ImageMaps[1] with
            {
                Applicability = document.ImageMaps[1].Applicability with { CapacityBytes = Number("32") },
            }],
            RegionSets = [document.RegionSets[0], document.RegionSets[1] with
            {
                Regions = [.. document.RegionSets[1].Regions.Select(region => region.RegionId switch
                {
                    "root" => region with { Range = Range(0, 32) },
                    "other" => region with { Range = Range(13, 19) },
                    _ => region,
                })],
            }],
        };
        FirmwareFamilyResolutionDefinition family = FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash, resolver);
        Assert.Equal(2, family.ImageMaps.Count);
        Assert.NotNull(family.AbFormatPolicy);
    }

    private static (FirmwareFamilyDocument Document, IFirmwareMetadataStructureDefinitionResolver Resolver)
        PrimaryContextDocument(bool movePrimary)
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) = AbPolicyDocument();
        FirmwareMetadataSetDocument metadata = Assert.Single(document.MetadataSets);
        FirmwareRegionSetDocument regions = Assert.Single(document.RegionSets);
        FirmwareRegionDocument root = regions.Regions[0];
        FirmwareRegionDocument config = regions.Regions[1] with { Range = Range(0, 13) };
        FirmwareRegionDocument tail = regions.Regions[2] with { Range = Range(13, 3) };
        FirmwareRegionSetDocument first = regions with { Regions = [root, config, tail] };
        FirmwareRegionSetDocument second = first with
        {
            RegionSetId = "other-physical",
            Regions = movePrimary
                ? [root, config with { Range = Range(3, 13) }, tail with { Range = Range(0, 3) }]
                : [root, config, tail with { Kind = "reserved", Owner = "reserved" }],
        };
        FirmwareImageMapDocument map = Assert.Single(document.ImageMaps);
        FirmwareImageMapDocument other = map with { MapId = "other-map", RegionSetIds = [second.RegionSetId] };
        FirmwareAbFormatPolicyDocument policy = Assert.IsType<FirmwareAbFormatPolicyDocument>(document.AbFormatPolicy);
        return (document with
        {
            RegionSets = [first, second],
            ImageMaps = [map, other],
            MetadataSets = [metadata with
            {
                Structures = [.. metadata.Structures.Select(structure => structure with
                {
                    Locator = new FirmwareMetadataLocatorDocument("region-relative", "config-region",
                        RegionId: "config-region", Offset: Number("0")),
                })],
            }],
            AbFormatPolicy = policy with
            {
                Variants = [policy.Variants[0], policy.Variants[1] with { MapId = "other-map" }],
            },
        }, resolver);
    }

    /// <summary>A policy object is reusable; discovery must use each owning family's own locator context.</summary>
    [Fact]
    public void SharedAbPolicyDoesNotRetainAnotherFamilysPrimaryAnchor()
    {
        (FirmwareFamilyDocument document, IFirmwareMetadataStructureDefinitionResolver resolver) = PrimaryContextDocument(false);
        FirmwareFamilyResolutionDefinition first = FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash, resolver);
        FirmwareFamilyDocument shifted = document with
        {
            FamilyId = "second-family",
            RegionSets = [.. document.RegionSets.Select(set => set with
            {
                Regions = [.. set.Regions.Select(region => region.RegionId switch
                {
                    "config-region" => region with { Range = Range(3, 13) },
                    "other" => region with { Range = Range(0, 3) },
                    _ => region,
                })],
            })],
        };
        FirmwareFamilyResolutionDefinition normalized = FirmwareFamilyResolutionNormalizer.Normalize(shifted, FamilyHash, resolver);
        var second = new FirmwareFamilyResolutionDefinition(normalized.FamilyId, normalized.FamilyVersion,
            normalized.FamilyContentHash, normalized.ImageMaps, normalized.MetadataSets, normalized.CapabilityBindings,
            normalized.FamilyRelationships, first.AbFormatPolicy);
        Assert.Same(first.AbFormatPolicy, second.AbFormatPolicy);
        byte[] bytes = new byte[16];
        bytes[0] = 0xAA;
        bytes[1] = (byte)'A';
        bytes[2] = 2;
        bytes[4] = 0x31;
        bytes[5] = 0xCE;
        bytes[12] = 0x41;
        byte[] moved = new byte[16];
        bytes.AsSpan(0, 13).CopyTo(moved.AsSpan(3));
        Assert.True(second.TryResolveAbPrimaryFirmwareConfig("NT00001", [new FirmwareArtifactPayload("tp-a", moved)],
            null, out FirmwareMetadataStructureResolution? secondA, out FirmwareMetadataStructureResolution? secondB));
        Assert.True(first.TryResolveAbPrimaryFirmwareConfig("NT00001", [new FirmwareArtifactPayload("tp-a", bytes)],
            null, out FirmwareMetadataStructureResolution? firstA, out FirmwareMetadataStructureResolution? firstB));
        Assert.Equal(3, secondA.Resolved!.LocatorOutcome.ResolvedRange.Range.Start);
        Assert.Equal(0, firstA.Resolved!.LocatorOutcome.ResolvedRange.Range.Start);
        Assert.Equal(FirmwareMetadataStructureResolutionStatus.Pending, firstB.Status);
        Assert.Equal(FirmwareMetadataStructureResolutionStatus.Pending, secondB.Status);
        Assert.False(first.TryResolveAbPrimaryFirmwareConfig("other-member", [], null, out _, out _));
    }
}
