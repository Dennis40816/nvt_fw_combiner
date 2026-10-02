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
            ["profile=NT51917", "profile=NT51919", "profile=NT51923", "profile=NT51926", "profile=NT51927",
                "profile=NT51928", "profile=NT51929", "profile=NT51932", "profile=NT51950", "profile=NT51951"],
            Lines(result.Output));
    }

    /// <summary>AB discovery exposes the declared topology tokens and accepts the CLI's numeric IC alias.</summary>
    [Fact]
    public async Task ListsAbTopologyChoicesAsync()
    {
        CliRunResult result = await RunAsync(["list", "--workflow", "ab-merge", "--profile", "51950"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal(["profile=NT51950 ab-topology=cascade", "profile=NT51950 ab-topology=single"], Lines(result.Output));
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
                string context = $"profile={ic} ic-num={choice.Token}";
                expected.Add(context);
                expected.AddRange(host.CtrlRamAuthoring.GetDiscoveryDisplay(ic, choice.Token).InputSlots
                    .Select(static slot => slot.SlotId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                    .Select(slot => $"{context} slot={slot}"));
            }
        }

        Assert.Equal(expected, lines);
        Assert.Contains("profile=NT51927 ic-num=2", lines);
        Assert.Contains("profile=NT51927 ic-num=3", lines);
        Assert.Contains("profile=NT51929 ic-num=single slot=replace-ctrlram-vn", lines);
    }

    /// <summary>A known count filter returns only that IC/count's declared slots.</summary>
    [Theory]
    [InlineData("ctrlram-replace", "51927", "2")]
    [InlineData(" CtrlRam-Replace ", " 51927 ", " 2 ")]
    public async Task FiltersCtrlRamSlotsByDeclaredIcAndCountAsync(string workflow, string profile, string number)
    {
        CliRunResult result = await RunAsync(
            ["list", "--workflow", workflow, "--profile", profile, "--ic-num", number]);
        string[] lines = Lines(result.Output);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal("profile=NT51927 ic-num=2", lines[0]);
        Assert.True(lines.Length > 1);
        Assert.All(lines[1..], static line => Assert.StartsWith("profile=NT51927 ic-num=2 slot=", line, StringComparison.Ordinal));
    }

    /// <summary>Invalid filters and shared-parser failures never emit a partial listing or files.</summary>
    [Theory]
    [InlineData("--workflow", "general-merge")]
    [InlineData("--workflow", "general-replace")]
    [InlineData("--workflow", "unknown")]
    [InlineData("--profile", "NT51929")]
    [InlineData("--workflow", "ctrlram-replace", "--profile", "unknown")]
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

    /// <summary>A known IC without an AB route is distinguished from an unknown IC.</summary>
    [Theory]
    [InlineData("NT51917", 1, "IC 'NT51917' has no route for workflow 'ab-merge'")]
    [InlineData("unknown", 64, "unknown IC 'unknown'")]
    public async Task ProfileFilterExplainsMissingWorkflowRouteAsync(string profile, int expectedExit, string message)
    {
        CliRunResult result = await RunAsync(["list", "--workflow", "ab-merge", "--profile", profile]);

        Assert.Equal(expectedExit, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains(message, result.Error, StringComparison.Ordinal);
    }

    /// <summary>A well-formed count absent from the declared catalog is a composition refusal, without partial output.</summary>
    [Theory]
    [InlineData("ctrlram-replace", null, "unknown")]
    [InlineData("standard-merge", null, "single")]
    [InlineData("ctrlram-replace", "NT51929", "2")]
    [InlineData("ab-merge", "NT51929", "cascade")]
    public async Task UndeclaredCountUsesCompositionExitAsync(string workflow, string? profile, string number)
    {
        string[] selection = profile is null ? [] : ["--profile", profile];
        CliRunResult result = await RunAsync(["list", "--workflow", workflow, .. selection, "--ic-num", number]);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Equal($"error: unknown {workflow} IC count '{number}'" + Environment.NewLine, result.Error);
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
