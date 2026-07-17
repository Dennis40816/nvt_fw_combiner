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
    /// <summary>Verifies pure Combiner pasteback produces the same bytes as the older pre-pasted work image model.</summary>
    [Theory]
    [MemberData(nameof(PureCombinerPastebackEquivalenceCases))]
    public async Task DirectRealToolPureCombinerPastebackMatchesPrePasteFlow(
        string icId,
        string manifestIc,
        IcNumberInputMode mode,
        string selectionToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        string toolRoot = Path.Combine(repositoryRoot, "external-tools");
        ExternalCombinerToolManifest manifest = LoadManifest(
            Path.Combine(toolRoot, "legacy-combiner", "1.13.0", "manifest.json"));
        string executableSource = Path.Combine(toolRoot, manifest.ToolId, manifest.ToolVersion, manifest.ExecutableName);
        Assert.Equal(manifest.Sha256, Sha256(executableSource));

        Assert.True(LegacyCombinerPostbuildCatalog.TryGetDefaultProfile(icId, out LegacyCombinerPostbuildProfile? profile));
        IcNumberSelection selection = new(mode, [selectionToken]);
        TpFlashMapRegion selectedRegion = BuiltInTpFlashMapCatalog.GetPostbuildMappedCtrlRamRegions(icId, selection, profile)
            .OrderBy(region => region.Range.Start)
            .ThenBy(region => region.RegionId, StringComparer.Ordinal)
            .First();
        byte[] baseBytes = File.ReadAllBytes(FindGoldenExpectedOutput(goldenRoot, manifestIc));
        byte[] replacementBytes = baseBytes.AsSpan(
                (int)selectedRegion.Range.Start,
                (int)selectedRegion.Range.Length)
            .ToArray();
        replacementBytes[0] ^= 0x5A;
        byte[] prePastedBytes = [.. baseBytes];
        replacementBytes.AsSpan().CopyTo(prePastedBytes.AsSpan((int)selectedRegion.Range.Start));
        Assert.NotEqual(baseBytes[(int)selectedRegion.Range.Start], prePastedBytes[(int)selectedRegion.Range.Start]);

        List<ByteRange> allowWholeImage = [new ByteRange(0, baseBytes.LongLength)];
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-direct-combiner-pasteback-equivalence-{Guid.NewGuid():N}");
        try
        {
            var registry = new ExternalCombinerToolRegistry([manifest]);
            var processor = new LegacyCombinerPostbuildProcessor(
                registry,
                [profile!],
                toolRoot,
                stagingRoot,
                new SystemExternalProcessRunner());
            byte[] prePastedOutput = await RunPostbuildProcessorAsync(
                processor,
                profile!,
                selection,
                prePastedBytes,
                allowWholeImage,
                $"{icId}-{selectionToken}-pre-pasted",
                CancellationToken.None,
                assertChangedRanges: false);
            byte[] purePastebackOutput = await RunPostbuildProcessorAsync(
                processor,
                profile!,
                selection,
                baseBytes,
                allowWholeImage,
                $"{icId}-{selectionToken}-pure-pasteback",
                CancellationToken.None,
                [new ExternalProcessorStagedSource(selectedRegion.Range, replacementBytes)],
                assertChangedRanges: false);

            Assert.Equal(prePastedOutput, purePastebackOutput);
            Assert.Equal(replacementBytes[0], purePastebackOutput[(int)selectedRegion.Range.Start]);
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
