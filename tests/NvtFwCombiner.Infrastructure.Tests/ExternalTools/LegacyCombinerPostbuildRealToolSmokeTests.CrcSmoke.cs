using System.Security.Cryptography;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildRealToolSmokeTests
{
    /// <summary>Verifies the real Combiner.exe can run a golden-backed NT51927 CRC-only command with bundled or selected runtime deployment.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealToolRunsNt51927GoldenCrcOnlyWithoutUnexpectedChanges(bool useSelectedRuntime)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string toolRoot = Path.Combine(repositoryRoot, "external-tools");
        string executionRoot = Path.Combine(Path.GetTempPath(), $"nfc-real-combiner-smoke-{Guid.NewGuid():N}");
        string stagingRoot = Path.Combine(executionRoot, "firmware");

        try
        {
            byte[] goldenBytes = File.ReadAllBytes(FindGoldenExpectedOutput("51927"));
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
            ExternalRuntimeDeployment? deployment = null;
            if (useSelectedRuntime)
            {
                string runtimePath = Path.Combine(Environment.SystemDirectory, "vcruntime140.dll");
                byte[] runtimeBytes = await File.ReadAllBytesAsync(runtimePath, TestContext.Current.CancellationToken);
                string runtimeHash = Convert.ToHexStringLower(SHA256.HashData(runtimeBytes));
                var selection = new ToolchainRuntimeSelection(ToolchainRuntimeSource.User, runtimePath, runtimeHash);
                deployment = new ExternalRuntimeDeployment(
                    new(1, ToolchainRuntimeConfigurationStatus.Current, selection, selection, []),
                    new LocalFileStore(),
                    Path.Combine(executionRoot, "toolchain"));
            }
            var processor = new LegacyCombinerPostbuildProcessor(
                registry,
                toolRoot,
                stagingRoot,
                new SystemExternalProcessRunner(),
                deployment);
            var request = new ExternalProcessorRequest(
                "real-tool-nt51927-crc-smoke",
                smokeProfile.ProcessorId,
                "legacy-combiner-1.13.0",
                goldenBytes,
                [],
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]),
                protocolPlan: smokeProfile.ResolvePlan(
                    new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])).ProtocolPlan);

            ExternalProcessorResult result = await processor.TransformAsync(request, CancellationToken.None);

            Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.Empty(result.ChangedRanges);
            Assert.Equal(goldenBytes, result.OutputBytes.ToArray());
        }
        finally
        {
            if (Directory.Exists(executionRoot))
            {
                Directory.Delete(executionRoot, recursive: true);
            }
        }
    }
}
