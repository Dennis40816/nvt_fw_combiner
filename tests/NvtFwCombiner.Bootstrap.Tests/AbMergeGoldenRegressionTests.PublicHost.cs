using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeGoldenRegressionTests
{
    private const string ReferenceManifestSha256 =
        "d3bd2cf76fcdb5dae6cb5e387f47cfdb22c4ff8ea3ef10387bb7086479a7086d";

    /// <summary>True only where the packaged legacy Combiner processor can execute.</summary>
    public static bool IsWindows { get; } = OperatingSystem.IsWindows();

    /// <summary>
    /// The production CLI/public host accepts the owner Golden's one physical TP file as both
    /// logical AB bindings and commits the NT51950 direct expected output byte-for-byte.
    /// </summary>
    [Fact(
        Skip = "Requires the packaged Windows legacy Combiner processor.",
        SkipUnless = nameof(IsWindows))]
    public async Task Nt51950PublicHostBuildAcceptsOneCanonicalTpFileForBothLogicalSlotsAsync()
    {
        JsonElement goldenCase = ReadGoldenCase("nt51950-ab-boe-d82t80");
        JsonElement[] artifacts = [.. goldenCase.GetProperty("artifacts").EnumerateArray()];
        string dpPath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(static artifact =>
            artifact.GetProperty("artifactId").GetString() == CompositionAddressSpaceIds.DpAbInput));
        string tpAPath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(static artifact =>
            artifact.GetProperty("artifactId").GetString() == CompositionAddressSpaceIds.TpAInput));
        string tpBPath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(static artifact =>
            artifact.GetProperty("artifactId").GetString() == CompositionAddressSpaceIds.TpBInput));
        Assert.Equal(tpAPath, tpBPath);
        byte[] originalDp = File.ReadAllBytes(dpPath);
        byte[] originalTp = File.ReadAllBytes(tpAPath);
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-public-same-tp");
        string selectedDpPath = workspace.Write("dp-ab.bin", originalDp);
        string selectedTpPath = workspace.Write("shared-tp.bin", originalTp);
        string outputPath = workspace.PathFor("nt51950-ab-output.bin");
        string reportPath = workspace.PathFor("nt51950-ab-report.json");

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "ab-merge",
                "build",
                "--profile",
                "NT51950",
                "--ab-topology",
                "single",
                "--dp-ab",
                selectedDpPath,
                "--tp-a",
                selectedTpPath,
                "--tp-b",
                selectedTpPath,
                "--output",
                outputPath,
                "--report",
                reportPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        Assert.Contains($"Committed: {outputPath}", result.Output, StringComparison.Ordinal);
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(ReadExpected(goldenCase), output);
        AssertPostbuildMpeg2Crc(output, bTpCodeStart: 0x4A000);
        Assert.Equal(originalDp, File.ReadAllBytes(selectedDpPath));
        Assert.Equal(originalTp, File.ReadAllBytes(selectedTpPath));
        Assert.Equal(originalDp, File.ReadAllBytes(dpPath));
        Assert.Equal(originalTp, File.ReadAllBytes(tpAPath));
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(
            reportPath,
            TestContext.Current.CancellationToken));
        AssertPublicSameTpBuildReport(
            report.RootElement,
            "NT51950",
            "nt51950-ab-merge",
            "nfc-nt51950-ab-merge-combiner-v1",
            0x4A000,
            "0x40000");
    }

    /// <summary>
    /// The production CLI/public host also preserves one physical TP source for both NT51951
    /// logical bindings and matches the immutable Python reference. This synthetic topology
    /// regression does not claim a direct NT51951 product Golden or expand support authority.
    /// </summary>
    [Fact(
        Skip = "Requires the packaged Windows legacy Combiner processor.",
        SkipUnless = nameof(IsWindows))]
    public async Task Nt51951PublicHostBuildAcceptsOneTpFileForBothLogicalSlotsAsync()
    {
        const int outputLength = 0x100000;
        const int tpLength = 0x37000;
        const string expectedSha256 = "b84b63f30c964fad9818b612b77167bd9615cc31d6f72c1eab49f1b1579c8f32";
        byte[] dp = CreatePattern(outputLength, 37, 11);
        byte[] sharedTp = CreatePattern(tpLength, 19, 23);
        WriteHeaderPointers(sharedTp);
        BinaryPrimitives.WriteUInt32LittleEndian(
            sharedTp.AsSpan(0xA130, sizeof(uint)),
            0x1F6CF3EC);
        byte[] originalDp = [.. dp];
        byte[] originalTp = [.. sharedTp];
        using var workspace = TempWorkspace.Create("nfc-nt51951-ab-public-same-tp");
        using var referenceWorkspace = TempWorkspace.Create("nfc-nt51951-ab-public-same-tp-reference");
        string dpPath = workspace.Write("dp-ab.bin", dp);
        string sharedTpPath = workspace.Write("shared-tp.bin", sharedTp);
        string outputPath = workspace.PathFor("nt51951-ab-output.bin");
        string reportPath = workspace.PathFor("nt51951-ab-report.json");
        byte[] expected = await RunPythonReferenceAsync(
            referenceWorkspace,
            "51951",
            dp,
            sharedTp,
            sharedTp,
            TestContext.Current.CancellationToken);
        Assert.Equal(expectedSha256, Hash(expected));

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "ab-merge",
                "build",
                "--profile",
                "NT51951",
                "--dp-ab",
                dpPath,
                "--tp-a",
                sharedTpPath,
                "--tp-b",
                sharedTpPath,
                "--output",
                outputPath,
                "--report",
                reportPath,
            ],
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.Error + Environment.NewLine + result.Output);
        Assert.Contains($"Committed: {outputPath}", result.Output, StringComparison.Ordinal);
        byte[] output = File.ReadAllBytes(outputPath);
        Assert.Equal(expected, output);
        Assert.Equal(expectedSha256, Hash(output));
        AssertPostbuildMpeg2Crc(output, bTpCodeStart: 0x8A000);
        Assert.Equal(originalDp, File.ReadAllBytes(dpPath));
        Assert.Equal(originalTp, File.ReadAllBytes(sharedTpPath));
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(
            reportPath,
            TestContext.Current.CancellationToken));
        AssertPublicSameTpBuildReport(
            report.RootElement,
            "NT51951",
            "nt51951-ab-merge",
            "nfc-nt51951-ab-merge-combiner-v1",
            0x8A000,
            "0x80000");
    }

    private static void AssertPublicSameTpBuildReport(
        JsonElement report,
        string expectedIcId,
        string expectedProfileId,
        string expectedProcessorId,
        long expectedBCodeStart,
        string expectedBankLength)
    {
        Assert.Equal(expectedIcId, report.GetProperty("IcId").GetString());
        Assert.Equal(expectedProfileId, report.GetProperty("ProfileId").GetString());
        Assert.True(report.GetProperty("Output").GetProperty("Committed").GetBoolean());
        JsonElement[] inputs = [.. report.GetProperty("Inputs").EnumerateArray()];
        Assert.Equal(
            [
                CompositionAddressSpaceIds.DpAbInput,
                CompositionAddressSpaceIds.TpAInput,
                CompositionAddressSpaceIds.TpBInput,
            ],
            inputs.Select(static input => input.GetProperty("AddressSpaceId").GetString()));
        Assert.Equal(
            inputs.Single(static input => input.GetProperty("AddressSpaceId").GetString() ==
                CompositionAddressSpaceIds.TpAInput).GetProperty("Sha256").GetString(),
            inputs.Single(static input => input.GetProperty("AddressSpaceId").GetString() ==
                CompositionAddressSpaceIds.TpBInput).GetProperty("Sha256").GetString());
        JsonElement processor = Assert.Single(
            report.GetProperty("Operations").EnumerateArray(),
            operation => operation.GetProperty("ProcessorId").GetString() == expectedProcessorId);
        Assert.Equal("legacy-combiner-1.13.0", processor.GetProperty("ToolBindingId").GetString());
        Assert.Equal(
            [
                new ByteRange(expectedBCodeStart + 0x100, sizeof(uint)),
                new ByteRange(expectedBCodeStart + 0x110, sizeof(uint)),
                new ByteRange(expectedBCodeStart + 0x130, sizeof(uint)),
            ],
            processor.GetProperty("ProcessorAllowedWriteRanges").EnumerateArray().Select(
                static range => new ByteRange(
                    range.GetProperty("Start").GetInt64(),
                    range.GetProperty("Length").GetInt64())));
        JsonElement command = Assert.Single(processor.GetProperty("ExecutedCommands").EnumerateArray());
        string[] arguments = [.. command.GetProperty("Arguments").EnumerateArray().Select(
            static argument => argument.GetString()!)];
        Assert.Equal("NT51950BASED_MERGE_AB_MODE", arguments[0]);
        Assert.Equal(expectedBankLength, arguments[^1]);
    }

    private static async Task<byte[]> RunPythonReferenceAsync(
        TempWorkspace workspace,
        string icId,
        byte[] dp,
        byte[] tpA,
        byte[] tpB,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(dp);
        ArgumentNullException.ThrowIfNull(tpA);
        ArgumentNullException.ThrowIfNull(tpB);

        // This test-only process invokes a hash-pinned, cache-free copy of the immutable reference;
        // production never loads refcode.
        string dpPath = workspace.Write("DP_AB/input.bin", dp);
        string tpAPath = workspace.Write("TPA/input.bin", tpA);
        string tpBPath = workspace.Write("TPB/input.bin", tpB);
        string outputPath = workspace.PathFor("python-reference-output.bin");
        string referenceSourceRoot = RepositoryPaths.FromRepositoryRoot("refcode", "ab_code_combiner");
        byte[] sourceManifestBytes = File.ReadAllBytes(Path.Combine(referenceSourceRoot, "SOURCE_MANIFEST.json"));
        Assert.Equal(ReferenceManifestSha256, Hash(sourceManifestBytes));
        using JsonDocument sourceManifest = JsonDocument.Parse(sourceManifestBytes);
        string referenceRoot = workspace.PathFor("reference-src");
        foreach (string sourceName in new[] { "combine.py", "ic_config.py" })
        {
            JsonElement sourceEntry = sourceManifest.RootElement.GetProperty("included")
                .EnumerateArray()
                .Single(entry => StringComparer.Ordinal.Equals(
                    entry.GetProperty("path").GetString(),
                    sourceName));
            byte[] sourceBytes = File.ReadAllBytes(Path.Combine(referenceSourceRoot, sourceName));
            Assert.Equal(sourceEntry.GetProperty("sha256").GetString(), Hash(sourceBytes));
            _ = workspace.Write(Path.Combine("reference-src", sourceName), sourceBytes);
        }

        const string script = """
            import pathlib
            import sys

            reference_root = pathlib.Path(sys.argv[1])
            sys.path.insert(0, str(reference_root))

            from combine import combine
            from ic_config import IC_CONFIGS

            dp = bytearray(pathlib.Path(sys.argv[2]).read_bytes())
            tpa = bytearray(pathlib.Path(sys.argv[3]).read_bytes())
            tpb = bytearray(pathlib.Path(sys.argv[4]).read_bytes())
            output = combine(IC_CONFIGS[sys.argv[5]], dp, tpa, tpb, debug=0)
            pathlib.Path(sys.argv[6]).write_bytes(output)
            """;
        var startInfo = new ProcessStartInfo
        {
            FileName = "python",
            WorkingDirectory = workspace.Root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-I");
        startInfo.ArgumentList.Add("-B");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add(referenceRoot);
        startInfo.ArgumentList.Add(dpPath);
        startInfo.ArgumentList.Add(tpAPath);
        startInfo.ArgumentList.Add(tpBPath);
        startInfo.ArgumentList.Add(icId);
        startInfo.ArgumentList.Add(outputPath);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the Python AB reference snapshot.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        string output = await standardOutput;
        string error = await standardError;
        Assert.True(
            process.ExitCode == 0,
            $"Python AB reference failed for NT{icId}. Exit={process.ExitCode}{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{output}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{error}");
        Assert.True(File.Exists(outputPath), $"Python AB reference did not produce {outputPath}.");
        return File.ReadAllBytes(outputPath);
    }
}
