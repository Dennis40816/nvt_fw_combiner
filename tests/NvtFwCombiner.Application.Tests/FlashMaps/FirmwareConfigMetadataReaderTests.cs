using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Application.Tests.FlashMaps;

/// <summary>Golden-backed checks for FWConfig metadata extraction.</summary>
public sealed class FirmwareConfigMetadataReaderTests
{
    /// <summary>Reads Common FW, FW/bar, and PID facts from owner-approved standard-merge golden outputs.</summary>
    [Theory]
    [MemberData(nameof(GoldenFirmwareConfigCases))]
    public void GoldenFlashImagesExposeExpectedFirmwareFacts(
        string ic,
        string relativePath,
        string commonFwVersion,
        byte firmwareVersion,
        byte firmwareVersionBar,
        byte firmwareSubVersion,
        ushort projectId)
    {
        string repositoryRoot = FindRepositoryRoot();
        byte[] image = File.ReadAllBytes(Path.Combine(
            repositoryRoot,
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            "expected",
            relativePath));

        Assert.True(TpFlashMapCatalog.TryGetFirmwareConfigStart($"NT{ic}", out long firmwareConfigStart));
        Assert.True(FirmwareConfigMetadataReader.TryRead(
            image,
            firmwareConfigStart,
            out FirmwareConfigMetadata metadata));

        Assert.Equal(commonFwVersion, metadata.CommonFwVersion);
        Assert.Equal(firmwareVersion, metadata.FirmwareVersion);
        Assert.Equal(firmwareVersionBar, metadata.FirmwareVersionBar);
        Assert.Equal(firmwareSubVersion, metadata.FirmwareSubVersion);
        Assert.Equal(projectId, metadata.ProjectId);
        Assert.True(metadata.IsFirmwareVersionBarValid);
    }

    /// <summary>NT51926 golden evidence identifies the owner-confirmed 1.4.1 Common FW codebase.</summary>
    [Fact]
    public void Nt51926GoldenReadsAsCommonFw141()
    {
        FirmwareConfigMetadata metadata = ReadGoldenMetadata("51926", "51926/flash.bin");

        Assert.Equal("1.4.1", metadata.CommonFwVersion);
    }

    /// <summary>Golden FWConfig facts used by UI display and postbuild-category detection discussion.</summary>
    public static TheoryData<string, string, string, byte, byte, byte, ushort> GoldenFirmwareConfigCases()
    {
        TheoryData<string, string, string, byte, byte, byte, ushort> data = [];
        data.Add("51920", "51920/flash.bin", "1.2.0", 0x01, 0xFE, 0x00, 0x1404);
        data.Add("51923", "51923/flash.bin", "1.3.0", 0x06, 0xF9, 0x00, 0x1606);
        data.Add("51926", "51926/flash.bin", "1.4.1", 0x01, 0xFE, 0x00, 0x5102);
        data.Add("51927", "51927/flash.bin", "1.4.1", 0x02, 0xFD, 0x00, 0x1348);
        data.Add("51928", "51928/flash.bin", "1.3.2", 0x84, 0x7B, 0x00, 0xF206);
        data.Add("51929", "51929/flash.bin", "2.0.0", 0x01, 0xFE, 0x00, 0x1707);
        data.Add("51930", "51930/flash.bin", "1.3.0", 0x04, 0xFB, 0x00, 0x110D);
        data.Add("51931", "51931/flash.bin", "1.3.0", 0x82, 0x7D, 0x00, 0x131B);
        data.Add("51932", "51932/flash.bin", "2.0.0", 0x80, 0x7F, 0x00, 0x4801);
        data.Add("51950", "51950/dp-256k/flash.bin", "2.0.0", 0x04, 0xFB, 0x00, 0x135E);
        data.Add("51951", "51951/dp-512k/flash.bin", "2.0.0", 0x03, 0xFC, 0x00, 0x5901);
        return data;
    }

    private static FirmwareConfigMetadata ReadGoldenMetadata(string ic, string relativePath)
    {
        string repositoryRoot = FindRepositoryRoot();
        byte[] image = File.ReadAllBytes(Path.Combine(
            repositoryRoot,
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            "expected",
            relativePath));

        Assert.True(TpFlashMapCatalog.TryGetFirmwareConfigStart($"NT{ic}", out long firmwareConfigStart));
        Assert.True(FirmwareConfigMetadataReader.TryRead(
            image,
            firmwareConfigStart,
            out FirmwareConfigMetadata metadata));

        return metadata;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SPEC.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
