using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI discovery projects only current public workflow choices without input files.</summary>
public sealed class CliWorkflowListingTests
{
    /// <summary>The committed catalog advertises the three approved CLI workflows in ordinal order.</summary>
    [Fact]
    public async Task ListsOnlyApprovedWorkflowsInStableOrderAsync()
    {
        CliRunResult result = await RunAsync(["list"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal(["ab-merge", "ctrlram-replace", "standard-merge"], Lines(result.Output));
    }

    /// <summary>Standard discovery pins the committed IC inventory and does not invent count selectors.</summary>
    [Fact]
    public async Task ListsCommittedStandardIcChoicesAsync()
    {
        CliRunResult result = await RunAsync(["list", "--workflow", "standard-merge"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal(
            ["ic=NT51917", "ic=NT51919", "ic=NT51923", "ic=NT51926", "ic=NT51927",
                "ic=NT51928", "ic=NT51929", "ic=NT51932", "ic=NT51950", "ic=NT51951"],
            Lines(result.Output));
    }

    /// <summary>AB discovery exposes the declared topology tokens and accepts the CLI's numeric IC alias.</summary>
    [Fact]
    public async Task ListsAbTopologyChoicesAsync()
    {
        CliRunResult result = await RunAsync(["list", "--workflow", "ab-merge", "--profile", "51950"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal(["ic=NT51950 ic-num=cascade", "ic=NT51950 ic-num=single"], Lines(result.Output));
    }

    /// <summary>CtrlRAM discovery renders exactly the Application's declared slot ids for every count.</summary>
    [Fact]
    public async Task ListsCtrlRamCountsAndApplicationSlotsWithoutABaseAsync()
    {
        CompositionHostServices host = BootstrapTestHost.ProductServices;
        CliRunResult result = await RunAsync(["list", "--workflow", "ctrlram-replace"]);
        string[] lines = Lines(result.Output);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.NotEmpty(lines);
        var expected = new List<string>();
        CapabilitySelectorPublication selector = host.CompositionCapabilityExperience.GetSelectorPublication();
        foreach (string ic in selector.IcIds)
        {
            foreach (CapabilityNumberChoice? choice in selector.GetNumberSelectionChoices(ic, "ctrlram-replace")
                         .OrderBy(static choice => choice.Token, StringComparer.Ordinal))
            {
                string context = $"ic={ic} ic-num={choice.Token}";
                expected.Add(context);
                expected.AddRange(host.CtrlRamAuthoring.GetDiscoveryDisplay(ic, choice.Token).InputSlots
                    .Select(static slot => slot.SlotId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                    .Select(slot => $"{context} slot={slot}"));
            }
        }

        Assert.Equal(expected, lines);
        Assert.Contains("ic=NT51927 ic-num=2", lines);
        Assert.Contains("ic=NT51927 ic-num=3", lines);
        Assert.Contains("ic=NT51929 ic-num=single slot=replace-ctrlram-vn", lines);
    }

    /// <summary>A known count filter returns only that IC/count's declared slots.</summary>
    [Fact]
    public async Task FiltersCtrlRamSlotsByDeclaredIcAndCountAsync()
    {
        CliRunResult result = await RunAsync(
            ["list", "--workflow", "ctrlram-replace", "--profile", "51927", "--ic-num", "2"]);
        string[] lines = Lines(result.Output);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal("ic=NT51927 ic-num=2", lines[0]);
        Assert.True(lines.Length > 1);
        Assert.All(lines[1..], static line => Assert.StartsWith("ic=NT51927 ic-num=2 slot=", line, StringComparison.Ordinal));
    }

    /// <summary>Invalid filters and shared-parser failures never emit a partial listing or files.</summary>
    [Theory]
    [InlineData("--workflow", "general-merge")]
    [InlineData("--workflow", "general-replace")]
    [InlineData("--workflow", "unknown")]
    [InlineData("--profile", "NT51929")]
    [InlineData("--workflow", "ctrlram-replace", "--profile", "unknown")]
    [InlineData("--workflow", "ctrlram-replace", "--ic-num", "unknown")]
    [InlineData("--workflow", "standard-merge", "--ic-num", "single")]
    [InlineData("--workflow", "ab-merge", "--workflow", "ab-merge")]
    [InlineData("--workflow", "ab-merge", "--profile", "NT51950", "--profile", "NT51950")]
    [InlineData("--workflow", "ctrlram-replace", "--ic-num", "single", "--ic-num", "single")]
    [InlineData("--unknown", "value")]
    [InlineData("--workflow", "--profile", "NT51929")]
    public async Task RefusesInvalidFiltersWithUsageExitAsync(params string[] options)
    {
        CliRunResult result = await RunAsync(["list", .. options]);

        Assert.Equal(64, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.StartsWith("error:", result.Error, StringComparison.Ordinal);
    }

    /// <summary>The public command dispatches discovery without firmware arguments or execution.</summary>
    [Fact]
    public async Task PublicCommandDispatchesWorkflowListingAsync()
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            ["workflows", "list"], TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal(["ab-merge", "ctrlram-replace", "standard-merge"], Lines(result.Output));
    }

    private static async Task<CliRunResult> RunAsync(string[] args)
    {
        CompositionHostServices host = BootstrapTestHost.ProductServices;
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exit = await CliApplication.RunWorkflowsAsync(
            host.CompositionCapabilityExperience, host.CtrlRamAuthoring, args, output, error);
        return new(exit, output.ToString(), error.ToString());
    }

    private static string[] Lines(string output)
    {
        return output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }
}
