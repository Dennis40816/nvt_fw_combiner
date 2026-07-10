using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.WorkbenchIssueCodes;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Workbench facade tests for report generation around gated workflows.</summary>
public sealed class WorkbenchCompositionServiceTests
{
    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>Verifies FlashCode output naming reads DP/FWConfig metadata outside the UI layer.</summary>
    [Fact]
    public void FlashCodeOutputNameUsesCatalogBackedDpAndTpMetadata()
    {
        string dpPath = GoldenPath("inputs/51926/dp.bin");
        string tpPath = GoldenPath("inputs/51926/tp.bin");

        WorkbenchOutputFileNameSuggestion suggestion = WorkbenchCompositionService.CreateFlashCodeOutputFileName(
            "51926",
            [
                new WorkbenchOutputNameCandidate(WorkbenchOutputNameCandidateKind.Dp, dpPath),
                new WorkbenchOutputNameCandidate(WorkbenchOutputNameCandidateKind.Tp, tpPath),
            ],
            new DateOnly(2026, 7, 8));

        Assert.Equal("NT51926_FlashCode_D0102T0100_20260708.bin", suggestion.FileName);
        Assert.Equal("0102", suggestion.DpVersionToken);
        Assert.True(suggestion.HasDpVersion);
        Assert.Equal("0100", suggestion.TpVersionToken);
        Assert.True(suggestion.HasTpVersion);
        Assert.Equal("20260708", suggestion.DateToken);
    }

    /// <summary>Verifies missing metadata produces explicit unknown tokens without guessing offsets.</summary>
    [Fact]
    public void FlashCodeOutputNameUsesUnknownTokensWhenMetadataIsMissing()
    {
        WorkbenchOutputFileNameSuggestion suggestion = WorkbenchCompositionService.CreateFlashCodeOutputFileName(
            "NT51950",
            [],
            new DateOnly(2026, 7, 8));

        Assert.Equal("NT51950_FlashCode_DxxxxTxxxx_20260708.bin", suggestion.FileName);
        Assert.Equal("xxxx", suggestion.DpVersionToken);
        Assert.False(suggestion.HasDpVersion);
        Assert.Equal("xxxx", suggestion.TpVersionToken);
        Assert.False(suggestion.HasTpVersion);
    }

    /// <summary>Verifies Standard Merge extracts a sufficient nonstandard DP artifact and reports the size warning.</summary>
    [Fact]
    public async Task StandardMergePreviewWarnsButDoesNotBlockUnexpectedDpLength()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-dp-length");
        byte[] dp = File.ReadAllBytes(GoldenPath("inputs/51926/dp.bin"));
        Array.Resize(ref dp, dp.Length + 1);
        dp[^1] = 0xA5;
        string dpPath = workspace.Write("dp-nonstandard.bin", dp);
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["dp-input"] = dpPath,
            ["tp-input"] = GoldenPath("inputs/51926/tp.bin"),
        };

        WorkbenchRunResult result = await WorkbenchCompositionService.RunStandardMergeAsync(
            "NT51926",
            slotPaths,
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(GoldenPath("expected/51926/flash.bin")))).ToLowerInvariant(),
            result.OutputSha256);
        using var document = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            document.RootElement.GetProperty("Issues").EnumerateArray(),
            issue =>
                issue.GetProperty("Code").GetString() == CompositionIssueCodes.InputAddressSpaceLengthUnexpected &&
                issue.GetProperty("Severity").GetString() == "warning");
    }

    /// <summary>Verifies firmware metadata exposes display-ready postbuild category names outside the UI layer.</summary>
    [Fact]
    public void FirmwareConfigMetadataShortensPostbuildSetupCategoryForDisplay()
    {
        WorkbenchFirmwareConfigMetadata? metadata = WorkbenchCompositionService.TryReadFirmwareConfigMetadata(
            "NT51926",
            GoldenPath("expected/51926/flash.bin"));

        Assert.NotNull(metadata);
        Assert.Equal("1.4.1", metadata.CommonFwVersion);
        Assert.Equal(0x02, metadata.ChipNumber);
        Assert.Equal("51926_1.4.1", metadata.PostbuildCategory);
    }

    /// <summary>Verifies General Replace build writes a profile-approved DP explicit mapping.</summary>
    [Fact]
    public async Task GeneralReplaceBuildWritesDpExplicitMapping()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-general");
        byte[] baseBytes = CreatePattern(0x40000, 0x20);
        byte[] replacementBytes = [0xA5, 0x5A];
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("replacement.bin", replacementBytes);
        string outputPath = workspace.PathFor("out.bin");
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
        };

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "single",
            "General",
            slotPaths,
            [new WorkbenchGeneralReplaceMappingInput("general-map-1", replacementPath, "0x00100", "0x00101")],
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        byte[] output = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        Assert.Equal(baseBytes.Length, output.Length);
        Assert.Equal(0xA5, output[0x100]);
        Assert.Equal(0x5A, output[0x101]);
        Assert.Equal(baseBytes[0x102], output[0x102]);

        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement operation = Assert.Single(document.RootElement.GetProperty("Operations").EnumerateArray());
        Assert.Equal("ReplaceRange", operation.GetProperty("Kind").GetString());
        Assert.Equal("general-map-1", operation.GetProperty("OperationId").GetString());
    }

    /// <summary>Verifies General Replace runs postbuild when an explicit mapping touches TP/CtrlRAM.</summary>
    [Fact]
    public async Task GeneralReplacePreviewRunsPostbuildForTpRange()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-general-tp");
        string basePath = GoldenPath("expected/51950/dp-256k/flash.bin");
        string replacementPath = workspace.PathFor("replacement.bin");
        byte[] baseBytes = File.ReadAllBytes(basePath);
        File.WriteAllBytes(replacementPath, baseBytes[0x22C00..0x22C02]);
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
        };

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "single",
            "General",
            slotPaths,
            [new WorkbenchGeneralReplaceMappingInput("general-map-1", replacementPath, "0x22C00", "0x22C01")],
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        using var document = JsonDocument.Parse(result.ReportJson);
        Assert.Empty(document.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Collection(
            document.RootElement.GetProperty("Operations").EnumerateArray(),
            operation => Assert.Equal("ReplaceRange", operation.GetProperty("Kind").GetString()),
            operation =>
            {
                Assert.Equal("RunExternalProcessor", operation.GetProperty("Kind").GetString());
                Assert.Equal("nfc.nt51950.ctrlram-postbuild-v1", operation.GetProperty("ProcessorId").GetString());
                Assert.Equal("legacy-combiner-1.13.0", operation.GetProperty("ToolBindingId").GetString());
            });
    }

    /// <summary>Verifies General Replace keeps TP Overview information rows protected.</summary>
    [Fact]
    public async Task GeneralReplaceRejectsFwInformationRows()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-general-protected");
        string basePath = workspace.Write("base.bin", CreatePattern(0x40000, 0x20));
        string replacementPath = workspace.Write("replacement.bin", [0xA5]);
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
        };

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51950",
            "single",
            "General",
            slotPaths,
            [new WorkbenchGeneralReplaceMappingInput("general-map-1", replacementPath, "0x36000", "0x36000")],
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        using var document = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            document.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == "profile.explicit-mapping.region-not-enabled");
    }

    /// <summary>Rejects Workbench DP Replace build outputs that would overwrite selected input BINs.</summary>
    [Fact]
    public async Task DpReplaceBuildRejectsOutputPathThatAliasesInput()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-dp-alias");
        byte[] baseBytes = CreatePattern(0x40000, 0x20);
        string basePath = workspace.Write("base.bin", baseBytes);
        string dpPath = workspace.Write("dp.bin", CreatePattern(0x40000, 0x80));
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
            ["replace-dp"] = dpPath,
        };

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            WorkbenchCompositionService
                .RunReplaceAsync(
                    "NT51950",
                    "single",
                    "DP",
                    slotPaths,
                    build: true,
                    TestContext.Current.CancellationToken,
                    outputPath: basePath)
                .AsTask());

        Assert.Contains("Output path must not overwrite input artifact", exception.Message, StringComparison.Ordinal);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies versioned CtrlRAM postbuild fails closed when FWConfig FW/bar is invalid.</summary>
    [Fact]
    public async Task CtrlRamReplaceRejectsInvalidFwVersionBarBeforePostbuildCategorySelection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-invalid-fwbar");
        byte[] baseBytes = File.ReadAllBytes(GoldenPath("expected/51926/flash.bin"));
        Assert.True(TpFlashMapCatalog.TryGetFirmwareConfigStart("NT51926", out long firmwareConfigStart));
        baseBytes[checked((int)firmwareConfigStart + FirmwareConfigLayout.FirmwareVersionBarOffset)] ^= 0x01;

        string basePath = workspace.Write("base-invalid-fwbar.bin", baseBytes);
        string replacementPath = workspace.Write("normal.bin", baseBytes[0x22800..0x25400]);

        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
            ["replace-ctrlram-normal"] = replacementPath,
        };

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51926",
            "single",
            "CtrlRAM",
            slotPaths,
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        using var document = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            document.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == ReplaceCtrlRamPostbuildCategoryUnknown);
    }

    /// <summary>Verifies gated Replace reports can summarize missing inputs without throwing.</summary>
    [Fact]
    public async Task GeneralReplacePlanningReportIncludesMissingInputSummary()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench");
        string missingBase = workspace.PathFor("missing-base.bin");
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = missingBase,
        };

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51927",
            "single",
            "General",
            slotPaths,
            build: false,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement input = Assert.Single(document.RootElement.GetProperty("Inputs").EnumerateArray());
        Assert.Equal("replace-base", input.GetProperty("AddressSpaceId").GetString());
        Assert.Equal("missing-base.bin", input.GetProperty("ArtifactId").GetString());
        Assert.Equal(0, input.GetProperty("Size").GetInt64());
        Assert.Equal(EmptySha256, input.GetProperty("Sha256").GetString());

        JsonElement issue = Assert.Single(document.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(InputArtifactReadFailed, issue.GetProperty("Code").GetString());
        Assert.Equal("error", issue.GetProperty("Severity").GetString());
    }

    private static byte[] CreatePattern(int length, byte seed)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(seed + index));
        }

        return bytes;
    }

    private static string GoldenPath(string relativePath)
    {
        return Path.Combine(
            RepositoryPaths.FindRepositoryRoot(),
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
