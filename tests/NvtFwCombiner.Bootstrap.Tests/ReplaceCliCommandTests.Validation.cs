using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    /// <summary>Hidden DP Replace is absent from help and rejects before file option validation.</summary>
    [Fact]
    public async Task DpReplaceIsHiddenAndFailsClosedBeforeReadingInputs()
    {
        CliRunResult help = await CliTestHarness.RunAsync(
            ["--help"],
            TestContext.Current.CancellationToken);
        CliRunResult direct = await CliTestHarness.RunAsync([
            "dp-replace",
            "preview",
            "--profile",
            "NT51950",
        ], TestContext.Current.CancellationToken);

        Assert.DoesNotContain("dp-replace", help.Output, StringComparison.Ordinal);
        Assert.Equal(64, direct.ExitCode);
        Assert.Contains("cli.retired-experience", direct.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("--ic-num is required", direct.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("--base is required", direct.Error, StringComparison.Ordinal);
    }

    /// <summary>Atomically replaces an unrelated existing Replace output.</summary>
    [Fact]
    public async Task GeneralReplaceBuildReplacesExistingOutputWithoutOverwrite()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", File.ReadAllBytes(BootstrapTestData.GoldenArtifactPath("51926", "expected-output")));
        byte[] dpBytes = [0x3C, 0x4D];
        dpBytes[0] = 0x3C;
        string dp = workspace.Write("dp.bin", dpBytes);
        byte[] existingOutput = [0xA5, 0x5A];
        string output = workspace.Write("out.bin", existingOutput);

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51926",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--mapping",
            $"0x3E020+0x2={dp}",
            "--output",
            output,
        ]);

        Assert.Equal(0, result.ExitCode);
        byte[] outputBytes = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal(0x40000, outputBytes.Length);
        GoldenAssert.AssertEqualsGolden((ReadOnlySpan<byte>)[0x3C], [outputBytes[0x3E020]], "firmware image");
        byte[] expected = await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken);
        dpBytes.CopyTo(expected, 0x3E020);
        GoldenAssert.AssertEqualsGolden(expected, outputBytes, "firmware image");
    }

    /// <summary>Rejects Replace build outputs that would overwrite an input BIN.</summary>
    [Fact]
    public async Task GeneralReplaceBuildRejectsOutputPathThatAliasesInput()
    {
        using var workspace = TempWorkspace.Create();
        byte[] referenceBytes = File.ReadAllBytes(BootstrapTestData.GoldenArtifactPath("51926", "expected-output"));
        string reference = workspace.Write("reference.bin", referenceBytes);
        string dp = workspace.Write("dp.bin", new byte[2]);

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51926",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--mapping",
            $"0x3E020+0x2={dp}",
            "--output",
            reference,
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.True(GoldenDiagnostics.Contains("Output path must not overwrite input artifact", result.Error, StringComparison.Ordinal), "Captured text did not match the expected marker check.");
        GoldenAssert.AssertEqualsGolden(referenceBytes, await File.ReadAllBytesAsync(reference, TestContext.Current.CancellationToken), "firmware image");
    }

    /// <summary>Rejects Replace report paths that would overwrite the build output.</summary>
    [Fact]
    public async Task GeneralReplaceBuildRejectsReportPathThatAliasesOutput()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", File.ReadAllBytes(BootstrapTestData.GoldenArtifactPath("51926", "expected-output")));
        string dp = workspace.Write("dp.bin", new byte[2]);
        string output = workspace.PathFor("out.bin");

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "build",
            "--profile",
            "NT51926",
            "--ic-num",
            "single",
            "--base",
            reference,
            "--mapping",
            $"0x3E020+0x2={dp}",
            "--output",
            output,
            "--report",
            output,
        ]);

        Assert.Equal(70, result.ExitCode);
        Assert.True(GoldenDiagnostics.Contains("Report path must not overwrite built firmware output", result.Error, StringComparison.Ordinal), "Captured text did not match the expected marker check.");
        Assert.False(File.Exists(output));
    }

    /// <summary>Verifies Replace commands require IC number selection before planning.</summary>
    [Fact]
    public async Task ReplacePreviewRejectsMissingIcNumber()
    {
        using var workspace = TempWorkspace.Create();
        string reference = workspace.Write("reference.bin", File.ReadAllBytes(BootstrapTestData.GoldenArtifactPath("51926", "expected-output")));
        string dp = workspace.Write("dp.bin", new byte[2]);

        CliRunResult result = await RunCliAsync([
            "general-replace",
            "preview",
            "--profile",
            "NT51926",
            "--base",
            reference,
            "--mapping",
            $"0x3E020+0x2={dp}",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.True(GoldenDiagnostics.Contains("--ic-num is required", result.Error, StringComparison.Ordinal), "Captured text did not match the expected marker check.");
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
        Assert.True(GoldenDiagnostics.Contains("replace.workflow.not-supported", result.Error, StringComparison.Ordinal), "Captured text did not match the expected marker check.");
        Assert.True(!GoldenDiagnostics.Contains("--ic-num is required", result.Error, StringComparison.Ordinal), "Captured text did not match the expected marker check.");
        Assert.True(!GoldenDiagnostics.Contains("--base is required", result.Error, StringComparison.Ordinal), "Captured text did not match the expected marker check.");
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
        Assert.True(GoldenDiagnostics.Contains("unknown option '--dp'", result.Error, StringComparison.Ordinal), "Captured text did not match the expected marker check.");
    }

}
