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
    /// <summary>Legacy modes without an explicit FWConfig copy block have an evidence-locked propagation result.</summary>
    public static TheoryData<string, string, string, IcNumberInputMode, string>
        ImplicitFirmwareConfigPropagationCases()
    {
        return new TheoryData<string, string, string, IcNumberInputMode, string>
        {
            { "NT51919", "51929", "nfc.nt51919.ctrlram-postbuild-v1", IcNumberInputMode.SingleSelector, "single" },
            { "NT51929", "51929", "nfc.nt51929.ctrlram-postbuild-v1", IcNumberInputMode.SingleSelector, "single" },
            { "NT51930", "51930", "nfc.nt51930.ctrlram-postbuild-fw1.x", IcNumberInputMode.CascadeSelector, "cascade" },
            { "NT51932", "51932", "nfc.nt51932.ctrlram-postbuild-v1", IcNumberInputMode.CascadeSelector, "cascade" },
            { "NT51950", "51950", "nfc.nt51950.ctrlram-postbuild-v1", IcNumberInputMode.SingleSelector, "single" },
            { "NT51951", "51951", "nfc.nt51951.ctrlram-postbuild-v1", IcNumberInputMode.SingleSelector, "single" },
        };
    }

    /// <summary>Proves whether each blockless production mode propagates Primary FWConfig version fields to Backup.</summary>
    [Theory]
    [MemberData(nameof(ImplicitFirmwareConfigPropagationCases))]
    public async Task BlocklessModesHaveEvidenceLockedFirmwareConfigPropagation(
        string icId,
        string manifestIc,
        string processorId,
        IcNumberInputMode mode,
        string selectionToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const byte firmwareVersion = 0x27;
        const byte firmwareSubVersion = 0x04;
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string toolRoot = Path.Combine(repositoryRoot, "external-tools");
        ExternalCombinerToolManifest manifest = LoadManifest(
            Path.Combine(toolRoot, "legacy-combiner", "1.13.0", "manifest.json"));
        string executableSource = Path.Combine(toolRoot, manifest.ToolId, manifest.ToolVersion, manifest.ExecutableName);
        Assert.Equal(manifest.Sha256, Sha256(executableSource));

        LegacyCombinerPostbuildProfile profile = Assert.Single(
            LegacyCombinerPostbuildCatalog.All,
            candidate => StringComparer.Ordinal.Equals(candidate.ProcessorId, processorId));
        Assert.Equal(icId, profile.IcId);
        Assert.Equal(
            LegacyCombinerFirmwareConfigWriteRoute.PrimaryToCanonicalBackup,
            profile.FirmwareConfigWriteRoute);
        Assert.True(BuiltInTpFlashMapCatalog.TryFind(icId, out TpFlashMapProfile? flashMap));

        string goldenPath = FindGoldenExpectedOutput(manifestIc);
        byte[] ownerGolden = File.ReadAllBytes(goldenPath);
        byte[] stagedFirmware = [.. ownerGolden];
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(stagedFirmware, out FirmwareConfigMetadata backup));
        Assert.True(FirmwareConfigMetadataReader.TryReadAtAbsoluteAddress(
            stagedFirmware,
            flashMap!.FirmwareConfigPrimaryStart,
            out FirmwareConfigMetadata primary));
        Assert.NotEqual(backup.FirmwareConfigStart, primary.FirmwareConfigStart);
        Assert.Equal(backup, primary with { FirmwareConfigStart = backup.FirmwareConfigStart });

        int sourceStart = checked((int)primary.FirmwareConfigStart);
        stagedFirmware[sourceStart + FirmwareConfigLayout.FirmwareVersionOffset] = firmwareVersion;
        stagedFirmware[sourceStart + FirmwareConfigLayout.FirmwareVersionBarOffset] = unchecked((byte)~firmwareVersion);
        stagedFirmware[sourceStart + FirmwareConfigLayout.FirmwareSubVersionOffset] = firmwareSubVersion;

        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-fwconfig-route-{Guid.NewGuid():N}");
        try
        {
            var registry = new ExternalCombinerToolRegistry([manifest]);
            var processor = new LegacyCombinerPostbuildProcessor(
                registry,
                [profile],
                toolRoot,
                stagingRoot,
                new SystemExternalProcessRunner());
            var selection = new IcNumberSelection(mode, [selectionToken]);
            LegacyCombinerPostbuildCommandPlan commandPlan =
                LegacyCombinerPostbuildPlanner.CreatePlan(profile, selection);
            ByteRange[] stagedTargetRanges =
            [
                .. LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(commandPlan)
                    .Select(block => block.FirmwareRange),
            ];
            List<ByteRange> allowedChanges =
            [
                .. LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForStagedSources(
                        commandPlan,
                        stagedFirmware.LongLength,
                        stagedTargetRanges,
                        stagedTargetRanges)
                    .Select(write => write.Range),
                new(backup.FirmwareConfigStart + FirmwareConfigLayout.FirmwareVersionOffset, sizeof(ushort)),
                new(backup.FirmwareConfigStart + FirmwareConfigLayout.FirmwareSubVersionOffset, sizeof(byte)),
            ];
            byte[] output = await RunPostbuildProcessorAsync(
                processor,
                profile,
                selection,
                stagedFirmware,
                allowedChanges,
                $"{icId}-fwconfig-route",
                TestContext.Current.CancellationToken,
                assertChangedRanges: false);

            Assert.True(FirmwareConfigMetadataReader.TryReadBackup(output, out FirmwareConfigMetadata updatedBackup));
            Assert.Equal(firmwareVersion, updatedBackup.FirmwareVersion);
            Assert.Equal(unchecked((byte)~firmwareVersion), updatedBackup.FirmwareVersionBar);
            Assert.Equal(firmwareSubVersion, updatedBackup.FirmwareSubVersion);
            Assert.Equal(ownerGolden, File.ReadAllBytes(goldenPath));
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
