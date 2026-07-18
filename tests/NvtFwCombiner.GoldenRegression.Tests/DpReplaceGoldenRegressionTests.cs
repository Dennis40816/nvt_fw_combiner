using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.GoldenRegression.Tests;

/// <summary>Golden-derived regression tests for built-in V2 DP Replace workbench behavior.</summary>
public sealed class DpReplaceGoldenRegressionTests
{
    private static readonly string[] ExpectedDpReplaceOperations =
    [
        "replace-dp-container",
        "restore-base-tp",
    ];

    /// <summary>Verifies 950/951 DP Replace self-replacement preserves owner-approved golden flash bytes.</summary>
    [Theory]
    [InlineData("51950", "dp-256k")]
    [InlineData("51951", "dp-512k")]
    public async Task Nt51950FamilyDpReplaceWithOriginalDpInputMatchesGoldenBaseBytes(string ic, string variant)
    {
        string goldenRoot = RepositoryPaths.FromRepositoryRoot("testdata", "golden", "standard-merge-gen-flash");
        using JsonDocument manifestDocument = LoadJson(Path.Combine(goldenRoot, "manifest.json"));
        JsonElement goldenCase = FindDpPerspectiveCase(manifestDocument.RootElement, ic, variant);
        byte[] expectedBaseBytes = ReadManifestFile(goldenRoot, goldenCase.GetProperty("expectedOutput"));
        byte[] replacementDpBytes = ReadManifestFile(goldenRoot, goldenCase.GetProperty("inputs").GetProperty("dp-input"));

        using var workspace = TempWorkspace.Create($"nvt-fw-combiner-dp-replace-golden-{ic}");
        string basePath = workspace.Write("base-flash.bin", expectedBaseBytes);
        string replacementDpPath = workspace.Write("replacement-dp.bin", replacementDpBytes);
        string outputPath = workspace.PathFor($"nt{ic}-dp-replace.bin");

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
                $"NT{ic}",
                "single",
                "DP",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["replace-base"] = basePath,
                    ["replace-dp"] = replacementDpPath,
                },
                build: true,
                TestContext.Current.CancellationToken,
                outputPath);

        Assert.True(result.Succeeded, result.Status);
        Assert.NotNull(result.CommittedOutputId);
        Assert.True(File.Exists(result.CommittedOutputId), result.CommittedOutputId);
        byte[] actualBytes = await File.ReadAllBytesAsync(result.CommittedOutputId, TestContext.Current.CancellationToken);
        Assert.Equal(expectedBaseBytes, actualBytes);
        Assert.Equal(expectedBaseBytes.LongLength, result.OutputSize);
        Assert.Equal(Sha256Hex(expectedBaseBytes), result.OutputSha256);

        using var reportDocument = JsonDocument.Parse(result.ReportJson);
        JsonElement root = reportDocument.RootElement;
        Assert.Equal($"nt{ic}-dp-replace-dp-perspective", root.GetProperty("ProfileId").GetString());
        Assert.Equal($"NT{ic}", root.GetProperty("IcId").GetString());
        Assert.Empty(root.GetProperty("Issues").EnumerateArray());
        Assert.Equal(expectedBaseBytes.LongLength, root.GetProperty("Output").GetProperty("Size").GetInt64());
        Assert.Equal(Sha256Hex(expectedBaseBytes), root.GetProperty("Output").GetProperty("Sha256").GetString());
        Assert.Empty(root.GetProperty("OutputDifferences").EnumerateArray());
        Assert.Equal(
            ExpectedDpReplaceOperations,
            root.GetProperty("Operations").EnumerateArray()
                .Select(operation => operation.GetProperty("OperationId").GetString())
                .ToArray());
    }

    /// <summary>Verifies NT51930 self-replacement preserves the owner Standard Merge output without promoting direct DP Replace golden status.</summary>
    [Fact]
    public async Task Nt51930DpReplaceWithOriginalDpInputMatchesGoldenBaseBytes()
    {
        string goldenRoot = RepositoryPaths.FromRepositoryRoot("testdata", "golden", "standard-merge-gen-flash");
        using JsonDocument manifestDocument = LoadJson(Path.Combine(goldenRoot, "manifest.json"));
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(item => item.GetProperty("ic").GetString() == "51930")
            .Clone();
        byte[] expectedBaseBytes = ReadManifestFile(goldenRoot, goldenCase.GetProperty("expectedOutput"));
        byte[] replacementDpBytes = ReadManifestFile(goldenRoot, goldenCase.GetProperty("inputs").GetProperty("dp-input"));

        using var workspace = TempWorkspace.Create("nvt-fw-combiner-dp-replace-golden-51930");
        string outputPath = workspace.PathFor("nt51930-dp-replace.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51930",
            "single",
            "DP",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["replace-base"] = workspace.Write("reference-flash.bin", expectedBaseBytes),
                ["replace-dp"] = workspace.Write("replacement-dp.bin", replacementDpBytes),
            },
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        byte[] actualBytes = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        Assert.Equal(expectedBaseBytes, actualBytes);
        Assert.Equal(Sha256Hex(expectedBaseBytes), result.OutputSha256);
        using var reportDocument = JsonDocument.Parse(result.ReportJson);
        Assert.Equal("nt51930-dp-replace-flashmap", reportDocument.RootElement.GetProperty("ProfileId").GetString());
        Assert.Equal(
            ["replace-dp-code"],
            reportDocument.RootElement.GetProperty("Operations")
                .EnumerateArray()
                .Select(static operation => operation.GetProperty("OperationId").GetString()));
    }

    private static JsonElement FindDpPerspectiveCase(JsonElement manifest, string ic, string variant)
    {
        return manifest.GetProperty("cases")
            .EnumerateArray()
            .Single(item =>
                item.GetProperty("ic").GetString() == ic &&
                item.TryGetProperty("variant", out JsonElement caseVariant) &&
                caseVariant.GetString() == variant)
            .Clone();
    }

    private static byte[] ReadManifestFile(string goldenRoot, JsonElement manifestFile)
    {
        string fullPath = RepositoryPaths.ManifestPath(goldenRoot, manifestFile);
        byte[] bytes = File.ReadAllBytes(fullPath);

        Assert.Equal(manifestFile.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(manifestFile.GetProperty("sha256").GetString(), Sha256Hex(bytes));
        return bytes;
    }

    private static JsonDocument LoadJson(string path)
    {
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
