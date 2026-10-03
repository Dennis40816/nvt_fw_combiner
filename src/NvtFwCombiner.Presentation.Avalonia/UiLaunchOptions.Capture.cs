using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Capture request validation and public help for the single launch parser.</summary>
internal sealed partial class UiLaunchOptions
{
    // Slice 1 accepts PNG only; other image encoders are not part of this contract.
    internal const string CaptureExtension = ".png";

    /// <summary>Gets whether standalone public help was requested.</summary>
    public bool Help => _capture.Help;

    /// <summary>Gets the normalized destination for a requested capture.</summary>
    public string? CapturePath => _capture.Path;

    /// <summary>Gets whether atomic replacement of an existing capture was authorized.</summary>
    public bool OverwriteCapture => _capture.Overwrite;

    internal IReadOnlyList<string> ProtectedInputs { get; private set; } = [];

    private CaptureLaunchRequest _capture;
    private readonly record struct CaptureLaunchRequest(bool Help, string? Path, bool Overwrite);

    private sealed partial class LaunchParseState
    {
        internal bool Help { get; set; }
        internal string? CapturePath { get; set; }
        internal bool OverwriteCapture { get; set; }
        internal CaptureLaunchRequest Capture { get; set; }
    }

    static partial void ConsumeCaptureToken(LaunchParseState parsed, IReadOnlyList<string> args, ref int index, ref bool consumed)
    {
        if (consumed) { return; }
        string argument = args[index];
        if (argument is "--help" or "--overwrite-capture")
        {
            if (!parsed.SingletonFlags.Add(argument) && parsed.Scripted)
            {
                parsed.Issues.Add($"Duplicate option '{argument}'.");
            }
            parsed.Help |= argument == "--help";
            parsed.OverwriteCapture |= argument == "--overwrite-capture";
            consumed = true;
        }
        else if (TrySplitValue(argument, "--capture", out string? inlineCapture))
        {
            if (!parsed.SingletonFlags.Add("--capture"))
            {
                parsed.Issues.Add("Duplicate option '--capture'.");
            }
            parsed.CapturePath = TakeOptionValue(args, ref index, "--capture", inlineCapture, parsed.Issues);
            consumed = true;
        }
    }

    static partial void ValidateCaptureSyntax(LaunchParseState parsed, IReadOnlyList<string> args)
    {
        if (parsed.Help && args.Count != 1) { parsed.Issues.Add("--help must be used alone."); }
    }

    static partial void ValidateCapture(LaunchParseState parsed, IReadOnlyList<string> args, ILocalFileStore? files)
    {
        // Only the delivered saved-report target grants readiness in slice 1.
        if (!string.IsNullOrWhiteSpace(parsed.ReportPath) && parsed.OpenReport && !parsed.OpenSettings && !parsed.HasInputOptions)
        {
            parsed.CaptureTargetSupported = true;
            parsed.ProtectedInputs.Add(parsed.ReportPath);
        }
        if (parsed.Scripted)
        {
            if (parsed.SingletonFlags.Contains("--capture"))
            {
                if (!parsed.CaptureTargetSupported)
                {
                    parsed.Issues.Add("--capture: target is not available for capture yet; use --load-report <json-path> with --open-report.");
                }
                else if (!string.IsNullOrWhiteSpace(parsed.CapturePath))
                {
                    parsed.CapturePath = ValidateCapturePath(
                        files ?? throw new InvalidOperationException("Capture validation requires a local-file port."),
                        parsed.CapturePath, parsed.ProtectedInputs, parsed.OverwriteCapture, parsed.Issues);
                }
            }
        }
        if (parsed.Scripted && parsed.OverwriteCapture && !parsed.SingletonFlags.Contains("--capture"))
        {
            parsed.Issues.Add("--overwrite-capture requires --capture.");
        }
        parsed.Capture = new(parsed.Help, NormalizeBlank(parsed.CapturePath), parsed.OverwriteCapture);
    }

    partial void InitializeCapture(LaunchParseState? parsed)
    {
        _capture = parsed?.Capture ?? default;
        ProtectedInputs = parsed?.ProtectedInputs ?? [];
    }

    static partial void AppendCaptureHelp(List<string> entries)
    {
        entries.Add("""
          --help                              Print this help (alone).
          --capture <png-path>                Capture a loaded, open saved report, then close.
          --overwrite-capture                 Atomically replace an existing capture.
        Value options accept --name value and --name=value.
        Capture requires an existing parent folder, its real path, and a .png filename.
        Capture persists nothing and uses saved appearance preferences.
        Strict request refusal applies only with --capture or --help.
        Capture exits: 0 committed PNG; 1 load/draw/save failure; 64 refused request;
        70 unexpected exception, cancellation or the 300-second capture deadline.
        Without capture, launch stays interactive.
        """);
    }

    internal static string? ValidateCapturePath(
        ILocalFileStore files,
        string capturePath,
        IReadOnlyList<string> protectedInputs,
        bool overwrite,
        List<string> issues)
    {
        try
        {
            return DesktopScreenshotCapture.ValidateDestinationAsync(files, capturePath, protectedInputs, overwrite)
                .AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or
            UnauthorizedAccessException or NotSupportedException)
        {
            issues.Add($"validation --capture '{capturePath}'{DesktopScreenshotCapture.DescribeProtectedInputs(protectedInputs)}: {exception.Message}");
            return null;
        }
    }
}
