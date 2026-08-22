using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

internal static partial class ReplaceCliCommandHandler
{
    private const int Success = 0;
    private const int CompositionFailed = 1;
    private const int UsageError = 64;

    internal static async Task<int> RunAsync(
        CompositionHostServices host,
        string command,
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
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
            CliBundleOptions.ParentOption,
            CliBundleOptions.NameOption,
        ];
        List<string> repeatableValueOptions = [];
        switch (command)
        {
            case ExperienceIds.DpReplace:
                valueOptions.Add("--dp");
                valueOptions.Add("--ldc");
                break;
            case ExperienceIds.CtrlRamReplace:
                valueOptions.Add("--ctrlram");
                repeatableValueOptions.Add("--ctrlram");
                break;
            case ExperienceIds.GeneralReplace:
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

        if (!CliBundleOptions.TryValidateCombination(action, options.Values, error))
        {
            return UsageError;
        }

        if (!options.Values.TryGetValue("--profile", out string? profileSelector))
        {
            await error.WriteLineAsync("error: --profile is required").ConfigureAwait(false);
            return UsageError;
        }

        if (!TryResolveReplaceIc(
                host.CompositionCapabilityExperience,
                command,
                profileSelector,
                out string? icId))
        {
            return await UnknownReplaceProfileAsync(command, profileSelector, error).ConfigureAwait(false);
        }

        string replaceMode = command switch
        {
            ExperienceIds.DpReplace => ExperienceIds.DpReplace,
            ExperienceIds.CtrlRamReplace => ExperienceIds.CtrlRamReplace,
            _ => ExperienceIds.GeneralReplace,
        };
        if (!host.CompositionCapabilityExperience.IsReplaceWorkflowAvailable(
                icId,
                replaceMode))
        {
            await error.WriteLineAsync(
                $"error: {CompositionPlanningIssueCodes.ReplaceWorkflowNotSupported}: {icId} {replaceMode} Replace is Not available.")
                .ConfigureAwait(false);
            return CompositionFailed;
        }

        return command == ExperienceIds.DpReplace
            ? await RunDpReplaceAsync(
                    host,
                    action,
                    icId,
                    options,
                    output,
                    error,
                    cancellationToken)
                .ConfigureAwait(false)
            : command == ExperienceIds.CtrlRamReplace
            ? await RunCtrlRamReplaceAsync(
                    host,
                    action,
                    icId,
                    options,
                    output,
                    error,
                    cancellationToken)
                .ConfigureAwait(false)
            : await RunGeneralReplaceAsync(
                    host,
                    action,
                    icId,
                    options,
                    output,
                    error,
                    cancellationToken)
                .ConfigureAwait(false);
    }

}
