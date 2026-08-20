using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    /// <summary>Atomically replaces an unrelated existing Replace output.</summary>
    [Fact]
    public async Task DpReplaceBuildReplacesExistingOutputWithoutOverwrite()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", new byte[0x40000]);
        byte[] dpBytes = new byte[0x40000];
        dpBytes[0] = 0x3C;
        string dp = workspace.Write("dp.bin", dpBytes);
        byte[] existingOutput = [0xA5, 0x5A];
        string output = workspace.Write("out.bin", existingOutput);

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
        Assert.Contains(
            "output-naming.metadata-unknown",
            result.Error,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "output-naming.metadata-required",
            result.Error,
            StringComparison.Ordinal);
        byte[] outputBytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal(0x40000, outputBytes.Length);
        Assert.Equal(0x3C, outputBytes[0]);
    }

    /// <summary>Rejects Replace build outputs that would overwrite an input BIN.</summary>
    [Fact]
    public async Task DpReplaceBuildRejectsOutputPathThatAliasesInput()
    {
        using var workspace = TempWorkspace.Create();
        byte[] referenceBytes = new byte[0x40000];
        string reference = workspace.Write("reference.bin", referenceBytes);
        string dp = workspace.Write("dp.bin", new byte[0x40000]);

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
            reference,
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
        string reference = workspace.Write("reference.bin", new byte[0x40000]);
        string dp = workspace.Write("dp.bin", new byte[0x40000]);
        string output = workspace.PathFor("out.bin");

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
        string reference = workspace.Write("reference.bin", new byte[0x40000]);
        string dp = workspace.Write("dp.bin", new byte[0x40000]);

        CliRunResult result = await RunCliAsync([
            "dp-replace",
            "preview",
            "--profile",
            "NT51950",
            "--base",
            reference,
            "--dp",
            dp,
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("--ic-num is required", result.Error, StringComparison.Ordinal);
    }

    /// <summary>An unavailable General Replace route rejects before request-field validation.</summary>
    [Fact]
    public async Task GeneralReplaceRejectsMissingIcNumberBeforeMappingOptions()
    {
        CliRunResult result = await RunCliAsync([
            "general-replace",
            "preview",
            "--profile",
            "NT51950",
        ]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("replace.workflow.not-supported", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("--ic-num is required", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("--base is required", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Verifies each Replace workflow rejects firmware options owned by another workflow.</summary>
    [Fact]
    public async Task ReplacePreviewRejectsForeignFirmwareInputOption()
    {
        CliRunResult result = await RunCliAsync([
            "ctrlram-replace",
            "preview",
            "--dp",
            "ignored.bin",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("unknown option '--dp'", result.Error, StringComparison.Ordinal);
    }

}
