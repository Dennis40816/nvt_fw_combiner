using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI tests for Standard Merge command groups.</summary>
public sealed class StandardMergeCliCommandTests
{
    /// <summary>Verifies Standard Merge preview can export a structured JSON report.</summary>
    [Fact]
    public async Task StandardMergePreviewWritesReportJson()
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x30000];
        dp[0x3E000] = 0x11;
        tp[0] = 0x22;
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);
        string report = workspace.PathFor("standard-report.json");

        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "preview",
            "--profile",
            "51920",
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
        Assert.Equal("nt51920-standard-merge-gen-flash", root.GetProperty("ProfileId").GetString());
        Assert.Equal("NT51920", root.GetProperty("IcId").GetString());
        Assert.Equal("standard-merge", root.GetProperty("ExperienceId").GetString());
        Assert.Equal(2, root.GetProperty("Operations").GetArrayLength());
        Assert.Equal("copy-tp", root.GetProperty("Operations")[0].GetProperty("OperationId").GetString());
    }

    /// <summary>Rejects report paths that would overwrite an input BIN.</summary>
    [Fact]
    public async Task StandardMergePreviewRejectsReportPathThatAliasesInput()
    {
        using var workspace = TempWorkspace.Create();
        byte[] dp = new byte[0x40000];
        byte[] tp = new byte[0x30000];
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);

        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "preview",
            "--profile",
            "51920",
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
        byte[] tp = new byte[0x30000];
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);

        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "build",
            "--profile",
            "51920",
            "--dp",
            dpPath,
            "--tp",
            tpPath,
            "--output",
            dpPath,
            "--overwrite",
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
        byte[] tp = new byte[0x30000];
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);
        string outputPath = workspace.PathFor("out.bin");

        CliRunResult result = await RunCliAsync([
            "standard-merge",
            "build",
            "--profile",
            "51920",
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
        byte[] tp = new byte[0x30000];
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            WorkbenchCompositionService
                .RunStandardMergeAsync(
                    "NT51920",
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
            "--overwrite",
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

    private static GoldenCasePaths LoadGoldenCase(string caseId)
    {
        string goldenRoot = Path.Combine(
            RepositoryPaths.FindRepositoryRoot(),
            "testdata",
            "golden",
            "standard-merge-gen-flash");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(goldenRoot, "manifest.json")));
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
