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

    internal static async Task<CliRunResult> RunRetainedReplaceAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0)
        {
            throw new ArgumentException(
                "A retained Replace regression command is required.",
                nameof(args));
        }

        CompositionHostServices host =
            BootstrapTestHost.RetainedDpReplaceServices;
        var services = new CliCompositionServices(
            host.CompositionCapabilityExperience,
            host.SavedRuleAuthoring,
            host.StandardMergeAuthoring,
            host.AbMergeAuthoring,
            host.DpReplaceAuthoring,
            host.CtrlRamAuthoring,
            host.GeneralAuthoring,
            host.CompositionOutputNaming,
            host.CompositionExecution);
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode;
        try
        {
            exitCode = await ReplaceCliCommandHandler.RunAsync(
                services,
                host.LocalFiles,
                args[0],
                args[1..],
                output,
                error,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("error: operation canceled");
            exitCode = 70;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await error.WriteLineAsync($"error: {exception.Message}");
            exitCode = 70;
        }
        return new CliRunResult(
            exitCode,
            output.ToString(),
            error.ToString());
    }
}

internal sealed record CliRunResult(int ExitCode, string Output, string Error);
