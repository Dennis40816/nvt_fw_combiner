using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
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
        CtrlRamLaunchRequest? ctrlRam = null,
        AbMergeLaunchRequest? abMerge = null,
        StandardMergeLaunchRequest? standardMerge = null,
        bool scriptedRequest = false,
        LaunchParseState? parsed = null)
    {
        Page = page;
        OpenSettings = openSettings;
        ReportPath = reportPath;
        OpenReport = openReport;
        Issues = issues;
        CtrlRam = ctrlRam;
        AbMerge = abMerge;
        StandardMerge = standardMerge;
        IsScriptedRequest = scriptedRequest;
        InitializeCapture(parsed);
        InitializeReportTabs(parsed);
        InitializeAppearance(parsed);
        InitializeWorkflowState(parsed);
        InitializeNavigation(parsed);
        InitializeUtilities(parsed);
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

    /// <summary>Gets startup argument issues displayed interactively or refused for scripted requests.</summary>
    public IReadOnlyList<string> Issues { get; }

    /// <summary>Explicit input selection only; never grants Preview or Build authority.</summary>
    public CtrlRamLaunchRequest? CtrlRam { get; }

    /// <summary>Explicit AB selections inspected through the ordinary Browse owner; never executes a run.</summary>
    public AbMergeLaunchRequest? AbMerge { get; }

    /// <summary>Explicit Standard Merge selections inspected through the ordinary Browse owner.</summary>
    public StandardMergeLaunchRequest? StandardMerge { get; }

    /// <summary>Gets whether ordinary firmware input preload was requested.</summary>
    public bool HasStartupInputs => CtrlRam is not null || AbMerge is not null || StandardMerge is not null;

    internal bool HasStartupReportStage => Issues.Count > 0 || !string.IsNullOrWhiteSpace(ReportPath) || OpenReport;

    /// <summary>Parses UI shell startup arguments; capture validation requires the supplied local-file port.</summary>
    public static UiLaunchOptions Parse(IReadOnlyList<string> args, ILocalFileStore? files = null,
        IReadOnlyList<string>? protectedInputs = null)
    {
        ArgumentNullException.ThrowIfNull(args);

        ShellPage? page = null;
        int pageCount = 0;
        int reportCount = 0;
        bool scriptedRequest = RequestsScriptedCompletion(args);
        var singletonFlags = new HashSet<string>(StringComparer.Ordinal);
        bool openSettings = false;
        string? reportPath = null;
        bool openReport = false;
        List<string> issues = [];
        var inputOptions = new Dictionary<string, string>(StringComparer.Ordinal);
        var inputs = new List<CtrlRamLaunchInput>();
        var unknownArguments = new List<string>();
        var parsed = new LaunchParseState(scriptedRequest, singletonFlags, issues, inputOptions, inputs, unknownArguments);
        if (protectedInputs is not null) { parsed.ProtectedInputs.AddRange(protectedInputs); }

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            if (TakeInputOption(args, ref index, inputOptions, inputs, issues))
            {
                continue;
            }
            bool consumed = false;
            ConsumeCaptureToken(parsed, args, ref index, ref consumed);
            ConsumeReportTabsToken(parsed, args, ref index, ref consumed);
            ConsumeAppearanceToken(parsed, args, ref index, ref consumed);
            ConsumeWorkflowStateToken(parsed, args, ref index, ref consumed);
            ConsumeNavigationToken(parsed, args, ref index, ref consumed);
            ConsumeUtilitiesToken(parsed, args, ref index, ref consumed);
            if (consumed)
            {
                continue;
            }
            if (argument == "--open-report")
            {
                if (!singletonFlags.Add(argument) && scriptedRequest)
                {
                    issues.Add($"Duplicate option '{argument}'.");
                }
                openReport = true;
                continue;
            }
            if (TrySplitValue(argument, "--page", out string? inlinePage))
            {
                pageCount++;
                string? value = scriptedRequest
                    ? TakeOptionValue(args, ref index, "--page", inlinePage, issues)
                    : inlinePage ?? TakeValue(args, ref index, "--page", issues);
                page = ParsePage(value, issues, out bool settingsRequested);
                openSettings |= settingsRequested;
                continue;
            }

            if (TrySplitValue(argument, "--load-report", out string? inlineReport))
            {
                reportCount++;
                reportPath = TakeOptionValue(args, ref index, "--load-report", inlineReport, issues);
                continue;
            }

            if (TrySplitValue(argument, "--report", out inlineReport))
            {
                reportCount++;
                reportPath = TakeOptionValue(args, ref index, "--report", inlineReport, issues);
                continue;
            }

            unknownArguments.Add(argument);
        }

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            try
            {
                _ = Path.GetFullPath(reportPath);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                issues.Add($"Invalid report path: {exception.Message}");
            }
        }

        if (scriptedRequest)
        {
            foreach (string argument in unknownArguments)
            {
                issues.Add($"Unsupported startup argument '{argument}'.");
            }
            unknownArguments.Clear();
            if (reportCount > 1) { issues.Add("Duplicate report option '--load-report'/'--report'."); }
            ValidateCaptureSyntax(parsed, args);
            if (openReport && string.IsNullOrWhiteSpace(reportPath))
            {
                issues.Add("--open-report requires a loaded report. Pass --load-report <path> or --report <path>.");
            }
            if (openSettings && (reportPath is not null || openReport))
            {
                issues.Add("--page settings cannot be combined with report loading or --open-report.");
            }
        }
        parsed.Page = page;
        parsed.OpenSettings = openSettings;
        parsed.ReportPath = reportPath;
        parsed.OpenReport = openReport;
        ValidateReportTabs(parsed, args, files);
        ValidateAppearance(parsed, args, files);
        ValidateWorkflowState(parsed, args, files);
        ValidateNavigation(parsed, args, files);
        ValidateUtilities(parsed, args, files);
        ValidateCapture(parsed, args, files);
        if (pageCount > 1) { issues.Add("Duplicate option '--page'."); }
        ParseLegacyWorkflowInputs(parsed);
        return new UiLaunchOptions(parsed.AbMerge is not null || parsed.StandardMerge is not null
                ? ShellPage.Merge : parsed.CtrlRam is not null ? ShellPage.Replace : parsed.Page,
            parsed.OpenSettings, NormalizeBlank(parsed.ReportPath), parsed.OpenReport, issues,
            parsed.CtrlRam, parsed.AbMerge, parsed.StandardMerge, scriptedRequest, parsed);
    }

    private static void ParseLegacyWorkflowInputs(LaunchParseState parsed)
    {
        if (parsed.WorkflowInputs == WorkflowInputDisposition.LeaveSetupUnconfirmed)
        {
            return;
        }
        bool isAbMerge = parsed.InputOptions.GetValueOrDefault("--workflow") == ExperienceIds.AbMerge;
        bool isStandardMerge = parsed.InputOptions.GetValueOrDefault("--workflow") == ExperienceIds.StandardMerge;
        parsed.AbMerge = isAbMerge ? ParseAbMergeRequest(
            parsed.InputOptions, parsed.Page, parsed.OpenSettings, parsed.ReportPath, parsed.OpenReport,
            parsed.UnknownArguments, parsed.Issues) : null;
        parsed.StandardMerge = isStandardMerge ? ParseStandardMergeRequest(
            parsed.InputOptions, parsed.Page, parsed.OpenSettings, parsed.ReportPath, parsed.OpenReport,
            parsed.UnknownArguments, parsed.Issues) : null;
        parsed.CtrlRam = isAbMerge || isStandardMerge ? null : ParseCtrlRamRequest(
            parsed.InputOptions, parsed.CtrlRamInputs, parsed.Page, parsed.OpenSettings, parsed.ReportPath, parsed.OpenReport,
            parsed.UnknownArguments, parsed.Issues);
    }

    static partial void ConsumeCaptureToken(LaunchParseState parsed, IReadOnlyList<string> args, ref int index, ref bool consumed);
    static partial void ValidateCaptureSyntax(LaunchParseState parsed, IReadOnlyList<string> args);
    static partial void ValidateCapture(LaunchParseState parsed, IReadOnlyList<string> args, ILocalFileStore? files);
    partial void InitializeCapture(LaunchParseState? parsed);
    static partial void AppendCaptureHelp(List<string> entries);

    static partial void ConsumeReportTabsToken(LaunchParseState parsed, IReadOnlyList<string> args, ref int index, ref bool consumed);
    static partial void ValidateReportTabs(LaunchParseState parsed, IReadOnlyList<string> args, ILocalFileStore? files);
    partial void InitializeReportTabs(LaunchParseState? parsed);
    static partial void AppendReportTabsHelp(List<string> entries);

    static partial void ConsumeAppearanceToken(LaunchParseState parsed, IReadOnlyList<string> args, ref int index, ref bool consumed);
    static partial void ValidateAppearance(LaunchParseState parsed, IReadOnlyList<string> args, ILocalFileStore? files);
    partial void InitializeAppearance(LaunchParseState? parsed);
    static partial void AppendAppearanceHelp(List<string> entries);

    static partial void ConsumeWorkflowStateToken(LaunchParseState parsed, IReadOnlyList<string> args, ref int index, ref bool consumed);
    static partial void ValidateWorkflowState(LaunchParseState parsed, IReadOnlyList<string> args, ILocalFileStore? files);
    partial void InitializeWorkflowState(LaunchParseState? parsed);
    static partial void AppendWorkflowStateHelp(List<string> entries);

    static partial void ConsumeNavigationToken(LaunchParseState parsed, IReadOnlyList<string> args, ref int index, ref bool consumed);
    static partial void ValidateNavigation(LaunchParseState parsed, IReadOnlyList<string> args, ILocalFileStore? files);
    partial void InitializeNavigation(LaunchParseState? parsed);
    static partial void AppendNavigationHelp(List<string> entries);

    static partial void ConsumeUtilitiesToken(LaunchParseState parsed, IReadOnlyList<string> args, ref int index, ref bool consumed);
    static partial void ValidateUtilities(LaunchParseState parsed, IReadOnlyList<string> args, ILocalFileStore? files);
    partial void InitializeUtilities(LaunchParseState? parsed);
    static partial void AppendUtilitiesHelp(List<string> entries);

    private enum WorkflowInputDisposition { LoadInputs, LeaveSetupUnconfirmed }

    private sealed partial class LaunchParseState(bool scripted, HashSet<string> singletonFlags, List<string> issues,
        Dictionary<string, string> inputOptions, List<CtrlRamLaunchInput> ctrlRamInputs, List<string> unknownArguments)
    {
        internal bool Scripted { get; } = scripted;
        internal HashSet<string> SingletonFlags { get; } = singletonFlags;
        internal List<string> Issues { get; } = issues;
        internal ShellPage? Page { get; set; }
        internal bool OpenSettings { get; set; }
        internal string? ReportPath { get; set; }
        internal bool OpenReport { get; set; }
        internal Dictionary<string, string> InputOptions { get; } = inputOptions;
        internal List<CtrlRamLaunchInput> CtrlRamInputs { get; } = ctrlRamInputs;
        internal List<string> UnknownArguments { get; } = unknownArguments;
        internal bool HasInputOptions => InputOptions.Count > 0;
        internal WorkflowInputDisposition WorkflowInputs { get; set; } = WorkflowInputDisposition.LoadInputs;
        internal CtrlRamLaunchRequest? CtrlRam { get; set; }
        internal AbMergeLaunchRequest? AbMerge { get; set; }
        internal StandardMergeLaunchRequest? StandardMerge { get; set; }
        internal bool CaptureTargetSupported { get; set; }
        internal List<string> ProtectedInputs { get; } = [];
    }

    internal bool IsScriptedRequest { get; }

    internal static bool RequestsScriptedCompletion(IReadOnlyList<string> args)
    {
        return args.Any(IsScriptedArgument);
    }

    private static bool IsScriptedArgument(string argument)
    {
        return argument is "--capture" or "--help" ||
            argument.StartsWith("--capture=", StringComparison.Ordinal) ||
            argument.StartsWith("--help=", StringComparison.Ordinal);
    }

    private const string CoreUsage = """
        Usage: NvtFwCombiner.Desktop [options]
          --page home|merge|replace|hex-editor|settings
          --load-report <json-path>            Load a saved report (alias: --report).
          --open-report                       Open the loaded report modal.
          --workflow standard-merge|ab-merge|ctrlram-replace
          --ic <catalog-id> --ic-num <count>   Required input-preload context.
          --dp <bin-path> --tp <bin-path>      Standard Merge inputs.
          --dp <bin-path> --tp-a <bin-path> --tp-b <bin-path>
                                              AB Merge inputs.
          --base <bin-path> --ctrlram <slot-id>=<bin-path>
                                              CtrlRAM inputs; distinct slots may repeat.
        """;

    internal static string Usage
    {
        get
        {
            List<string> entries = [CoreUsage];
            AppendCaptureHelp(entries);
            AppendReportTabsHelp(entries);
            AppendAppearanceHelp(entries);
            AppendWorkflowStateHelp(entries);
            AppendNavigationHelp(entries);
            AppendUtilitiesHelp(entries);
            return string.Join(Environment.NewLine, entries);
        }
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
        if (value is not null && (inlineValue is not null || RequestsScriptedCompletion(args)) && string.IsNullOrWhiteSpace(value))
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
