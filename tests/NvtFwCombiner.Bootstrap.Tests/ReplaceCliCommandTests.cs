namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI tests for Replace command groups.</summary>
public sealed class ReplaceCliCommandTests
{
    /// <summary>Verifies DP Replace build accepts separate DP and LD replacement inputs.</summary>
    [Fact]
    public async Task DpReplaceBuildWritesSeparateDpAndLdPayloads()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [0, 0, 0, 0, 0, 0, 0, 0]);
        string dp = workspace.Write("dp.bin", [0x11, 0x22]);
        string ld = workspace.Write("ld.bin", [0x33]);
        string output = workspace.PathFor("out.bin");

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            "synthetic-dp-replace",
            "--ic-num",
            "51920",
            "--base",
            reference,
            "--dp",
            dp,
            "--ld",
            ld,
            "--output",
            output,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("Committed:", result.Output, StringComparison.Ordinal);
        Assert.Contains("replace-dp", result.Output, StringComparison.Ordinal);
        Assert.Contains("replace-ld", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Issues:", result.Error, StringComparison.Ordinal);
        byte[] bytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal([0x11, 0x22, 0xFF, 0xFF, 0, 0, 0x33, 0xFF], bytes);
    }

    /// <summary>Verifies CtrlRAM Replace preview reports truncation warnings while succeeding.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewReportsOversizedInputTruncation()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [0, 0, 0, 0, 0, 0, 0, 0]);
        string ctrlram = workspace.Write("ctrlram.bin", [0xAA, 0xBB, 0xCC, 0xDD]);

        CliRunResult result = await RunCliAsync([
            "ctrlram-replace",
            "preview",
            "--profile",
            "synthetic-ctrlram-replace",
            "--ic-family",
            "NT51",
            "--ic-num",
            "932",
            "--base",
            reference,
            "--ctrlram",
            ctrlram,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("replace-ctrlram", result.Output, StringComparison.Ordinal);
        Assert.Contains("input.address-space.truncated", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Verifies General Replace build writes the mapped output after internal preview approval.</summary>
    [Fact]
    public async Task GeneralReplaceBuildWritesMappedOutput()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [0, 1, 2, 3, 4, 5, 6, 7]);
        string input = workspace.Write("input.bin", [0xAA, 0xBB]);
        string output = workspace.PathFor("out.bin");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "synthetic-general-replace",
            "--ic-num",
            "51920",
            "--base",
            reference,
            "--input",
            input,
            "--source-start",
            "0",
            "--target-start",
            "0x2",
            "--length",
            "2",
            "--output",
            output,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Committed:", result.Output, StringComparison.Ordinal);
        byte[] bytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal([0, 1, 0xAA, 0xBB, 4, 5, 6, 7], bytes);
    }

    /// <summary>Verifies Replace commands require IC number selection before planning.</summary>
    [Fact]
    public async Task ReplacePreviewRejectsMissingIcNumber()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [0, 0, 0, 0, 0, 0, 0, 0]);
        string dp = workspace.Write("dp.bin", [0x11, 0x22]);
        string ld = workspace.Write("ld.bin", [0x33]);

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "preview",
            "--profile",
            "synthetic-dp-replace",
            "--base",
            reference,
            "--dp",
            dp,
            "--ld",
            ld,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("--ic-num is required", result.Error, StringComparison.Ordinal);
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
