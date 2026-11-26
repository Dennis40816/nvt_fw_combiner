// Copyright (c) 2026 Dennis Liu. All rights reserved.
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class FirmwareFamilyResolutionNormalizerTests
{
    /// <summary>Optional slots are normalized as typed values, with the omitted first slot at 1.0.0.</summary>
    [Fact]
    public void NormalizerPreservesCommonFirmwareVersionSlots()
    {
        FirmwareFamilyResolutionDefinition family = FirmwareFamilyResolutionNormalizer.Normalize(
            VersionDocument(null, "2.0.0"), FamilyHash);

        Assert.Null(family.ImageMaps[0].Applicability.CommonFirmwareVersionFrom);
        Assert.Equal(new CommonFirmwareVersion(2, 0, 0), family.ImageMaps[1].Applicability.CommonFirmwareVersionFrom);
    }

    /// <summary>Malformed, below-minimum, duplicate and unordered slots fail at normalization.</summary>
    [Theory]
    [InlineData("1.0", "2.0.0")]
    [InlineData("01.0.0", "2.0.0")]
    [InlineData("256.0.0", "2.0.0")]
    [InlineData("0.9.9", "2.0.0")]
    [InlineData("2.0.0", "3.0.0")]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.0.0", "0.9.0")]
    [InlineData("2.0.0", "1.0.0")]
    public void NormalizerRejectsInvalidCommonFirmwareVersionSlots(string first, string second)
    {
        _ = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(VersionDocument(first, second), FamilyHash));
    }

    /// <summary>Version intervals cannot be widened or erased by aliases, including the implicit first slot.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void VersionConditionedFamiliesRejectAliases(int targetIndex, int sourceIndex)
    {
        FirmwareFamilyDocument document = VersionDocument(null, "2.0.0");
        FirmwareImageMapDocument[] maps = [.. document.ImageMaps];
        FirmwareImageMapDocument target = maps[targetIndex];
        maps[targetIndex] = target with { RegionSetIds = ["aliased-physical"] };
        document = document with
        {
            ImageMaps = maps,
            FactAliases = [new FirmwareRegionSetAliasDocument(
            "version-alias", "NT00001", target.MapId, "aliased-physical", "NT00001", maps[sourceIndex].MapId, "physical",
            new(["standard"], new("none"), Number("16")), "synthetic alias", ["map-evidence"])]
        };

        FirmwareFamilyNormalizationException error = Assert.Throws<FirmwareFamilyNormalizationException>(() =>
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));
        Assert.Contains("Version-conditioned families do not admit fact aliases", error.Message, StringComparison.Ordinal);
    }

    private static FirmwareFamilyDocument VersionDocument(string? first, string second)
    {
        FirmwareFamilyDocument source = Document(includePredicate: false);
        FirmwareMetadataSetDocument set = Assert.Single(source.MetadataSets);
        FirmwareMetadataStructureDocument structure = Assert.Single(set.Structures) with
        {
            StructureId = CommonFirmwareVersionMetadata.StructureId,
            Fields = [new(CommonFirmwareVersionMetadata.MajorFieldId, Number("0"), Number("1"), "unsigned-integer", "little"),
                new(CommonFirmwareVersionMetadata.MinorFieldId, Number("1"), Number("1"), "unsigned-integer", "little"),
                new(CommonFirmwareVersionMetadata.AdditionalFieldId, Number("2"), Number("1"), "unsigned-integer", "little")],
        };
        FirmwareImageMapDocument map = Assert.Single(source.ImageMaps);
        return source with
        {
            Capabilities = [],
            MetadataSets = [set with { Structures = [structure] }],
            ImageMaps = [map with { Applicability = map.Applicability with { CommonFirmwareVersionFrom = first } },
                map with { MapId = "map-two", Applicability = map.Applicability with { CommonFirmwareVersionFrom = second } }],
        };
    }
}
