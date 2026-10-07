using Avalonia.Headless.XUnit;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class DesktopCaptureTests
{
    /// <summary>All three overrides preserve real stores and issue no local-state writes on success, failure or cancellation.</summary>
    [AvaloniaTheory]
    [InlineData("success", 0)]
    [InlineData("failure", 1)]
    [InlineData("cancel", 70)]
    public async Task AppearanceOverridesDoNotWriteStoresOnCaptureTerminalPaths(string outcome, int expectedExit)
    {
        using var workspace = TempWorkspace.Create("capture-appearance-terminal");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        ShellPreferenceSnapshot saved = new("Dark", "Traditional Chinese", true, false);
        string preferences = ShellPreferenceFileStore.PathIn(services.LocalStateDirectory);
        await ShellPreferenceFileStore.SaveAsync(services.LocalFiles, preferences, saved, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(preferences, "\n ", TestContext.Current.CancellationToken);
        string report = workspace.Write("report.json", System.Text.Encoding.UTF8.GetBytes(ReportJsonSamples.Succeeded()));
        Dictionary<string, byte[]> before = Directory.GetFiles(workspace.Root, "*", SearchOption.AllDirectories)
            .ToDictionary(static path => path, File.ReadAllBytes);
        var observedFiles = new PendingReportFiles(services.LocalFiles, report, passThrough: true);
        services = ReplaceFiles(services, observedFiles);
        string destination = workspace.PathFor("out.png");
        UiLaunchOptions options = UiLaunchOptions.Parse(["--theme=light", "--language=en", "--motion=full",
            "--report", report, "--open-report", "--capture", destination], services.LocalFiles);
        Assert.Empty(options.Issues);
        using var window = new MainWindow(options, StartupTraceSession.Disabled, services, saved);
        if (outcome == "failure") { window.LaunchCoordinator.CaptureSession.ScreenshotCapture = new FaultCapture("encode"); }
        Task closure = WhenClosed(window);
        window.Show();
        await window.LaunchCoordinator.CaptureSession.CaptureFrameRequested.WaitAsync(TimeSpan.FromSeconds(20),
            TestContext.Current.CancellationToken);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.Equal(new("Light", "English", false, false), shell.ExportShellPreferences());
        shell.ExpandInputDetailsByDefault = true;
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
}
