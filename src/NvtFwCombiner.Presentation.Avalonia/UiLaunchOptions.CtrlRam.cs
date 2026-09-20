using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

internal sealed record CtrlRamLaunchInput(string SlotId, string Path);

internal sealed record CtrlRamLaunchRequest(
    string IcId, string Number, string BasePath, IReadOnlyList<CtrlRamLaunchInput> Inputs);

internal sealed partial class UiLaunchOptions
{
    private static bool TakeInputOption(
        IReadOnlyList<string> args,
        ref int index,
        Dictionary<string, string> options,
        List<CtrlRamLaunchInput> inputs,
        List<string> issues)
    {
        foreach (string name in new[] { "--workflow", "--ic", "--ic-num", "--base", "--ctrlram", "--dp", "--tp-a", "--tp-b" })
        {
            if (!TrySplitValue(args[index], name, out string? inlineValue))
            {
                continue;
            }
            string? value = TakeOptionValue(args, ref index, name, inlineValue, issues);
            bool first = options.TryAdd(name, value ?? string.Empty);
            if (name != "--ctrlram")
            {
                if (!first) { issues.Add($"Duplicate option '{name}'."); }
                return true;
            }
            int separator = value?.IndexOf('=', StringComparison.Ordinal) ?? -1;
            if (separator <= 0 || string.IsNullOrWhiteSpace(value![..separator]) ||
                string.IsNullOrWhiteSpace(value[(separator + 1)..]))
            {
                issues.Add("--ctrlram requires <slot-id>=<path>.");
                return true;
            }
            string slotId = value![..separator];
            if (inputs.Any(input => StringComparer.Ordinal.Equals(input.SlotId, slotId)))
            {
                issues.Add($"Duplicate CtrlRAM slot '{slotId}'.");
            }
            inputs.Add(new(slotId, value[(separator + 1)..]));
            return true;
        }
        return false;
    }

    private static CtrlRamLaunchRequest? ParseCtrlRamRequest(
        Dictionary<string, string> options,
        List<CtrlRamLaunchInput> inputs,
        ShellPage? page,
        bool openSettings,
        string? reportPath,
        bool openReport,
        List<string> unknownArguments,
        List<string> issues)
    {
        if (options.Count == 0) { return null; }
        if (options.Keys.Any(name => name is "--dp" or "--tp-a" or "--tp-b"))
        {
            issues.Add("AB input options require --workflow ab-merge and cannot be combined with CtrlRAM inputs.");
        }
        foreach (string name in new[] { "--workflow", "--ic", "--ic-num", "--base" })
        {
            if (!options.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                issues.Add($"CtrlRAM startup requires {name}.");
            }
        }
        if (options.GetValueOrDefault("--workflow") != ExperienceIds.CtrlRamReplace)
        {
            issues.Add("Input startup supports only --workflow ctrlram-replace.");
        }
        if (inputs.Count == 0) { issues.Add("CtrlRAM startup requires at least one --ctrlram <slot-id>=<path>."); }
        if (page is not (null or ShellPage.Replace) || openSettings || reportPath is not null || openReport)
        {
            issues.Add("CtrlRAM startup cannot be combined with another page, Settings or report loading.");
        }
        foreach (string argument in unknownArguments)
        {
            issues.Add($"Unsupported input startup argument '{argument}'.");
        }
        if (issues.Count > 0) { return null; }
        try
        {
            return new(options["--ic"], options["--ic-num"], Path.GetFullPath(options["--base"]),
                [.. inputs.Select(input => input with { Path = Path.GetFullPath(input.Path) })]);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issues.Add(exception.Message);
            return null;
        }
    }
}
