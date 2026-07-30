using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI tests for Standard Merge command groups.</summary>
public sealed class StandardMergeCliCommandTests
{
    /// <summary>Verifies Standard Merge preview can export a structured JSON report.</summary>
    [Theory]
    [InlineData("NT51923")]
    [InlineData("51923")]
    [InlineData("nt51923-standard-merge-gen-flash")]
    public async Task StandardMergePreviewWritesReportJson(string profileSelector)
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x3C000];
        dp[0x3E000] = 0x11;
        tp[0] = 0x22;
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);
        string report = workspace.PathFor("standard-report.json");

        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "preview",
            "--profile",
            profileSelector,
            "--dp",
            dpPath,
            "--tp",
            tpPath,
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("Report:", result.Output, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement root = document.RootElement;
        Assert.Equal("nt51923-standard-merge-gen-flash", root.GetProperty("ProfileId").GetString());
        Assert.Equal("NT51923", root.GetProperty("IcId").GetString());
        Assert.Equal("standard-merge", root.GetProperty("ExperienceId").GetString());
        Assert.Equal(2, root.GetProperty("Operations").GetArrayLength());
        Assert.Equal("copy-tp", root.GetProperty("Operations")[0].GetProperty("OperationId").GetString());
    }

    /// <summary>Verifies the packaged NT51923 V2 profile accepts a caller-selected plain output path through the Standard Merge CLI.</summary>
    [Fact]
    public async Task StandardMergeBuildWritesCallerOutputThroughNt51923V2Profile()
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x3C000];
        dp[0x3E000] = 0x11;
        tp[0] = 0x22;
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);
        string outputPath = workspace.PathFor("caller-output.bin");

        CliRunResult result = await RunCliAsync(
        [
            "standard-merge",
            "build",
            "--profile",
            "NT51923",
            "--dp",
            dpPath,
            "--tp",
            tpPath,
            "--output",
            outputPath,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(outputPath));
        byte[] output = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        Assert.Equal(0x40000, output.Length);
        Assert.Equal(0x22, output[0]);
        Assert.Equal(0x11, output[0x3E000]);
    }

    /// <summary>Verifies the packaged NT51929 V2 profile accepts a caller-selected plain output path through the Standard Merge CLI.</summary>
    [Fact]
    public async Task StandardMergeBuildWritesCallerOutputThroughNt51929V2Profile()
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x40000];
        dp[0] = 0x11;
        tp[0x7000] = 0x22;
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);
        string outputPath = workspace.PathFor("caller-output.bin");

        CliRunResult result = await RunCliAsync(
        [
            "standard-merge",
            "build",
            "--profile",
            "NT51929",
            "--dp",
            dpPath,
            "--tp",
            tpPath,
            "--output",
            outputPath,
        ]);

        Assert.True(WorkbenchCompositionService.IsStandardMergeSupported("NT51929"));
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(outputPath));
        byte[] output = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        Assert.Equal(0x40000, output.Length);
        Assert.Equal(0x11, output[0]);
        Assert.Equal(0x22, output[0x7000]);
    }

    /// <summary>Verifies the NT51917 alias and NT51927 direct V2 routes accept their approved two-mebibyte DP container without an extraction warning.</summary>
    [Theory]
    [InlineData("NT51917")]
    [InlineData("NT51927")]
    public async Task StandardMergeBuildWritesCallerOutputThroughNt51927FamilyV2Profile(string icId)
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x200000];
        byte[] tp = new byte[0x35000];
        dp[0x3C000] = 0x11;
        tp[0] = 0x22;
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);
        string outputPath = workspace.PathFor("caller-output.bin");

        CliRunResult result = await RunCliAsync(
        [
            "standard-merge",
            "build",
            "--profile",
            icId,
            "--dp",
            dpPath,
            "--tp",
            tpPath,
            "--output",
            outputPath,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("Issues:", result.Output, StringComparison.Ordinal);
        Assert.True(File.Exists(outputPath));
        byte[] output = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        Assert.Equal(0x40000, output.Length);
        Assert.Equal(0x22, output[0]);
        Assert.Equal(0x11, output[0x3C000]);
    }

    /// <summary>Verifies the NT51928 V2 profile binds the required LDC BIN through the generic Standard Merge CLI.</summary>
    [Fact]
    public async Task StandardMergeBuildWritesCallerOutputThroughNt51928V2ProfileWithLdc()
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x80000];
        byte[] tp = new byte[0x35000];
        byte[] ld = new byte[0x80000];
        dp[0x3C000] = 0x11;
        tp[0] = 0x22;
        ld[0x40000] = 0x33;
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);
        string ldPath = workspace.Write("ld.bin", ld);
        string outputPath = workspace.PathFor("caller-output.bin");

        CliRunResult result = await RunCliAsync(
        [
            "standard-merge",
            "build",
            "--profile",
            "NT51928",
            "--dp",
            dpPath,
            "--tp",
            tpPath,
            "--ldc",
            ldPath,
            "--output",
            outputPath,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(outputPath));
        byte[] output = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        Assert.Equal(0x80000, output.Length);
        Assert.Equal(0x22, output[0]);
        Assert.Equal(0x11, output[0x3C000]);
        Assert.Equal(0x33, output[0x40000]);
    }

    /// <summary>Verifies omitted NT51928 LDC selects the 256-KiB Initial-Code/TP variant.</summary>
    [Fact]
    public async Task StandardMergePreviewAcceptsOmittedLdcForNt51928V2Profile()
    {
        using var workspace = TempWorkspace.Create();
        string dpPath = workspace.Write("dp.bin", new byte[0x40000]);
        string tpPath = workspace.Write("tp.bin", new byte[0x35000]);

        CliRunResult result = await RunCliAsync(
        [
            "standard-merge",
            "preview",
            "--profile",
            "NT51928",
            "--dp",
            dpPath,
            "--tp",
            tpPath,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains("Size: 262144 bytes", result.Output, StringComparison.Ordinal);
    }

    /// <summary>Unknown Standard Merge selectors remain a usage error.</summary>
    [Fact]
    public async Task StandardMergePreviewRejectsUnknownProfile()
    {
        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "preview",
            "--profile",
            "NT00000",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("unknown standard merge profile 'NT00000'", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Required input validation still runs before DP-length specialization.</summary>
    [Fact]
    public async Task StandardMergePreviewRejectsMissingRequiredInput()
    {
        using var workspace = TempWorkspace.Create();
        string dpPath = workspace.Write("dp.bin", new byte[0x40000]);

        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "preview",
            "--profile",
            "51950",
            "--dp",
            dpPath,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("--tp is required for address space 'tp-input'", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Missing DP Perspective files report a stable input issue instead of an empty compilation failure.</summary>
    [Theory]
    [InlineData("51950")]
    [InlineData("51951")]
    public async Task StandardMergePreviewReportsMissingDpPerspectiveFile(string profileSelector)
    {
        using var workspace = TempWorkspace.Create();
        string missingDpPath = workspace.PathFor("missing-dp.bin");
        string tpPath = workspace.Write("tp.bin", new byte[0x3C000]);

        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "preview",
            "--profile",
            profileSelector,
            "--dp",
            missingDpPath,
            "--tp",
            tpPath,
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.Contains("input.artifact.read-failed [dp-input]", result.Error, StringComparison.Ordinal);
        Assert.Contains("Selected DP BIN path does not exist", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Workbench DP Perspective runs surface the same stable missing-input issue as the CLI.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public async Task WorkbenchStandardMergeReportsMissingDpPerspectiveFile(string icId)
    {
        using var workspace = TempWorkspace.Create();
        string missingDpPath = workspace.PathFor("missing-dp.bin");
        string tpPath = workspace.Write("tp.bin", new byte[0x30000]);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WorkbenchCompositionService.RunStandardMergeAsync(
                icId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["dp-input"] = missingDpPath,
                    ["tp-input"] = tpPath,
                },
                build: false,
                TestContext.Current.CancellationToken).AsTask());

        Assert.Contains("input.artifact.read-failed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Selected DP BIN path does not exist", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Inputs outside the compiled profile summary remain rejected.</summary>
    [Fact]
    public async Task StandardMergePreviewRejectsUnusedInput()
    {
        using var workspace = TempWorkspace.Create();
        string dpPath = workspace.Write("dp.bin", new byte[0x40000]);
        string tpPath = workspace.Write("tp.bin", new byte[0x3C000]);
        string ldPath = workspace.Write("ld.bin", [0x11]);

        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "preview",
            "--profile",
            "51923",
            "--dp",
            dpPath,
            "--tp",
            tpPath,
            "--ldc",
            ldPath,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("--ldc is not used by this profile", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects report paths that would overwrite an input BIN.</summary>
    [Fact]
    public async Task StandardMergePreviewRejectsReportPathThatAliasesInput()
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x3C000];
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);

        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "preview",
            "--profile",
            "51923",
            "--dp",
            dpPath,
            "--tp",
            tpPath,
            "--report",
            dpPath,
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.Contains("Report path must not overwrite input artifact", result.Error, StringComparison.Ordinal);
        Assert.Equal(dp, await File.ReadAllBytesAsync(dpPath, TestContext.Current.CancellationToken));
    }

    /// <summary>Rejects Standard Merge build outputs that would overwrite an input BIN.</summary>
    [Fact]
    public async Task StandardMergeBuildRejectsOutputPathThatAliasesInput()
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x3C000];
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);

        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "build",
            "--profile",
            "51923",
            "--dp",
            dpPath,
            "--tp",
            tpPath,
            "--output",
            dpPath,
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.Contains("Output path must not overwrite input artifact", result.Error, StringComparison.Ordinal);
        Assert.Equal(dp, await File.ReadAllBytesAsync(dpPath, TestContext.Current.CancellationToken));
    }

    /// <summary>Rejects report paths that would overwrite a successfully built firmware image.</summary>
    [Fact]
    public async Task StandardMergeBuildRejectsReportPathThatAliasesOutput()
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x3C000];
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);
        string outputPath = workspace.PathFor("out.bin");

        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "build",
            "--profile",
            "51923",
            "--dp",
            dpPath,
            "--tp",
            tpPath,
            "--output",
            outputPath,
            "--report",
            outputPath,
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.Contains("Report path must not overwrite built firmware output", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(outputPath));
    }

    /// <summary>Rejects Workbench build outputs that would overwrite selected input BINs.</summary>
    [Fact]
    public async Task WorkbenchStandardMergeBuildRejectsOutputPathThatAliasesInput()
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x3C000];
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            WorkbenchCompositionService
                .RunStandardMergeAsync(
                    "NT51923",
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["dp-input"] = dpPath,
                        ["tp-input"] = tpPath,
                    },
                    build: true,
                    TestContext.Current.CancellationToken,
                    outputPath: dpPath)
                .AsTask());

        Assert.Contains("Output path must not overwrite input artifact", exception.Message, StringComparison.Ordinal);
        Assert.Equal(dp, await File.ReadAllBytesAsync(dpPath, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies DP Perspective Standard Merge build uses the selected DP BIN length.</summary>
    [Fact]
    public async Task StandardMergeBuildUsesDpLengthForDpPerspectiveGolden()
    {
        GoldenCasePaths golden = LoadGoldenCase("51950-dp-256k");
        using var workspace = TempWorkspace.Create();
        string outputPath = workspace.PathFor("out.bin");

        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "build",
            "--profile",
            "51950",
            "--dp",
            golden.DpPath,
            "--tp",
            golden.TpPath,
            "--output",
            outputPath,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("Profile: nt51950-standard-merge-dp-perspective (NT51950)", result.Output, StringComparison.Ordinal);
        Assert.Contains("Size: 262144 bytes", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Issues:", result.Error, StringComparison.Ordinal);
        byte[] actual = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
        byte[] expected = await File.ReadAllBytesAsync(golden.ExpectedPath, TestContext.Current.CancellationToken);
        Assert.Equal(0x40000, actual.Length);
        Assert.Equal(expected, actual);
    }

    /// <summary>Unsupported DP Perspective lengths fail closed before composition starts.</summary>
    [Fact]
    public async Task StandardMergePreviewRejectsUnsupportedDpPerspectiveLength()
    {
        using var workspace = TempWorkspace.Create();
        string dpPath = workspace.Write("dp.bin", new byte[0x40001]);
        string tpPath = workspace.Write("tp.bin", new byte[0x30000]);

        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "preview",
            "--profile",
            "51950",
            "--dp",
            dpPath,
            "--tp",
            tpPath,
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.Contains("accepts DP input lengths", result.Error, StringComparison.Ordinal);
    }

    private static GoldenCasePaths LoadGoldenCase(string caseId)
    {
        string goldenRoot = CanonicalGoldenTestData.Root;
        using JsonDocument manifestDocument = CanonicalGoldenTestData.LoadDirectWorkflowManifest("standard-merge");
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(item =>
                item.TryGetProperty("caseId", out JsonElement id) &&
                string.Equals(id.GetString(), caseId, StringComparison.Ordinal));

        JsonElement inputs = goldenCase.GetProperty("inputs");
        return new GoldenCasePaths(
            Path.Combine(goldenRoot, inputs.GetProperty("dp-input").GetProperty("path").GetString()!),
            Path.Combine(goldenRoot, inputs.GetProperty("tp-input").GetProperty("path").GetString()!),
            Path.Combine(goldenRoot, goldenCase.GetProperty("expectedOutput").GetProperty("path").GetString()!));
    }

    private static Task<CliRunResult> RunCliAsync(string[] args)
    {
        return CliTestHarness.RunAsync(args, TestContext.Current.CancellationToken);
    }

    private sealed record GoldenCasePaths(string DpPath, string TpPath, string ExpectedPath);

}
