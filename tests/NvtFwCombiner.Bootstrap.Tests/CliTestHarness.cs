namespace NvtFwCombiner.Bootstrap.Tests;

internal static class CliTestHarness
{
    internal static async Task<CliRunResult> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = await CliApplication.RunAsync(args, output, error, cancellationToken);
        return new CliRunResult(exitCode, output.ToString(), error.ToString());
    }
}

internal sealed record CliRunResult(int ExitCode, string Output, string Error);
