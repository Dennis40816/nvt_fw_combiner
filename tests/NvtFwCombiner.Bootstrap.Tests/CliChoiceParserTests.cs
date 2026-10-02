using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>New choice options retain the shared parser's unknown, missing-value and duplicate behavior.</summary>
public sealed class CliChoiceParserTests
{
    /// <summary>Every new value option is single-use and requires a non-option value.</summary>
    [Theory]
    [InlineData("ab-merge", "--dp-mode", "dummy")]
    [InlineData("ctrlram-replace", "--bank", "both")]
    [InlineData("ctrlram-replace", "--firmware-version", "21")]
    [InlineData("ctrlram-replace", "--firmware-sub-version", "32")]
    [InlineData("ctrlram-replace", "--a-firmware-version", "21")]
    [InlineData("ctrlram-replace", "--a-firmware-sub-version", "32")]
    [InlineData("ctrlram-replace", "--b-firmware-version", "29")]
    [InlineData("ctrlram-replace", "--b-firmware-sub-version", "41")]
    public async Task DuplicateAndMissingValuesUseUsageExitWithoutWritesAsync(string workflow, string option, string value)
    {
        using var workspace = TempWorkspace.Create("cli-choice-parser");
        string bin = workspace.PathFor("output.bin");
        string report = workspace.PathFor("report.json");
        foreach (string[] arguments in new[] { new[] { option, value, option, value }, [option] })
        {
            CliRunResult result = await CliTestHarness.RunAsync(
                [workflow, "build", "--output", bin, "--report", report, .. arguments],
                TestContext.Current.CancellationToken);

            Assert.Equal(64, result.ExitCode);
            Assert.Contains(arguments.Length == 1 ? $"option '{option}' requires a value" : $"duplicate option '{option}'",
                result.Error, StringComparison.Ordinal);
            Assert.Empty(result.Output);
            Assert.False(File.Exists(bin));
            Assert.False(File.Exists(report));
        }
    }

    /// <summary>Options for another workflow remain unknown options, without reading inputs or writing reports.</summary>
    [Theory]
    [InlineData("standard-merge", "--dp-mode", "dummy")]
    [InlineData("standard-merge", "--acknowledge-non-tp-ff", null)]
    [InlineData("standard-merge", "--bank", "a")]
    [InlineData("ab-merge", "--firmware-version", "21")]
    [InlineData("ab-merge", "--firmware-sub-version", "32")]
    [InlineData("ab-merge", "--a-firmware-version", "21")]
    [InlineData("ab-merge", "--a-firmware-sub-version", "32")]
    [InlineData("ab-merge", "--b-firmware-version", "29")]
    [InlineData("ab-merge", "--b-firmware-sub-version", "41")]
    [InlineData("ctrlram-replace", "--dp-mode", "dummy")]
    [InlineData("ctrlram-replace", "--acknowledge-non-tp-ff", null)]
    public async Task WrongWorkflowOptionUsesUsageExitWithoutWritesAsync(string workflow, string option, string? value)
    {
        using var workspace = TempWorkspace.Create("cli-choice-wrong-workflow");
        string bin = workspace.PathFor("output.bin");
        string report = workspace.PathFor("report.json");
        CliRunResult result = await CliTestHarness.RunAsync(
            [workflow, "build", "--output", bin, "--report", report,
                option, .. value is null ? Array.Empty<string>() : [value]],
            TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains($"unknown option '{option}'", result.Error, StringComparison.Ordinal);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(bin));
        Assert.False(File.Exists(report));
    }
}
