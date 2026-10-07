using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Strict public requests and production PNG capture of the real report modal.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed partial class DesktopCaptureTests
{
    /// <summary>Every later-slice option remains unknown, even without input preload.</summary>
    [Theory]
    [InlineData("--report-tab")]
    [InlineData("--window-size")]
    [InlineData("--ldc")]
    [InlineData("--ab-dp-mode")]
    [InlineData("--ack-ab-dummy-dp")]
    [InlineData("--ctrlram-banks")]
    [InlineData("--open-workflow-setup")]
    [InlineData("--input-details")]
    [InlineData("--open-history")]
    [InlineData("--diagnostics")]
    [InlineData("--settings-section")]
    [InlineData("--load-bin")]
    [InlineData("--open-build-settings")]
    [InlineData("--unknown")]
    [InlineData("positional")]
    [InlineData("--HELP")]
    [InlineData("--open-report=true")]
    public void UnknownOptionsAreRefusedBeforeComposition(string option)
    {
        AssertRefused(["--capture=out.png", option], "Unsupported startup argument");
    }

    /// <summary>Singletons, aliases, blanks, missing values and conflicting modals report their actual cause.</summary>
    [Theory]
    [InlineData("--report requires a value", "--report", "", "--open-report")]
    [InlineData("--report requires a value", "--report= ")]
    [InlineData("--report requires a value", "--report")]
    [InlineData("--page requires a value", "--page=")]
    [InlineData("--page requires a value", "--page", " ")]
    [InlineData("Duplicate option '--page'", "--page", "home", "--page=merge")]
    [InlineData("Duplicate report option", "--report=a.json", "--load-report=b.json")]
    [InlineData("Duplicate report option", "--report=a.json", "--report=b.json")]
    [InlineData("Duplicate option '--open-report'", "--report=a.json", "--open-report", "--open-report")]
    [InlineData("--open-report requires", "--open-report")]
    [InlineData("--page settings cannot", "--report=a.json", "--page=settings")]
    [InlineData("--help must be used alone", "--help", "--page=home")]
    [InlineData("Duplicate option '--help'", "--help", "--help")]
    [InlineData("--overwrite-capture requires", "--help", "--overwrite-capture")]
    [InlineData("--capture requires a value", "--capture")]
    [InlineData("--capture requires a value", "--capture= ")]
    [InlineData("target is not available", "--page=home", "--capture=out.png")]
    [InlineData("target is not available", "--report=a.json", "--capture=out.png")]
    [InlineData("Duplicate option '--capture'", "--report=a.json", "--open-report", "--capture=a.png", "--capture=b.png")]
    [InlineData("Duplicate option '--overwrite-capture'", "--report=a.json", "--open-report", "--capture=a.png", "--overwrite-capture", "--overwrite-capture")]
    public void MalformedRequestsExit64(string diagnostic, params string[] arguments)
    {
        AssertRefused(UiLaunchOptions.RequestsScriptedCompletion(arguments)
            ? arguments : [.. arguments, "--capture=out.png"], diagnostic);
    }

    /// <summary>Strict parsing refuses invalid UI requests only when capture or help is present.</summary>
    [Theory]
    [InlineData("--capture")]
    [InlineData("--capture=")]
    [InlineData("--help=value")]
    [InlineData("--help", "--unknown")]
    [InlineData("--help", "--page", "home", "--page", "merge")]
    public void InvalidScriptedArgumentsNeverReachHostConstruction(params string[] arguments)
    {
        using var workspace = TempWorkspace.Create("scripted-arguments");
        int hostConstructions = 0;
        int exit = DesktopApplication.Run(() =>
        {
            hostConstructions++;
            throw new InvalidOperationException("Host factory reached.");
        }, new NvtFwCombiner.Infrastructure.Files.LocalFileStore(), workspace.Root, arguments);
        Assert.Equal(64, exit);
        Assert.Equal(0, hostConstructions);
    }

    /// <summary>Report aliases and open flags retain their interactive last-value/recoverable behavior.</summary>
    [Fact]
    public void OrdinaryReportArgumentsKeepExistingRecoveryAndAliasBehavior()
    {
        UiLaunchOptions options = ParseOptions(
            ["--report=first.json", "--load-report=last.json", "--open-report", "--open-report", "--unknown"]);
        Assert.Equal("last.json", options.ReportPath);
        Assert.Empty(options.Issues);
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Null(DesktopApplication.CompletePublicRequest(options, output, error));
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>Help prints only shipped public options and never constructs the host.</summary>
    [Fact]
    public void HelpCompletesBeforeHostCompositionAndListsOnlyShippedOptions()
    {
        UiLaunchOptions options = ParseOptions(["--help"]);
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, DesktopApplication.CompletePublicRequest(options, output, error));
        Assert.Contains("--capture", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--workflow", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("--report-tab", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("--theme", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("--managed-root", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        using var workspace = TempWorkspace.Create("desktop-help");
        int constructions = 0;
        int exit = DesktopApplication.Run(() =>
        {
            constructions++;
            throw new InvalidOperationException("Help must not construct a host.");
        }, new NvtFwCombiner.Infrastructure.Files.LocalFileStore(), workspace.Root, ["--help"]);
        Assert.Equal(0, exit);
        Assert.Equal(0, constructions);
    }

    /// <summary>Both path syntaxes normalize relative destinations without changing the working directory.</summary>
    [Fact]
    public void AbsoluteAndRelativeCaptureDestinationsHaveTheSameIdentity()
    {
        using var workspace = TempWorkspace.Create("capture-path");
        string report = workspace.PathFor("report.json");
        string destination = workspace.PathFor("capture.png");
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, destination);
        UiLaunchOptions separated = ParseOptions(["--report", report, "--open-report", "--capture", destination]);
        UiLaunchOptions inline = ParseOptions([$"--load-report={report}", "--open-report", $"--capture={relative}"]);
        Assert.Empty(separated.Issues);
        Assert.Empty(inline.Issues);
        Assert.Equal(destination, separated.CapturePath);
        Assert.Equal(destination, inline.CapturePath);
        Assert.False(File.Exists(destination));
    }

    /// <summary>Unsafe destinations are refused before any file or staging artifact exists.</summary>
    [Fact]
    public void InvalidDestinationsAndExistingFilesAreRefusedWithoutWrites()
    {
        using var workspace = TempWorkspace.Create("capture-refusals");
        string report = workspace.Write("report.png", [1, 2, 3]);
        string old = workspace.Write("old.png", [4, 5, 6]);
        string directory = workspace.PathFor("directory.png");
        _ = Directory.CreateDirectory(directory);
        string[] invalid = [workspace.PathFor("missing/out.png"), directory,
            workspace.PathFor("out.jpg"), workspace.Root, report, old];
        foreach (string destination in invalid)
        {
            AssertRefused(["--report", report, "--open-report", "--capture", destination], "--capture");
        }
        Assert.Equal([1, 2, 3], File.ReadAllBytes(report));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(old));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp"));
        Assert.False(Directory.Exists(workspace.PathFor("missing")));
    }

    /// <summary>A differently spelled report destination is refused even when overwrite is authorized.</summary>
    [Fact]
    public void DifferentlySpelledReportAliasIsRefusedEvenWithOverwrite()
    {
        using var workspace = TempWorkspace.Create("capture-spelling-alias");
        string report = workspace.Write("report.png", [1, 2, 3]);
        string destination = Path.GetRelativePath(Environment.CurrentDirectory,
            Path.Combine(workspace.Root, ".", "report.png"));

        AssertRefused(["--report", report, "--open-report", "--capture", destination, "--overwrite-capture"], "aliases");

        Assert.Equal([1, 2, 3], File.ReadAllBytes(report));
    }

    /// <summary>Windows physical file IDs reject a differently named hard-link destination.</summary>
    [Fact]
    public void HardLinkReportAliasIsRefusedEvenWithOverwrite()
    {
        using var workspace = TempWorkspace.Create("capture-hardlink");
        string report = workspace.Write("report.json", [1, 2, 3]);
        string destination = workspace.PathFor("alias.png");
        bool linked = OperatingSystem.IsWindows() ? CreateHardLink(destination, report, IntPtr.Zero)
            : CreateUnixHardLink(UnixPath(report), UnixPath(destination)) == 0;
        Assert.True(linked, new Win32Exception(Marshal.GetLastPInvokeError()).Message);
        AssertRefused(["--report", report, "--open-report", "--capture", destination, "--overwrite-capture"], "aliases");
        Assert.Equal([1, 2, 3], File.ReadAllBytes(report));
    }

    /// <summary>Capture refuses hard links to either persisted state owner before host composition.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaptureCannotOverwriteHistoryOrPreferencesAliases(bool preferences)
    {
        using var workspace = TempWorkspace.Create("capture-state-alias");
        string protectedPath = preferences ? ShellPreferenceFileStore.PathIn(workspace.Root)
            : ReportHistoryFileStore.PathIn(workspace.Root);
        File.WriteAllBytes(protectedPath, [4, 3, 2, 1]);
        string destination = workspace.PathFor("state.png");
        bool linked = OperatingSystem.IsWindows() ? CreateHardLink(destination, protectedPath, IntPtr.Zero)
            : CreateUnixHardLink(UnixPath(protectedPath), UnixPath(destination)) == 0;
        Assert.True(linked, new Win32Exception(Marshal.GetLastPInvokeError()).Message);
        int constructions = 0;
        int exit = DesktopApplication.Run(() =>
        {
            constructions++;
            throw new InvalidOperationException("Aliasing must not compose a host.");
        }, new NvtFwCombiner.Infrastructure.Files.LocalFileStore(), workspace.Root,
            ["--report=report.json", "--open-report", $"--capture={destination}", "--overwrite-capture"]);
        Assert.Equal(64, exit);
        Assert.Equal(0, constructions);
        Assert.Equal([4, 3, 2, 1], File.ReadAllBytes(protectedPath));
    }

    /// <summary>The real XAML modal is rendered to a decodable full-size PNG and closes once.</summary>
    [AvaloniaFact]
    public async Task SavedReportModalProducesWindowSizedPngAndClosesExactlyOnce()
    {
        using var workspace = TempWorkspace.Create("capture-report-modal");
        string report = workspace.PathFor("report.json");
        await File.WriteAllTextAsync(report, ReportJsonSamples.Succeeded(runId: "captured-report"), TestContext.Current.CancellationToken);
        string destination = workspace.PathFor("report.png");
        using MainWindow window = await CreateWindowAsync(workspace, report, destination);
        var observation = new ObservedCapture();
        window.LaunchCoordinator.CaptureSession.ScreenshotCapture = observation;
        MainWindowViewModel viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        bool confirmationShown = false;
        viewModel.Navigation.PropertyChanged += (_, _) =>
            confirmationShown |= viewModel.Navigation.IsNavigationClearConfirmationOpen;
        int closed = 0;
        window.Closed += (_, _) => closed++;
        Task closure = WhenClosed(window);
        window.Show();
        await window.LaunchCoordinator.CaptureSession.CaptureFrameRequested.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.True(viewModel.HasSelectedFiles);
        Assert.True(viewModel.Reports.HasLoadedReport);
        DriveFrame(window);
        await closure.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(0, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
        Assert.Equal(1, closed);
        Assert.False(confirmationShown);
        Assert.False(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.True(new FileInfo(destination).Length > 0);
        using var decoded = new Bitmap(destination);
        Assert.Equal(observation.ClientPixels, decoded.PixelSize);
        Assert.True(HasPixelContent(decoded));
        using var blank = new RenderTargetBitmap(decoded.PixelSize);
        Assert.False(HasPixelContent(blank));
        Assert.Equal("captured-report", observation.RunId);
        Assert.True(observation.HasVisibleModal);
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp"));
    }

    /// <summary>Capture never changes History, preferences or any existing local-state bytes, even during close.</summary>
    [AvaloniaTheory]
    [InlineData("success", 0)]
    [InlineData("failure", 1)]
    [InlineData("cancel", 70)]
    public async Task CaptureLeavesHistoryPreferencesAndLocalStateByteIdentical(string outcome, int expectedExit)
    {
        using var workspace = TempWorkspace.Create("capture-no-persistence");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        string historyPath = ReportHistoryFileStore.PathIn(services.LocalStateDirectory);
        string preferencesPath = ShellPreferenceFileStore.PathIn(services.LocalStateDirectory);
        MainWindowViewModel previous = PresentationTestHost.CreateViewModel();
        previous.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "history-before"), "previous.json");
        await ReportHistoryFileStore.SaveAsync(services.LocalFiles, historyPath,
            previous.Reports.ExportReportHistory(), TestContext.Current.CancellationToken);
        await ShellPreferenceFileStore.SaveAsync(services.LocalFiles, preferencesPath,
            new("Dark", "Traditional Chinese", true, true), TestContext.Current.CancellationToken);
        // Noncanonical whitespace detects even a save that serializes otherwise identical state.
        await File.AppendAllTextAsync(historyPath, "\n ", TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(preferencesPath, "\n ", TestContext.Current.CancellationToken);
        string report = workspace.PathFor("report.json");
        await File.WriteAllTextAsync(report, ReportJsonSamples.Succeeded(runId: "capture-only"), TestContext.Current.CancellationToken);
        Dictionary<string, byte[]> before = Directory.GetFiles(workspace.Root, "*", SearchOption.AllDirectories)
            .ToDictionary(static path => path, File.ReadAllBytes);
        string destination = workspace.PathFor("out.png");
        var observedFiles = new PendingReportFiles(services.LocalFiles, report, passThrough: true);
        services = ReplaceFiles(services, observedFiles);
        using var window = new MainWindow(
            ParseOptions(["--report", report, "--open-report", "--capture", destination]),
            StartupTraceSession.Disabled, services, await ShellPreferenceFileStore.LoadAsync(services.LocalFiles, preferencesPath));
        MainWindowViewModel viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        if (outcome == "failure") { window.LaunchCoordinator.CaptureSession.ScreenshotCapture = new FaultCapture("encode"); }
        Task closure = WhenClosed(window);
        window.Show();
        await window.LaunchCoordinator.CaptureSession.CaptureFrameRequested.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal("Dark", viewModel.SelectedTheme);
        Assert.True(viewModel.IsReducedMotionEnabled);
        Assert.Equal("Traditional Chinese", viewModel.SelectedLanguage);
        Assert.Equal("previous.json", Assert.Single(viewModel.Reports.ReportHistoryEntries).SourceName);
        // Exercise the normal preference-queue trigger as well as automatic History restoration.
        viewModel.SelectedTheme = "Light";
        if (outcome == "cancel") { window.Close(); }
        else { DriveFrame(window); }
        await closure.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(expectedExit, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
        Assert.All(observedFiles.Writes, path => Assert.Equal(destination, path));
        if (outcome == "success") { _ = Assert.Single(observedFiles.Writes); }
        else { Assert.Empty(observedFiles.Writes); }
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(workspace.Root, "*", SearchOption.AllDirectories)
            .Where(path => path != destination).Order());
        foreach ((string path, byte[] bytes) in before) { Assert.Equal(bytes, File.ReadAllBytes(path)); }
    }

    /// <summary>Malformed, oversized, directory and missing reports fail without a retry prompt or PNG.</summary>
    [AvaloniaTheory]
    [InlineData("malformed")]
    [InlineData("oversized")]
    [InlineData("missing")]
    [InlineData("rejected")]
    [InlineData("directory")]
    public async Task FailedReportLoadsExit1WithoutPngAndCloseOnce(string kind)
    {
        using var workspace = TempWorkspace.Create("capture-report-failure");
        string report = workspace.PathFor("report.json");
        if (kind == "malformed")
        {
            await File.WriteAllTextAsync(report, "{broken", TestContext.Current.CancellationToken);
        }
        else if (kind == "rejected")
        {
            await File.WriteAllTextAsync(report, "[]", TestContext.Current.CancellationToken);
        }
        else if (kind == "directory")
        {
            _ = Directory.CreateDirectory(report);
        }
        else if (kind == "oversized")
        {
            await using FileStream stream = File.Create(report);
            stream.SetLength((10L * 1024 * 1024) + 1);
        }
        string destination = workspace.PathFor("out.png");
        using MainWindow window = await CreateWindowAsync(workspace, report, destination);
        using var error = new StringWriter();
        window.LaunchCoordinator.CaptureSession.CaptureError = error;
        int closed = 0;
        window.Closed += (_, _) => closed++;
        Task closure = WhenClosed(window);
        window.Show();
        await closure.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(1, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
        Assert.Equal(1, closed);
        Assert.Contains("report --capture", error.ToString(), StringComparison.Ordinal);
        Assert.Contains(report, error.ToString(), StringComparison.Ordinal);
        Assert.Contains(destination, error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp"));
    }

    /// <summary>A failed required preload closes rather than leaving a retry surface awaiting a user.</summary>
    [AvaloniaFact]
    public async Task FailedRequiredPreloadExits1AndClosesExactlyOnce()
    {
        using var workspace = TempWorkspace.Create("capture-preload-failure");
        string report = workspace.PathFor("report.json");
        string destination = workspace.PathFor("out.png");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        var decorated = new PresentationHostServices(services.Composition, services.FileReveal,
            services.SupportMatrix, services.SystemInformation, services.SystemDiagnosticsExporter,
            services.RawBinaryEditorFileSessions, new FailedCatalogLoader(), services.ExternalEnvironmentLoader,
            services.LocalFiles, services.LocalStateDirectory);
        using var window = new MainWindow(ParseOptions(["--report", report, "--open-report", "--capture", destination]),
            StartupTraceSession.Disabled, decorated, ShellPreferenceSnapshot.Default);
        using var error = new StringWriter();
        window.LaunchCoordinator.CaptureSession.CaptureError = error;
        int closed = 0;
        window.Closed += (_, _) => closed++;
        Task closure = WhenClosed(window);
        window.Show();
        await closure.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(1, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
        Assert.Equal(1, closed);
        Assert.Contains("preload --capture", error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(destination));
    }

    /// <summary>Cancellation during a real pending report load exits 70 and closes once.</summary>
    [AvaloniaFact]
    public async Task CancelledReportLoadExits70WithoutPngAndClosesOnce()
    {
        using var workspace = TempWorkspace.Create("capture-report-cancel");
        string report = workspace.PathFor("report.json");
        string destination = workspace.PathFor("out.png");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        var blocked = new PendingReportFiles(services.LocalFiles, report);
        PresentationHostServices decorated = ReplaceFiles(services, blocked);
        using var window = new MainWindow(ParseOptions(["--report", report, "--open-report", "--capture", destination]),
            StartupTraceSession.Disabled, decorated, ShellPreferenceSnapshot.Default);
        using var error = new StringWriter();
        window.LaunchCoordinator.CaptureSession.CaptureError = error;
        int closed = 0;
        window.Closed += (_, _) => closed++;
        Task closure = WhenClosed(window);
        window.Show();
        await blocked.Started.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        window.Close();
        await closure.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(70, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
        Assert.Equal(1, closed);
        Assert.Contains("cancel", error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(destination));
    }

    /// <summary>Unexpected report adapter exceptions keep exit 70 instead of being flattened into stage failure 1.</summary>
    [AvaloniaFact]
    public async Task UnexpectedReportReadExits70WithoutPngAndClosesOnce()
    {
        using var workspace = TempWorkspace.Create("capture-report-unexpected");
        string report = workspace.PathFor("report.json");
        string destination = workspace.PathFor("out.png");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        PresentationHostServices decorated = ReplaceFiles(services,
            new PendingReportFiles(services.LocalFiles, report, unexpected: true));
        using var window = new MainWindow(ParseOptions(["--report", report, "--open-report", "--capture", destination]),
            StartupTraceSession.Disabled, decorated, ShellPreferenceSnapshot.Default);
        int closed = 0;
        window.Closed += (_, _) => closed++;
        Task closure = WhenClosed(window);
        window.Show();
        await closure.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(70, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
        Assert.Equal(1, closed);
        Assert.False(File.Exists(destination));
    }

    /// <summary>Target capture finishes even while unrelated external environment preparation is pending.</summary>
    [AvaloniaFact]
    public async Task CaptureDoesNotAwaitUnrelatedOptionalPreparation()
    {
        using var workspace = TempWorkspace.Create("capture-pending-optional");
        string report = workspace.PathFor("report.json");
        await File.WriteAllTextAsync(report, ReportJsonSamples.Succeeded(), TestContext.Current.CancellationToken);
        string destination = workspace.PathFor("out.png");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        var pending = new PendingEnvironmentLoader(services.ExternalEnvironmentLoader);
        var decorated = new PresentationHostServices(services.Composition, services.FileReveal,
            services.SupportMatrix, services.SystemInformation, services.SystemDiagnosticsExporter,
            services.RawBinaryEditorFileSessions, services.CanonicalCatalogLoader, pending,
            services.LocalFiles, services.LocalStateDirectory);
        using var window = new MainWindow(ParseOptions(["--report", report, "--open-report", "--capture", destination]),
            StartupTraceSession.Disabled, decorated, ShellPreferenceSnapshot.Default);
        Task closure = WhenClosed(window);
        window.Show();
        await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        await window.LaunchCoordinator.CaptureSession.CaptureFrameRequested.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.False(pending.WasCancelled);
        DriveFrame(window);
        await closure.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(0, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
        Assert.True(pending.WasCancelled);
        using var bitmap = new Bitmap(destination);
        Assert.True(bitmap.PixelSize.Width > 0);
    }

    /// <summary>Drawing, encoding, saving, replacement and unexpected faults terminate deterministically.</summary>
    [AvaloniaTheory]
    [InlineData("draw", 1)]
    [InlineData("encode", 1)]
    [InlineData("write", 1)]
    [InlineData("replace", 1)]
    [InlineData("unexpected", 70)]
    [InlineData("cancel", 70)]
    public async Task CaptureFailuresPreserveOldBytesCleanStagingAndCloseOnce(string fault, int expectedExit)
    {
        using var workspace = TempWorkspace.Create("capture-publication-failure");
        string report = workspace.PathFor("report.json");
        await File.WriteAllTextAsync(report, ReportJsonSamples.Succeeded(), TestContext.Current.CancellationToken);
        string destination = workspace.Write("out.png", [9, 8, 7, 6]);
        using MainWindow window = await CreateWindowAsync(workspace, report, destination, overwrite: true);
        window.LaunchCoordinator.CaptureSession.ScreenshotCapture = new FaultCapture(fault);
        using var error = new StringWriter();
        window.LaunchCoordinator.CaptureSession.CaptureError = error;
        int closed = 0;
        window.Closed += (_, _) => closed++;
        Task closure = WhenClosed(window);
        window.Show();
        await window.LaunchCoordinator.CaptureSession.CaptureFrameRequested.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        DriveFrame(window);
        await closure.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(expectedExit, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
        Assert.Equal(1, closed);
        Assert.Contains(destination, error.ToString(), StringComparison.Ordinal);
        Assert.Equal([9, 8, 7, 6], File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp"));
    }

    /// <summary>Atomic replacement commits new PNG bytes; a competing no-overwrite writer retains its bytes.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicationHonorsOverwriteAndDestinationCreationRace(bool overwrite)
    {
        using var workspace = TempWorkspace.Create("capture-publication-race");
        string report = workspace.PathFor("report.json");
        await File.WriteAllTextAsync(report, ReportJsonSamples.Succeeded(), TestContext.Current.CancellationToken);
        string destination = workspace.PathFor("out.png");
        if (overwrite) { _ = workspace.Write("out.png", [5, 4, 3]); }
        using MainWindow window = await CreateWindowAsync(workspace, report, destination, overwrite);
        if (!overwrite) { window.LaunchCoordinator.CaptureSession.ScreenshotCapture = new RacingCapture(destination); }
        Task closure = WhenClosed(window);
        window.Show();
        await window.LaunchCoordinator.CaptureSession.CaptureFrameRequested.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        DriveFrame(window);
        await closure.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        if (overwrite)
        {
            Assert.Equal(0, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
            using var bitmap = new Bitmap(destination);
            Assert.True(bitmap.PixelSize.Width > 0);
        }
        else
        {
            Assert.Equal(1, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
            Assert.Equal([5, 4, 3], File.ReadAllBytes(destination));
        }
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp"));
    }

    /// <summary>A valid ordinary launch retains an interactive window and no capture terminal code.</summary>
    [AvaloniaFact]
    public async Task OrdinaryReportLaunchStaysInteractive()
    {
        using var workspace = TempWorkspace.Create("ordinary-report-launch");
        string report = workspace.PathFor("report.json");
        await File.WriteAllTextAsync(report, ReportJsonSamples.Succeeded(), TestContext.Current.CancellationToken);
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(ParseOptions(["--report", report, "--open-report"]),
            StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        try
        {
            await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            Assert.True(window.IsVisible);
            Assert.True(Assert.IsType<MainWindowViewModel>(window.DataContext).Reports.IsReportModalOpen);
            Assert.Null(window.LaunchCoordinator.CaptureSession.CaptureExitCode);
            Assert.Equal(WindowClosePhase.Open, window.ClosePhase);
        }
        finally
        {
            await ReportControlTestHost.CloseAndFlushAsync(window);
        }
    }

    /// <summary>A real locked destination fails atomic promotion and leaves only the original bytes.</summary>
    [AvaloniaFact]
    public async Task LockedPromotionPreservesOldPngCleansStagingAndClosesOnce()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        using var workspace = TempWorkspace.Create("capture-locked-promotion");
        string report = workspace.Write("report.json", Encoding.UTF8.GetBytes(ReportJsonSamples.Succeeded()));
        string destination = workspace.Write("out.png", [9, 8, 7, 6]);
        using MainWindow window = await CreateWindowAsync(workspace, report, destination, overwrite: true);
        using var error = new StringWriter();
        window.LaunchCoordinator.CaptureSession.CaptureError = error;
        int closed = 0;
        window.Closed += (_, _) => closed++;
        Task closure = WhenClosed(window);
        window.Show();
        await window.LaunchCoordinator.CaptureSession.CaptureFrameRequested.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        await using (var locked = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            DriveFrame(window);
            await closure.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        }
        Assert.Equal(1, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
        Assert.Equal(1, closed);
        Assert.Contains("publish --capture", error.ToString(), StringComparison.Ordinal);
        Assert.Equal([9, 8, 7, 6], File.ReadAllBytes(destination));
        string[] expectedFiles = [destination, report];
        Assert.Equal(expectedFiles.Order(), Directory.GetFiles(workspace.Root).Order());
    }

    /// <summary>The single deadline terminates a stalled report read or an unrendered frame with its phase.</summary>
    [AvaloniaTheory]
    [InlineData(true, "report")]
    [InlineData(false, "layout/frame")]
    public async Task CaptureDeadlineClosesStalledPreparationOnce(bool stallRead, string phase)
    {
        using var workspace = TempWorkspace.Create("capture-deadline");
        string report = workspace.Write("report.json", Encoding.UTF8.GetBytes(ReportJsonSamples.Succeeded()));
        string destination = workspace.PathFor("out.png");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        var blocked = new PendingReportFiles(services.LocalFiles, report);
        using var window = new MainWindow(ParseOptions(["--report", report, "--open-report", "--capture", destination]),
            StartupTraceSession.Disabled, stallRead ? ReplaceFiles(services, blocked) : services, ShellPreferenceSnapshot.Default);
        var deadline = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.LaunchCoordinator.CaptureSession.DeadlineFactory = (duration, token) =>
        {
            Assert.Equal(TimeSpan.FromSeconds(300), duration);
            return deadline.Task.WaitAsync(token);
        };
        using var error = new StringWriter();
        window.LaunchCoordinator.CaptureSession.CaptureError = error;
        int closed = 0;
        window.Closed += (_, _) => closed++;
        Task closure = WhenClosed(window);
        window.Show();
        await (stallRead ? blocked.Started.Task : window.LaunchCoordinator.CaptureSession.CaptureFrameRequested)
            .WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        _ = deadline.TrySetResult();
        await closure.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(70, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
        Assert.Equal(1, closed);
        Assert.Contains($"{phase} --capture", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("300 seconds expired", error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(destination));
        Assert.Equal([report], Directory.GetFiles(workspace.Root));
    }

    /// <summary>A parent alias is refused through the port before composition, even when it exists.</summary>
    [Fact]
    public void ParentWhoseRealPathDiffersIsRefusedBeforeComposition()
    {
        using var workspace = TempWorkspace.Create("capture-parent-spelling");
        var files = new PendingReportFiles(new NvtFwCombiner.Infrastructure.Files.LocalFileStore(), "unused")
        {
            DestinationInfo = new(true, false, false, false),
        };
        int composed = 0;
        int exit = DesktopApplication.Run(() =>
        {
            composed++;
            throw new InvalidOperationException("Refused capture composed a host.");
        }, files, workspace.Root,
            ["--report", workspace.PathFor("report.json"), "--open-report", "--capture", workspace.PathFor("out.png")]);
        Assert.Equal(64, exit);
        Assert.Equal(0, composed);
        UiLaunchOptions parsed = UiLaunchOptions.Parse(
            ["--report", "report.json", "--open-report", "--capture", workspace.PathFor("out.png")], files);
        Assert.Contains(parsed.Issues, issue => issue.Contains("folder must be given by its real path", StringComparison.Ordinal));
        Assert.Empty(Directory.GetFiles(workspace.Root));
    }

    /// <summary>A future target can provide readiness without adding a saved-report preload stage.</summary>
    [AvaloniaFact]
    public async Task TargetHooksCanCaptureWithoutAReportStage()
    {
        using var workspace = TempWorkspace.Create("capture-target-hook");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        MainWindowViewModel viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        // Simulate a later slice's target hook directly; public slice-1 parsing still refuses this target.
        UiLaunchOptions captureOptions = ParseOptions(["--page", "home", "--capture", workspace.PathFor("out.png")]);
        using var preload = new ShellPreloadSession(_ => { }, viewModel.Text, includeStartupReport: false);
        var context = new DesktopLaunchContext(window, viewModel, captureOptions, services, new(true));
        int readinessCalls = 0;
        int publicationCalls = 0;
        context.TargetReadiness += (_, _) => { readinessCalls++; return Task.CompletedTask; };
        context.AfterTargetPublication += (_, _) => { publicationCalls++; return Task.CompletedTask; };
        int closes = 0;
        var session = new DesktopCaptureSession(window, captureOptions, StartupTraceSession.Disabled, services, preload,
            () => { closes++; window.Close(); })
        {
            ScreenshotCapture = new FaultCapture("draw"),
        };
        window.Show();
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Task capture = session.RunTargetCaptureAsync(context, TestContext.Current.CancellationToken);
        await session.CaptureFrameRequested.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        DriveFrame(window);
        await capture.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        await window.CloseAttempt.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(1, session.CaptureExitCode);
        Assert.Equal(3, readinessCalls);
        Assert.Equal(1, publicationCalls);
        Assert.Equal(1, closes);
        Assert.DoesNotContain(preload.Stages, stage => stage.Id == ShellPreloadSession.ReportStageId);
    }

    private static bool HasPixelContent(Bitmap bitmap)
    {
        int stride = checked(bitmap.PixelSize.Width * 4);
        byte[] pixels = new byte[checked(stride * bitmap.PixelSize.Height)];
        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), handle.AddrOfPinnedObject(), pixels.Length, stride);
        }
        finally
        {
            handle.Free();
        }
        // A blank bitmap (transparent, solid black or solid white) must fail this content gate.
        var colors = new HashSet<int>();
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            _ = colors.Add(BitConverter.ToInt32(pixels, offset));
            if (colors.Count >= 32) { return true; }
        }
        return false;
    }

    private static UiLaunchOptions ParseOptions(string[] arguments)
    {
        return UiLaunchOptions.Parse(arguments, new NvtFwCombiner.Infrastructure.Files.LocalFileStore());
    }

    private static void AssertRefused(string[] arguments, string? diagnostic)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        UiLaunchOptions options = ParseOptions(arguments);
        Assert.Equal(64, DesktopApplication.CompletePublicRequest(options, output, error));
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("validation:", error.ToString(), StringComparison.Ordinal);
        if (diagnostic is not null) { Assert.Contains(diagnostic, error.ToString(), StringComparison.Ordinal); }
    }

    private static async Task<PresentationHostServices> CreateServicesAsync(TempWorkspace workspace)
    {
        return await Task.Run(() => PresentationTestHost.CreateServices("ui-smoke",
            static authoring => authoring, workspace.Root), TestContext.Current.CancellationToken);
    }

    private static async Task<MainWindow> CreateWindowAsync(
        TempWorkspace workspace, string report, string destination, bool overwrite = false)
    {
        string[] overwriteArguments = overwrite ? ["--overwrite-capture"] : [];
        string[] arguments = ["--report", report, "--open-report", "--capture", destination, .. overwriteArguments];
        UiLaunchOptions options = ParseOptions(arguments);
        Assert.Empty(options.Issues);
        return new MainWindow(options, StartupTraceSession.Disabled, await CreateServicesAsync(workspace),
            ShellPreferenceSnapshot.Default);
    }

    private static Task WhenClosed(Window window)
    {
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        return closed.Task;
    }

    private static void DriveFrame(MainWindow window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static PresentationHostServices ReplaceFiles(PresentationHostServices services, ILocalFileStore files)
    {
        return new(services.Composition, services.FileReveal, services.SupportMatrix,
            services.SystemInformation, services.SystemDiagnosticsExporter, services.RawBinaryEditorFileSessions,
            services.CanonicalCatalogLoader, services.ExternalEnvironmentLoader, files, services.LocalStateDirectory);
    }

    private sealed class ObservedCapture : DesktopScreenshotCapture
    {
        internal PixelSize ClientPixels { get; private set; }
        internal string? RunId { get; private set; }
        internal bool HasVisibleModal { get; private set; }

        internal override RenderTargetBitmap Render(MainWindow window)
        {
            ClientPixels = PixelSize.FromSize(window.ClientSize, window.RenderScaling);
            RunId = Assert.IsType<MainWindowViewModel>(window.DataContext).Reports.LoadedReport.RunId;
            ReportModal modal = Assert.Single(window.GetVisualDescendants().OfType<ReportModal>());
            HasVisibleModal = modal.IsEffectivelyVisible && modal.Bounds.Width > 0 && modal.Bounds.Height > 0;
            return base.Render(window);
        }
    }

    private sealed class InjectedCaptureException(string message) : Exception(message);

    private sealed class FaultCapture(string fault) : DesktopScreenshotCapture
    {
        internal override RenderTargetBitmap Render(MainWindow window)
        {
            return fault == "draw" ? throw new DesktopCaptureFailureException("draw", "Injected drawing failure.") : base.Render(window);
        }

        internal override void Encode(Bitmap bitmap, Stream stream)
        {
            if (fault == "encode") { throw new InvalidOperationException("Injected encoder failure."); }
            if (fault == "unexpected") { throw new InjectedCaptureException("Injected unexpected failure."); }
            if (fault == "cancel") { throw new OperationCanceledException("Injected cancellation."); }
            stream.WriteByte(1);
            if (fault == "write") { throw new IOException("Injected staging write failure."); }
            stream.Position = 0;
            base.Encode(bitmap, stream);
        }

        internal override ValueTask PublishAsync(ILocalFileStore files, string destination,
            ReadOnlyMemory<byte> bytes, bool overwrite, CancellationToken cancellationToken)
        {
            return fault == "replace" ? throw new IOException("Injected atomic replacement failure.")
                : base.PublishAsync(files, destination, bytes, overwrite, cancellationToken);
        }
    }

    private sealed class RacingCapture(string destination) : DesktopScreenshotCapture
    {
        internal override void Encode(Bitmap bitmap, Stream stream)
        {
            File.WriteAllBytes(destination, [5, 4, 3]);
            base.Encode(bitmap, stream);
        }
    }

    private sealed class FailedCatalogLoader : ICanonicalCapabilityCatalogLoader
    {
        public async IAsyncEnumerable<CanonicalCapabilityCatalogLoadUpdate> LoadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new(null, new CapabilityCatalogReloadResult(false, false, null,
                [new CapabilityCatalogIssue(CapabilityCatalogIssueCodes.SourceInvalid, "Injected catalog failure.")]));
        }
    }

    private sealed class PendingEnvironmentLoader(IExternalProcessorEnvironmentLoader inner) : IExternalProcessorEnvironmentLoader
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool WasCancelled { get; private set; }
        public ExternalProcessorEnvironmentStatus Current => inner.Current;

        public async IAsyncEnumerable<ExternalProcessorEnvironmentLoadUpdate> LoadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new(0, 1, null);
            _ = Started.TrySetResult();
            try
            {
                await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task.WaitAsync(cancellationToken);
            }
            finally
            {
                WasCancelled = cancellationToken.IsCancellationRequested;
            }
        }
    }

    private sealed class PendingReportFiles(ILocalFileStore inner, string report, bool unexpected = false, bool passThrough = false) : ILocalFileStore
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal LocalFileDestinationInfo? DestinationInfo { get; init; }
        internal string? UnreachableIdentityPath { get; init; }
        internal ConcurrentQueue<string> Writes { get; } = new();

        public async ValueTask<string> ReadTextAsync(string path, long maximumBytes,
            CancellationToken cancellationToken, Action<LocalFileReadProgress>? progress = null)
        {
            if (path != report || passThrough) { return await inner.ReadTextAsync(path, maximumBytes, cancellationToken, progress); }
            _ = Started.TrySetResult();
            if (unexpected) { throw new InjectedCaptureException("Injected report read failure."); }
            await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task.WaitAsync(cancellationToken);
            throw new InvalidOperationException("The cancelled read cannot publish.");
        }

        public ValueTask<T> ReadAsync<T>(string path, long maximumBytes,
            Func<Stream, CancellationToken, ValueTask<T>> project, CancellationToken cancellationToken)
        {
            return inner.ReadAsync(path, maximumBytes, project, cancellationToken);
        }

        public ValueTask<string> ReadTextAsync(Func<CancellationToken, ValueTask<Stream>> openReadAsync,
            long maximumBytes, CancellationToken cancellationToken)
        {
            return inner.ReadTextAsync(openReadAsync, maximumBytes, cancellationToken);
        }

        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            Writes.Enqueue(path);
            return inner.WriteAsync(path, bytes, cancellationToken);
        }

        public ValueTask<LocalFileDestinationInfo> InspectDestinationAsync(string path, CancellationToken cancellationToken)
        {
            return DestinationInfo is { } info ? ValueTask.FromResult(info) : inner.InspectDestinationAsync(path, cancellationToken);
        }

        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes,
            LocalFileWriteOptions options, CancellationToken cancellationToken)
        {
            Writes.Enqueue(path);
            return inner.WriteAsync(path, bytes, options, cancellationToken);
        }

        public ValueTask<bool> RefersToSameFileAsync(string first, string second, CancellationToken cancellationToken)
        {
            return second == UnreachableIdentityPath
                ? throw new IOException("The protected path is unreachable.")
                : inner.RefersToSameFileAsync(first, second, cancellationToken);
        }

        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes,
            LocalFileWriteMode mode, CancellationToken cancellationToken)
        {
            return mode == LocalFileWriteMode.ReplaceExisting ? WriteAsync(path, bytes, cancellationToken)
                : inner.WriteAsync(path, bytes, mode, cancellationToken);
        }
    }

#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
    [DllImport("libc", EntryPoint = "link", SetLastError = true, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int CreateUnixHardLink(byte[] existingFileName, byte[] fileName);

    private static byte[] UnixPath(string path)
    {
        return Encoding.UTF8.GetBytes(path + '\0');
    }
#pragma warning restore SYSLIB1054
}
