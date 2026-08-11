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
    /// <summary>Locks the accepted 16-byte direct-combiner behavior for CtrlRAM self-replacement scope.</summary>
    [Theory]
    [MemberData(nameof(SixteenByteSelfReplacementCases))]
    public async Task DirectRealToolSixteenByteCasesMatchForSingleAndMultipleCtrlRamSelfReplacement(
        string icId,
        string manifestIc,
        IcNumberInputMode mode,
        string selectionToken,
        long[] expectedChangedRangeValues)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestIc);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectionToken);
        ArgumentNullException.ThrowIfNull(expectedChangedRangeValues);

        string testName = $"{icId}/{selectionToken}";
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string toolRoot = Path.Combine(repositoryRoot, "external-tools");
        ExternalCombinerToolManifest manifest = LoadManifest(
            Path.Combine(toolRoot, "legacy-combiner", "1.13.0", "manifest.json"));
        string executableSource = Path.Combine(toolRoot, manifest.ToolId, manifest.ToolVersion, manifest.ExecutableName);
        Assert.Equal(manifest.Sha256, Sha256(executableSource));

        Assert.True(LegacyCombinerPostbuildCatalog.TryGetDefaultProfile(icId, out LegacyCombinerPostbuildProfile? profile));
        IcNumberSelection selection = new(mode, [selectionToken]);
        TpFlashMapRegion[] mappedRegions =
        [
            .. BuiltInTpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection, profile)
                .OrderBy(region => region.Range.Start)
                .ThenBy(region => region.RegionId, StringComparer.Ordinal),
        ];
        Assert.True(
            mappedRegions.Length >= 2,
            $"{testName} must expose multiple postbuild-mapped CtrlRAM regions.");

        byte[] baseBytes = File.ReadAllBytes(FindGoldenExpectedOutput(manifestIc));
        byte[] singleRegionBytes = CreateSelfReplacementBytes(baseBytes, [mappedRegions[0]]);
        byte[] multipleRegionBytes = CreateSelfReplacementBytes(baseBytes, mappedRegions);
        Assert.Equal(baseBytes, singleRegionBytes);
        Assert.Equal(baseBytes, multipleRegionBytes);

        List<ByteRange> expectedChangedRanges = DecodeRanges(expectedChangedRangeValues);
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-direct-combiner-16byte-{Guid.NewGuid():N}");
        try
        {
            var registry = new ExternalCombinerToolRegistry([manifest]);
            var processor = new LegacyCombinerPostbuildProcessor(
                registry,
                toolRoot,
                stagingRoot,
                new SystemExternalProcessRunner());
            byte[] singleOutput = await RunPostbuildProcessorAsync(
                processor,
                profile!,
                selection,
                singleRegionBytes,
                expectedChangedRanges,
                $"{icId}-{selectionToken}-single-region",
                CancellationToken.None);
            byte[] multipleOutput = await RunPostbuildProcessorAsync(
                processor,
                profile!,
                selection,
                multipleRegionBytes,
                expectedChangedRanges,
                $"{icId}-{selectionToken}-multiple-regions",
                CancellationToken.None);

            Assert.Equal(singleOutput, multipleOutput);

            IReadOnlyList<ByteRange> changedRanges = ByteDiff.FindChangedRanges(baseBytes, singleOutput);
            Assert.Equal(expectedChangedRanges, changedRanges);
            Assert.Equal(16, changedRanges.Sum(range => range.Length));
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
