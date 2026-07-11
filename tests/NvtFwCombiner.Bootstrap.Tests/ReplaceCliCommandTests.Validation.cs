using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
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
}
