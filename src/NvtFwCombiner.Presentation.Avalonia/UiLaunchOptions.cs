using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Command-line options that put the UI shell into a reviewable startup state.</summary>
internal sealed class UiLaunchOptions
{
    private UiLaunchOptions(
        ShellPage? page,
        string? reportPath,
        bool openReport,
        IReadOnlyList<string> issues)
    {
        Page = page;
        ReportPath = reportPath;
        OpenReport = openReport;
        Issues = issues;
    }

    /// <summary>Gets empty launch options.</summary>
    public static UiLaunchOptions Empty { get; } = new(null, null, openReport: false, []);

    /// <summary>Gets the shell page selected after startup.</summary>
    public ShellPage? Page { get; }

    /// <summary>Gets the run report JSON path loaded after startup.</summary>
    public string? ReportPath { get; }

    /// <summary>True when the report modal should open after loading a report.</summary>
    public bool OpenReport { get; }

    /// <summary>Gets startup argument parse issues shown through the report surface.</summary>
    public IReadOnlyList<string> Issues { get; }

    /// <summary>Parses UI shell startup arguments.</summary>
    public static UiLaunchOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ShellPage? page = null;
        string? reportPath = null;
        bool openReport = false;
        List<string> issues = [];

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            if (TrySplitValue(argument, "--page", out string? inlinePage))
            {
                string? value = inlinePage ?? TakeValue(args, ref index, "--page", issues);
                page = ParsePage(value, issues);
                continue;
            }

            if (TrySplitValue(argument, "--load-report", out string? inlineReport))
            {
                reportPath = TakeOptionValue(args, ref index, "--load-report", inlineReport, issues);
                continue;
            }

            if (TrySplitValue(argument, "--report", out inlineReport))
            {
                reportPath = TakeOptionValue(args, ref index, "--report", inlineReport, issues);
                continue;
            }

            if (string.Equals(argument, "--open-report", StringComparison.Ordinal))
            {
                openReport = true;
            }
        }

        return new UiLaunchOptions(page, NormalizeBlank(reportPath), openReport, issues);
    }

    private static bool TrySplitValue(string argument, string option, out string? value)
    {
        value = null;
        if (string.Equals(argument, option, StringComparison.Ordinal))
        {
            return true;
        }

        string prefix = option + "=";
        if (!argument.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        value = argument[prefix.Length..];
        return true;
    }

    private static string? TakeValue(IReadOnlyList<string> args, ref int index, string option, List<string> issues)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            issues.Add($"{option} requires a value.");
            return null;
        }

        index++;
        return args[index];
    }

    private static string? TakeOptionValue(
        IReadOnlyList<string> args,
        ref int index,
        string option,
        string? inlineValue,
        List<string> issues)
    {
        string? value = inlineValue ?? TakeValue(args, ref index, option, issues);
        if (inlineValue is not null && string.IsNullOrWhiteSpace(inlineValue))
        {
            issues.Add($"{option} requires a value.");
        }

        return value;
    }

    private static ShellPage? ParsePage(string? value, List<string> issues)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant() switch
        {
            "home" => ShellPage.Home,
            "settings" => ShellPage.Settings,
            "merge" => ShellPage.Merge,
            "replace" => ShellPage.Replace,
            "hex-editor" => ShellPage.HexEditor,
            _ => InvalidPage(value, issues),
        };
    }

    private static ShellPage? InvalidPage(string value, List<string> issues)
    {
        issues.Add($"Unsupported --page value '{value}'. Use home, settings, merge, replace, or hex-editor.");
        return null;
    }

    private static string? NormalizeBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
