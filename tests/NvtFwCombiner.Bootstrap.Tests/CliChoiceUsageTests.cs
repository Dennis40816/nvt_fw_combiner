namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Help exposes the approved discovery and authoring choices with their explicit confirmation semantics.</summary>
public sealed class CliChoiceUsageTests
{
    /// <summary>Global usage advertises each new option and the exact Dummy acknowledgement.</summary>
    [Fact]
    public async Task HelpDescribesApprovedCliChoicesAsync()
    {
        CliRunResult result = await CliTestHarness.RunAsync(["--help"], TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        foreach (string option in new[] { "workflows list", "--workflow", "--dp-mode", "--acknowledge-non-tp-ff",
                     "--bank", "--firmware-version", "--firmware-sub-version", "--a-firmware-version",
                     "--a-firmware-sub-version", "--b-firmware-version", "--b-firmware-sub-version" })
        {
            Assert.Contains(option, result.Output, StringComparison.Ordinal);
        }

        Assert.Contains("0xFF replaces every non-TP output", result.Output, StringComparison.Ordinal);
        Assert.Contains("two hexadecimal digits (00..FF)", result.Output, StringComparison.Ordinal);
        Assert.Contains("omitted pairs preserve versions", result.Output, StringComparison.Ordinal);
    }
}
