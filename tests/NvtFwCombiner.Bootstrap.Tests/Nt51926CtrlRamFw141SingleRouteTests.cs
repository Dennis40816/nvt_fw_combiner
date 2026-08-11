using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Production route evidence for the NT51926 Common FW 1.x single CtrlRAM plan.</summary>
public sealed class Nt51926CtrlRamFw141SingleRouteTests
{
    /// <summary>Runs the Common FW 1.x single route against its committed owner golden.</summary>
    [Fact]
    public async Task RoutedFw141SingleDiffersFromOwnerGoldenOnlyAtApprovedCrcWordsAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        JsonElement evidence = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51926-fw141-single-auto-prj-747-20260717");
        var artifacts = evidence.GetProperty("artifacts")
            .EnumerateArray()
            .ToDictionary(
                static artifact => artifact.GetProperty("artifactId").GetString()!,
                static artifact => artifact.Clone(),
                StringComparer.Ordinal);
        string PathFor(string artifactId)
        {
            return CanonicalGoldenTestData.ArtifactPath(artifacts[artifactId]);
        }
        var immutableHashes = artifacts.Values
            .Where(static artifact => artifact.GetProperty("role").GetString() == "input")
            .ToDictionary(
                static artifact => artifact.GetProperty("artifactId").GetString()!,
                artifact => Hash(File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(artifact))),
                StringComparer.Ordinal);

        using var workspace = TempWorkspace.Create("nfc-nt51926-fw141-single-route");
        string referencePath = workspace.PathFor("reference.bin");
        CompositionRunResult reference = await StandardMergeTestSupport.RunAsync(BootstrapTestHost.Services,
            "NT51926",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = PathFor("dp-input"),
                [CompositionAddressSpaceIds.TpInput] = PathFor("tp-input"),
            },
            build: true,
            TestContext.Current.CancellationToken,
            referencePath);
        Assert.True(reference.Succeeded, CompositionRunReportJson.Serialize(reference));

        string outputPath = workspace.PathFor("single-output.bin");
        CompositionRunResult result = await CtrlRamReplaceTestSupport.RunAsync(BootstrapTestHost.Canonical,
            "NT51926",
            "single",
            ExperienceIds.CtrlRamReplace,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionSlotIds.ReplaceBase] = referencePath,
                ["replace-ctrlram-normal"] = PathFor("postbuild-normal-ctrlram"),
                ["replace-ctrlram-mp"] = PathFor("postbuild-mp-ctrlram"),
                ["replace-ctrlram-vn"] = PathFor("postbuild-vn-ctrlram"),
                ["replace-ctrlram-nf"] = PathFor("postbuild-nf-ctrlram"),
            },
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        byte[] expected = File.ReadAllBytes(PathFor("expected-output"));
        byte[] actual = File.ReadAllBytes(outputPath);
        int[] differences = [
            .. Enumerable.Range(0, expected.Length).Where(index => expected[index] != actual[index]),
        ];
        Assert.Equal("30de4735472950e3e7b22e941cc0ae080f393b2b6e601e82b7b1b767b7c56e7a", Hash(actual));
        Assert.Equal(
            [
                0x1C, 0x1D, 0x1E, 0x1F,
                0xFC, 0xFD, 0xFE, 0xFF,
                0x32F6C, 0x32F6D, 0x32F6E, 0x32F6F,
                0x3304C, 0x3304D, 0x3304E, 0x3304F,
            ],
            differences);
        using var report = JsonDocument.Parse(CompositionRunReportJson.Serialize(result));
        Assert.Equal(
            "nt51926-ctrlram-replace-fw141-runtime-single",
            report.RootElement.GetProperty("ProfileId").GetString());
        Assert.All(
            immutableHashes,
            pair => Assert.Equal(pair.Value, Hash(File.ReadAllBytes(PathFor(pair.Key)))));
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
