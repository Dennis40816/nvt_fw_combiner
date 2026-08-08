using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.WorkbenchIssueCodes;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Focused composition-adapter tests for report generation around gated workflows.</summary>
public sealed class FocusedCompositionAdaptersTests
{
    /// <summary>General facades expose typed drafts without restoring raw mapping compatibility overloads.</summary>
    [Fact]
    public void GeneralFacadesExposeTypedDraftsWithoutRawMappingOverloads()
    {
        MethodInfo[] workbenchMethods = typeof(CompositionExecutionAdapter).GetMethods(
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo[] executionMethods = typeof(CompositionExecutionAdapter).GetMethods(
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo[] standardMerge =
            [.. executionMethods.Where(static method => method.Name == "RunStandardMergeAsync")];
        MethodInfo[] generalMerge =
            [.. workbenchMethods.Where(static method => method.Name == "RunGeneralMergeAsync")];
        MethodInfo[] replace =
            [.. workbenchMethods.Where(static method => method.Name == "RunReplaceAsync")];

        Assert.Equal([5], standardMerge.Select(static method => method.GetParameters().Length));
        Assert.Empty(generalMerge);
        Assert.Equal([8], replace.Select(static method => method.GetParameters().Length));
        Assert.All(
            standardMerge.Concat(generalMerge).Concat(replace),
            static method => Assert.DoesNotContain(
                method.GetParameters(),
                static parameter => parameter.ParameterType == typeof(CompositionRunProgressFeed)));
        AssertProgressAwareMethod(
            executionMethods,
            "RunStandardMergeWithProgressAsync",
            expectedParameterCount: 6);
        AssertProgressAwareMethod(workbenchMethods, "RunGeneralMergeAcceptedSessionWithProgressAsync", expectedParameterCount: 6);
        AssertProgressAwareMethod(workbenchMethods, "PreviewGeneralReplaceAcceptedSessionWithProgressAsync", expectedParameterCount: 6);
        AssertProgressAwareMethod(workbenchMethods, "BuildGeneralReplaceAcceptedSessionWithProgressAsync", expectedParameterCount: 7);
        AssertProgressAwareMethod(workbenchMethods, "RunReplaceAcceptedSessionWithProgressAsync", expectedParameterCount: 10);
        AssertProgressAwareMethod(workbenchMethods, "RunReplaceWithProgressAsync", expectedParameterCount: 9);
    }

    /// <summary>General Replace Preview/Build crosses workflow boundaries as explicit entry points.</summary>
    [Fact]
    public void GeneralReplaceRunBoundaryDoesNotExposeBooleanActionAdapters()
    {
        MethodInfo[] methods = typeof(CompositionExecutionAdapter).GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo[] generalReplace =
        [
            .. methods.Where(static method =>
                method.Name.Contains("GeneralReplace", StringComparison.Ordinal) &&
                (method.Name.Contains("Run", StringComparison.Ordinal) ||
                 method.Name.Contains("Preview", StringComparison.Ordinal) ||
                 method.Name.Contains("Build", StringComparison.Ordinal) ||
                 method.Name == "TryCreateGeneralReplaceRunContext")),
        ];

        Assert.NotEmpty(generalReplace);
        Assert.All(
            generalReplace,
            static method => Assert.DoesNotContain(
                method.GetParameters(),
                static parameter => parameter.ParameterType == typeof(bool)));
    }

    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private sealed class CountingExternalProcessor : IExternalProcessor
    {
        internal int CallCount { get; private set; }

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(ExternalProcessorResult.Success(request.InputBytes.ToArray(), [], []));
        }
    }

    private sealed class InspectingExternalProcessor : IExternalProcessor
    {
        private readonly Func<ExternalProcessorRequest, ExternalProcessorResult> _transform;

        internal InspectingExternalProcessor(Func<ExternalProcessorRequest, ExternalProcessorResult> transform)
        {
            _transform = transform;
        }

        internal int CallCount { get; private set; }

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(_transform(request));
        }
    }

    /// <summary>Verifies Standard Merge reads its DP source view and ignores a non-authoritative trailing byte.</summary>
    [Fact]
    public async Task StandardMergePreviewIgnoresNonAuthoritativeDpTail()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-dp-length");
        byte[] dp = File.ReadAllBytes(GoldenArtifactPath("51926", "dp-input"));
        Array.Resize(ref dp, dp.Length + 1);
        dp[^1] = 0xA5;
        string dpPath = workspace.Write("dp-nonstandard.bin", dp);
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["dp-input"] = dpPath,
            ["tp-input"] = GoldenArtifactPath("51926", "tp-input"),
        };

        WorkbenchRunResult result = await CompositionExecutionAdapter.RunStandardMergeAsync(
            "NT51926",
            slotPaths,
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(GoldenArtifactPath("51926", "expected-output")))).ToLowerInvariant(),
            result.OutputSha256);
        using var document = JsonDocument.Parse(result.ReportJson);
        Assert.Empty(document.RootElement.GetProperty("Issues").EnumerateArray());
    }

    /// <summary>Verifies firmware metadata exposes display-ready postbuild category names outside the UI layer.</summary>
    [Fact]
    public void FirmwareConfigMetadataShortensPostbuildSetupCategoryForDisplay()
    {
        WorkbenchFirmwareConfigMetadata? metadata = FirmwareInspectionAdapter.TryReadFirmwareConfigMetadata(
            "NT51926",
            GoldenArtifactPath("51926", "expected-output"));

        Assert.NotNull(metadata);
        byte[] image = File.ReadAllBytes(GoldenArtifactPath("51926", "expected-output"));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(image, out FirmwareConfigMetadata backup));
        Assert.Equal(backup.StructureStart, metadata.FirmwareConfigBackupStart);
        Assert.Equal("1.4.1", metadata.CommonFwVersion);
        Assert.Equal(0x02, metadata.ChipNumber);
        Assert.Equal("51926_1.4.1", metadata.PostbuildCategory);
    }

    /// <summary>Uses a unique NVT FWConfig Backup to map the verified chip number to a planner token.</summary>
    [Fact]
    public void FirmwareContextSuggestionUsesVerifiedNvtBackupAndApprovedBranch()
    {
        WorkbenchFirmwareContextSuggestion? suggestion = FirmwareInspectionTestSupport.TryReadFirmwareContextSuggestion(
            "NT51926",
            GoldenArtifactPath("51926", "expected-output"));

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
        byte[] bytes = File.ReadAllBytes(GoldenArtifactPath("51926", "expected-output"));
        Assert.True(BuiltInTpFlashMapCatalog.TryFind("NT51926", out TpFlashMapProfile? flashMap));
        long firmwareConfigStart = flashMap!.FirmwareConfigPrimaryStart;
        bytes[checked((int)firmwareConfigStart + FirmwareConfigLayout.ChipNumberOffset)] = 0x01;
        string path = workspace.Write("fwconfig-mismatch.bin", bytes);

        WorkbenchFirmwareContextSuggestion? suggestion = FirmwareInspectionTestSupport.TryReadFirmwareContextSuggestion(
            "NT51926",
            path);

        Assert.NotNull(suggestion);
        Assert.Equal((byte)0x02, suggestion.ChipNumber);
        Assert.Equal("cascade", suggestion.NumberToken);
        Assert.Equal("1.4.1", suggestion.CommonFwVersion);
    }

    /// <summary>Number suggestions use the effective runtime profile and only offer a registered V2 route.</summary>
    [Fact]
    public void FirmwareContextSuggestionUsesEffectiveProfileAndRegisteredV2Route()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-effective-number-suggestion");
        byte[] bytes = File.ReadAllBytes(GoldenArtifactPath("51926", "expected-output"));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(bytes, out FirmwareConfigMetadata metadata));
        int start = checked((int)metadata.StructureStart);
        bytes[start + FirmwareConfigLayout.CommonFwMajorVersionOffset] = 2;
        bytes[start + FirmwareConfigLayout.CommonFwMinorVersionOffset] = 0;
        bytes[start + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = 0;
        bytes[start + FirmwareConfigLayout.ChipNumberOffset] = 1;
        string path = workspace.Write("fw200-single.bin", bytes);

        WorkbenchFirmwareContextSuggestion suggestion = Assert.IsType<WorkbenchFirmwareContextSuggestion>(
            FirmwareInspectionTestSupport.TryReadFirmwareContextSuggestion("NT51926", path));

        Assert.Equal("single", suggestion.NumberToken);
        Assert.Equal("2.0.0", suggestion.CommonFwVersion);
        WorkbenchFirmwareContextSuggestion nt51928Suggestion = Assert.IsType<WorkbenchFirmwareContextSuggestion>(
            FirmwareInspectionTestSupport.TryReadFirmwareContextSuggestion("NT51928", path));
        Assert.Equal("NT51928", nt51928Suggestion.IcId);
        Assert.Equal("single", nt51928Suggestion.NumberToken);
        Assert.Equal("2.0.0", nt51928Suggestion.CommonFwVersion);
    }

    /// <summary>Firmware display readers fail closed when the selected image disappears before reading.</summary>
    [Fact]
    public void FirmwareMetadataReadersRejectMissingImage()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-missing-metadata");
        string missing = workspace.PathFor("missing.bin");

        Assert.Null(FirmwareInspectionTestSupport.TryReadDpVersionMetadata("NT51950", missing));
        Assert.Null(FirmwareInspectionTestSupport.TryReadCmiDpCodeMetadata("NT51950", missing));
        Assert.Null(FirmwareInspectionAdapter.TryReadFirmwareConfigMetadata("NT51926", missing));
        Assert.Null(FirmwareInspectionTestSupport.TryReadFirmwareContextSuggestion("NT51926", missing));
    }

    /// <summary>Uses the selected TP NVT FWConfig ChipNumber to resolve NT51950's 1IC CMI location.</summary>
    [Fact]
    public void Nt51950CmiMetadataRequiresTpNvtFirmwareConfig()
    {
        string dpPath = GoldenArtifactPath("51950", "dp-input", "dp-256k");
        string tpPath = GoldenArtifactPath("51950", "tp-input", "dp-256k");

        Assert.Null(FirmwareInspectionTestSupport.TryReadCmiDpCodeMetadata("NT51950", dpPath));

        WorkbenchCmiDpCodeMetadata metadata = Assert.IsType<WorkbenchCmiDpCodeMetadata>(
            FirmwareInspectionTestSupport.TryReadCmiDpCodeMetadata(
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

        WorkbenchRunResult result = await CompositionExecutionAdapter.BuildGeneralReplaceEphemeralDraftAsync(
            "NT51950",
            "single",
            slotPaths,
            GeneralTestDraftFactory.CreateReplaceDraft([
                GeneralTestDraftFactory.ReplaceFile("general-map-1", replacementPath, "0x00100", "0x2"),
            ]),
            outputPath,
            TestContext.Current.CancellationToken);

        AssertWorkflowNotSupported(result, outputPath);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>General Replace TP mappings do not fall back to the retired V1 compiler.</summary>
    [Fact]
    public async Task GeneralReplaceTpPreviewFailsClosed()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-general-tp");
        string basePath = GoldenArtifactPath("51950", "expected-output", "dp-256k");
        string replacementPath = workspace.PathFor("replacement.bin");
        byte[] baseBytes = File.ReadAllBytes(basePath);
        File.WriteAllBytes(replacementPath, baseBytes[0x22C00..0x22C02]);
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
        };

        WorkbenchRunResult result = await CompositionExecutionAdapter.PreviewGeneralReplaceEphemeralDraftAsync(
            "NT51950",
            "single",
            slotPaths,
            GeneralTestDraftFactory.CreateReplaceDraft([
                GeneralTestDraftFactory.ReplaceFile("general-map-1", replacementPath, "0x22C00", "0x2"),
            ]),
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

        WorkbenchRunResult result = await CompositionExecutionAdapter.PreviewGeneralReplaceEphemeralDraftAsync(
            "NT51950",
            "single",
            slotPaths,
            GeneralTestDraftFactory.CreateReplaceDraft([
                GeneralTestDraftFactory.ReplaceFile("general-map-1", replacementPath, "0x36000", "0x1"),
            ]),
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
            CompositionExecutionAdapter.RunReplaceAsync(
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

    /// <summary>FW/bar validity cannot block Common FW interval selection during CtrlRAM Preview.</summary>
    [Fact]
    public async Task CtrlRamReplaceKeepsPostbuildSelectionIndependentFromFirmwareVersionBar()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-invalid-fwbar");
        byte[] baseBytes = File.ReadAllBytes(GoldenArtifactPath("51926", "expected-output"));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(baseBytes, out FirmwareConfigMetadata backup));
        baseBytes[checked((int)backup.StructureStart + FirmwareConfigLayout.FirmwareVersionBarOffset)] ^= 0x01;

        string basePath = workspace.Write("base-invalid-fwbar.bin", baseBytes);
        string replacementPath = workspace.Write("normal.bin", baseBytes[0x22800..0x25400]);

        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
            ["replace-ctrlram-normal"] = replacementPath,
        };

        WorkbenchRunResult result = await CompositionExecutionAdapter.RunReplaceAsync(
            "NT51926",
            "cascade",
            "CtrlRAM",
            slotPaths,
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        using var document = JsonDocument.Parse(result.ReportJson);
        Assert.DoesNotContain(
            document.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == ReplaceCtrlRamPostbuildCategoryUnknown);
    }

    /// <summary>TP-version edits execute as plan patches before V2 CtrlRAM postbuild and propagate to Backup.</summary>
    [Fact]
    public async Task CtrlRamReplaceBuildAppliesFirmwareVersionBeforePostbuild()
    {
        const int firmwareConfigSourceStart = 0x22000;
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-fw-version-edit");
        byte[] baseBytes = ReadNt51926Fw200SingleReference();
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(baseBytes, out FirmwareConfigMetadata originalBackup));
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("normal.bin", baseBytes[0x22800..0x25400]);
        string outputPath = workspace.PathFor("edited.bin");
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
            ["replace-ctrlram-normal"] = replacementPath,
        };
        var processor = new InspectingExternalProcessor(request =>
        {
            ReadOnlySpan<byte> input = request.InputBytes.Span;
            Assert.Equal(0x27, input[firmwareConfigSourceStart + FirmwareConfigLayout.FirmwareVersionOffset]);
            Assert.Equal(0xD8, input[firmwareConfigSourceStart + FirmwareConfigLayout.FirmwareVersionBarOffset]);
            Assert.Equal(0x04, input[firmwareConfigSourceStart + FirmwareConfigLayout.FirmwareSubVersionOffset]);

            byte[] output = request.InputBytes.ToArray();
            int backupStart = checked((int)originalBackup.StructureStart);
            output[backupStart + FirmwareConfigLayout.FirmwareVersionOffset] = 0x27;
            output[backupStart + FirmwareConfigLayout.FirmwareVersionBarOffset] = 0xD8;
            output[backupStart + FirmwareConfigLayout.FirmwareSubVersionOffset] = 0x04;
            return ExternalProcessorResult.Success(output, [], []);
        });

        WorkbenchRunResult result = await CompositionExecutionAdapter.RunCtrlRamReplaceWithProcessorAsync(
            "NT51926",
            "single",
            slotPaths,
            build: true,
            outputPath: outputPath,
            firmwareVersionEdit: new WorkbenchCtrlRamFirmwareVersionEdit(0x27, 0x04),
            externalProcessor: processor,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal(1, processor.CallCount);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
        byte[] outputBytes = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        Assert.Equal(0x27, outputBytes[firmwareConfigSourceStart + FirmwareConfigLayout.FirmwareVersionOffset]);
        Assert.Equal(0xD8, outputBytes[firmwareConfigSourceStart + FirmwareConfigLayout.FirmwareVersionBarOffset]);
        Assert.Equal(0x04, outputBytes[firmwareConfigSourceStart + FirmwareConfigLayout.FirmwareSubVersionOffset]);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(outputBytes, out FirmwareConfigMetadata backup));
        Assert.Equal(0x27, backup.FirmwareVersion);
        Assert.Equal(0xD8, backup.FirmwareVersionBar);
        Assert.Equal(0x04, backup.FirmwareSubVersion);

        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement[] operations = [.. document.RootElement.GetProperty("Operations").EnumerateArray()];
        int versionPatch = Array.FindIndex(operations, operation =>
            operation.GetProperty("OperationId").GetString() == "patch-fw-version-and-bar");
        int subVersionPatch = Array.FindIndex(operations, operation =>
            operation.GetProperty("OperationId").GetString() == "patch-fw-sub-version");
        int postbuild = Array.FindIndex(operations, operation =>
            operation.GetProperty("Kind").GetString() == "RunExternalProcessor");
        Assert.True(versionPatch >= 0 && subVersionPatch > versionPatch && postbuild > subVersionPatch);
        JsonElement validation = Assert.Single(document.RootElement.GetProperty("Validations").EnumerateArray());
        Assert.Equal("verify-nvt-fwconfig-backup-version", validation.GetProperty("RuleId").GetString());
        Assert.Equal("Passed", validation.GetProperty("Status").GetString());
    }

    /// <summary>A postbuild that does not propagate the confirmed version fails before output publication.</summary>
    [Fact]
    public async Task CtrlRamReplaceBuildRejectsUnpropagatedFirmwareVersion()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-fwconfig-ambiguous");
        byte[] baseBytes = ReadNt51926Fw200SingleReference();
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("normal.bin", baseBytes[0x22800..0x25400]);
        string outputPath = workspace.PathFor("not-published.bin");
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
            ["replace-ctrlram-normal"] = replacementPath,
        };
        var processor = new CountingExternalProcessor();

        WorkbenchRunResult result = await CompositionExecutionAdapter.RunCtrlRamReplaceWithProcessorAsync(
            "NT51926",
            "single",
            slotPaths,
            build: true,
            outputPath: outputPath,
            firmwareVersionEdit: new WorkbenchCtrlRamFirmwareVersionEdit(0x27, 0x04),
            externalProcessor: processor,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded, result.ReportJson);
        Assert.False(File.Exists(outputPath));
        Assert.Equal(1, processor.CallCount);
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
        using var document = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            document.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == ReplaceCtrlRamFirmwareVersionOutputMismatch);
    }

    /// <summary>
    /// A selected plan that contradicts readable FWConfig chip count fails before V2 compilation.
    /// </summary>
    [Fact]
    public async Task CtrlRamReplaceBuildWithFirmwareCountMismatchFailsClosed()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-workbench-fw-version-preserve");
        byte[] baseBytes = File.ReadAllBytes(GoldenArtifactPath("51926", "expected-output"));
        string basePath = workspace.Write("base.bin", baseBytes);
        string replacementPath = workspace.Write("normal.bin", baseBytes[0x22800..0x25400]);
        string outputPath = workspace.PathFor("preserved.bin");
        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            ["replace-base"] = basePath,
            ["replace-ctrlram-normal"] = replacementPath,
        };

        WorkbenchRunResult result = await CompositionExecutionAdapter.RunReplaceAsync(
            "NT51926",
            "single",
            "CtrlRAM",
            slotPaths,
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.False(result.Succeeded, result.ReportJson);
        Assert.False(File.Exists(outputPath));
        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(document.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(ReplaceCtrlRamIcNumberMismatch, issue.GetProperty("Code").GetString());
        Assert.Equal(baseBytes, await File.ReadAllBytesAsync(basePath, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies TP FW version editing is rejected for a CtrlRAM preview before any firmware processing starts.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewRejectsFirmwareVersionEdit()
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            CompositionExecutionAdapter.RunReplaceAsync(
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

        WorkbenchRunResult result = await CompositionExecutionAdapter.PreviewGeneralReplaceEphemeralDraftAsync(
            "NT51926",
            "single",
            slotPaths,
            GeneralTestDraftFactory.CreateReplaceDraft([]),
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

    /// <summary>An unknown IC DP Replace stays blocked without projecting legacy flash-map operations.</summary>
    [Fact]
    public async Task UnsupportedDpReplacePlanningReportHasNoLegacyOperations()
    {
        WorkbenchRunResult result = await CompositionExecutionAdapter.RunReplaceAsync(
            "NT00000",
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

    private static byte[] ReadNt51926Fw200SingleReference()
    {
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51926-fw200-single-auto-prj-597-20260718");
        JsonElement expected = goldenCase.GetProperty("artifacts").EnumerateArray().Single(artifact =>
            artifact.GetProperty("role").GetString() == "expected");
        return File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(expected));
    }

    private static void AssertProgressAwareMethod(
        IEnumerable<MethodInfo> methods,
        string name,
        int expectedParameterCount)
    {
        MethodInfo method = Assert.Single(
            methods,
            candidate =>
                candidate.Name == name &&
                candidate.GetParameters().Length == expectedParameterCount);
        Assert.Contains(
            method.GetParameters(),
            static parameter => parameter.ParameterType == typeof(CompositionRunProgressFeed));
    }

}
