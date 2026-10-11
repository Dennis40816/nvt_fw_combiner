using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Coverage for the thin process entry point retained outside the Bootstrap assembly.</summary>
public sealed class CliProgramTests
{
    /// <summary>The executable entry point delegates invalid usage to the shared CLI application.</summary>
    [Fact]
    public async Task MainRejectsAMissingCommandAsync()
    {
        int exitCode = await Program.Main([]);

        Assert.Equal(64, exitCode);
    }

    /// <summary>An unknown command is refused by the production host composed over an isolated directory.</summary>
    [Fact]
    public async Task UnknownCommandIsRejectedWithTheUsageExitCode()
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            ["unknown-command"],
            TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.True(StringComparer.Ordinal.Equals(string.Empty, result.Output), "Captured text differs from the expected text.");
        Assert.True(GoldenDiagnostics.Contains("error: unknown command 'unknown-command'", result.Error, StringComparison.Ordinal), "Captured text did not match the expected marker check.");
    }
}
