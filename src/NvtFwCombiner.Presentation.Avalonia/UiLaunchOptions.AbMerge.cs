using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

internal sealed record AbMergeLaunchRequest(string IcId, string Number, string DpPath, string TpAPath, string TpBPath);

internal sealed partial class UiLaunchOptions
{
    private static AbMergeLaunchRequest? ParseAbMergeRequest(
        Dictionary<string, string> options, ShellPage? page, bool openSettings,
        string? reportPath, bool openReport, List<string> unknownArguments, List<string> issues)
    {
        foreach (string name in new[] { "--ic", "--ic-num", "--dp", "--tp-a", "--tp-b" })
        {
            if (!options.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                issues.Add($"AB startup requires {name}.");
            }
        }
        if (options.ContainsKey("--base") || options.ContainsKey("--ctrlram"))
        {
            issues.Add("AB startup cannot be combined with CtrlRAM inputs.");
        }
        if (page is not (null or ShellPage.Merge) || openSettings || reportPath is not null || openReport)
        {
            issues.Add("AB startup cannot be combined with another page, Settings or report loading.");
        }
        foreach (string argument in unknownArguments)
        {
            issues.Add($"Unsupported input startup argument '{argument}'.");
        }
        if (issues.Count > 0) { return null; }
        try
        {
            return new(options["--ic"], options["--ic-num"], Path.GetFullPath(options["--dp"]),
                Path.GetFullPath(options["--tp-a"]), Path.GetFullPath(options["--tp-b"]));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issues.Add(exception.Message);
            return null;
        }
    }
}
