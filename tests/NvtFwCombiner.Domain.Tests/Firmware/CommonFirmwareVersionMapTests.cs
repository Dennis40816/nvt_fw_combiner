// Copyright (c) 2026 Dennis Liu. All rights reserved.
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Canonical Common FW interval selection and identity boundaries.</summary>
public sealed class CommonFirmwareVersionMapTests
{
    /// <summary>Exercises the declared Common FW selection boundary.</summary>
    [Theory]
    [InlineData(1, 0, 0, "map-one")]
    [InlineData(1, 255, 255, "map-one")]
    [InlineData(2, 0, 0, "map-two")]
    [InlineData(3, 1, 7, "map-two")]
    public void GreatestSlotNotAboveTpVersionIsSelected(byte major, byte minor, byte patch, string expected)
    {
        FirmwareMapResolutionResult result = Family().ResolveMap(Inputs([major, minor, patch, 0]));

        Assert.Equal(FirmwareMapResolutionStatus.Unique, result.Status);
        Assert.Equal(expected, result.ResolvedMap!.ImageMap.MapId);
        Assert.Equal(new CommonFirmwareVersion(major, minor, patch), result.ResolvedMap.CommonFirmwareVersion);
    }

    /// <summary>Exercises the declared Common FW selection boundary.</summary>
    [Theory]
    [InlineData(null, FirmwareMapResolutionRejectionKind.CommonFirmwareVersionRequired)]
    [InlineData(new byte[] { 0, 9, 9, 0 }, FirmwareMapResolutionRejectionKind.CommonFirmwareVersionBelowMinimum)]
    public void MissingOrBelowMinimumVersionIsTypedRejection(byte[]? bytes, FirmwareMapResolutionRejectionKind expected)
    {
        FirmwareMapResolutionResult result = Family().ResolveMap(Inputs(bytes));

        Assert.Equal(FirmwareMapResolutionStatus.Rejected, result.Status);
        Assert.Equal(expected, result.RejectionKind);
        Assert.Null(result.ResolvedMap);
    }

    /// <summary>Exercises the declared Common FW selection boundary.</summary>
    [Theory]
    [InlineData("2.0.0", "3.0.0")]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("2.0.0", "1.0.0")]
    public void InvalidSlotSequenceIsRejected(string first, string second)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => Family(first, second));
        Assert.Contains("Common FW map slots", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Preserves typed selection and map identity without unrelated payload identity.</summary>
    [Fact]
    public void VersionSelectionDoesNotFingerprintUnrelatedTpBytes()
    {
        FirmwareFamilyResolutionDefinition family = Family();
        FirmwareMapResolutionResult first = family.ResolveMapWithinForProfile(
            Inputs([2, 0, 0, 0]), new HashSet<string> { "map-one", "map-two" }, new HashSet<string>());
        FirmwareMapResolutionResult changed = family.ResolveMapWithinForProfile(
            Inputs([2, 0, 0, 99]), new HashSet<string> { "map-one", "map-two" }, new HashSet<string>());

        Assert.Equal(FirmwareMapResolutionStatus.Unique, first.Status);
        Assert.Equal(first.ResolvedMap!.ResolutionFingerprint, changed.ResolvedMap!.ResolutionFingerprint);
        Assert.Empty(first.ResolvedMap.ArtifactIdentities);
    }

    /// <summary>A profile omitting the selected upper interval cannot fall back to its lower map.</summary>
    [Fact]
    public void ProfileSubsetCannotRemoveAnIntervalUpperBound()
    {
        FirmwareMapResolutionResult result = Family().ResolveMapWithinForProfile(Inputs([2, 0, 0, 0]),
            new HashSet<string> { "map-one" }, new HashSet<string>());

        Assert.Equal(FirmwareMapResolutionRejectionKind.NoMatchingMap, result.RejectionKind);
    }

    /// <summary>An unreadable canonical structure fails with the version-specific issue.</summary>
    [Fact]
    public void TruncatedFwConfigCannotSelectAnInterval()
    {
        FirmwareMapResolutionResult result = Family().ResolveMap(Inputs([1]));

        Assert.Equal(FirmwareMapResolutionRejectionKind.CommonFirmwareVersionRequired, result.RejectionKind);
    }

    private static FirmwareMapResolutionInputs Inputs(byte[]? bytes)
    {
        return new(
            "NT00001", "standard-merge", 4, null,
            bytes is null ? [] : [new FirmwareArtifactPayload("tp-input", bytes)]);
    }

    private static FirmwareFamilyResolutionDefinition Family(string? first = null, string second = "2.0.0")
    {
        var structure = new FirmwareMetadataStructure("firmware-config-general-parameters", "tp-input", 3,
            new FirmwareAbsoluteRangeLocator(new FirmwareAddressedRange("flash", new ByteRange(0, 3)), "root"),
            [new("common-firmware-major-version", 0, 1, FirmwareMetadataEncoding.UnsignedInteger, FirmwareMetadataByteOrder.LittleEndian),
             new("common-firmware-minor-version", 1, 1, FirmwareMetadataEncoding.UnsignedInteger, FirmwareMetadataByteOrder.LittleEndian),
             new("common-firmware-additional-version", 2, 1, FirmwareMetadataEncoding.UnsignedInteger, FirmwareMetadataByteOrder.LittleEndian)], []);
        var metadata = new FirmwareMetadataSet("config", [structure], ["synthetic"]);
        var regions = new FirmwareRegionSet("regions", "flash",
            [new("root", null, FirmwareRegionOwner.Tp, FirmwareRegionKind.Image, new ByteRange(0, 4), FirmwareWriteConstraint.Forbidden)], ["synthetic"]);
        FirmwareImageMap Map(string id, string? version)
        {
            return FirmwareImageMapTestFactory.CreateDirect(
                id, "flash", new FirmwareMapApplicability(["NT00001"], ["standard-merge"],
                    TopologyRequirement.NoTopologyConstraint(), 4,
                    commonFirmwareVersionFrom: version is null ? null : Parse(version)),
                FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps, [regions], [metadata], ["synthetic"]);
        }
        return new("synthetic", "1.0.0", new string('a', 64),
            [Map("map-one", first), Map("map-two", second)], [metadata]);
    }

    private static CommonFirmwareVersion Parse(string text)
    {
        Assert.True(CommonFirmwareVersion.TryParse(text, out CommonFirmwareVersion version));
        return version;
    }
}
