using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed class Nt51925EndFlagSelectionTests
{
    [Theory]
    [InlineData(1, 0, 0, 1)]
    [InlineData(1, 9, 9, 1)]
    [InlineData(2, 0, 0, 2)]
    [InlineData(2, 9, 9, 2)]
    public void TrySelect_DeclaredNt51925EndFlag_SelectsCommonFwSlot(byte major, byte minor, byte additional, int expectedSlot)
    {
        byte[] image = CreateBase(0x2FFFC, major, minor, additional);
        bool readable = FirmwareConfigMetadataReader.TryReadBackup(image,
            BuiltInFirmwareInspection.ResolveCtrlRamBaseNvtEndFlag("NT51925"), out FirmwareConfigMetadata metadata, out _);
        Assert.True(readable);
        Assert.True(BuiltInCommonFwSelector.TrySelect("NT51925", readable, metadata.CommonFwVersion,
            out BuiltInCommonFwSelection? selection, out CompositionIssue? issue));
        Assert.Null(issue);
        Assert.Equal(expectedSlot, selection!.TpFlashMap!.EffectiveCommonFwVersion.Major);
    }

    [Theory]
    [InlineData(0x1FFC)]
    [InlineData(0x3BFFC)]
    public void TrySelect_MarkerOutsideDeclaredEndFlag_RefusesUnreadableVersion(int markerStart)
    {
        byte[] image = CreateBase(markerStart, 1, 9, 9);
        bool readable = FirmwareConfigMetadataReader.TryReadBackup(image,
            BuiltInFirmwareInspection.ResolveCtrlRamBaseNvtEndFlag("NT51925"), out _, out _);
        Assert.False(readable);
        Assert.False(BuiltInCommonFwSelector.TrySelect("NT51925", readable, null,
            out BuiltInCommonFwSelection? selection, out CompositionIssue? issue));
        Assert.Null(selection);
        Assert.Equal(CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildCategoryUnknown, issue!.Code);
    }

    [Fact]
    public void ReadBackup_ExtraMarkerOutsideDeclaredRange_UsesDeclaredEndFlag()
    {
        byte[] image = CreateBase(0x2FFFC, 2, 0, 0);
        FirmwareNvtEndFlag.MarkerBytes.CopyTo(image.AsSpan(0x3BFFC));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(image,
            BuiltInFirmwareInspection.ResolveCtrlRamBaseNvtEndFlag("NT51925"), out FirmwareConfigMetadata metadata, out _));
        Assert.Equal("2.0.0", metadata.CommonFwVersion);
    }

    [Theory]
    [InlineData("nt51925-ctrlram-replace-candidate", "nt51925-ctrlram-replace-fw141-runtime-single", "nt51925-ctrlram-fw141-tp-work-240k")]
    [InlineData("nt51925-ctrlram-replace-candidate", "nt51925-ctrlram-replace-fw141-runtime-single", "nt51925-ctrlram-fw141-full-flash-256k")]
    [InlineData("nt51925-ctrlram-replace-candidate", "nt51925-ctrlram-replace-fw200-runtime-single", "nt51925-ctrlram-fw200-tp-work-240k")]
    [InlineData("nt51925-ctrlram-replace-candidate", "nt51925-ctrlram-replace-fw200-runtime-single", "nt51925-ctrlram-fw200-full-flash-256k")]
    [InlineData("nt51925-standard-merge-candidate", "nt51925-standard-merge-gen-flash", "nt51925-standard-merge-256k")]
    public void ResolveNvtEndFlag_EveryCandidateMap_DeclaresSameMarker(string bundleId, string profileId, string mapId)
    {
        FirmwareFamilyResolutionDefinition family = BuiltInV2BundleRegistry.All[bundleId].GetFirmwareFamily(profileId, "0.1.0");
        FirmwareNvtEndFlagResolution resolution = family.ResolveNvtEndFlag(mapId);
        Assert.True(resolution.Succeeded);
        Assert.False(resolution.UsesLegacyCompatibilityRead);
        Assert.Equal(0x2FFFC, resolution.EndFlag!.Position.Range.Start);
        Assert.Equal(4, resolution.EndFlag.Position.Range.Length);
        Assert.Equal("flash", resolution.EndFlag.Position.AddressSpaceId);
    }

    private static byte[] CreateBase(int markerStart, byte major, byte minor, byte additional)
    {
        byte[] image = new byte[0x40000];
        int backupStart = markerStart - 0xFFC;
        image[backupStart + FirmwareConfigLayout.CommonFwMajorVersionOffset] = major;
        image[backupStart + FirmwareConfigLayout.CommonFwMinorVersionOffset] = minor;
        image[backupStart + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = additional;
        FirmwareNvtEndFlag.MarkerBytes.CopyTo(image.AsSpan(markerStart));
        return image;
    }
}
