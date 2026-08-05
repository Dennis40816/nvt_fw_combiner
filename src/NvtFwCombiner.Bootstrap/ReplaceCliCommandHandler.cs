using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private const int Success = 0;
    private const int CompositionFailed = 1;
    private const int UsageError = 64;

    internal static async Task<int> RunAsync(
        string command,
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] is "--help")
        {
            await WriteUsageAsync(command, output).ConfigureAwait(false);
            return args.Length == 0 ? UsageError : Success;
        }

        string action = args[0];
        if (action is not ("preview" or "build"))
        {
            await error.WriteLineAsync($"error: unknown {command} command '{action}'").ConfigureAwait(false);
            return UsageError;
        }

        List<string> valueOptions =
        [
            "--profile",
            "--ic-num",
            "--base",
            "--output",
            "--report",
        ];
        List<string> repeatableValueOptions = [];
        switch (command)
        {
            case IcWorkflowIds.DpReplace:
                valueOptions.Add("--dp");
                valueOptions.Add("--ldc");
                break;
            case IcWorkflowIds.CtrlRamReplace:
                valueOptions.Add("--ctrlram");
                repeatableValueOptions.Add("--ctrlram");
                break;
            case IcWorkflowIds.GeneralReplace:
                valueOptions.AddRange(
                    ["--mapping", "--patch", "--fill", "--rule", "--slot"]);
                repeatableValueOptions.AddRange(
                    ["--mapping", "--patch", "--fill", "--slot"]);
                break;
            default:
                break;
        }

        if (!CliOptionParser.TryParse(
                args[1..],
                valueOptions,
                repeatableValueOptions,
                [],
                error,
                out ParsedCliOptions options))
        {
            return UsageError;
        }

        if (!options.Values.TryGetValue("--profile", out string? profileSelector))
        {
            await error.WriteLineAsync("error: --profile is required").ConfigureAwait(false);
            return UsageError;
        }

        if (!TryResolveReplaceIc(command, profileSelector, out string? icId))
        {
            return await UnknownReplaceProfileAsync(command, profileSelector, error).ConfigureAwait(false);
        }

        string replaceMode = command switch
        {
            IcWorkflowIds.DpReplace => WorkbenchReplaceModes.Dp,
            IcWorkflowIds.CtrlRamReplace => WorkbenchReplaceModes.CtrlRam,
            _ => WorkbenchReplaceModes.General,
        };
        if (!CanonicalCapabilityProjection.IsReplaceWorkflowAvailable(icId, replaceMode))
        {
            await error.WriteLineAsync(
                $"error: {WorkbenchIssueCodes.ReplaceWorkflowNotSupported}: {icId} {replaceMode} Replace is Not available.")
                .ConfigureAwait(false);
            return CompositionFailed;
        }

        return command == IcWorkflowIds.DpReplace
            ? await RunWorkbenchDpReplaceAsync(
                    action,
                    icId,
                    options,
                    output,
                    error,
                    cancellationToken)
                .ConfigureAwait(false)
            : command == IcWorkflowIds.CtrlRamReplace
            ? await RunWorkbenchCtrlRamReplaceAsync(
                    action,
                    icId,
                    options,
                    output,
                    error,
                    cancellationToken)
                .ConfigureAwait(false)
            : await RunWorkbenchGeneralReplaceAsync(
                    action,
                    icId,
                    options,
                    output,
                    error,
                    cancellationToken)
                .ConfigureAwait(false);
    }

}
