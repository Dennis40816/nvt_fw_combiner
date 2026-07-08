using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildRealToolSmokeTests
{
    /// <summary>Verifies the real Combiner.exe can run a golden-backed NT51927 CRC-only command through the host adapter.</summary>
    [Fact]
    public async Task RealToolRunsNt51927GoldenCrcOnlyWithoutUnexpectedChanges()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string repositoryRoot = FindRepositoryRoot();
        string goldenRoot = Path.Combine(repositoryRoot, "testdata", "golden", "standard-merge-gen-flash");
        string toolRoot = Path.Combine(repositoryRoot, "external-tools");
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-real-combiner-smoke-{Guid.NewGuid():N}");

        try
        {
            byte[] goldenBytes = File.ReadAllBytes(FindGoldenExpectedOutput(goldenRoot, "51927"));
            ExternalCombinerToolManifest manifest = LoadManifest(
                Path.Combine(toolRoot, "legacy-combiner", "1.13.0", "manifest.json"));
            Assert.Equal(
                manifest.Sha256,
                Sha256(Path.Combine(toolRoot, manifest.ToolId, manifest.ToolVersion, manifest.ExecutableName)));

            var crcOnlyCommand = new LegacyCombinerPostbuildCommand(
                "nt51927-real-tool-crc-smoke",
                LegacyCombinerCommandFamily.CrcOnlyMode,
                "NT51927BASED_GEN_CRC_MODE",
                "CRC32",
                []);
            var smokeProfile = new LegacyCombinerPostbuildProfile(
                "nfc.nt51927.real-tool-crc-smoke-v1",
                "NT51927",
                "legacy-combiner-1.13.0",
                "nt51927_fw.bin",
                [crcOnlyCommand],
                [crcOnlyCommand],
                "Windows smoke test for the committed Combiner 1.13.0 binding.");
            var registry = new ExternalCombinerToolRegistry([manifest]);
            var processor = new LegacyCombinerPostbuildProcessor(
                registry,
                [smokeProfile],
                toolRoot,
                stagingRoot,
                new SystemExternalProcessRunner());
            var request = new ExternalProcessorRequest(
                "real-tool-nt51927-crc-smoke",
                smokeProfile.ProcessorId,
                "legacy-combiner-1.13.0",
                goldenBytes,
                [],
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

            ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

            Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.Empty(result.ChangedRanges);
            Assert.Equal(goldenBytes, result.OutputBytes.ToArray());
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
