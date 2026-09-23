using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

internal sealed record StandardMergeLaunchRequest(string IcId, string Number, string DpPath, string TpPath);

internal sealed partial class UiLaunchOptions
{
    private static StandardMergeLaunchRequest? ParseStandardMergeRequest(
        Dictionary<string, string> options, ShellPage? page, bool openSettings,
        string? reportPath, bool openReport, List<string> unknownArguments, List<string> issues)
    {
        foreach (string name in new[] { "--ic", "--ic-num", "--dp", "--tp" })
        {
            if (!options.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                issues.Add($"Standard Merge startup requires {name}.");
            }
        }
        if (options.Keys.Any(name => name is "--base" or "--ctrlram" or "--tp-a" or "--tp-b"))
        {
            issues.Add("Standard Merge startup cannot be combined with AB or CtrlRAM inputs.");
        }
        if (page is not (null or ShellPage.Merge) || openSettings || reportPath is not null || openReport)
        {
            issues.Add("Standard Merge startup cannot be combined with another page, Settings or report loading.");
        }
        foreach (string argument in unknownArguments)
        {
            issues.Add($"Unsupported input startup argument '{argument}'.");
        }
        if (issues.Count > 0) { return null; }
        try
        {
            return new(options["--ic"], options["--ic-num"],
                Path.GetFullPath(options["--dp"]), Path.GetFullPath(options["--tp"]));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issues.Add(exception.Message);
            return null;
        }
    }
}
