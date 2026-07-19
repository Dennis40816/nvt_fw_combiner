using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.WorkbenchIssueCodes;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Workbench facade tests for report generation around gated workflows.</summary>
public sealed class WorkbenchCompositionServiceTests
{
    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private sealed class CountingExternalProcessor : IExternalProcessor
    {
        internal int CallCount { get; private set; }

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(ExternalProcessorResult.Success(request.InputBytes.ToArray(), []));
        }
    }

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
                issue.GetProperty("Code").GetString() == "DP_SIZE_WARNING" &&
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
        byte[] image = File.ReadAllBytes(GoldenPath("expected/51926/flash.bin"));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(image, out FirmwareConfigMetadata backup));
        Assert.Equal(backup.FirmwareConfigStart, metadata.FirmwareConfigBackupStart);
        Assert.Equal("1.4.1", metadata.CommonFwVersion);
        Assert.Equal(0x02, metadata.ChipNumber);
        Assert.Equal("51926_1.4.1", metadata.PostbuildCategory);
    }

    /// <summary>Uses a unique NVT FWConfig Backup to map the verified chip number to a planner token.</summary>
    [Fact]
    public void FirmwareContextSuggestionUsesVerifiedNvtBackupAndApprovedBranch()
    {
        WorkbenchFirmwareContextSuggestion? suggestion = WorkbenchCompositionService.TryReadFirmwareContextSuggestion(
            "NT51926",
            GoldenPath("expected/51926/flash.bin"));

        Assert.NotNull(suggestion);
        Assert.Equal("NT51926", suggestion.IcId);
        Assert.Equal((byte)0x02, suggestion.ChipNumber);
        Assert.Equal("cascade", suggestion.NumberToken);
        Assert.Equal("1.4.1", suggestion.CommonFwVersion);
    }

    /// <summary>Uses canonical Backup facts when the TP Overview primary cross-check differs.</summary>
    [Fact]
    public void FirmwareContextSuggestionKeepsBackupAuthorityWhenPrimaryDiffers()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-fwconfig-mismatch");
        byte[] bytes = File.ReadAllBytes(GoldenPath("expected/51926/flash.bin"));
        Assert.True(BuiltInTpFlashMapCatalog.TryFind("NT51926", out TpFlashMapProfile? flashMap));
        long firmwareConfigStart = flashMap!.FirmwareConfigPrimaryStart;
        bytes[checked((int)firmwareConfigStart + FirmwareConfigLayout.ChipNumberOffset)] = 0x01;
        string path = workspace.Write("fwconfig-mismatch.bin", bytes);

        WorkbenchFirmwareContextSuggestion? suggestion = WorkbenchCompositionService.TryReadFirmwareContextSuggestion(
            "NT51926",
            path);

        Assert.NotNull(suggestion);
        Assert.Equal((byte)0x02, suggestion.ChipNumber);
        Assert.Equal("cascade", suggestion.NumberToken);
        Assert.Equal("1.4.1", suggestion.CommonFwVersion);
    }

    /// <summary>Firmware display readers fail closed when the selected image disappears before reading.</summary>
    [Fact]
    public void FirmwareMetadataReadersRejectMissingImage()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-missing-metadata");
        string missing = workspace.PathFor("missing.bin");

        Assert.Null(WorkbenchCompositionService.TryReadDpVersionMetadata("NT51950", missing));
        Assert.Null(WorkbenchCompositionService.TryReadCmiDpCodeMetadata("NT51950", missing));
        Assert.Null(WorkbenchCompositionService.TryReadFirmwareConfigMetadata("NT51926", missing));
        Assert.Null(WorkbenchCompositionService.TryReadFirmwareContextSuggestion("NT51926", missing));
    }

    /// <summary>Uses the selected TP NVT FWConfig ChipNumber to resolve NT51950's 1IC CMI location.</summary>
    [Fact]
    public void Nt51950CmiMetadataRequiresTpNvtFirmwareConfig()
    {
        string dpPath = GoldenPath("inputs/51950/dp-256k/dp.bin");
        string tpPath = GoldenPath("inputs/51950/dp-256k/tp.bin");

        Assert.Null(WorkbenchCompositionService.TryReadCmiDpCodeMetadata("NT51950", dpPath));

        WorkbenchCmiDpCodeMetadata metadata = Assert.IsType<WorkbenchCmiDpCodeMetadata>(
            WorkbenchCompositionService.TryReadCmiDpCodeMetadata(
                "NT51950",
                dpPath,
                tpPath));

        Assert.Equal(0x3B016, metadata.Register16Offset);
        Assert.Equal(576, metadata.JiraNumber);
        Assert.Equal("AUTO_PRJ-576", metadata.JiraBadge);
    }

    /// <summary>General Replace mappings fail closed after the production V1 compiler is retired.</summary>
    [Fact]
    public async Task GeneralReplaceBuildFailsClosedWithoutMutatingTheBase()
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

        AssertWorkflowNotSupported(result, outputPath);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>General Replace TP mappings do not fall back to the retired V1 compiler.</summary>
    [Fact]
    public async Task GeneralReplaceTpPreviewFailsClosed()
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

        AssertWorkflowNotSupported(result);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>Unsupported General Replace rows fail at routing after input parsing succeeds.</summary>
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

        AssertWorkflowNotSupported(result);
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
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(baseBytes, out FirmwareConfigMetadata backup));
        baseBytes[checked((int)backup.FirmwareConfigStart + FirmwareConfigLayout.FirmwareVersionBarOffset)] ^= 0x01;

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

    /// <summary>
    /// Firmware-version edits do not route through the retired V1 CtrlRAM compiler.
    /// </summary>
    [Fact]
    public async Task CtrlRamReplaceBuildWithFirmwareVersionEditFailsClosed()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-fw-version-edit");
        byte[] baseBytes = File.ReadAllBytes(GoldenPath("expected/51926/flash.bin"));
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("normal.bin", baseBytes[0x22800..0x25400]);
        string outputPath = workspace.PathFor("edited.bin");
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
            build: true,
            TestContext.Current.CancellationToken,
            outputPath,
            ctrlRamFirmwareVersionEdit: new WorkbenchCtrlRamFirmwareVersionEdit(0x27, 0x04));

        AssertWorkflowNotSupported(result, outputPath);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Firmware-version edit shapes fail before an external processor can synthesize Backup evidence.
    /// </summary>
    [Fact]
    public async Task CtrlRamReplaceBuildWithCustomProcessorFailsClosedBeforeInvocation()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-fwconfig-ambiguous");
        byte[] baseBytes = File.ReadAllBytes(GoldenPath("expected/51926/flash.bin"));
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("normal.bin", baseBytes[0x22800..0x25400]);
        string outputPath = workspace.PathFor("not-published.bin");
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
            ["replace-ctrlram-normal"] = replacementPath,
        };
        var processor = new CountingExternalProcessor();

        WorkbenchRunResult result = await WorkbenchCompositionService.RunCtrlRamReplaceWithProcessorAsync(
            "NT51926",
            "single",
            slotPaths,
            build: true,
            outputPath: outputPath,
            firmwareVersionEdit: new WorkbenchCtrlRamFirmwareVersionEdit(0x27, 0x04),
            externalProcessor: processor,
            cancellationToken: TestContext.Current.CancellationToken);

        AssertWorkflowNotSupported(result, outputPath);
        Assert.Equal(0, processor.CallCount);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A CtrlRAM shape without an exact V2 evidence route does not fall back to V1.
    /// </summary>
    [Fact]
    public async Task CtrlRamReplaceBuildWithoutExactV2RouteFailsClosed()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-fw-version-preserve");
        byte[] baseBytes = File.ReadAllBytes(GoldenPath("expected/51926/flash.bin"));
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("normal.bin", baseBytes[0x22800..0x25400]);
        string outputPath = workspace.PathFor("preserved.bin");
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
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        AssertWorkflowNotSupported(result, outputPath);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies TP FW version editing is rejected for a CtrlRAM preview before any firmware processing starts.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewRejectsFirmwareVersionEdit()
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            WorkbenchCompositionService.RunReplaceAsync(
                "NT51926",
                "single",
                "CtrlRAM",
                new Dictionary<string, string>(StringComparer.Ordinal),
                build: false,
                TestContext.Current.CancellationToken,
                ctrlRamFirmwareVersionEdit: new WorkbenchCtrlRamFirmwareVersionEdit(0x27, 0x04)).AsTask());

        Assert.Contains("CtrlRAM Replace Build", exception.Message, StringComparison.Ordinal);
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
        Assert.Empty(document.RootElement.GetProperty("Operations").EnumerateArray());
    }

    /// <summary>Unsupported DP Replace stays blocked without projecting legacy flash-map operations.</summary>
    [Fact]
    public async Task UnsupportedDpReplacePlanningReportHasNoLegacyOperations()
    {
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51927",
            "single",
            "DP",
            new Dictionary<string, string>(StringComparer.Ordinal),
            build: false,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(document.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(ReplaceWorkflowNotSupported, issue.GetProperty("Code").GetString());
        Assert.Empty(document.RootElement.GetProperty("Operations").EnumerateArray());
    }

    private static void AssertWorkflowNotSupported(WorkbenchRunResult result, string? outputPath = null)
    {
        Assert.False(result.Succeeded, result.ReportJson);
        if (outputPath is not null)
        {
            Assert.False(File.Exists(outputPath));
        }

        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(document.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(ReplaceWorkflowNotSupported, issue.GetProperty("Code").GetString());
        Assert.False(document.RootElement.GetProperty("Output").GetProperty("Committed").GetBoolean());
    }

}
