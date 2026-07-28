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
        string goldenRoot = CanonicalGoldenTestData.Root;
        using JsonDocument manifestDocument = CanonicalGoldenTestData.LoadDirectWorkflowManifest("standard-merge");
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
        string goldenRoot = CanonicalGoldenTestData.Root;
        using JsonDocument manifestDocument = CanonicalGoldenTestData.LoadDirectWorkflowManifest("standard-merge");
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

    /// <summary>Every Gen Flash IC replaces its declared DP payload and NT51928 LDC without changing the approved base.</summary>
    [Theory]
    [InlineData("51917", "51927", "nt51917-dp-replace-gen-flash-alias", false)]
    [InlineData("51919", "51929", "nt51919-dp-replace-gen-flash-alias", false)]
    [InlineData("51920", "51920", "nt51920-dp-replace-gen-flash", false)]
    [InlineData("51923", "51923", "nt51923-dp-replace-gen-flash", false)]
    [InlineData("51926", "51926", "nt51926-dp-replace-gen-flash", false)]
    [InlineData("51927", "51927", "nt51927-dp-replace-gen-flash", false)]
    [InlineData("51928", "51928", "nt51928-dp-replace-gen-flash", true)]
    [InlineData("51929", "51929", "nt51929-dp-replace-gen-flash", false)]
    [InlineData("51931", "51931", "nt51931-dp-replace-gen-flash", false)]
    [InlineData("51932", "51932", "nt51932-dp-replace-gen-flash", false)]
    public async Task GenFlashDpReplaceWithOriginalInputsMatchesGoldenBaseBytes(
        string ic,
        string goldenIc,
        string expectedProfileId,
        bool expectsLdc)
    {
        string goldenRoot = CanonicalGoldenTestData.Root;
        using JsonDocument manifestDocument = CanonicalGoldenTestData.LoadDirectWorkflowManifest("standard-merge");
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(item => item.GetProperty("ic").GetString() == goldenIc)
            .Clone();
        byte[] expectedBaseBytes = ReadManifestFile(goldenRoot, goldenCase.GetProperty("expectedOutput"));
        JsonElement inputs = goldenCase.GetProperty("inputs");

        using var workspace = TempWorkspace.Create($"nvt-fw-combiner-dp-replace-golden-{ic}");
        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["replace-base"] = workspace.Write("reference-flash.bin", expectedBaseBytes),
            ["replace-dp"] = workspace.Write(
                "replacement-dp.bin",
                ReadManifestFile(goldenRoot, inputs.GetProperty("dp-input"))),
        };
        if (expectsLdc)
        {
            slotPaths["replace-ldc"] = workspace.Write(
                "replacement-ldc.bin",
                ReadManifestFile(goldenRoot, inputs.GetProperty("ld-input")));
        }

        string outputPath = workspace.PathFor($"nt{ic}-dp-replace.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            $"NT{ic}",
            "single",
            "DP",
            slotPaths,
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(expectedBaseBytes, await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken));
        Assert.Equal(expectedBaseBytes.LongLength, result.OutputSize);
        Assert.Equal(Sha256Hex(expectedBaseBytes), result.OutputSha256);
        using var reportDocument = JsonDocument.Parse(result.ReportJson);
        JsonElement root = reportDocument.RootElement;
        Assert.Equal(expectedProfileId, root.GetProperty("ProfileId").GetString());
        JsonElement[] issues = [.. root.GetProperty("Issues").EnumerateArray()];
        Assert.All(issues, issue =>
        {
            Assert.Equal("DP_SIZE_WARNING", issue.GetProperty("Code").GetString());
            Assert.Equal("warning", issue.GetProperty("Severity").GetString());
        });
        Assert.Empty(root.GetProperty("OutputDifferences").EnumerateArray());
        Assert.Equal(
            expectsLdc ? ["replace-dp-code", "replace-ldc-code"] : ["replace-dp-code"],
            root.GetProperty("Operations")
                .EnumerateArray()
                .Select(static operation => operation.GetProperty("OperationId").GetString()));
    }

    /// <summary>NT51928 changes only the independently selected Initial Code and LDC parts.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Nt51928DpReplacePreservesEveryUnselectedReferenceByte(
        bool selectInitialCode,
        bool selectLdc)
    {
        const int initialCodeStart = 0x3C000;
        const int initialCodeLength = 0x4000;
        const int ldcStart = 0x40000;
        const int ldcLength = 0x22000;
        string goldenRoot = CanonicalGoldenTestData.Root;
        using JsonDocument manifestDocument = CanonicalGoldenTestData.LoadDirectWorkflowManifest("standard-merge");
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(item => item.GetProperty("ic").GetString() == "51928")
            .Clone();
        byte[] referenceBytes = ReadManifestFile(
            goldenRoot,
            goldenCase.GetProperty("expectedOutput"));
        byte[] initialCodeBytes = [.. referenceBytes];
        byte[] ldcBytes = [.. referenceBytes];
        Array.Fill(initialCodeBytes, (byte)0xA1, initialCodeStart, initialCodeLength);
        Array.Fill(ldcBytes, (byte)0xB2, ldcStart, ldcLength);
        byte[] expectedBytes = [.. referenceBytes];
        if (selectInitialCode)
        {
            Array.Fill(expectedBytes, (byte)0xA1, initialCodeStart, initialCodeLength);
        }

        if (selectLdc)
        {
            Array.Fill(expectedBytes, (byte)0xB2, ldcStart, ldcLength);
        }

        using var workspace = TempWorkspace.Create(
            $"nvt-fw-combiner-dp-replace-51928-{selectInitialCode}-{selectLdc}");
        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["replace-base"] = workspace.Write("reference-flash.bin", referenceBytes),
        };
        if (selectInitialCode)
        {
            slotPaths["replace-dp"] = workspace.Write("initial-code.bin", initialCodeBytes);
        }

        if (selectLdc)
        {
            slotPaths["replace-ldc"] = workspace.Write("ldc.bin", ldcBytes);
        }

        string outputPath = workspace.PathFor("nt51928-dp-replace.bin");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51928",
            "single",
            "DP",
            slotPaths,
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(expectedBytes, await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken));
        Assert.Equal(expectedBytes.LongLength, result.OutputSize);
        Assert.Equal(Sha256Hex(expectedBytes), result.OutputSha256);
        using var reportDocument = JsonDocument.Parse(result.ReportJson);
        var expectedOperations = new List<string>();
        if (selectInitialCode)
        {
            expectedOperations.Add("replace-dp-code");
        }

        if (selectLdc)
        {
            expectedOperations.Add("replace-ldc-code");
        }

        Assert.Equal(
            expectedOperations,
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

    private static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
