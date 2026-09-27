using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using System.Reflection;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class VersionManagementSettingsTests
{
    /// <summary>A current Settings result waits through failed handoff and publishes once after resume.</summary>
    [AvaloniaFact]
    public async Task SettingsResultSuspendsDuringHandoffAndPublishesAfterResume()
    {
        VersionManagementSnapshot reviewed = Snapshot(retentionReviewDue: true);
        var inner = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var held = new TaskCompletionSource<VersionManagementSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bool holdRefresh = false;
        IVersionManagementExperience experience =
            DispatchProxy.Create<IVersionManagementExperience, WindowLifetimeStorageProxy>();
        ((WindowLifetimeStorageProxy)experience).Call = (method, args) =>
        {
            return holdRefresh && method == nameof(IVersionManagementExperience.InitializeAsync)
                ? new ValueTask<VersionManagementSnapshot>(held.Task)
                : typeof(IVersionManagementExperience).GetMethod(method)!.Invoke(inner, args);
        };
        var handoff = new GatedWindowLifetimeHandoff();
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.5", experience, handoff);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        await window.StartupWork;
        holdRefresh = true;
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Task refreshing = shell.Settings.RefreshVersionAsync(isAutomatic: false);
        Assert.True(shell.Settings.IsVersionBusy);
        window.CloseDeadlineFactory = _ => Task.CompletedTask;
        window.RequestStableLauncherRestart();
        window.Close();
        await handoff.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.HandingOff, window.ClosePhase);
        held.SetResult(reviewed);
        Dispatcher.UIThread.RunJobs();
        Assert.False(shell.Settings.HasRetentionReview);
        handoff.Release(started: false);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!window.IsEnabled)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
        await refreshing.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(shell.Settings.HasRetentionReview);
    }

    /// <summary>Actual Closed is deferred until the stable launcher confirms its handoff.</summary>
    [AvaloniaFact]
    public async Task RealCloseWaitsForStableLauncherBeforeClosed()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var handoff = new GatedWindowLifetimeHandoff();
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.5", experience, handoff);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        window.RequestStableLauncherRestart();
        window.Close();
        await handoff.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.HandingOff, window.ClosePhase);
        Assert.False(closed.Task.IsCompleted);
        handoff.Release(started: true);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.Closed, window.ClosePhase);
    }

    /// <summary>A failed handoff keeps pending activation; the next real Close exits without retrying.</summary>
    [AvaloniaFact]
    public async Task FailedHandoffSecondCloseKeepsPendingWithoutLauncherOrClear()
    {
        using var workspace = TempWorkspace.Create("w6a-second-close-no-dialog");
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        _ = await experience.PrepareActivationAsync(ManagedAppVersion.Parse("0.10.4"), CancellationToken.None);
        var handoff = new RecordingStableLauncherHandoff(started: false);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            PresentationTestHost.CreateServices("0.10.5", experience, handoff),
            ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        window.RequestStableLauncherRestart();
        window.Close();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!window.IsEnabled)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.Equal(PendingActivationRecoveryStatus.ConfirmedKept, shell.Settings.PendingRecoveryStatus);
        Assert.True(shell.Settings.CanRetryPendingActivation);
        Assert.NotNull(experience.Current.State!.PendingActivation);
        shell.ShowMergeCommand.Execute(null);
        shell.WorkflowSession.SelectedIc = "NT51928";
        await shell.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp, workspace.Write("selected.bin", new byte[0x40000]),
            TestContext.Current.CancellationToken);
        Assert.True(shell.HasSelectedFiles);
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        window.Close();
        Assert.False(shell.Navigation.IsExitConfirmationOpen);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(1, handoff.Attempts);
        Assert.NotNull(experience.Current.State!.PendingActivation);
    }

    /// <summary>Only the Settings Retry command starts another handoff after confirmed recovery.</summary>
    [AvaloniaFact]
    public async Task ConfirmedPendingSettingsRetryStartsOneNewHandoff()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        _ = await experience.PrepareActivationAsync(ManagedAppVersion.Parse("0.10.4"), CancellationToken.None);
        var handoff = new RecordingStableLauncherHandoff(started: false);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            PresentationTestHost.CreateServices("0.10.5", experience, handoff),
            ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        window.RequestStableLauncherRestart();
        window.Close();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!window.IsEnabled || handoff.Attempts != 1)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.True(shell.Settings.CanRetryPendingActivation);
        Assert.True(shell.Settings.RetryPendingActivationCommand.CanExecute(null));
        await shell.Settings.RetryPendingActivationCommand.ExecuteAsync(null);
        while (!window.IsEnabled || handoff.Attempts != 2)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
        Assert.Equal(2, handoff.Attempts);
        Assert.NotNull(experience.Current.State!.PendingActivation);
    }

    /// <summary>A new launcher request during the second Close cannot override the ordinary exit.</summary>
    [AvaloniaFact]
    public async Task ActivationRaceDuringSecondCloseDoesNotRestartLauncher()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        _ = await experience.PrepareActivationAsync(ManagedAppVersion.Parse("0.10.4"), CancellationToken.None);
        var handoff = new RecordingStableLauncherHandoff(started: false);
        PresentationHostServices original = PresentationTestHost.CreateServices("0.10.5", experience, handoff);
        var held = new TaskCompletionSource<Application.Configuration.IEventBufferFormatConfigurationSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new PresentationHostServices(
            original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions, original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader, original.LocalFiles, original.LocalStateDirectory,
            experience, null, handoff, _ => held.Task);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        window.RequestStableLauncherRestart();
        window.Close();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!window.IsEnabled)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        shell.OpenSettingsCommand.Execute(null);
        shell.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        Assert.True(shell.Settings.IsEventBufferFormatLoading);
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        window.Close();
        Assert.Equal(WindowClosePhase.Draining, window.ClosePhase);
        window.RequestStableLauncherRestart();
        held.SetException(new InvalidOperationException("synthetic factory fault"));
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(1, handoff.Attempts);
    }

    /// <summary>Activation admitted while ordinary Close drains upgrades the terminal choice.</summary>
    [AvaloniaFact]
    public async Task ActivationDuringDrainStartsLauncherBeforeFinalClose()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var handoff = new GatedWindowLifetimeHandoff();
        PresentationHostServices original = PresentationTestHost.CreateServices("0.10.5", experience, handoff);
        var held = new TaskCompletionSource<Application.Configuration.IEventBufferFormatConfigurationSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new PresentationHostServices(
            original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions, original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader, original.LocalFiles, original.LocalStateDirectory,
            experience, null, handoff, _ => held.Task);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        await window.StartupWork;
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        shell.OpenSettingsCommand.Execute(null);
        shell.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        Assert.True(shell.Settings.IsEventBufferFormatLoading);
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        window.Close();
        Assert.Equal(WindowClosePhase.Draining, window.ClosePhase);
        window.RequestStableLauncherRestart();
        window.Close();
        held.SetException(new InvalidOperationException("synthetic factory fault"));
        await handoff.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.HandingOff, window.ClosePhase);
        Assert.False(closed.Task.IsCompleted);
        handoff.Release(started: true);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    /// <summary>A window result committed while a store is sealed is replayed after handoff recovery.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalStateChangedDuringFailedHandoffReplaysAfterReopen(bool reportHistory)
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var handoff = new GatedWindowLifetimeHandoff();
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.5", experience, handoff);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        window.RequestStableLauncherRestart();
        window.Close();
        await handoff.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        if (reportHistory)
        {
            shell.Reports.LoadReportJson(
                ReportJsonSamples.Succeeded(runId: "sealed-history"), "sealed-history.json");
        }
        else
        {
            shell.SelectedTheme = "Dark";
        }
        handoff.Release(started: false);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!window.IsEnabled)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        window.Close();
        if (shell.Navigation.IsExitConfirmationOpen)
        {
            shell.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
        }
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        if (reportHistory)
        {
            IReadOnlyList<ReportHistorySnapshot> saved = await ReportHistoryFileStore.LoadAsync(
                services.LocalFiles, ReportHistoryFileStore.PathIn(services.LocalStateDirectory),
                TestContext.Current.CancellationToken);
            Assert.Contains(saved, entry => entry.SourceName == "sealed-history.json");
        }
        else
        {
            ShellPreferenceSnapshot saved = await ShellPreferenceFileStore.LoadAsync(
                services.LocalFiles, ShellPreferenceFileStore.PathIn(services.LocalStateDirectory));
            Assert.Equal("Dark", saved.Theme);
        }
    }

    private sealed class GatedWindowLifetimeHandoff : IStableLauncherHandoff
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Entered => _entered.Task;

        internal void Release(bool started)
        {
            _ = _released.TrySetResult(started);
        }

        public async ValueTask<bool> TryStartLauncherAsync(CancellationToken cancellationToken)
        {
            _ = _entered.TrySetResult();
            return await _released.Task;
        }
    }

    /// <summary>A failed real Close reopens preference admission and a later Close flushes the new value.</summary>
    [AvaloniaFact]
    public async Task FailedHandoffReopensPreferenceSaveAndSecondCloseFlushesIt()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var handoff = new RecordingStableLauncherHandoff(started: false);
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.5", experience, handoff);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();

        _ = await experience.PrepareActivationAsync(ManagedAppVersion.Parse("0.10.4"), CancellationToken.None);
        window.RequestStableLauncherRestart();
        window.Close();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (handoff.Attempts == 0 || !window.IsEnabled)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }

        shell.SelectedTheme = "Dark";
        window.Close();
        if (shell.Navigation.IsExitConfirmationOpen)
        {
            shell.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
        }
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        ShellPreferenceSnapshot saved = await ShellPreferenceFileStore.LoadAsync(
            services.LocalFiles, ShellPreferenceFileStore.PathIn(services.LocalStateDirectory));
        Assert.Equal("Dark", saved.Theme);
    }

    /// <summary>A recovered window persists Report history added before its second Close.</summary>
    [AvaloniaFact]
    public async Task FailedHandoffReopensReportHistorySaveAndSecondCloseFlushesIt()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var handoff = new RecordingStableLauncherHandoff(started: false);
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.5", experience, handoff);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        _ = await experience.PrepareActivationAsync(ManagedAppVersion.Parse("0.10.4"), CancellationToken.None);
        window.RequestStableLauncherRestart();
        window.Close();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (handoff.Attempts == 0 || !window.IsEnabled)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }

        shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "after-recovery"), "after-recovery.json");
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        window.Close();
        if (shell.Navigation.IsExitConfirmationOpen)
        {
            shell.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
        }
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        IReadOnlyList<ReportHistorySnapshot> saved = await ReportHistoryFileStore.LoadAsync(
            services.LocalFiles, ReportHistoryFileStore.PathIn(services.LocalStateDirectory),
            TestContext.Current.CancellationToken);
        Assert.Contains(saved, row => row.SourceName == "after-recovery.json");
    }

    /// <summary>A report import admitted after recovery uses the new session cancellation ticket.</summary>
    [AvaloniaFact]
    public async Task ReportJsonImportAfterHandoffRecoveryPublishes()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var handoff = new RecordingStableLauncherHandoff(started: false);
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.5", experience, handoff);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        _ = await experience.PrepareActivationAsync(ManagedAppVersion.Parse("0.10.4"), CancellationToken.None);
        window.RequestStableLauncherRestart();
        window.Close();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (handoff.Attempts == 0 || !window.IsEnabled)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }

        string json = ReportJsonSamples.Succeeded(runId: "recovered-import");
        using IStorageFile file = DispatchProxy.Create<IStorageFile, WindowLifetimeStorageProxy>();
        ((WindowLifetimeStorageProxy)file).Call = (method, _) => method switch
        {
            "get_Name" => "recovered.json",
            "Dispose" => null,
            "OpenReadAsync" => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(json))),
            _ => throw new NotSupportedException(method),
        };
        IStorageProvider picker = DispatchProxy.Create<IStorageProvider, WindowLifetimeStorageProxy>();
        ((WindowLifetimeStorageProxy)picker).Call = (method, _) => method switch
        {
            nameof(IStorageProvider.OpenFilePickerAsync) => Task.FromResult<IReadOnlyList<IStorageFile>>([file]),
            _ => throw new NotSupportedException(method),
        };
        await window.LoadReportJsonAsync(null, picker);
        Assert.Equal(json, shell.Reports.LoadedReportJson);

        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        window.Close();
        if (shell.Navigation.IsExitConfirmationOpen)
        {
            shell.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
        }
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1852:Seal internal types",
        Justification = "DispatchProxy creates a runtime subclass.")]
    private class WindowLifetimeStorageProxy : DispatchProxy
    {
        internal Func<string, object?[]?, object?> Call { get; set; } = (_, _) => throw new NotSupportedException();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return Call(targetMethod!.Name, args);
        }
    }

    /// <summary>A READY result returned after final Close cannot update the disposed window's Settings.</summary>
    [AvaloniaFact]
    public async Task ReadyResultAfterFinalCloseDoesNotPublishSnapshotOrOpenSettings()
    {
        VersionManagementSnapshot readySnapshot = Snapshot(retentionReviewDue: true) with
        {
            ShouldPromptForUpdate = true,
        };
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var startup = new IgnoringReadyStartup();
        PresentationHostServices baseServices = PresentationTestHost.CreateServices("0.10.5", experience);
        var services = new PresentationHostServices(
            baseServices.Composition, baseServices.FileReveal, baseServices.SupportMatrix,
            baseServices.SystemInformation, baseServices.SystemDiagnosticsExporter,
            baseServices.RawBinaryEditorFileSessions, baseServices.CanonicalCatalogLoader,
            baseServices.ExternalEnvironmentLoader, baseServices.LocalFiles,
            baseServices.LocalStateDirectory, experience, startup, null);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await startup.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        window.Close();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        startup.Release(new(ApplicationReadySignalOutcome.Reported, readySnapshot));
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(shell.Settings.HasRetentionReview);
        Assert.False(shell.IsSettingsModalOpen);
    }

    private sealed class IgnoringReadyStartup : IManagedApplicationStartupCoordinator
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<ManagedApplicationStartupResult> _released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Entered => _entered.Task;
        internal void Release(ManagedApplicationStartupResult result)
        {
            _released.SetResult(result);
        }
        public ValueTask<ManagedApplicationStartupResult> CompleteStartupAsync(CancellationToken cancellationToken)
        {
            _ = _entered.TrySetResult();
            return new(_released.Task);
        }
    }

    /// <summary>A durable read that ignores cancellation cannot hold the failed-handoff window forever.</summary>
    [AvaloniaFact]
    public async Task FailedHandoffBoundedReadReturnsUnknownWithoutClearingPending()
    {
        var inner = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        _ = await inner.PrepareActivationAsync(ManagedAppVersion.Parse("0.10.4"), CancellationToken.None);
        TaskCompletionSource readEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<VersionManagementSnapshot> readRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int clears = 0;
        bool gateRead = false;
        IVersionManagementExperience experience =
            DispatchProxy.Create<IVersionManagementExperience, WindowLifetimeStorageProxy>();
        ((WindowLifetimeStorageProxy)experience).Call = (method, args) =>
        {
            if (method == nameof(IVersionManagementExperience.CancelPendingActivationAsync))
            {
                _ = Interlocked.Increment(ref clears);
                throw new InvalidOperationException("Recovery must not clear pending activation.");
            }
            if (method == nameof(IVersionManagementExperience.InitializeAsync) && gateRead)
            {
                _ = readEntered.TrySetResult();
                return new ValueTask<VersionManagementSnapshot>(readRelease.Task);
            }
            return typeof(IVersionManagementExperience).GetMethod(method)!.Invoke(inner, args);
        };
        var handoff = new RecordingStableLauncherHandoff(started: false);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            PresentationTestHost.CreateServices("0.10.5", experience, handoff),
            ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        gateRead = true;
        TaskCompletionSource recoveryExpired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int deadlines = 0;
        window.CloseDeadlineFactory = _ => Interlocked.Increment(ref deadlines) == 3
            ? recoveryExpired.Task : Task.CompletedTask;
        window.RequestStableLauncherRestart();
        window.Close();
        await readEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.False(window.IsEnabled);
        recoveryExpired.SetResult();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!window.IsEnabled)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.Equal(PendingActivationRecoveryStatus.Unknown, shell.Settings.PendingRecoveryStatus);
        Assert.False(shell.Settings.CanRetryPendingActivation);
        Assert.False(shell.Settings.RetryPendingActivationCommand.CanExecute(null));
        Assert.Equal(0, clears);
        Assert.NotNull(inner.Current.State!.PendingActivation);
        readRelease.SetResult(inner.Current);
        Assert.Equal(PendingActivationRecoveryStatus.ConfirmedKept,
            await shell.Settings.RecheckPendingActivationStatusAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>An unknown durable status fences new activation until a fresh read succeeds.</summary>
    [Fact]
    public async Task UnknownPendingStatusFencesNewActivation()
    {
        var inner = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        int versionWrites = 0;
        TaskCompletionSource<VersionManagementSnapshot> readRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        IVersionManagementExperience experience =
            DispatchProxy.Create<IVersionManagementExperience, WindowLifetimeStorageProxy>();
        ((WindowLifetimeStorageProxy)experience).Call = (method, args) =>
        {
            if (method is nameof(IVersionManagementExperience.CommitUpdateSourceAsync) or
                nameof(IVersionManagementExperience.AcknowledgeRetentionReviewAsync))
            {
                _ = Interlocked.Increment(ref versionWrites);
                throw new InvalidOperationException("Unknown status must fence version writes.");
            }
            return method == nameof(IVersionManagementExperience.InitializeAsync)
                ? new ValueTask<VersionManagementSnapshot>(readRelease.Task)
                : typeof(IVersionManagementExperience).GetMethod(method)!.Invoke(inner, args);
        };
        MainWindowViewModel shell = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        shell.Settings.ApplyVersionSnapshot(inner.Current);
        using var expiry = new CancellationTokenSource();
        Task<PendingActivationRecoveryStatus> recovery =
            shell.Settings.HandleLauncherHandoffFailureAsync(expiry.Token);
        expiry.Cancel();
        Assert.Equal(PendingActivationRecoveryStatus.Unknown, await recovery);
        SettingsVersionRowViewModel installed = Assert.Single(shell.Settings.VersionRows,
            row => row.Version == ManagedAppVersion.Parse("0.10.4"));
        shell.Settings.RequestVersionPrimaryActionCommand.Execute(installed);
        await shell.Settings.ConfirmVersionActionCommand.ExecuteAsync(null);
        await shell.Settings.RetryPendingActivationCommand.ExecuteAsync(null);
        shell.Settings.BeginEditUpdateSourceCommand.Execute(null);
        shell.Settings.UpdateSourceDraft = "C:/candidate";
        await shell.Settings.ConfirmUpdateSourceCommand.ExecuteAsync(null);
        await shell.Settings.KeepAllVersionsCommand.ExecuteAsync(null);
        Assert.Empty(inner.Activations);
        Assert.Equal(0, versionWrites);
        readRelease.SetResult(inner.Current);
    }

    /// <summary>A stalled fresh read bounds Retry and never launches from an old kept snapshot.</summary>
    [Fact]
    public async Task SettingsRetryRequiresBoundedFreshDurableConfirmation()
    {
        var inner = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        _ = await inner.PrepareActivationAsync(ManagedAppVersion.Parse("0.10.4"), CancellationToken.None);
        var held = new TaskCompletionSource<VersionManagementSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bool stallRead = false;
        IVersionManagementExperience experience =
            DispatchProxy.Create<IVersionManagementExperience, WindowLifetimeStorageProxy>();
        ((WindowLifetimeStorageProxy)experience).Call = (method, args) =>
            method == nameof(IVersionManagementExperience.InitializeAsync) && stallRead
                ? new ValueTask<VersionManagementSnapshot>(held.Task)
                : typeof(IVersionManagementExperience).GetMethod(method)!.Invoke(inner, args);
        MainWindowViewModel shell = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        Assert.Equal(PendingActivationRecoveryStatus.ConfirmedKept,
            await shell.Settings.HandleLauncherHandoffFailureAsync(TestContext.Current.CancellationToken));
        int activationRequests = 0;
        shell.Settings.ActivationRequested += (_, _) => activationRequests++;
        using var retryExpired = new CancellationTokenSource();
        shell.Settings.RetryReadCancellationFactory = _ => retryExpired;
        stallRead = true;
        Task retry = shell.Settings.RetryPendingActivationCommand.ExecuteAsync(null);
        Assert.False(retry.IsCompleted);
        retryExpired.Cancel();
        await retry.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(0, activationRequests);
        Assert.Equal(PendingActivationRecoveryStatus.Unknown, shell.Settings.PendingRecoveryStatus);
        Assert.False(shell.Settings.RetryPendingActivationCommand.CanExecute(null));
        held.SetResult(inner.Current);
    }
    /// <summary>Launcher handoff failure keeps the pending switch in saved state.</summary>
    [Fact]
    public async Task LauncherHandoffFailureKeepsPendingActivationAndReportsOpenState()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(experience.Current);
        SettingsVersionRowViewModel installed = Assert.Single(
            viewModel.Settings.VersionRows,
            row => row.Version == ManagedAppVersion.Parse("0.10.4"));
        viewModel.Settings.RequestVersionPrimaryActionCommand.Execute(installed);
        await viewModel.Settings.ConfirmVersionActionCommand.ExecuteAsync(null);
        Assert.NotNull(experience.Current.State!.PendingActivation);

        PendingActivationRecoveryStatus status = await viewModel.Settings.HandleLauncherHandoffFailureAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(PendingActivationRecoveryStatus.ConfirmedKept, status);
        Assert.NotNull(experience.Current.State!.PendingActivation);
        Assert.Contains("remains saved", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.SelectedLanguage = "Traditional Chinese";
        Assert.Contains("待處理的版本切換仍保存在", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
    }

    /// <summary>A failed handoff calls pending kept only after a fresh durable read confirms it.</summary>
    [AvaloniaFact]
    public async Task HandoffRequiresDurableKeptConfirmation()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false))
        {
            FailPendingActivationCancellation = true,
        };
        var handoff = new RecordingStableLauncherHandoff(started: false);
        using var window = new MainWindow(
            UiLaunchOptions.Empty,
            StartupTraceSession.Disabled,
            PresentationTestHost.CreateServices("0.10.5", experience, handoff),
            ShellPreferenceSnapshot.Default);
        _ = await experience.PrepareActivationAsync(ManagedAppVersion.Parse("0.10.4"), CancellationToken.None);
        window.RequestStableLauncherRestart();

        Assert.False(await window.TryCompleteStableLauncherHandoffAsync());

        Assert.Equal(1, handoff.Attempts);
        MainWindowViewModel viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.Equal(PendingActivationRecoveryStatus.ConfirmedKept, viewModel.Settings.PendingRecoveryStatus);
        Assert.Contains("remains saved", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
        Assert.True(window.IsEnabled);
    }

    /// <summary>The window reports a failed launcher start while it is still alive and usable.</summary>
    [AvaloniaFact]
    public async Task StableLauncherMustStartBeforeWindowCanCompleteHandoff()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var handoff = new RecordingStableLauncherHandoff(started: false);
        PresentationHostServices services = PresentationTestHost.CreateServices(
            "0.10.5",
            experience,
            handoff);
        using var window = new MainWindow(
            UiLaunchOptions.Empty,
            StartupTraceSession.Disabled,
            services,
            ShellPreferenceSnapshot.Default);
        window.RequestStableLauncherRestart();

        bool started = await window.TryCompleteStableLauncherHandoffAsync();

        Assert.False(started);
        Assert.Equal(1, handoff.Attempts);
        Assert.True(window.IsEnabled);
        MainWindowViewModel viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.Contains("remains open", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
    }

    /// <summary>A state-save failure during activation preparation never requests launcher handoff.</summary>
    [Fact]
    public async Task ActivationPreparationFailureRemainsVisibleWithoutClosing()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false))
        {
            FailActivationPreparation = true,
        };
        MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        viewModel.Settings.ApplyVersionSnapshot(experience.Current);
        bool activationRequested = false;
        viewModel.Settings.ActivationRequested += (_, _) => activationRequested = true;
        SettingsVersionRowViewModel installed = Assert.Single(
            viewModel.Settings.VersionRows,
            row => row.Version == ManagedAppVersion.Parse("0.10.4"));

        viewModel.Settings.RequestVersionPrimaryActionCommand.Execute(installed);
        await viewModel.Settings.ConfirmVersionActionCommand.ExecuteAsync(null);

        Assert.False(activationRequested);
        Assert.Contains("could not be prepared", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
    }
}
