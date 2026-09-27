using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using System.Reflection;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Real-window close and startup publication lifetime regressions.</summary>
public sealed partial class WindowLifetimeTests
{
    /// <summary>A completed user-file save cannot notify a window after its final lease is revoked.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "The modal takes ownership of the selected storage file.")]
    [AvaloniaFact]
    public async Task ReportSaveAfterFinalRevocationKeepsFileWithoutNotification()
    {
        var reports = new ReportPresentationViewModel(
            () => ShellTextResources.For(ShellLanguage.English), static () => { });
        var lease = new WindowPublicationLease();
        reports.WindowPublication = lease;
        string json = ReportJsonSamples.Succeeded(runId: "late-save");
        reports.LoadReportJson(json, "late-save.json");
        string previousToast = reports.ReportToastText;
        var modal = new ReportModal { DataContext = reports };
        var stream = new MemoryStream();
        IStorageFile file = DispatchProxy.Create<IStorageFile, LifetimeReportStorageProxy>();
        ((LifetimeReportStorageProxy)file).Call = (name, _) => name switch
        {
            "get_Name" => "late-save.json",
            "OpenWriteAsync" => Task.FromResult<Stream>(stream),
            "Dispose" => null,
            _ => throw new NotSupportedException(name),
        };
        var selected = new TaskCompletionSource<IStorageFile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        IStorageProvider picker = DispatchProxy.Create<IStorageProvider, LifetimeReportStorageProxy>();
        ((LifetimeReportStorageProxy)picker).Call = (name, _) => name == "SaveFilePickerAsync"
            ? selected.Task : throw new NotSupportedException(name);
        Task saving = modal.SaveReportAsync(picker);
        lease.Revoke();
        selected.SetResult(file);
        await saving.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(json, Encoding.UTF8.GetString(stream.ToArray()));
        Assert.Equal(previousToast, reports.ReportToastText);
    }

    /// <summary>A completed save keeps its terminal toast until the same window resumes.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "The modal takes ownership of the selected storage file.")]
    [AvaloniaFact]
    public async Task ReportSaveNotificationResumesOnceAfterFailedHandoff()
    {
        var reports = new ReportPresentationViewModel(
            () => ShellTextResources.For(ShellLanguage.English), static () => { });
        var lease = new WindowPublicationLease();
        reports.WindowPublication = lease;
        reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "resumed-save"), "resumed-save.json");
        var modal = new ReportModal { DataContext = reports };
        var stream = new MemoryStream();
        IStorageFile file = DispatchProxy.Create<IStorageFile, LifetimeReportStorageProxy>();
        ((LifetimeReportStorageProxy)file).Call = (name, _) => name switch
        {
            "get_Name" => "resumed-save.json",
            "OpenWriteAsync" => Task.FromResult<Stream>(stream),
            "Dispose" => null,
            _ => throw new NotSupportedException(name),
        };
        IStorageProvider picker = DispatchProxy.Create<IStorageProvider, LifetimeReportStorageProxy>();
        ((LifetimeReportStorageProxy)picker).Call = (name, _) => name == "SaveFilePickerAsync"
            ? Task.FromResult<IStorageFile?>(file) : throw new NotSupportedException(name);
        lease.Suspend();
        Task saving = modal.SaveReportAsync(picker);
        Assert.False(saving.IsCompleted);
        Assert.DoesNotContain("Report saved", reports.ReportToastText, StringComparison.Ordinal);
        lease.Resume();
        await saving.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(reports.Text.FormatReportSavedToast("resumed-save.json"), reports.ReportToastText);
    }

    /// <summary>A terminal result waits through handoff and wakes once on resume or final revocation.</summary>
    [AvaloniaFact]
    public async Task WindowPublicationLeaseSuspendsAndWakesTerminalOwners()
    {
        var resumed = new WindowPublicationLease();
        resumed.Suspend();
        Task<bool> delivery = resumed.WaitToPublishAsync(() => true, CancellationToken.None);
        Assert.False(delivery.IsCompleted);
        resumed.Resume();
        Assert.True(await delivery.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        var revoked = new WindowPublicationLease();
        revoked.Suspend();
        Task<bool> dropped = revoked.WaitToPublishAsync(() => true, CancellationToken.None);
        Assert.False(dropped.IsCompleted);
        revoked.Revoke();
        Assert.False(await dropped.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    /// <summary>Initial configuration acquisition is owned even before its Busy flag is set.</summary>
    [AvaloniaFact]
    public async Task SettingsIdleWaitIncludesInitialEventBufferFactory()
    {
        var held = new TaskCompletionSource<Application.Configuration.IEventBufferFormatConfigurationSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        PresentationHostServices original = PresentationTestHost.CreateServices("0.10.5");
        var services = new PresentationHostServices(
            original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions, original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader, original.LocalFiles, original.LocalStateDirectory,
            versionManagement: null, managedApplicationStartup: null, stableLauncherHandoff: null,
            eventBufferFormatConfigurationSessionFactory: _ => held.Task);
        MainWindowViewModel shell = ShellViewModelFactory.Create(services, ShellLanguage.English);
        shell.OpenSettingsCommand.Execute(null);
        shell.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        Assert.True(shell.Settings.IsEventBufferFormatLoading);
        Assert.False(shell.Settings.IsEventBufferFormatBusy);
        Task idle = shell.Settings.WhenOperationsIdleAsync();
        Assert.False(idle.IsCompleted);

        held.SetException(new InvalidOperationException("synthetic factory fault"));
        await idle.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.False(shell.Settings.IsEventBufferFormatLoading);
    }

    /// <summary>Close drains initial Settings acquisition even while its Busy flag remains false.</summary>
    [AvaloniaFact]
    public async Task CloseWaitsForInitialEventBufferFactory()
    {
        var held = new TaskCompletionSource<Application.Configuration.IEventBufferFormatConfigurationSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        PresentationHostServices original = PresentationTestHost.CreateServices("0.10.5");
        var services = new PresentationHostServices(
            original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions, original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader, original.LocalFiles, original.LocalStateDirectory,
            versionManagement: null, managedApplicationStartup: null, stableLauncherHandoff: null,
            eventBufferFormatConfigurationSessionFactory: _ => held.Task);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        await window.StartupWork;
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        shell.OpenSettingsCommand.Execute(null);
        shell.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        Assert.True(shell.Settings.IsEventBufferFormatLoading);
        Assert.False(shell.Settings.IsEventBufferFormatBusy);
        var neverExpires = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int deadlineCalls = 0;
        window.CloseDeadlineFactory = _ =>
        {
            deadlineCalls++;
            return neverExpires.Task;
        };
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        window.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(WindowClosePhase.Draining, window.ClosePhase);
        window.Close();
        window.Close();
        Assert.Equal(1, deadlineCalls);
        Assert.False(closed.Task.IsCompleted);
        held.SetException(new InvalidOperationException("synthetic factory fault"));
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.Closed, window.ClosePhase);
    }

    /// <summary>A deadline adapter fault still completes final revocation and closes the window.</summary>
    [AvaloniaFact]
    public async Task CloseDeadlineFaultDoesNotStrandWindowOrEscapeDispatcher()
    {
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.5");
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        window.CloseDeadlineFactory = _ => throw new InvalidOperationException("synthetic deadline failure");
        bool escaped = false;
        void Capture(object sender, DispatcherUnhandledExceptionEventArgs args)
        {
            escaped = true;
            args.Handled = true;
        }
        Dispatcher.UIThread.UnhandledException += Capture;
        try
        {
            TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            window.Closed += (_, _) => _ = closed.TrySetResult();
            window.Close();
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.False(escaped);
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= Capture;
        }
    }

    /// <summary>Cancellation of a READY write during Close stays inside the startup owner.</summary>
    [AvaloniaFact]
    public async Task ReadyCancellationDuringCloseDoesNotEscapeDispatcherOrPublish()
    {
        var startup = new GatedStartup();
        PresentationHostServices baseServices = PresentationTestHost.CreateServices("0.10.5");
        var services = new PresentationHostServices(
            baseServices.Composition, baseServices.FileReveal, baseServices.SupportMatrix,
            baseServices.SystemInformation, baseServices.SystemDiagnosticsExporter,
            baseServices.RawBinaryEditorFileSessions, baseServices.CanonicalCatalogLoader,
            baseServices.ExternalEnvironmentLoader, baseServices.LocalFiles,
            baseServices.LocalStateDirectory, null, startup, null);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        bool escaped = false;
        void Capture(object sender, DispatcherUnhandledExceptionEventArgs args)
        {
            escaped = true;
            args.Handled = true;
        }
        Dispatcher.UIThread.UnhandledException += Capture;
        try
        {
            window.Show();
            await startup.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            window.Closed += (_, _) => _ = closed.TrySetResult();
            window.Close();
            if (shell.Navigation.IsExitConfirmationOpen)
            {
                shell.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
            }
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.False(escaped);
            Assert.False(startup.ReportedReady);
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= Capture;
        }
    }

    private sealed class GatedStartup : IManagedApplicationStartupCoordinator
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Entered => _entered.Task;
        internal bool ReportedReady { get; private set; }

        public async ValueTask<ManagedApplicationStartupResult> CompleteStartupAsync(CancellationToken cancellationToken)
        {
            _ = _entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            ReportedReady = true;
            throw new InvalidOperationException("Unreachable READY completion.");
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1852:Seal internal types",
        Justification = "DispatchProxy creates a runtime subclass.")]
    private class LifetimeReportStorageProxy : DispatchProxy
    {
        internal Func<string, object?[]?, object?> Call { get; set; } = (_, _) => throw new NotSupportedException();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return Call(targetMethod!.Name, args);
        }
    }
}
