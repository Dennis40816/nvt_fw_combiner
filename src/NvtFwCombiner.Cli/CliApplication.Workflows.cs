using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

public static partial class CliApplication
{
    internal static async Task<int> RunWorkflowsAsync(
        ICompositionCapabilityExperience capabilities,
        ICtrlRamAuthoring ctrlRamAuthoring,
        string[] args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Length == 0 || args[0] != "list")
        {
            await error.WriteLineAsync("error: expected workflows list").ConfigureAwait(false);
            return UsageError;
        }

        if (!CliOptionParser.TryParse(args[1..],
                ["--workflow", "--profile", "--ic-num"], [], [], error,
                out ParsedCliOptions options))
        {
            return UsageError;
        }

        CapabilitySelectorPublication selector = capabilities.GetSelectorPublication();
        string[] workflows =
        [
            .. selector.IcIds.SelectMany(selector.GetAuthorableWorkflowIds)
                .Where(static workflow => workflow is ExperienceIds.StandardMerge or
                    ExperienceIds.AbMerge or ExperienceIds.CtrlRamReplace)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
        ];
        if (!options.Values.TryGetValue("--workflow", out string? workflowFilter))
        {
            if (options.Values.Count != 0)
            {
                await error.WriteLineAsync("error: --profile and --ic-num require --workflow").ConfigureAwait(false);
                return UsageError;
            }

            foreach (string workflow in workflows)
            {
                await output.WriteLineAsync(workflow).ConfigureAwait(false);
            }

            return Success;
        }

        string? workflowId = workflows.FirstOrDefault(workflow =>
            StringComparer.OrdinalIgnoreCase.Equals(workflow, workflowFilter));
        if (workflowId is null)
        {
            await error.WriteLineAsync($"error: unknown workflow '{workflowFilter}'").ConfigureAwait(false);
            return UsageError;
        }

        string[] icIds = [.. selector.IcIds.Where(ic => selector.IsWorkflowAuthorable(ic, workflowId))];
        if (options.Values.TryGetValue("--profile", out string? profileFilter))
        {
            icIds = [.. icIds.Where(ic =>
                StringComparer.OrdinalIgnoreCase.Equals(ic, profileFilter.Trim()) ||
                StringComparer.OrdinalIgnoreCase.Equals(CliCompositionRunSupport.GetIcNumber(ic), profileFilter.Trim()))];
            if (icIds.Length == 0)
            {
                await error.WriteLineAsync($"error: unknown {workflowId} IC '{profileFilter}'").ConfigureAwait(false);
                return UsageError;
            }
        }

        string? numberFilter = options.Values.GetValueOrDefault("--ic-num");
        var lines = new List<string>();
        foreach (string icId in icIds)
        {
            string[] numbers = workflowId == ExperienceIds.AbMerge
                ? [.. selector.GetAbMergeTopologyChoices(icId).Select(static choice => choice.Token)]
                : [.. selector.GetNumberSelectionChoices(icId, workflowId).Select(static choice => choice.Token)];
            if (numberFilter is not null)
            {
                numbers = [.. numbers.Where(number => StringComparer.OrdinalIgnoreCase.Equals(number, numberFilter.Trim()))];
            }

            if (numbers.Length == 0 && numberFilter is null)
            {
                lines.Add($"ic={icId}");
            }

            foreach (string number in numbers.Order(StringComparer.Ordinal))
            {
                string context = $"ic={icId} ic-num={number}";
                lines.Add(context);
                if (workflowId == ExperienceIds.CtrlRamReplace)
                {
                    foreach (string slotId in ctrlRamAuthoring.GetDiscoveryDisplay(icId, number).InputSlots
                                 .Select(static slot => slot.SlotId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                    {
                        lines.Add($"{context} slot={slotId}");
                    }
                }
            }
        }

        if (numberFilter is not null && lines.Count == 0)
        {
            await error.WriteLineAsync($"error: unknown {workflowId} IC count '{numberFilter}'").ConfigureAwait(false);
            return UsageError;
        }

        foreach (string line in lines)
        {
            await output.WriteLineAsync(line).ConfigureAwait(false);
        }

        return Success;
    }
}
