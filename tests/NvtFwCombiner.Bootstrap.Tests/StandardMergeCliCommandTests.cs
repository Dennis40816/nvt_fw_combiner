using System.Text.Json;

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

    private static async Task<CliRunResult> RunCliAsync(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = await CliApplication
            .RunAsync(args, output, error, TestContext.Current.CancellationToken);
        return new CliRunResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed record CliRunResult(int ExitCode, string Output, string Error);

    private sealed class TempWorkspace : IDisposable
    {
        private TempWorkspace(string root)
        {
            Root = root;
        }

        private string Root { get; }

        internal static TempWorkspace Create()
        {
            string root = Path.Combine(Path.GetTempPath(), $"nvt-fw-combiner-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(root);
            return new TempWorkspace(root);
        }

        internal string PathFor(string fileName)
        {
            return Path.Combine(Root, fileName);
        }

        internal string Write(string fileName, byte[] bytes)
        {
            string path = PathFor(fileName);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
