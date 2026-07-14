using System.Reflection;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Runs the command-line application through the production composition services.</summary>
public static partial class CliApplication
{
    private const int Success = 0;
    private const int CompositionFailed = 1;
    private const int UsageError = 64;
    private const int SoftwareError = 70;

    /// <summary>Runs one command-line invocation and returns the process exit code.</summary>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args is ["--version"] or ["version"])
        {
            await output.WriteLineAsync(Version).ConfigureAwait(false);
            return Success;
        }

        if (args is ["doctor"])
        {
            await output.WriteLineAsync("NVT FW Combiner repository bootstrap is healthy.").ConfigureAwait(false);
            await output.WriteLineAsync($"CLI assembly version: {Version}").ConfigureAwait(false);
            await output.WriteLineAsync("Composition core command surface is available.").ConfigureAwait(false);
            return Success;
        }

        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
        {
            await WriteUsageAsync(output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        try
        {
            return args[0] switch
            {
                "profiles" => await RunProfilesAsync(args[1..], output, error).ConfigureAwait(false),
                "candidate-intake" => await RunCandidateIntakeAsync(args[1..], output, error).ConfigureAwait(false),
                IcWorkflowIds.StandardMerge => await RunStandardMergeAsync(args[1..], output, error, cancellationToken)
                    .ConfigureAwait(false),
                IcWorkflowIds.GeneralMerge => await MergeCliCommandHandler.RunAsync(
                        args[0],
                        args[1..],
                        output,
                        error,
                        cancellationToken)
                    .ConfigureAwait(false),
                "saved-rule" => await SavedRuleCliCommandHandler.RunAsync(
                        args[1..],
                        output,
                        error,
                        cancellationToken)
                    .ConfigureAwait(false),
                IcWorkflowIds.DpReplace or IcWorkflowIds.CtrlRamReplace or IcWorkflowIds.GeneralReplace =>
                    await ReplaceCliCommandHandler.RunAsync(args[0], args[1..], output, error, cancellationToken)
                        .ConfigureAwait(false),
                _ => await UnknownCommandAsync(args[0], error).ConfigureAwait(false),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("error: operation canceled").ConfigureAwait(false);
            return SoftwareError;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await error.WriteLineAsync($"error: {exception.Message}").ConfigureAwait(false);
            return SoftwareError;
        }
    }

    private static string Version => (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ??
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version?.ToString() ??
        "unknown";
}
