using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildRealToolSmokeTests
{
    /// <summary>The NT51929 legacy mode propagates an edited primary FWConfig version to the canonical Backup.</summary>
    [Fact]
    public async Task Nt51929ModePropagatesPrimaryFirmwareVersionToBackup()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const byte firmwareVersion = 0x27;
        const byte firmwareSubVersion = 0x04;
        const string icId = "NT51929";
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string toolRoot = Path.Combine(repositoryRoot, "external-tools");
        ExternalCombinerToolManifest manifest = LoadManifest(
            Path.Combine(toolRoot, "legacy-combiner", "1.13.0", "manifest.json"));
        string executableSource = Path.Combine(toolRoot, manifest.ToolId, manifest.ToolVersion, manifest.ExecutableName);
        Assert.Equal(manifest.Sha256, Sha256(executableSource));
        Assert.True(LegacyCombinerPostbuildCatalog.TryGetDefaultProfile(icId, out LegacyCombinerPostbuildProfile? profile));
        Assert.Equal(
            LegacyCombinerFirmwareConfigWriteRoute.PrimaryToCanonicalBackup,
            profile!.FirmwareConfigWriteRoute);
        Assert.True(BuiltInTpFlashMapCatalog.TryFind(icId, out TpFlashMapProfile? flashMap));

        byte[] ownerGolden = File.ReadAllBytes(FindGoldenExpectedOutput("51929"));
        byte[] stagedFirmware = [.. ownerGolden];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(stagedFirmware, out FirmwareConfigMetadata backup));
        Assert.True(FirmwareConfigMetadataReader.TryReadAtAbsoluteAddress(
            stagedFirmware,
            flashMap!.FirmwareConfigPrimaryStart,
            out FirmwareConfigMetadata primary));
        Assert.NotEqual(backup.StructureStart, primary.StructureStart);

        int sourceStart = checked((int)primary.StructureStart);
        stagedFirmware[sourceStart + FirmwareConfigLayout.FirmwareVersionOffset] = firmwareVersion;
        stagedFirmware[sourceStart + FirmwareConfigLayout.FirmwareVersionBarOffset] = unchecked((byte)~firmwareVersion);
        stagedFirmware[sourceStart + FirmwareConfigLayout.FirmwareSubVersionOffset] = firmwareSubVersion;

        List<ByteRange> allowedChanges = DecodeRanges(Nt51929RangeValues());
        allowedChanges.Add(new ByteRange(
            backup.StructureStart + FirmwareConfigLayout.FirmwareVersionOffset,
            sizeof(ushort)));
        allowedChanges.Add(new ByteRange(
            backup.StructureStart + FirmwareConfigLayout.FirmwareSubVersionOffset,
            sizeof(byte)));
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-nt51929-version-propagation-{Guid.NewGuid():N}");
        try
        {
            var registry = new ExternalCombinerToolRegistry([manifest]);
            var processor = new LegacyCombinerPostbuildProcessor(
                registry,
                [profile],
                toolRoot,
                stagingRoot,
                new SystemExternalProcessRunner());
            byte[] output = await RunPostbuildProcessorAsync(
                processor,
                profile,
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
                stagedFirmware,
                allowedChanges,
                "nt51929-version-propagation",
                TestContext.Current.CancellationToken,
                assertChangedRanges: false);

            Assert.True(FirmwareConfigMetadataReader.TryReadBackup(output, out FirmwareConfigMetadata updatedBackup));
            Assert.Equal(firmwareVersion, updatedBackup.FirmwareVersion);
            Assert.Equal(unchecked((byte)~firmwareVersion), updatedBackup.FirmwareVersionBar);
            Assert.Equal(firmwareSubVersion, updatedBackup.FirmwareSubVersion);
            Assert.Equal(ownerGolden, File.ReadAllBytes(FindGoldenExpectedOutput("51929")));
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }
}
