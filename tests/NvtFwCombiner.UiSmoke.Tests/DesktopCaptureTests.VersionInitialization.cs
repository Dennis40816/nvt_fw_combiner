using System.Text;
using Avalonia.Headless.XUnit;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class DesktopCaptureTests
{
    /// <summary>The real startup dispatch requests read-only initialization only for capture and still invokes READY coordination once.</summary>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CaptureStartupRequestsReadOnlyVersionInitializationWhileOrdinaryStartupKeepsWrites(bool capture)
    {
        using var workspace = TempWorkspace.Create("capture-version-startup");
        string report = workspace.Write("report.json", Encoding.UTF8.GetBytes(ReportJsonSamples.Succeeded()));
        string destination = workspace.PathFor("out.png");
        string[] arguments = ["--report", report, "--open-report", .. capture ? new[] { "--capture", destination } : []];
        PresentationHostServices original = await CreateServicesAsync(workspace);
        var startup = new RecordingCaptureStartup();
        var services = new PresentationHostServices(original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter, original.RawBinaryEditorFileSessions,
            original.CanonicalCatalogLoader, original.ExternalEnvironmentLoader, original.LocalFiles,
            original.LocalStateDirectory, versionManagement: null, managedApplicationStartup: startup, stableLauncherHandoff: null);
        using var window = new MainWindow(ParseOptions(arguments), StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        Task closed = WhenClosed(window);
        window.Show();
        try
        {
            if (capture)
            {
                await window.LaunchCoordinator.CaptureSession.CaptureFrameRequested.WaitAsync(
                    TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
                DriveFrame(window);
                await closed.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
                Assert.Equal(0, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
                Assert.True(File.Exists(destination));
            }
            else
            {
                await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            }
            Assert.Equal(1, startup.Calls);
            Assert.Equal(capture, startup.ReadOnlyInitialization);
        }
        finally
        {
            if (!capture) { await ReportControlTestHost.CloseAndFlushAsync(window); }
        }
    }

    private sealed class RecordingCaptureStartup : IManagedApplicationStartupCoordinator
    {
        internal int Calls { get; private set; }
        internal bool ReadOnlyInitialization { get; private set; }

        public ValueTask<ManagedApplicationStartupResult> CompleteStartupAsync(
            CancellationToken cancellationToken, bool isReadOnly = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            ReadOnlyInitialization = isReadOnly;
            return ValueTask.FromResult(new ManagedApplicationStartupResult(ApplicationReadySignalOutcome.Reported,
                new(null, ManagedVersionInventory.Create([]), null, null, VersionSourceStatus.NotConfigured,
                    null, 0, false, VersionManagerStateLoadIssue.Missing)));
        }
    }
}
