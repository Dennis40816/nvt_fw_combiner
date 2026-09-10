using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Command-line options that put the UI shell into a reviewable startup state.</summary>
internal sealed partial class UiLaunchOptions
{
    private UiLaunchOptions(
        ShellPage? page,
        bool openSettings,
        string? reportPath,
        bool openReport,
        IReadOnlyList<string> issues,
        CtrlRamLaunchRequest? ctrlRam = null)
    {
        Page = page;
        OpenSettings = openSettings;
        ReportPath = reportPath;
        OpenReport = openReport;
        Issues = issues;
        CtrlRam = ctrlRam;
    }

    /// <summary>Gets empty launch options.</summary>
    public static UiLaunchOptions Empty { get; } = new(null, openSettings: false, null, openReport: false, []);

    /// <summary>Gets the shell page selected after startup.</summary>
    public ShellPage? Page { get; }

    /// <summary>True when the application Settings modal should open after launch navigation.</summary>
    public bool OpenSettings { get; }

    /// <summary>Gets the run report JSON path loaded after startup.</summary>
    public string? ReportPath { get; }

    /// <summary>True when the report modal should open after loading a report.</summary>
    public bool OpenReport { get; }

    /// <summary>Gets startup argument parse issues shown through the report surface.</summary>
    public IReadOnlyList<string> Issues { get; }

    /// <summary>Explicit input selection only; never grants Preview or Build authority.</summary>
    public CtrlRamLaunchRequest? CtrlRam { get; }

    /// <summary>Parses UI shell startup arguments.</summary>
    public static UiLaunchOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ShellPage? page = null;
        bool openSettings = false;
        string? reportPath = null;
        bool openReport = false;
        List<string> issues = [];
        var inputOptions = new Dictionary<string, string>(StringComparer.Ordinal);
        var inputs = new List<CtrlRamLaunchInput>();
        var unknownArguments = new List<string>();

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            if (TakeCtrlRamOption(args, ref index, inputOptions, inputs, issues))
            {
                continue;
            }
            if (TrySplitValue(argument, "--page", out string? inlinePage))
            {
                string? value = inlinePage ?? TakeValue(args, ref index, "--page", issues);
                page = ParsePage(value, issues, out bool settingsRequested);
                openSettings |= settingsRequested;
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
                continue;
            }
            unknownArguments.Add(argument);
        }

        CtrlRamLaunchRequest? ctrlRam = ParseCtrlRamRequest(
            inputOptions, inputs, page, openSettings, reportPath, openReport, unknownArguments, issues);
        return new UiLaunchOptions(ctrlRam is null ? page : ShellPage.Replace,
            openSettings, NormalizeBlank(reportPath), openReport, issues, ctrlRam);
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

    private static ShellPage? ParsePage(string? value, List<string> issues, out bool openSettings)
    {
        openSettings = string.Equals(value?.Trim(), "settings", StringComparison.OrdinalIgnoreCase);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant() switch
        {
            "home" => ShellPage.Home,
            "settings" => ShellPage.Home,
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
