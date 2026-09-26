using System.Buffers.Binary;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Golden-backed checks for FWConfig metadata extraction.</summary>
public sealed class FirmwareConfigMetadataReaderTests
{
    private static readonly byte[] NvtEndFlagBytes = [0x00, 0x4E, 0x56, 0x54];

    /// <summary>Locks reviewed FWConfig offsets used by UI traceability and postbuild category selection.</summary>
    [Fact]
    public void FirmwareConfigLayoutMatchesReviewedSourceOffsets()
    {
        Assert.Equal(0x000, FirmwareConfigLayout.FirmwareVersionOffset);
        Assert.Equal(0x001, FirmwareConfigLayout.FirmwareVersionBarOffset);
        Assert.Equal(0x011, FirmwareConfigLayout.FirmwareSubVersionOffset);
        Assert.Equal(0x017, FirmwareConfigLayout.ChipNumberOffset);
        Assert.Equal(0x01A, FirmwareConfigLayout.CommonFwMajorVersionOffset);
        Assert.Equal(0x01B, FirmwareConfigLayout.CommonFwMinorVersionOffset);
        Assert.Equal(0x01C, FirmwareConfigLayout.CommonFwAdditionalVersionOffset);
        Assert.Equal(0x022, FirmwareConfigLayout.ProjectIdOffset);
        Assert.Equal(sizeof(ushort), FirmwareConfigLayout.ProjectIdLength);
        Assert.Equal(0x029, FirmwareConfigLayout.HardwareInfoStartOffset);
        Assert.Equal(0x02A, FirmwareConfigLayout.FreeRunModeOffset);
        Assert.Equal(0x02C, FirmwareConfigLayout.SyncTypeOffset);
        Assert.Equal(0x02D, FirmwareConfigLayout.SenseTerminalCountOffset);
        Assert.Equal(0x02E, FirmwareConfigLayout.TouchPanelTerminalCountNormalOffset);
        Assert.Equal(0x02F, FirmwareConfigLayout.TouchPanelTerminalCountSelfOffset);
        Assert.Equal(0x031, FirmwareConfigLayout.I2cDeviceAddressOffset);
        Assert.Equal(0x032, FirmwareConfigLayout.InterpolationStepXOffset);
        Assert.Equal(0x033, FirmwareConfigLayout.InterpolationStepYOffset);
        Assert.Equal(0x034, FirmwareConfigLayout.S2dSensorDotsOffset);
        Assert.Equal(0x038, FirmwareConfigLayout.MaxZoneCountOffset);
        Assert.Equal(0x039, FirmwareConfigLayout.InterpolationStartOffsetXOffset);
        Assert.Equal(0x03A, FirmwareConfigLayout.InterpolationStartOffsetYOffset);
        Assert.Equal(0x03B, FirmwareConfigLayout.MaxFingerCountOffset);
        Assert.Equal(0x03C, FirmwareConfigLayout.GipBeforeLeftOffset);
        Assert.Equal(0x04C, FirmwareConfigLayout.GipBeforeRightOffset);
        Assert.Equal(0x05C, FirmwareConfigLayout.GipAfterLeftOffset);
        Assert.Equal(0x06C, FirmwareConfigLayout.GipAfterRightOffset);
        Assert.Equal(sizeof(uint), FirmwareConfigLayout.GipTableWordLength);
        Assert.Equal(4, FirmwareConfigLayout.GipTableWordCount);
        Assert.Equal(0x07C, FirmwareConfigLayout.HardwareInfoEndExclusive);
        Assert.Equal(0x07C, FirmwareConfigLayout.RequiredLength);
    }

    /// <summary>
    /// Reads Common FW, FW/bar, and PID facts from immutable owner-approved golden outputs.
    /// Retired IC rows in this evidence-only set do not imply production catalog admission.
    /// </summary>
    [Theory]
    [MemberData(nameof(GoldenFirmwareConfigCases))]
    public void GoldenFlashImagesExposeExpectedFirmwareFacts(
        string ic,
        string _,
        string commonFwVersion,
        byte firmwareVersion,
        byte firmwareVersionBar,
        byte firmwareSubVersion,
        byte chipNumber,
        ushort projectId)
    {
        byte[] image = File.ReadAllBytes(
            CanonicalGoldenTestData.ArtifactPath("standard-merge", ic, "expected-output"));

        Assert.True(
            FirmwareConfigMetadataReader.TryReadBackup(image, out FirmwareConfigMetadata metadata),
            $"NT{ic} golden image must expose one valid NVT FWConfig Backup.");

        Assert.Equal(commonFwVersion, metadata.CommonFwVersion);
        Assert.Equal(firmwareVersion, metadata.FirmwareVersion);
        Assert.Equal(firmwareVersionBar, metadata.FirmwareVersionBar);
        Assert.Equal(firmwareSubVersion, metadata.FirmwareSubVersion);
        Assert.Equal(chipNumber, metadata.ChipNumber);
        Assert.Equal(projectId, metadata.ProjectId);
        Assert.True(metadata.IsFirmwareVersionBarValid);
    }

    /// <summary>Reads every non-reserved common-FW hardware field with its documented byte order and signedness.</summary>
    [Fact]
    public void HardwareInfoModelReadsAllDocumentedFields()
    {
        byte[] image = new byte[FirmwareConfigLayout.RequiredLength];
        image[FirmwareConfigLayout.FreeRunModeOffset] = 0x11;
        image[FirmwareConfigLayout.SyncTypeOffset] = 0x12;
        image[FirmwareConfigLayout.SenseTerminalCountOffset] = 0x13;
        image[FirmwareConfigLayout.TouchPanelTerminalCountNormalOffset] = 0x14;
        image[FirmwareConfigLayout.TouchPanelTerminalCountSelfOffset] = 0x15;
        image[FirmwareConfigLayout.I2cDeviceAddressOffset] = 0x16;
        image[FirmwareConfigLayout.InterpolationStepXOffset] = 0x17;
        image[FirmwareConfigLayout.InterpolationStepYOffset] = 0x18;
        BinaryPrimitives.WriteUInt16LittleEndian(
            image.AsSpan(FirmwareConfigLayout.S2dSensorDotsOffset),
            0xABCD);
        image[FirmwareConfigLayout.MaxZoneCountOffset] = 0x19;
        image[FirmwareConfigLayout.InterpolationStartOffsetXOffset] = unchecked((byte)-2);
        image[FirmwareConfigLayout.InterpolationStartOffsetYOffset] = 3;
        image[FirmwareConfigLayout.MaxFingerCountOffset] = 0x1A;

        FirmwareConfigGipTable beforeLeft = new(0x10203040, 0x11213141, 0x12223242, 0x13233343);
        FirmwareConfigGipTable beforeRight = new(0x20203040, 0x21213141, 0x22223242, 0x23233343);
        FirmwareConfigGipTable afterLeft = new(0x30203040, 0x31213141, 0x32223242, 0x33233343);
        FirmwareConfigGipTable afterRight = new(0x40203040, 0x41213141, 0x42223242, 0x43233343);
        WriteGipTable(image, FirmwareConfigLayout.GipBeforeLeftOffset, beforeLeft);
        WriteGipTable(image, FirmwareConfigLayout.GipBeforeRightOffset, beforeRight);
        WriteGipTable(image, FirmwareConfigLayout.GipAfterLeftOffset, afterLeft);
        WriteGipTable(image, FirmwareConfigLayout.GipAfterRightOffset, afterRight);

        Assert.True(FirmwareConfigMetadataReader.TryReadAtAbsoluteAddress(image, 0, out FirmwareConfigMetadata metadata));

        FirmwareConfigHardwareMetadata hardware = metadata.Hardware;
        Assert.Equal(0x11, hardware.FreeRunMode);
        Assert.Equal(0x12, hardware.SyncType);
        Assert.Equal(0x13, hardware.SenseTerminalCount);
        Assert.Equal(0x14, hardware.TouchPanelTerminalCountNormal);
        Assert.Equal(0x15, hardware.TouchPanelTerminalCountSelf);
        Assert.Equal(0x16, hardware.I2cDeviceAddress);
        Assert.Equal(0x17, hardware.InterpolationStepX);
        Assert.Equal(0x18, hardware.InterpolationStepY);
        Assert.Equal(0xABCD, hardware.S2dSensorDots);
        Assert.Equal(0x19, hardware.MaxZoneCount);
        Assert.Equal((sbyte)-2, hardware.InterpolationStartOffsetX);
        Assert.Equal((sbyte)3, hardware.InterpolationStartOffsetY);
        Assert.Equal(0x1A, hardware.MaxFingerCount);
        Assert.Equal(beforeLeft, hardware.GipBeforeLeft);
        Assert.Equal(beforeRight, hardware.GipBeforeRight);
        Assert.Equal(afterLeft, hardware.GipAfterLeft);
        Assert.Equal(afterRight, hardware.GipAfterRight);
    }

    /// <summary>Confirms every admitted IC golden copies all exposed FWConfig fields to the NVT T-minus-FFF Backup block.</summary>
    [Theory]
    [MemberData(nameof(GoldenFirmwareConfigCopyCases))]
    public void GoldenFlashHardwareInfoMatchesNvtBackup(string ic, string _)
    {
        byte[] image = File.ReadAllBytes(
            CanonicalGoldenTestData.ArtifactPath("standard-merge", ic, "expected-output"));

        Assert.True(BuiltInTpFlashMapCatalog.TryFind($"NT{ic}", out TpFlashMapProfile? flashMap));
        long firmwareConfigStart = flashMap!.FirmwareConfigPrimaryStart;
        Assert.True(FirmwareConfigMetadataReader.TryReadAtAbsoluteAddress(image, firmwareConfigStart, out FirmwareConfigMetadata primary));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(image, out FirmwareConfigMetadata backup));
        int nvtTerminal = Assert.Single(FindNvtEndFlagTerminals(image));
        Assert.Equal(nvtTerminal - 0xFFF, backup.StructureStart);

        Assert.Equal(primary.FirmwareVersion, backup.FirmwareVersion);
        Assert.Equal(primary.FirmwareVersionBar, backup.FirmwareVersionBar);
        Assert.Equal(primary.IsFirmwareVersionBarValid, backup.IsFirmwareVersionBarValid);
        Assert.Equal(primary.FirmwareSubVersion, backup.FirmwareSubVersion);
        Assert.Equal(primary.ChipNumber, backup.ChipNumber);
        Assert.Equal(primary.CommonFwVersion, backup.CommonFwVersion);
        Assert.Equal(primary.ProjectId, backup.ProjectId);
        Assert.Equal(primary.Hardware, backup.Hardware);
    }

    /// <summary>Rejects missing, ambiguous, or out-of-range NVT Backup markers rather than guessing a FWConfig source.</summary>
    [Fact]
    public void NvtBackupReaderRejectsMissingAmbiguousOrOutOfRangeMarkers()
    {
        Assert.False(FirmwareConfigMetadataReader.TryReadBackup(new byte[0x1000], out _));

        byte[] ambiguous = new byte[0x2000];
        WriteEndFlag(ambiguous, 0x0FFC);
        WriteEndFlag(ambiguous, 0x1FFC);

        Assert.False(FirmwareConfigMetadataReader.TryReadBackup(ambiguous, out _));

        byte[] validAndEarly = new byte[0x2000];
        WriteEndFlag(validAndEarly, 0);
        WriteEndFlag(validAndEarly, 0x0FFC);

        Assert.False(FirmwareConfigMetadataReader.TryReadBackup(validAndEarly, out _));

        byte[] earlyOnly = new byte[0x1000];
        WriteEndFlag(earlyOnly, 0);

        Assert.False(FirmwareConfigMetadataReader.TryReadBackup(earlyOnly, out _));
    }

    /// <summary>Locates the Backup at the earliest valid start and marker-at-EOF boundaries.</summary>
    [Fact]
    public void NvtBackupReaderUsesTerminalMinusFffAtMarkerBoundaries()
    {
        byte[] firstMarker = new byte[0x1078];
        WriteEndFlag(firstMarker, 0x0FFC);

        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(firstMarker, out FirmwareConfigMetadata first));
        Assert.Equal(0, first.StructureStart);

        byte[] lastMarker = new byte[0x2000];
        WriteEndFlag(lastMarker, lastMarker.Length - NvtEndFlagBytes.Length);

        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(lastMarker, out FirmwareConfigMetadata last));
        Assert.Equal(0x1000, last.StructureStart);
    }

    /// <summary>
    /// NVT-END-FLAG-1113-01: through a declared end flag only that marker counts. A complete marker elsewhere is
    /// neither counted nor rejected, and without the end-flag marker the Backup is unreadable although a marker
    /// exists elsewhere.
    /// </summary>
    [Theory]
    [InlineData("51950", "nt51950-standard-merge-256k")]
    [InlineData("51951", "nt51951-standard-merge-512k")]
    public void DeclaredEndFlagReadCountsOnlyTheEndFlagMarker(string ic, string mapId)
    {
        byte[] image = File.ReadAllBytes(
            CanonicalGoldenTestData.ArtifactPath("standard-merge", ic, "expected-output"));
        FirmwareNvtEndFlagResolution endFlag = DpPerspectiveEndFlag(ic, mapId);

        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(image, endFlag, out FirmwareConfigMetadata declared,
            out int markerCount));
        Assert.Equal(1, markerCount);
        Assert.Equal(0x36000, declared.StructureStart);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(image, out FirmwareConfigMetadata wholeImage));
        Assert.Equal(wholeImage, declared);

        byte[] extraMarker = [.. image];
        WriteEndFlag(extraMarker, 0x20000);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(extraMarker, endFlag, out FirmwareConfigMetadata extra,
            out markerCount));
        Assert.Equal(1, markerCount);
        Assert.Equal(declared, extra);

        byte[] misplacedOnly = [.. extraMarker];
        misplacedOnly[0x36FFD] ^= 0xFF;
        Assert.False(FirmwareConfigMetadataReader.TryReadBackup(misplacedOnly, endFlag, out _, out markerCount));
        Assert.Equal(0, markerCount);
    }

    /// <summary>A failed declaration is unreadable without searching the image, as is an end flag past the image end.</summary>
    [Fact]
    public void UnresolvedOrOutOfImageEndFlagIsUnreadable()
    {
        byte[] image = File.ReadAllBytes(
            CanonicalGoldenTestData.ArtifactPath("standard-merge", "51950", "expected-output"));

        Assert.False(FirmwareConfigMetadataReader.TryReadBackup(image, FirmwareNvtEndFlagResolution.Unresolved, out _,
            out int markerCount));
        Assert.Equal(0, markerCount);
        Assert.False(FirmwareConfigMetadataReader.TryReadBackup(image.AsSpan(0, 0x36FFF),
            DpPerspectiveEndFlag("51950", "nt51950-standard-merge-256k"), out _, out markerCount));
        Assert.Equal(0, markerCount);
    }

    /// <summary>A migration-inventory layout keeps the existing compatibility read, ambiguity included.</summary>
    [Theory]
    [InlineData("51923")]
    [InlineData("51927")]
    [InlineData("51929")]
    public void MigrationInventoryLayoutKeepsTheCompatibilityRead(string ic)
    {
        byte[] image = File.ReadAllBytes(
            CanonicalGoldenTestData.ArtifactPath("standard-merge", ic, "expected-output"));
        FirmwareNvtEndFlagResolution legacy = BuiltInFirmwareInspection.ResolveCtrlRamBaseNvtEndFlag($"NT{ic}");
        Assert.True(legacy.UsesLegacyCompatibilityRead);

        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(image, legacy, out FirmwareConfigMetadata viaResolution,
            out int resolutionCount));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(image, out FirmwareConfigMetadata compatibility,
            out int compatibilityCount));
        Assert.Equal(compatibility, viaResolution);
        Assert.Equal(compatibilityCount, resolutionCount);

        byte[] ambiguous = [.. image];
        WriteEndFlag(ambiguous, 0x100);
        Assert.False(FirmwareConfigMetadataReader.TryReadBackup(ambiguous, legacy, out _, out resolutionCount));
        Assert.Equal(2, resolutionCount);
    }

    /// <summary>NT51926 golden evidence identifies the owner-confirmed 1.4.1 Common FW codebase.</summary>
    [Fact]
    public void Nt51926GoldenReadsAsCommonFw141()
    {
        FirmwareConfigMetadata metadata = ReadGoldenMetadata("51926");

        Assert.Equal("1.4.1", metadata.CommonFwVersion);
    }

    /// <summary>
    /// Historical golden FWConfig facts used for parser evidence; membership is not production support.
    /// </summary>
    public static TheoryData<string, string, string, byte, byte, byte, byte, ushort> GoldenFirmwareConfigCases()
    {
        TheoryData<string, string, string, byte, byte, byte, byte, ushort> data = [];
        data.Add("51923", "51923/flash.bin", "1.3.0", 0x06, 0xF9, 0x00, 0x01, 0x1606);
        data.Add("51926", "51926/flash.bin", "1.4.1", 0x01, 0xFE, 0x00, 0x02, 0x5102);
        data.Add("51927", "51927/flash.bin", "1.4.1", 0x02, 0xFD, 0x00, 0x01, 0x1348);
        data.Add("51928", "51928/flash.bin", "1.3.2", 0x84, 0x7B, 0x00, 0x02, 0xF206);
        data.Add("51929", "51929/flash.bin", "2.0.0", 0x01, 0xFE, 0x00, 0x01, 0x1707);
        data.Add("51932", "51932/flash.bin", "2.0.0", 0x80, 0x7F, 0x00, 0x05, 0x4801);
        data.Add("51950", "51950/dp-256k/flash.bin", "2.0.0", 0x04, 0xFB, 0x00, 0x01, 0x135E);
        data.Add("51951", "51951/dp-512k/flash.bin", "2.0.0", 0x03, 0xFC, 0x00, 0x01, 0x5901);
        return data;
    }

    /// <summary>All owner-approved flash outputs used to verify the shared NVT FWConfig-copy layout.</summary>
    public static TheoryData<string, string> GoldenFirmwareConfigCopyCases()
    {
        TheoryData<string, string> data = [];
        data.Add("51923", "51923/flash.bin");
        data.Add("51926", "51926/flash.bin");
        data.Add("51927", "51927/flash.bin");
        data.Add("51928", "51928/flash.bin");
        data.Add("51929", "51929/flash.bin");
        data.Add("51932", "51932/flash.bin");
        data.Add("51950", "51950/dp-256k/flash.bin");
        data.Add("51951", "51951/dp-512k/flash.bin");
        return data;
    }

    private static FirmwareConfigMetadata ReadGoldenMetadata(string ic)
    {
        byte[] image = File.ReadAllBytes(
            CanonicalGoldenTestData.ArtifactPath("standard-merge", ic, "expected-output"));

        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(image, out FirmwareConfigMetadata metadata));

        return metadata;
    }

    private static FirmwareNvtEndFlagResolution DpPerspectiveEndFlag(string ic, string mapId)
    {
        return BuiltInV2BundleRegistry.All["nt51950-nt51951-standard-merge"]
            .GetFirmwareFamily($"nt{ic}-standard-merge-dp-perspective", "0.8.0")
            .ResolveNvtEndFlag(mapId);
    }

    private static void WriteEndFlag(byte[] image, int start)
    {
        image[start] = 0x00;
        image[start + 1] = (byte)'N';
        image[start + 2] = (byte)'V';
        image[start + 3] = (byte)'T';
    }

    private static List<int> FindNvtEndFlagTerminals(ReadOnlySpan<byte> image)
    {
        List<int> terminals = [];
        for (int offset = 0; offset <= image.Length - NvtEndFlagBytes.Length; offset++)
        {
            if (image.Slice(offset, NvtEndFlagBytes.Length).SequenceEqual(NvtEndFlagBytes))
            {
                terminals.Add(offset + NvtEndFlagBytes.Length - 1);
            }
        }

        return terminals;
    }

    private static void WriteGipTable(byte[] image, int offset, FirmwareConfigGipTable table)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset), table.Table0);
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(offset + FirmwareConfigLayout.GipTableWordLength),
            table.Table1);
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(offset + (2 * FirmwareConfigLayout.GipTableWordLength)),
            table.Table2);
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(offset + (3 * FirmwareConfigLayout.GipTableWordLength)),
            table.Table3);
    }
}
