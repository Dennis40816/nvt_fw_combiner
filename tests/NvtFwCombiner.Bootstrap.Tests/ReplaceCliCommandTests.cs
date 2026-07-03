using System.Text.Json;

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
        string report = workspace.PathFor("report.json");

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
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("Committed:", result.Output, StringComparison.Ordinal);
        Assert.Contains("Report:", result.Output, StringComparison.Ordinal);
        Assert.Contains("replace-dp", result.Output, StringComparison.Ordinal);
        Assert.Contains("replace-ld", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Issues:", result.Error, StringComparison.Ordinal);
        byte[] bytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal([0x11, 0x22, 0xFF, 0xFF, 0, 0, 0x33, 0xFF], bytes);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement root = document.RootElement;
        Assert.Equal("synthetic-dp-replace", root.GetProperty("ProfileId").GetString());
        JsonElement operation = root.GetProperty("Operations")[0];
        Assert.Equal("replace-dp", operation.GetProperty("OperationId").GetString());
        Assert.Equal("dp-replacement", operation.GetProperty("SourceSpaceId").GetString());
        Assert.Equal("output-image", operation.GetProperty("TargetSpaceId").GetString());
        Assert.Equal(4, operation.GetProperty("TargetRange").GetProperty("Length").GetInt64());
        Assert.Equal("Replace synthetic DP declared partition.", operation.GetProperty("Reason").GetString());
    }

    /// <summary>Verifies NT51950 DP Replace is selected from the built-in Replace profile catalog.</summary>
    [Fact]
    public async Task DpReplaceBuildUsesNt51950CatalogProfile()
    {
        using var workspace = TempWorkspace.Create();
        byte[] referenceBytes = [.. Enumerable.Repeat((byte)0xA5, 0x100000)];
        Array.Fill(referenceBytes, (byte)0x22, 0x0A000, 0x2D000);
        Array.Fill(referenceBytes, (byte)0x33, 0x37000, 0x1000);
        byte[] dpBytes = [.. Enumerable.Repeat((byte)0x11, 0x40000)];
        string reference = workspace.Write("reference.bin", referenceBytes);
        string dp = workspace.Write("dp.bin", dpBytes);
        string output = workspace.PathFor("nt51950-dp-replace.bin");

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--dp",
            dp,
            "--output",
            output,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("nt51950-dp-replace-dp-perspective", result.Output, StringComparison.Ordinal);
        byte[] bytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal(0x100000, bytes.Length);
        Assert.Equal(0x11, bytes[0x00000]);
        Assert.Equal(0x11, bytes[0x09FFF]);
        Assert.Equal(0x22, bytes[0x0A000]);
        Assert.Equal(0x22, bytes[0x36FFF]);
        Assert.Equal(0x33, bytes[0x37000]);
        Assert.Equal(0x33, bytes[0x37FFF]);
        Assert.Equal(0x11, bytes[0x38000]);
        Assert.Equal(0x00, bytes[0x40000]);
    }

    /// <summary>Verifies NT51950 DP Replace rejects unapproved replacement sizes before output commit.</summary>
    [Fact]
    public async Task DpReplaceBuildRejectsInvalidNt51950ReplacementSize()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [.. Enumerable.Repeat((byte)0xA5, 0x100000)]);
        string dp = workspace.Write("dp.bin", [.. Enumerable.Repeat((byte)0x11, 0x3FFFF)]);
        string output = workspace.PathFor("nt51950-dp-replace.bin");

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "build",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--dp",
            dp,
            "--output",
            output,
        ]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("must match one of the declared lengths", result.Error, StringComparison.Ordinal);
        Assert.Contains("0x40000, 0x80000, 0x100000", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
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

    /// <summary>Rejects Replace build outputs that would overwrite an input BIN.</summary>
    [Fact]
    public async Task DpReplaceBuildRejectsOutputPathThatAliasesInput()
    {
        using var workspace = TempWorkspace.Create();
        byte[] referenceBytes = [0, 0, 0, 0, 0, 0, 0, 0];
        string reference = workspace.Write("reference.bin", referenceBytes);
        string dp = workspace.Write("dp.bin", [0x11, 0x22]);
        string ld = workspace.Write("ld.bin", [0x33]);

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
            reference,
            "--overwrite",
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.Contains("Output path must not overwrite input artifact", result.Error, StringComparison.Ordinal);
        Assert.Equal(referenceBytes, await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken));
    }

    /// <summary>Rejects Replace report paths that would overwrite the build output.</summary>
    [Fact]
    public async Task DpReplaceBuildRejectsReportPathThatAliasesOutput()
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
            "--report",
            output,
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.Contains("Report path must not overwrite built firmware output", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
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

    /// <summary>Verifies Replace commands reject firmware inputs that the selected profile would ignore.</summary>
    [Fact]
    public async Task ReplacePreviewRejectsUnusedFirmwareInputOption()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", [0, 0, 0, 0, 0, 0, 0, 0]);
        string ctrlram = workspace.Write("ctrlram.bin", [0xAA, 0xBB]);
        string dp = workspace.Write("dp.bin", [0x11, 0x22]);

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
            "--dp",
            dp,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("option '--dp' is not used", result.Error, StringComparison.Ordinal);
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
