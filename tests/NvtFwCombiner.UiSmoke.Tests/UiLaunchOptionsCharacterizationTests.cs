using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Complete launch-parser results fixed to the delivered b32652dd4 argument contract.</summary>
[Collection(UiProcessWideObservationCollection.Name)]
public sealed class UiLaunchOptionsCharacterizationTests
{
    private const string CaptureTargetIssue =
        "--capture: target is not available for capture yet; use --load-report <json-path> with --open-report.";
    private const string OpenReportIssue =
        "--open-report requires a loaded report. Pass --load-report <path> or --report <path>.";
    private const string SettingsReportIssue =
        "--page settings cannot be combined with report loading or --open-report.";
    private const string DuplicateReportIssue = "Duplicate report option '--load-report'/'--report'.";

    /// <summary>Interactive page/report recovery and help retain every observable option field.</summary>
    [Fact]
    public void InteractiveAndHelpVectorsKeepCompleteResults()
    {
        Check([], new());
        Check(["--page", "invalid"], new()
        {
            Issues = [
            "Unsupported --page value 'invalid'. Use home, settings, merge, replace, or hex-editor."]
        });
        Check(["--page", "home", "--page", "merge"], new()
        {
            Page = ShellPage.Merge,
            Issues = ["Duplicate option '--page'."]
        });
        Check(["--page"], new() { Issues = ["--page requires a value."] });
        Check(["--report=first.json", "--load-report=last.json", "--open-report", "--open-report", "--unknown"],
            new() { ReportPath = "last.json", OpenReport = true });
        Check(["--help"], new() { Help = true, Scripted = true });
        Check(["--help=value"], new()
        {
            Scripted = true,
            Issues = [
            "Unsupported startup argument '--help=value'."]
        });
        Check(["--help", "--unknown"], new()
        {
            Help = true,
            Scripted = true,
            Issues = [
            "Unsupported startup argument '--unknown'.", "--help must be used alone."]
        });
        Check(["--help", "--page=home"], new()
        {
            Page = ShellPage.Home,
            Help = true,
            Scripted = true,
            Issues = ["--help must be used alone."]
        });
        Check(["--help", "--page", "home", "--page", "merge"], new()
        {
            Page = ShellPage.Merge,
            Help = true,
            Scripted = true,
            Issues = ["--help must be used alone.", "Duplicate option '--page'."]
        });
        Check(["--help", "--help"], new()
        {
            Help = true,
            Scripted = true,
            Issues = [
            "Duplicate option '--help'.", "--help must be used alone."]
        });
        Check(["--overwrite-capture"], new() { Overwrite = true });
        Assert.False(UiLaunchOptions.Parse(["--overwrite-capture"], new LocalFileStore()).HasStartupReportStage);
        Check(["--help", "--overwrite-capture"], new()
        {
            Help = true,
            Overwrite = true,
            Scripted = true,
            Issues = ["--help must be used alone.", "--overwrite-capture requires --capture."]
        });

        // DesktopStartupArgumentTests includes an invalid report path whose BCL message is platform-specific.
        string invalidPath = "\0";
        string nativeMessage = Assert.ThrowsAny<ArgumentException>(() => Path.GetFullPath(invalidPath)).Message;
        Check(["--report", invalidPath], new()
        {
            ReportPath = invalidPath,
            Issues = [$"Invalid report path: {nativeMessage}"]
        });
    }

    /// <summary>All later-slice and unknown flags from DesktopCaptureTests stay unknown in this slice.</summary>
    [Fact]
    public void EveryUnsupportedCaptureVectorRetainsIssueOrder()
    {
        string[] unsupported = ["--report-tab", "--theme", "--language", "--motion", "--window-size",
            "--ldc", "--ab-dp-mode", "--ack-ab-dummy-dp", "--ctrlram-banks", "--open-workflow-setup",
            "--input-details", "--open-history", "--diagnostics", "--settings-section", "--load-bin",
            "--open-build-settings", "--unknown", "positional", "--HELP", "--open-report=true"];
        foreach (string option in unsupported)
        {
            Check(["--capture=out.png", option], new()
            {
                Scripted = true,
                CapturePath = "out.png",
                Issues = [$"Unsupported startup argument '{option}'.", CaptureTargetIssue]
            });
        }
    }

    /// <summary>Missing, blank, duplicate and conflicting public tokens preserve exact issue order.</summary>
    [Fact]
    public void MalformedScriptedVectorsKeepCompleteResults()
    {
        using var workspace = TempWorkspace.Create("parser-malformed");
        string outPath = workspace.PathFor("out.png");
        string aPath = workspace.PathFor("a.png");
        string bPath = workspace.PathFor("b.png");
        Check(["--capture"], new() { Scripted = true, Issues = ["--capture requires a value.", CaptureTargetIssue] });
        Check(["--capture="], new() { Scripted = true, Issues = ["--capture requires a value.", CaptureTargetIssue] });
        Check(["--capture= "], new() { Scripted = true, Issues = ["--capture requires a value.", CaptureTargetIssue] });
        Check(["--page=home", "--capture=out.png"], new()
        {
            Page = ShellPage.Home,
            Scripted = true,
            CapturePath = "out.png",
            Issues = [CaptureTargetIssue]
        });
        Check(["--page=", "--capture=out.png"], new()
        {
            Scripted = true,
            CapturePath = "out.png",
            Issues = ["--page requires a value.", CaptureTargetIssue]
        });
        Check(["--page", " ", "--capture=out.png"], new()
        {
            Scripted = true,
            CapturePath = "out.png",
            Issues = ["--page requires a value.", CaptureTargetIssue]
        });
        Check(["--page", "home", "--page=merge", "--capture=out.png"], new()
        {
            Page = ShellPage.Merge,
            Scripted = true,
            CapturePath = "out.png",
            Issues = [CaptureTargetIssue, "Duplicate option '--page'."]
        });
        Check(["--page=home", "--page=merge", "--capture=out.png"], new()
        {
            Page = ShellPage.Merge,
            Scripted = true,
            CapturePath = "out.png",
            Issues = [CaptureTargetIssue, "Duplicate option '--page'."]
        });
        Check(["--report", "", "--open-report", "--capture=out.png"], new()
        {
            OpenReport = true,
            Scripted = true,
            CapturePath = "out.png",
            Issues = ["--report requires a value.", OpenReportIssue, CaptureTargetIssue]
        });
        Check(["--report= ", "--capture=out.png"], new()
        {
            Scripted = true,
            CapturePath = "out.png",
            Issues = ["--report requires a value.", CaptureTargetIssue]
        });
        Check(["--report", "--capture=out.png"], new()
        {
            Scripted = true,
            CapturePath = "out.png",
            Issues = ["--report requires a value.", CaptureTargetIssue]
        });
        Check(["--report=a.json", "--capture=out.png"], new()
        {
            ReportPath = "a.json",
            Scripted = true,
            CapturePath = "out.png",
            Issues = [CaptureTargetIssue]
        });
        Check(["--report=a.json", "--load-report=b.json", "--capture=out.png"], new()
        {
            ReportPath = "b.json",
            Scripted = true,
            CapturePath = "out.png",
            Issues = [DuplicateReportIssue, CaptureTargetIssue]
        });
        Check(["--report=a.json", "--report=b.json", "--capture=out.png"], new()
        {
            ReportPath = "b.json",
            Scripted = true,
            CapturePath = "out.png",
            Issues = [DuplicateReportIssue, CaptureTargetIssue]
        });
        Check(["--open-report", "--capture=out.png"], new()
        {
            OpenReport = true,
            Scripted = true,
            CapturePath = "out.png",
            Issues = [OpenReportIssue, CaptureTargetIssue]
        });
        Check(["--report=a.json", "--page=settings", "--capture=out.png"], new()
        {
            Page = ShellPage.Home,
            OpenSettings = true,
            ReportPath = "a.json",
            Scripted = true,
            CapturePath = "out.png",
            Issues = [SettingsReportIssue, CaptureTargetIssue]
        });
        Check(["--report=a.json", "--open-report", "--open-report", $"--capture={outPath}"], new()
        {
            ReportPath = "a.json",
            OpenReport = true,
            Scripted = true,
            CapturePath = outPath,
            Issues = ["Duplicate option '--open-report'."]
        });
        Check(["--report=a.json", "--open-report", $"--capture={aPath}", $"--capture={bPath}"], new()
        {
            ReportPath = "a.json",
            OpenReport = true,
            Scripted = true,
            CapturePath = bPath,
            Issues = ["Duplicate option '--capture'."]
        });
        Check(["--report=a.json", "--open-report", $"--capture={aPath}", "--overwrite-capture", "--overwrite-capture"], new()
        {
            ReportPath = "a.json",
            OpenReport = true,
            Scripted = true,
            Overwrite = true,
            CapturePath = aPath,
            Issues = ["Duplicate option '--overwrite-capture'."]
        });
        Check(["--unknown", "--capture=out.png"], new()
        {
            Scripted = true,
            CapturePath = "out.png",
            Issues = ["Unsupported startup argument '--unknown'.", CaptureTargetIssue]
        });
        Check(["--report-tab=Raw", "--capture=out.png"], new()
        {
            Scripted = true,
            CapturePath = "out.png",
            Issues = ["Unsupported startup argument '--report-tab=Raw'.", CaptureTargetIssue]
        });
    }

    /// <summary>Valid capture syntax normalizes paths; unavailable paths retain the old validation result.</summary>
    [Fact]
    public void CaptureDestinationVectorsKeepCompleteResults()
    {
        using var workspace = TempWorkspace.Create("parser-characterization");
        string report = workspace.Write("report.png", [1, 2, 3]);
        string destination = workspace.PathFor("capture.png");
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, destination);
        Check(["--report", report, "--open-report"], new()
        {
            ReportPath = report,
            OpenReport = true
        });
        Check(["--report", report, "--open-report", "--capture", destination], new()
        {
            ReportPath = report,
            OpenReport = true,
            Scripted = true,
            CapturePath = destination
        });
        Check([$"--load-report={report}", "--open-report", $"--capture={relative}"], new()
        {
            ReportPath = report,
            OpenReport = true,
            Scripted = true,
            CapturePath = destination
        });
        Check(["--report", report, "--open-report", "--capture", destination, "--overwrite-capture"], new()
        {
            ReportPath = report,
            OpenReport = true,
            Scripted = true,
            CapturePath = destination,
            Overwrite = true
        });

        string missingParent = Path.Combine(workspace.Root, "missing", "out.png");
        string reason = new ArgumentException("--capture parent folder must already exist.", "destination").Message;
        string invalidIssue = $"validation --capture '{missingParent}' (protected inputs: '{report}'): {reason}";
        Check(["--report", report, "--open-report", "--capture", missingParent], new()
        {
            ReportPath = report,
            OpenReport = true,
            Scripted = true,
            Issues = [invalidIssue]
        });

        string oldFile = workspace.Write("old.png", [4, 5, 6]);
        string directory = workspace.PathFor("directory.png");
        _ = Directory.CreateDirectory(directory);
        (string Path, string Reason)[] invalidDestinations = [
            (directory, "--capture destination is a directory."),
            (workspace.PathFor("out.jpg"), "--capture requires a valid .png filename."),
            (workspace.Root, "--capture requires a valid .png filename."),
            (report, $"--capture destination aliases protected input '{report}' (protected inputs: '{report}')."),
            (oldFile, "--capture destination exists; use --overwrite-capture.")
        ];
        foreach ((string path, string reasonText) in invalidDestinations)
        {
            string detail = new ArgumentException(reasonText, "destination").Message;
            Check(["--report", report, "--open-report", "--capture", path], new()
            {
                ReportPath = report,
                OpenReport = true,
                Scripted = true,
                Issues = [$"validation --capture '{path}' (protected inputs: '{report}'): {detail}"]
            });
        }
        Check(["--report", report, "--open-report", "--capture", oldFile, "--overwrite-capture"], new()
        {
            ReportPath = report,
            OpenReport = true,
            Scripted = true,
            CapturePath = oldFile,
            Overwrite = true
        });
        string reportAlias = Path.GetRelativePath(Environment.CurrentDirectory,
            Path.Combine(workspace.Root, ".", "report.png"));
        string aliasDetail = new ArgumentException($"--capture destination aliases protected input '{report}' (protected inputs: '{report}').", "destination").Message;
        Check(["--report", report, "--open-report", "--capture", reportAlias, "--overwrite-capture"], new()
        {
            ReportPath = report,
            OpenReport = true,
            Scripted = true,
            Overwrite = true,
            Issues = [$"validation --capture '{reportAlias}' (protected inputs: '{report}'): {aliasDetail}"]
        });
    }

    /// <summary>All CtrlRAM preload arguments and the existing conflict vectors retain their typed result.</summary>
    [Fact]
    public void CtrlRamVectorsKeepCompleteResults()
    {
        string[] arguments = ["--workflow", "ctrlram-replace", "--ic", "NT51950", "--ic-num", "single",
            "--base", "base.bin", "--ctrlram", "replace-ctrlram-nf=nf.bin"];
        var valid = new Expected
        {
            Page = ShellPage.Replace,
            CtrlRam = new("NT51950", "single", Path.GetFullPath("base.bin"),
                [new("replace-ctrlram-nf", Path.GetFullPath("nf.bin"))])
        };
        Check(arguments, valid);
        Check([.. arguments, "--ctrlram", "replace-ctrlram-normal=normal.bin",
            "--ctrlram", "replace-ctrlram-vn=vn.bin"], valid with
            {
                CtrlRam = new("NT51950", "single", Path.GetFullPath("base.bin"),
                [new("replace-ctrlram-nf", Path.GetFullPath("nf.bin")),
                    new("replace-ctrlram-normal", Path.GetFullPath("normal.bin")),
                    new("replace-ctrlram-vn", Path.GetFullPath("vn.bin"))])
            });
        Check(["--workflow=ctrlram-replace", "--ic=NT51950", "--ic-num=single",
            "--base=folder with spaces/base.bin", "--ctrlram=replace-ctrlram-nf=folder with spaces/nf=1.bin",
            "--ctrlram=replace-ctrlram-vn=vn.bin"], new()
            {
                Page = ShellPage.Replace,
                CtrlRam = new("NT51950", "single", Path.GetFullPath("folder with spaces/base.bin"),
                [new("replace-ctrlram-nf", Path.GetFullPath("folder with spaces/nf=1.bin")),
                    new("replace-ctrlram-vn", Path.GetFullPath("vn.bin"))])
            });
        Check([.. arguments, "--ic", "NT51951"], Rejected("Duplicate option '--ic'."));
        Check([.. arguments, "--base", "another.bin"], Rejected("Duplicate option '--base'."));
        Check([.. arguments, "--workflow", "ctrlram-replace"], Rejected("Duplicate option '--workflow'."));
        Check([.. arguments, "--page", "merge"], Rejected(
            "CtrlRAM startup cannot be combined with another page, Settings or report loading.") with
        {
            Page = ShellPage.Merge
        });
        Check([.. arguments, "--page", "settings"], Rejected(
            "CtrlRAM startup cannot be combined with another page, Settings or report loading.") with
        {
            Page = ShellPage.Home,
            OpenSettings = true
        });
        Check([.. arguments, "--report", "run.json"], Rejected(
            "CtrlRAM startup cannot be combined with another page, Settings or report loading.") with
        {
            ReportPath = "run.json"
        });
        Check([.. arguments, "--ctrlram", "replace-ctrlram-nf=other.bin"],
            Rejected("Duplicate CtrlRAM slot 'replace-ctrlram-nf'."));
        string[] invalidAssignments = ["missing-assignment", "replace-ctrlram-vn=", "=vn.bin", " =vn.bin"];
        foreach (string assignment in invalidAssignments)
        {
            Check([.. arguments, "--ctrlram", assignment], Rejected("--ctrlram requires <slot-id>=<path>."));
        }
        Check([.. arguments, "--build", "true"], new()
        {
            Issues = [
            "Unsupported input startup argument '--build'.", "Unsupported input startup argument 'true'."]
        });
        Check(["--workflow"], new()
        {
            Issues = ["--workflow requires a value.",
            "CtrlRAM startup requires --workflow.", "CtrlRAM startup requires --ic.",
            "CtrlRAM startup requires --ic-num.", "CtrlRAM startup requires --base.",
            "Input startup supports only --workflow ctrlram-replace.",
            "CtrlRAM startup requires at least one --ctrlram <slot-id>=<path>."]
        });
        Check(["--workflow", "ctrlram-replace"], new()
        {
            Issues = [
            "CtrlRAM startup requires --ic.", "CtrlRAM startup requires --ic-num.",
            "CtrlRAM startup requires --base.",
            "CtrlRAM startup requires at least one --ctrlram <slot-id>=<path>."]
        });
        Check(["--base", "base.bin"], new()
        {
            Issues = [
            "CtrlRAM startup requires --workflow.", "CtrlRAM startup requires --ic.",
            "CtrlRAM startup requires --ic-num.", "Input startup supports only --workflow ctrlram-replace.",
            "CtrlRAM startup requires at least one --ctrlram <slot-id>=<path>."]
        });
    }

    /// <summary>AB preload keeps the old five-field request and refuses every tested conflicting token.</summary>
    [Fact]
    public void AbMergeVectorsKeepCompleteResults()
    {
        string[] arguments = ["--workflow", "ab-merge", "--ic", "NT51950", "--ic-num", "single",
            "--dp", "dp.bin", "--tp-a", "tp-a.bin", "--tp-b", "tp-b.bin"];
        Check(arguments, new()
        {
            Page = ShellPage.Merge,
            AbMerge = new("NT51950", "single", Path.GetFullPath("dp.bin"),
                Path.GetFullPath("tp-a.bin"), Path.GetFullPath("tp-b.bin"))
        });
        Check(["--workflow=ab-merge", "--ic=NT51950", "--ic-num=single",
            "--dp=folder with spaces/dp=1.bin", "--tp-a=a.bin", "--tp-b=b.bin"], new()
            {
                Page = ShellPage.Merge,
                AbMerge = new("NT51950", "single", Path.GetFullPath("folder with spaces/dp=1.bin"),
                Path.GetFullPath("a.bin"), Path.GetFullPath("b.bin"))
            });
        (string Option, string Value)[] duplicateOptions = [
            ("--ic", "NT51951"), ("--workflow", "ctrlram-replace"), ("--dp", "other.bin"),
            ("--tp-a", "other.bin"), ("--tp-b", "other.bin")
        ];
        foreach ((string option, string value) in duplicateOptions)
        {
            Check([.. arguments, option, value], Rejected($"Duplicate option '{option}'."));
        }
        (string Option, string Value)[] conflictingOptions = [
            ("--base", "base.bin"), ("--ctrlram", "replace-ctrlram-nf=nf.bin")
        ];
        foreach ((string option, string value) in conflictingOptions)
        {
            Check([.. arguments, option, value], Rejected(
                "AB startup cannot be combined with CtrlRAM or Standard Merge inputs."));
        }
        Check([.. arguments, "--page", "replace"], Rejected(
            "AB startup cannot be combined with another page, Settings or report loading.") with
        {
            Page = ShellPage.Replace
        });
        Check([.. arguments, "--page", "settings"], Rejected(
            "AB startup cannot be combined with another page, Settings or report loading.") with
        {
            Page = ShellPage.Home,
            OpenSettings = true
        });
        Check([.. arguments, "--report", "run.json"], Rejected(
            "AB startup cannot be combined with another page, Settings or report loading.") with
        {
            ReportPath = "run.json"
        });
        Check([.. arguments, "--open-report", ""], new()
        {
            OpenReport = true,
            Issues = [
                "AB startup cannot be combined with another page, Settings or report loading.",
                "Unsupported input startup argument ''."]
        });
        Check([.. arguments, "--build", "true"], new()
        {
            Issues = [
            "Unsupported input startup argument '--build'.", "Unsupported input startup argument 'true'."]
        });
        int[] missingOptionOffsets = [2, 4, 6, 8, 10];
        foreach (int offset in missingOptionOffsets)
        {
            string option = arguments[offset];
            Check([.. arguments.Take(offset), .. arguments.Skip(offset + 2)],
                Rejected($"AB startup requires {option}."));
        }
        Check([.. arguments.Skip(2)], new()
        {
            Issues = [
            "Merge input options require the corresponding Merge workflow and cannot be combined with CtrlRAM inputs.",
            "CtrlRAM startup requires --workflow.", "CtrlRAM startup requires --base.",
            "Input startup supports only --workflow ctrlram-replace.",
            "CtrlRAM startup requires at least one --ctrlram <slot-id>=<path>."]
        });
        string[] firstPages = ["replace", "merge"];
        foreach (string firstPage in firstPages)
        {
            Check([.. arguments, "--page", firstPage, "--page", "merge"],
                Rejected("Duplicate option '--page'.") with { Page = ShellPage.Merge });
        }
    }

    /// <summary>Standard Merge preload retains its typed fields and conflict admission.</summary>
    [Fact]
    public void StandardMergeVectorsKeepCompleteResults()
    {
        string[] arguments = ["--workflow", "standard-merge", "--ic", "NT51926",
            "--ic-num", "single", "--dp", "dp.bin", "--tp", "tp.bin"];
        Check(arguments, new()
        {
            Page = ShellPage.Merge,
            StandardMerge = new("NT51926", "single", Path.GetFullPath("dp.bin"), Path.GetFullPath("tp.bin"))
        });
        Check([.. arguments, "--tp-a", "other.bin"], Rejected(
            "Standard Merge startup cannot be combined with AB or CtrlRAM inputs."));
        Check([.. arguments, "--base", "other.bin"], Rejected(
            "Standard Merge startup cannot be combined with AB or CtrlRAM inputs."));
        Check([.. arguments, "--page", "replace"], Rejected(
            "Standard Merge startup cannot be combined with another page, Settings or report loading.") with
        {
            Page = ShellPage.Replace
        });
        Check([.. arguments, "--build", "true"], new()
        {
            Issues = [
            "Unsupported input startup argument '--build'.", "Unsupported input startup argument 'true'."]
        });
        Check(arguments[..^2], Rejected("Standard Merge startup requires --tp."));
    }

    private static Expected Rejected(string issue)
    {
        return new() { Issues = [issue] };
    }

    private static void Check(string[] arguments, Expected expected)
    {
        UiLaunchOptions actual = UiLaunchOptions.Parse(arguments, new LocalFileStore());
        Assert.Equal(expected.Page, actual.Page);
        Assert.Equal(expected.OpenSettings, actual.OpenSettings);
        Assert.Equal(expected.ReportPath, actual.ReportPath);
        Assert.Equal(expected.OpenReport, actual.OpenReport);
        Assert.Equal<string>(expected.Issues, actual.Issues);
        Assert.Equal(expected.Help, actual.Help);
        Assert.Equal(expected.CapturePath, actual.CapturePath);
        Assert.Equal(expected.Overwrite, actual.OverwriteCapture);
        Assert.Equal(expected.Scripted, actual.IsScriptedRequest);
        Assert.Equal(expected.CtrlRam is not null || expected.AbMerge is not null || expected.StandardMerge is not null,
            actual.HasStartupInputs);
        Assert.Equal(expected.AbMerge, actual.AbMerge);
        Assert.Equal(expected.StandardMerge, actual.StandardMerge);
        CtrlRamLaunchRequest? expectedCtrlRam = expected.CtrlRam;
        if (expectedCtrlRam is null)
        {
            Assert.Null(actual.CtrlRam);
        }
        else
        {
            CtrlRamLaunchRequest request = Assert.IsType<CtrlRamLaunchRequest>(actual.CtrlRam);
            Assert.Equal(expectedCtrlRam.IcId, request.IcId);
            Assert.Equal(expectedCtrlRam.Number, request.Number);
            Assert.Equal(expectedCtrlRam.BasePath, request.BasePath);
            Assert.Equal<CtrlRamLaunchInput>(expectedCtrlRam.Inputs, request.Inputs);
        }
    }

    private sealed record Expected
    {
        internal ShellPage? Page { get; init; }
        internal bool OpenSettings { get; init; }
        internal string? ReportPath { get; init; }
        internal bool OpenReport { get; init; }
        internal string[] Issues { get; init; } = [];
        internal CtrlRamLaunchRequest? CtrlRam { get; init; }
        internal AbMergeLaunchRequest? AbMerge { get; init; }
        internal StandardMergeLaunchRequest? StandardMerge { get; init; }
        internal bool Help { get; init; }
        internal string? CapturePath { get; init; }
        internal bool Overwrite { get; init; }
        internal bool Scripted { get; init; }
    }
}
