namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Coverage for the thin process entry point retained outside the Bootstrap assembly.</summary>
public sealed class CliProgramTests
{
    /// <summary>The executable entry point delegates invalid command handling to the shared CLI application.</summary>
    [Fact]
    public async Task MainRejectsAnUnknownCommandAsync()
    {
        int exitCode = await Program.Main(["unknown-command"]);

        Assert.Equal(64, exitCode);
    }
}
