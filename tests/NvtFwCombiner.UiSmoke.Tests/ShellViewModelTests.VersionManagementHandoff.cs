using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using System.Reflection;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

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

    /// <summary>A pending clear that ignores cancellation cannot hold the failed-handoff window forever.</summary>
    [AvaloniaFact]
    public async Task FailedHandoffBoundedRecoveryReturnsOpenWindowWhileClearIsUnsettled()
    {
        var inner = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        _ = await inner.PrepareActivationAsync(ManagedAppVersion.Parse("0.10.4"), CancellationToken.None);
        TaskCompletionSource clearEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<VersionManagementSnapshot> clearRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        IVersionManagementExperience experience =
            DispatchProxy.Create<IVersionManagementExperience, WindowLifetimeStorageProxy>();
        ((WindowLifetimeStorageProxy)experience).Call = (method, args) =>
        {
            if (method == nameof(IVersionManagementExperience.CancelPendingActivationAsync))
            {
                _ = clearEntered.TrySetResult();
                return new ValueTask<VersionManagementSnapshot>(clearRelease.Task);
            }
            MethodInfo target = typeof(IVersionManagementExperience).GetMethod(method)!;
            return target.Invoke(inner, args);
        };
        var handoff = new RecordingStableLauncherHandoff(started: false);
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.5", experience, handoff);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        TaskCompletionSource recoveryExpired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int deadlines = 0;
        window.CloseDeadlineFactory = _ => Interlocked.Increment(ref deadlines) == 3
            ? recoveryExpired.Task : Task.CompletedTask;
        window.RequestStableLauncherRestart();
        window.Close();
        await clearEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.False(window.IsEnabled);
        recoveryExpired.SetResult();

        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!window.IsEnabled)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
        Assert.Equal(1, handoff.Attempts);
        Assert.Equal(3, deadlines);
        Assert.NotNull(inner.Current.State!.PendingActivation);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.Equal(PendingActivationRecoveryStatus.Unknown, shell.Settings.PendingRecoveryStatus);
        Assert.False(shell.Settings.CanRetryPendingActivation);
        Assert.Contains("could not be confirmed", shell.Settings.VersionOperationStatus, StringComparison.Ordinal);

        clearRelease.SetResult(await inner.CancelPendingActivationAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PendingActivationRecoveryStatus.Cleared,
            await shell.Settings.RecheckPendingActivationStatusAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>A clear saved before its inventory return stays unknown until a fresh durable read confirms it.</summary>
    [AvaloniaFact]
    public async Task ClearSavedBeforeInventoryReturnsIsUnknownThenClearedOnRecheck()
    {
        var inner = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        _ = await inner.PrepareActivationAsync(ManagedAppVersion.Parse("0.10.4"), CancellationToken.None);
        TaskCompletionSource clearEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<VersionManagementSnapshot> inventoryReturn =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        IVersionManagementExperience experience =
            DispatchProxy.Create<IVersionManagementExperience, WindowLifetimeStorageProxy>();
        ((WindowLifetimeStorageProxy)experience).Call = (method, args) =>
        {
            if (method == nameof(IVersionManagementExperience.CancelPendingActivationAsync))
            {
                Task<VersionManagementSnapshot> saved = inner.CancelPendingActivationAsync(
                    TestContext.Current.CancellationToken).AsTask();
                Assert.True(saved.IsCompletedSuccessfully);
                _ = clearEntered.TrySetResult();
                return new ValueTask<VersionManagementSnapshot>(inventoryReturn.Task);
            }
            return typeof(IVersionManagementExperience).GetMethod(method)!.Invoke(inner, args);
        };
        var handoff = new RecordingStableLauncherHandoff(started: false);
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.5", experience, handoff);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        TaskCompletionSource recoveryExpired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int deadlines = 0;
        window.CloseDeadlineFactory = _ => Interlocked.Increment(ref deadlines) == 3
            ? recoveryExpired.Task : Task.CompletedTask;
        window.RequestStableLauncherRestart();
        window.Close();
        await clearEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Null(inner.Current.State!.PendingActivation);
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
        inventoryReturn.SetResult(inner.Current);
        Assert.Equal(PendingActivationRecoveryStatus.Cleared,
            await shell.Settings.RecheckPendingActivationStatusAsync(TestContext.Current.CancellationToken));

        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        window.Close();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(1, handoff.Attempts);
    }

    /// <summary>Version activation cannot enter while an earlier durable clear remains unsettled.</summary>
    [Fact]
    public async Task UnsettledPendingClearFencesNewActivation()
    {
        var inner = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        TaskCompletionSource clearEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<VersionManagementSnapshot> clearRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        IVersionManagementExperience experience =
            DispatchProxy.Create<IVersionManagementExperience, WindowLifetimeStorageProxy>();
        ((WindowLifetimeStorageProxy)experience).Call = (method, args) =>
        {
            if (method == nameof(IVersionManagementExperience.CancelPendingActivationAsync))
            {
                _ = clearEntered.TrySetResult();
                return new ValueTask<VersionManagementSnapshot>(clearRelease.Task);
            }
            return typeof(IVersionManagementExperience).GetMethod(method)!.Invoke(inner, args);
        };
        MainWindowViewModel shell = MainWindow.CreateStartupViewModel(
            PresentationTestHost.CreateServices("0.10.5", experience),
            ShellPreferenceSnapshot.Default);
        shell.Settings.ApplyVersionSnapshot(inner.Current);
        using var expiry = new CancellationTokenSource();
        Task<PendingActivationRecoveryStatus> recovery =
            shell.Settings.HandleLauncherHandoffFailureAsync(expiry.Token);
        await clearEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        expiry.Cancel();
        Assert.Equal(PendingActivationRecoveryStatus.Unknown, await recovery);
        SettingsVersionRowViewModel installed = Assert.Single(shell.Settings.VersionRows,
            row => row.Version == ManagedAppVersion.Parse("0.10.4"));
        shell.Settings.RequestVersionPrimaryActionCommand.Execute(installed);
        await shell.Settings.ConfirmVersionActionCommand.ExecuteAsync(null);

        Assert.Empty(inner.Activations);
        Assert.NotNull(inner.Current.State);
        clearRelease.SetResult(inner.Current);
    }

    /// <summary>Launcher handoff failure clears only the unlaunched request and leaves an actionable status.</summary>
    [Fact]
    public async Task LauncherHandoffFailureClearsPendingActivationAndReportsOpenState()
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

        Assert.Equal(PendingActivationRecoveryStatus.Cleared, status);
        Assert.Null(experience.Current.State!.PendingActivation);
        Assert.Contains("remains open", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
    }

    /// <summary>A failed clear is called kept only after a fresh durable read confirms pending activation.</summary>
    [AvaloniaFact]
    public async Task HandoffAndPendingClearFailureRequireDurableKeptConfirmation()
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
        Assert.Contains("confirmed to remain", viewModel.Settings.VersionOperationStatus, StringComparison.Ordinal);
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
