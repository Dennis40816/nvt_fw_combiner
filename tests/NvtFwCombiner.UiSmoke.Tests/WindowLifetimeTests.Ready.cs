using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using System.Reflection;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Real-window close and startup publication lifetime regressions.</summary>
public sealed partial class WindowLifetimeTests
{
    /// <summary>A view-owned Report Save is part of the window's bounded work drain.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "The modal takes ownership of the selected storage file.")]
    [AvaloniaFact]
    public async Task WindowCloseDrainsAdmittedReportSave()
    {
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            PresentationTestHost.CreateServices("0.10.5"), ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        string json = ReportJsonSamples.Succeeded(runId: "drained-save");
        shell.Reports.LoadReportJson(json, "drained-save.json");
        var modal = new ReportModal { DataContext = shell.Reports };
        var stream = new MemoryStream();
        IStorageFile file = DispatchProxy.Create<IStorageFile, LifetimeReportStorageProxy>();
        ((LifetimeReportStorageProxy)file).Call = (name, _) => name switch
        {
            "get_Name" => "drained-save.json",
            // A provider (non-local) destination keeps the stream write path; see the trunk's atomic local save.
            "get_Path" => new Uri("https://storage.example/drained-save.json"),
            "OpenWriteAsync" => Task.FromResult<Stream>(stream),
            "Dispose" => null,
            _ => throw new NotSupportedException(name),
        };
        var selected = new TaskCompletionSource<IStorageFile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        IStorageProvider picker = DispatchProxy.Create<IStorageProvider, LifetimeReportStorageProxy>();
        ((LifetimeReportStorageProxy)picker).Call = (name, _) => name == "SaveFilePickerAsync"
            ? selected.Task : throw new NotSupportedException(name);
        Task saving = modal.SaveReportAsync(picker);
        var neverExpires = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.CloseDeadlineFactory = _ => neverExpires.Task;
        window.RequestStableLauncherRestart();
        window.Close();
        Assert.Equal(WindowClosePhase.Draining, window.ClosePhase);
        Assert.False(window.CloseAttempt.IsCompleted);
        bool lateRunStarted = false;
        UiRunResultViewModel? lateRun = await shell.RunSession.RunCompositionAsync(
            shell.Merge.CaptureRunContext(ExperienceIds.StandardMerge, build: true), true,
            (_, _) =>
            {
                lateRunStarted = true;
                throw new InvalidOperationException("A run cannot start after close admission shuts.");
            }, (_, _) => { });
        Assert.Null(lateRun);
        Assert.False(lateRunStarted);
        selected.SetResult(file);
        await saving.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await window.CloseAttempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(json, Encoding.UTF8.GetString(stream.ToArray()));
    }

    /// <summary>A late Settings fault settles without notifying the finally closed window.</summary>
    [AvaloniaFact]
    public async Task SettingsFaultAfterFinalCloseDoesNotPublishBusyChange()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = new TaskCompletionSource<VersionManagementSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IVersionManagementExperience experience =
            DispatchProxy.Create<IVersionManagementExperience, LifetimeReportStorageProxy>();
        ((LifetimeReportStorageProxy)experience).Call = (name, _) => name == nameof(IVersionManagementExperience.InitializeAsync)
            ? new ValueTask<VersionManagementSnapshot>(EnterAndWait())
            : throw new NotSupportedException(name);
        Task<VersionManagementSnapshot> EnterAndWait()
        {
            _ = entered.TrySetResult();
            return held.Task;
        }
        PresentationHostServices original = PresentationTestHost.CreateServices("0.10.5");
        var services = new PresentationHostServices(
            original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions, original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader, original.LocalFiles, original.LocalStateDirectory,
            experience, null, null);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        await window.StartupWork;
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Task refreshing = shell.Settings.RefreshVersionAsync(isAutomatic: false);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(shell.Settings.IsVersionBusy);
        int postCloseNotifications = 0;
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        shell.Settings.PropertyChanged += (_, _) =>
        {
            if (closed.Task.IsCompleted)
            {
                postCloseNotifications++;
            }
        };
        window.CloseDeadlineFactory = _ => Task.CompletedTask;
        window.Close();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        held.SetException(new InvalidOperationException("late Settings fault"));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await refreshing);
        Assert.Equal(0, postCloseNotifications);
    }

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
            // A provider (non-local) destination keeps the stream write path; see the trunk's atomic local save.
            "get_Path" => new Uri("https://storage.example/late-save.json"),
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
            // A provider (non-local) destination keeps the stream write path; see the trunk's atomic local save.
            "get_Path" => new Uri("https://storage.example/resumed-save.json"),
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
        Assert.Equal(reports.Text.FormatReportSavedToast("resumed-save.json", bestEffortProviderWrite: true), reports.ReportToastText);
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

    /// <summary>An idle snapshot cannot be reused after a Settings operation registers in the next generation.</summary>
    [AvaloniaFact]
    public async Task SettingsOperationStartingAfterIdleProbeGetsNewIncompleteWaiter()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = new TaskCompletionSource<VersionManagementSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IVersionManagementExperience experience =
            DispatchProxy.Create<IVersionManagementExperience, LifetimeReportStorageProxy>();
        ((LifetimeReportStorageProxy)experience).Call = (name, _) =>
        {
            Assert.Equal(nameof(IVersionManagementExperience.InitializeAsync), name);
            Assert.True(entered.TrySetResult());
            return new ValueTask<VersionManagementSnapshot>(held.Task);
        };
        MainWindowViewModel shell = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience), ShellPreferenceSnapshot.Default);
        Task beforeAdmission = shell.Settings.WhenOperationsIdleAsync();
        Assert.True(beforeAdmission.IsCompletedSuccessfully);
        Task refreshing = shell.Settings.RefreshVersionAsync(isAutomatic: false);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Task afterAdmission = shell.Settings.WhenOperationsIdleAsync();
        Assert.NotSame(beforeAdmission, afterAdmission);
        Assert.False(afterAdmission.IsCompleted);
        held.SetException(new InvalidOperationException("late Settings read fault"));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await refreshing);
        await afterAdmission.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(afterAdmission.IsCompletedSuccessfully);
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

    /// <summary>A work-drain or local-state flush deadline fault still revokes the lease and closes.</summary>
    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CloseDeadlineFaultDoesNotStrandWindowOrEscapeDispatcher(int faultOnCall)
    {
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.5");
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        int deadlineCalls = 0;
        window.CloseDeadlineFactory = _ => Interlocked.Increment(ref deadlineCalls) == faultOnCall
            ? throw new InvalidOperationException("synthetic deadline failure")
            : Task.Delay(TimeSpan.FromSeconds(10));
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
            Assert.True(deadlineCalls >= faultOnCall);
            Assert.Equal(WindowClosePhase.Closed, window.ClosePhase);
            Assert.False(Assert.IsType<MainWindowViewModel>(window.DataContext)
                .Reports.WindowPublication!.CanPublish);
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= Capture;
        }

    }

    /// <summary>An external Close reentered from the posted final Closing event cannot become a second final Close.</summary>
    [AvaloniaFact]
    public async Task ClosingEventReentrantCloseDoesNotStackFinalClose()
    {
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            PresentationTestHost.CreateServices("0.10.5"), ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        await window.StartupWork;
        int finalClosingEvents = 0;
        int closedEvents = 0;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closing += (_, _) =>
        {
            if (window.ClosePhase != WindowClosePhase.Closing)
            {
                return;
            }
            finalClosingEvents++;
            if (finalClosingEvents == 1)
            {
                window.Close();
            }
        };
        window.Closed += (_, _) =>
        {
            closedEvents++;
            _ = closed.TrySetResult();
        };
        window.Close();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, finalClosingEvents);
        Assert.Equal(1, closedEvents);
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

        public async ValueTask<ManagedApplicationStartupResult> CompleteStartupAsync(CancellationToken cancellationToken, bool isReadOnly = false)
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
