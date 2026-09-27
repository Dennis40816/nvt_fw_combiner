using NvtFwCombiner.Infrastructure.VersionManagement;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class CliTestHarness
{
    internal static async Task<CliRunResult> RunAbAsync(CompositionHostServices host, string[] args, CancellationToken cancellationToken)
    {
        var services = new CliCompositionServices(host.CompositionCapabilityExperience, host.SavedRuleAuthoring,
            host.StandardMergeAuthoring, host.AbMergeAuthoring, host.CtrlRamAuthoring,
            host.GeneralAuthoring, host.CompositionOutputNaming, host.CompositionExecution);
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = await AbMergeCliCommandHandler.RunAsync(services, host.LocalFiles, args, output, error, cancellationToken);
        return new(exitCode, output.ToString(), error.ToString());
    }

    /// <summary>
    /// Runs the CLI through its internal entry, which composes the production host graph (default policy and
    /// external-tool discovery); its two local-state path inputs share this call's own isolated directory.
    /// </summary>
    internal static async Task<CliRunResult> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        string localStateDirectory = IsolatedLocalState.CreateDirectory("cli");
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = await CliApplication.RunAsync(
            args,
            output,
            error,
            () => localStateDirectory,
            cancellationToken,
            Path.Combine(localStateDirectory, JsonVersionManagerStateStore.StateFileName));
        return new CliRunResult(exitCode, output.ToString(), error.ToString());
    }

}

internal sealed record CliRunResult(int ExitCode, string Output, string Error);
