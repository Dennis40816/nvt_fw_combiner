using Avalonia.Headless.XUnit;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>A real shell keeps its report history and preferences inside its host's own local-state directory.</summary>
public sealed class LocalStateIsolationTests
{
    /// <summary>Startup restore, a history append and a preference change all address only the host directory.</summary>
    [AvaloniaFact]
    public async Task ShellHistoryAndPreferencesStayInsideTheHostLocalStateDirectory()
    {
        var files = new RecordingLocalFileStore();
        (PresentationHostServices services, string directory) = await PresentationTestHost.CreateServicesAsync(files);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        try
        {
            await ReportControlTestHost.AwaitHistoryReadyAsync(window);
            shell.SelectedLanguage = "Traditional Chinese";
            shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "isolated-history"), "isolated.json");
        }
        finally
        {
            await ReportControlTestHost.CloseAndFlushAsync(window);
        }

        Assert.Contains(files.Reads, static path => Path.GetFileName(path) == "report-history.v1.json");
        Assert.Contains(files.Writes, static path => Path.GetFileName(path) == "report-history.v1.json");
        Assert.Contains(files.Writes, static path => Path.GetFileName(path) == "preferences.v1.json");
        Assert.All(files.Paths, path => Assert.True(
            IsolatedLocalState.Contains(directory, path),
            $"Local state escaped the host directory {directory}: {path}"));
    }

    /// <summary>The UI test assembly declares the current user's local-state folder forbidden.</summary>
    [Fact]
    public void TestProcessDeclaresTheCurrentUserFolderForbidden()
    {
        Assert.True(AppContext.TryGetSwitch("NvtFwCombiner.LocalState.CurrentUserFolderForbidden", out bool forbidden));
        Assert.True(forbidden);
    }

    /// <summary>Every shared UI test host receives its own directory under the test temporary root.</summary>
    [Fact]
    public async Task TestHostsNeverShareALocalStateDirectory()
    {
        PresentationHostServices[] hosts = await Task.WhenAll(Enumerable.Range(0, 2).Select(static _ => Task.Run(
            static () => PresentationTestHost.CreateServices(ApplicationVersionProvider.InformationalVersion),
            TestContext.Current.CancellationToken)));

        Assert.NotEqual(hosts[0].LocalStateDirectory, hosts[1].LocalStateDirectory);
        Assert.All(hosts, static host => Assert.True(
            IsolatedLocalState.Contains(Path.GetTempPath(), host.LocalStateDirectory),
            host.LocalStateDirectory));
    }
}
