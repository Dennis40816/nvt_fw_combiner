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
        Assert.Contains("Version options are available only for ctrlram-replace build", result.Output, StringComparison.Ordinal);
        Assert.Contains("profile=<ic>, ab-topology=<token> for AB Merge, ic-num=<token> for CtrlRAM Replace",
            result.Output, StringComparison.Ordinal);
        string previewUsage = Assert.Single(result.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries),
            static line => line.Contains("ctrlram-replace preview --profile", StringComparison.Ordinal));
        Assert.Contains("[--bank <a|b|both>]", previewUsage, StringComparison.Ordinal);
        Assert.DoesNotContain("firmware-version options", previewUsage, StringComparison.Ordinal);
    }
}
