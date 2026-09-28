using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.VisualTree;
using System.Reflection;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class VersionManagementSettingsTests
{
    /// <summary>The approved failed-handoff notice renders through the real Settings Version page.</summary>
    [AvaloniaTheory]
    [InlineData(false, false, "kept-retry-en-light")]
    [InlineData(false, true, "unknown-en-light")]
    [InlineData(true, false, "kept-retry-zh-dark")]
    [InlineData(true, true, "unknown-zh-dark")]
    public async Task W6aSettingsRecoveryNoticeMatchesApprovedState(
        bool chinese, bool unknown, string imageName)
    {
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            PresentationTestHost.CreateServices("0.10.5"), ShellPreferenceSnapshot.Default)
        {
            Width = 1024,
            Height = 850,
        };
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        await window.StartupWork;
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
        shell.SelectedTheme = chinese ? "Dark" : "Light";
        shell.OpenSettingsCommand.Execute(null);
        shell.Settings.SelectSectionCommand.Execute(SettingsSection.Version);
        _ = await shell.Settings.HandleLauncherHandoffFailureAsync(TestContext.Current.CancellationToken);
        typeof(SettingsViewModel).GetProperty(nameof(SettingsViewModel.PendingRecoveryStatus),
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(shell.Settings, unknown
                ? PendingActivationRecoveryStatus.Unknown
                : PendingActivationRecoveryStatus.ConfirmedKept);
        shell.Settings.PublishPendingRecoveryStatus();
        Dispatcher.UIThread.RunJobs();

        Border notice = Assert.Single(window.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "PendingRecoveryNotice");
        Button retry = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
            button => button.Name == "RetryPendingActivationButton");
        string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            _ = Directory.CreateDirectory(outputDirectory);
            using Avalonia.Media.Imaging.WriteableBitmap? frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(outputDirectory, $"w6a-settings-{imageName}.png"));
        }
        Assert.True(notice.IsVisible);
        Assert.InRange(notice.Bounds.Width, 558, 562);
        Assert.Equal(!unknown, retry.IsVisible);
        Assert.Equal(!unknown, shell.Settings.RetryPendingActivationCommand.CanExecute(null));
        Assert.Equal(chinese ? "重試" : "Retry", AutomationProperties.GetName(retry));
        Assert.Equal(chinese ? "重試" : "Retry", retry.Content);
        Assert.Equal(unknown
            ? chinese
                ? "無法啟動穩定啟動器，也無法確認待處理的版本切換狀態；已在執行的清除作業仍可能改變它。關閉程式不會啟動啟動器，也不會再開始清除。"
                : "The stable launcher could not be started, and the pending version switch could not be confirmed. A clear already in progress may still change it. Closing the app starts no launcher and no new clear."
            : chinese
                ? "無法啟動穩定啟動器。版本切換仍待處理，設定未還原；關閉程式後也會保留。"
                : "The stable launcher could not be started. The version switch is still pending and was not rolled back; it stays pending if you close the app.",
            shell.Settings.PendingRecoveryMessage);
        Assert.Equal(shell.Settings.PendingRecoveryMessage, AutomationProperties.GetName(notice));
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(notice));
        if (!unknown)
        {
            Assert.True(retry.Focusable);
            Assert.True(retry.Focus());
            Assert.True(retry.IsFocused);
        }

    }

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

    /// <summary>A launcher that never returns cannot hold the window in HandingOff forever.</summary>
    [AvaloniaFact]
    public async Task StalledLauncherDeadlineRecoversAndIgnoresLateSuccess()
    {
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var handoff = new GatedWindowLifetimeHandoff();
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            PresentationTestHost.CreateServices("0.10.5", experience, handoff), ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        var expiry = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int deadlines = 0;
        window.CloseDeadlineFactory = _ => ++deadlines == 3 ? expiry.Task : Task.CompletedTask;
        window.RequestStableLauncherRestart();
        window.Close();
        await handoff.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.HandingOff, window.ClosePhase);
        expiry.SetResult();
        await window.CloseAttempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.Open, window.ClosePhase);
        Assert.True(window.IsEnabled);
        handoff.Release(started: true);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(WindowClosePhase.Open, window.ClosePhase);
        Assert.Equal(1, handoff.Attempts);
    }

    /// <summary>An already expired host deadline never admits launcher start.</summary>
    [AvaloniaFact]
    public async Task ExpiredHandoffDeadlineDoesNotCallLauncher()
    {
        var handoff = new RecordingStableLauncherHandoff(started: false);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            PresentationTestHost.CreateServices("0.10.5", new RecordingVersionExperience(
                Snapshot(retentionReviewDue: false)), handoff), ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        window.CloseDeadlineFactory = _ => Task.CompletedTask;
        window.RequestStableLauncherRestart();
        window.Close();
        await window.CloseAttempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(0, handoff.Attempts);
        Assert.Equal(WindowClosePhase.Open, window.ClosePhase);
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
        var handoff = new FailThenSucceedHandoff();
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
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        await shell.Settings.RetryPendingActivationCommand.ExecuteAsync(null);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(2, handoff.Attempts);
        Assert.Equal(WindowClosePhase.Closed, window.ClosePhase);
        Assert.NotNull(experience.Current.State!.PendingActivation);
    }

    private sealed class FailThenSucceedHandoff : IStableLauncherHandoff
    {
        internal int Attempts { get; private set; }

        public ValueTask<bool> TryStartLauncherAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(++Attempts == 2);
        }
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

    /// <summary>Activation before the terminal decision upgrades Close, including just after drain expiry.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActivationDuringDrainStartsLauncherBeforeFinalClose(bool afterDeadline)
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
        var expiry = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (afterDeadline)
        {
            int deadlineCount = 0;
            window.CloseDeadlineFactory = _ => ++deadlineCount == 1
                ? expiry.Task : Task.Delay(TimeSpan.FromSeconds(5));
        }
        window.Close();
        Assert.Equal(WindowClosePhase.Draining, window.ClosePhase);
        if (afterDeadline)
        {
            expiry.SetResult();
        }
        window.RequestStableLauncherRestart();
        window.Close();
        if (!afterDeadline)
        {
            held.SetException(new InvalidOperationException("synthetic factory fault"));
        }
        await handoff.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.HandingOff, window.ClosePhase);
        Assert.False(closed.Task.IsCompleted);
        handoff.Release(started: true);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        if (afterDeadline)
        {
            held.SetException(new InvalidOperationException("late factory fault"));
        }
    }

    /// <summary>A real accepted activation during sealed flush reaches the window before final revoke.</summary>
    [AvaloniaFact]
    public async Task ActivationPreparedDuringSealingStartsLauncher()
    {
        var inner = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var prepareEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var prepareRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IVersionManagementExperience experience =
            DispatchProxy.Create<IVersionManagementExperience, WindowLifetimeStorageProxy>();
        ((WindowLifetimeStorageProxy)experience).Call = (method, args) =>
            method == nameof(IVersionManagementExperience.PrepareActivationAsync)
                ? new ValueTask<VersionManagerState>(PrepareAsync())
                : typeof(IVersionManagementExperience).GetMethod(method)!.Invoke(inner, args);
        async Task<VersionManagerState> PrepareAsync()
        {
            prepareEntered.SetResult();
            await prepareRelease.Task;
            return await inner.PrepareActivationAsync(ManagedAppVersion.Parse("0.10.4"), CancellationToken.None);
        }

        var handoff = new GatedWindowLifetimeHandoff();
        PresentationHostServices original = PresentationTestHost.CreateServices("0.10.5", experience, handoff);
        var files = new HeldPreferenceWrite(original.LocalFiles,
            ShellPreferenceFileStore.PathIn(original.LocalStateDirectory));
        var services = new PresentationHostServices(original.Composition, original.FileReveal,
            original.SupportMatrix, original.SystemInformation, original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions, original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader, files, original.LocalStateDirectory,
            experience, null, handoff);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        await window.StartupWork;
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        shell.Settings.ApplyVersionSnapshot(inner.Current);
        SettingsVersionRowViewModel installed = Assert.Single(shell.Settings.VersionRows,
            row => row.Version == ManagedAppVersion.Parse("0.10.4"));
        shell.Settings.RequestVersionPrimaryActionCommand.Execute(installed);
        Task confirmation = shell.Settings.ConfirmVersionActionCommand.ExecuteAsync(null);
        await prepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        shell.SelectedTheme = "Dark";
        await files.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var flushExpiry = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int deadlines = 0;
        window.CloseDeadlineFactory = _ => ++deadlines switch
        {
            1 => Task.CompletedTask,
            2 => flushExpiry.Task,
            _ => Task.Delay(TimeSpan.FromSeconds(5)),
        };
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.SetResult();
        window.Close();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (window.ClosePhase != WindowClosePhase.Sealing)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
        prepareRelease.SetResult();
        while (inner.Current.State?.PendingActivation is null)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
        files.Release();
        Task first = await Task.WhenAny(handoff.Entered, closed.Task)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Same(handoff.Entered, first);
        handoff.Release(started: true);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await confirmation;
    }

    private sealed class HeldPreferenceWrite(ILocalFileStore inner, string preferencePath) : ILocalFileStore
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Entered => _entered.Task;
        internal void Release()
        {
            _released.SetResult();
        }
        public ValueTask<T> ReadAsync<T>(string path, long maximumBytes,
            Func<Stream, CancellationToken, ValueTask<T>> project, CancellationToken cancellationToken)
        {
            return inner.ReadAsync(path, maximumBytes, project, cancellationToken);
        }
        public ValueTask<string> ReadTextAsync(string path, long maximumBytes,
            CancellationToken cancellationToken, Action<LocalFileReadProgress>? progress = null)
        {
            return inner.ReadTextAsync(path, maximumBytes, cancellationToken, progress);
        }
        public ValueTask<string> ReadTextAsync(Func<CancellationToken, ValueTask<Stream>> openReadAsync,
            long maximumBytes, CancellationToken cancellationToken)
        {
            return inner.ReadTextAsync(openReadAsync, maximumBytes, cancellationToken);
        }
        public async ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken)
        {
            if (path == preferencePath)
            {
                _ = _entered.TrySetResult();
                await _released.Task;
            }
            await inner.WriteAsync(path, bytes, cancellationToken);
        }
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
        internal int Attempts { get; private set; }

        internal void Release(bool started)
        {
            _ = _released.TrySetResult(started);
        }

        public async ValueTask<bool> TryStartLauncherAsync(CancellationToken cancellationToken)
        {
            Attempts++;
            _ = _entered.TrySetResult();
            return await _released.Task;
        }
    }

    /// <summary>Repeated external Close never stacks attempts across drain, handoff, and recovery.</summary>
    [AvaloniaFact]
    public async Task ReentrantCloseAcrossDrainHandoffAndRecoveryKeepsOneAttempt()
    {
        var inner = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        _ = await inner.PrepareActivationAsync(ManagedAppVersion.Parse("0.10.4"), CancellationToken.None);
        var recoveryEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recoveryRead = new TaskCompletionSource<VersionManagementSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bool holdRecoveryRead = false;
        IVersionManagementExperience experience =
            DispatchProxy.Create<IVersionManagementExperience, WindowLifetimeStorageProxy>();
        ((WindowLifetimeStorageProxy)experience).Call = (method, args) =>
        {
            if (method == nameof(IVersionManagementExperience.InitializeAsync) && holdRecoveryRead)
            {
                _ = recoveryEntered.TrySetResult();
                return new ValueTask<VersionManagementSnapshot>(recoveryRead.Task);
            }
            return typeof(IVersionManagementExperience).GetMethod(method)!.Invoke(inner, args);
        };
        var handoff = new GatedWindowLifetimeHandoff();
        var factory = new TaskCompletionSource<Application.Configuration.IEventBufferFormatConfigurationSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        PresentationHostServices original = PresentationTestHost.CreateServices("0.10.5", experience, handoff);
        var services = new PresentationHostServices(
            original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions, original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader, original.LocalFiles, original.LocalStateDirectory,
            experience, null, handoff, _ => factory.Task);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        await window.StartupWork;
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        shell.OpenSettingsCommand.Execute(null);
        shell.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        Assert.True(shell.Settings.IsEventBufferFormatLoading);
        var neverExpires = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.CloseDeadlineFactory = _ => neverExpires.Task;
        window.RequestStableLauncherRestart();
        window.Close();
        Task first = window.CloseAttempt;
        Assert.Equal(WindowClosePhase.Draining, window.ClosePhase);
        window.Close();
        window.Close();
        Assert.Same(first, window.CloseAttempt);
        factory.SetException(new InvalidOperationException("factory stopped"));
        await handoff.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.HandingOff, window.ClosePhase);
        window.Close();
        window.Close();
        Assert.Same(first, window.CloseAttempt);
        holdRecoveryRead = true;
        handoff.Release(started: false);
        await recoveryEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.Recovering, window.ClosePhase);
        window.Close();
        window.Close();
        Assert.Same(first, window.CloseAttempt);
        recoveryRead.SetResult(inner.Current);
        await first.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.Open, window.ClosePhase);
        Assert.Equal(1, handoff.Attempts);
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        window.Close();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(WindowClosePhase.Closed, window.ClosePhase);
        Assert.Equal(1, handoff.Attempts);
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

    /// <summary>Recovery cancels the old startup generation before its READY callback returns.</summary>
    [AvaloniaFact]
    public async Task ReadyFromOldSessionAfterFailedHandoffCannotPublish()
    {
        VersionManagementSnapshot late = Snapshot(retentionReviewDue: true) with
        {
            ShouldPromptForUpdate = true,
        };
        var experience = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var startup = new IgnoringReadyStartup();
        var handoff = new RecordingStableLauncherHandoff(started: false);
        PresentationHostServices original = PresentationTestHost.CreateServices("0.10.5", experience, handoff);
        var services = new PresentationHostServices(
            original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions, original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader, original.LocalFiles, original.LocalStateDirectory,
            experience, startup, handoff);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await startup.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        window.RequestStableLauncherRestart();
        Assert.False(await window.TryCompleteStableLauncherHandoffAsync());
        Assert.Equal(WindowClosePhase.Open, window.ClosePhase);
        startup.Release(new(ApplicationReadySignalOutcome.Reported, late));
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.False(shell.Settings.HasRetentionReview);
        Assert.False(shell.IsSettingsModalOpen);
        Assert.False(shell.Settings.IsSourceChecking);
        Assert.Equal(1, handoff.Attempts);
    }

    /// <summary>A post-READY discovery fault is owned by the startup session during Close.</summary>
    [AvaloniaFact]
    public async Task PostReadyDiscoveryFaultDuringCloseIsObservedWithoutPublication()
    {
        var inner = new RecordingVersionExperience(Snapshot(retentionReviewDue: false));
        var checkEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checkRelease = new TaskCompletionSource<VersionManagementSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IVersionManagementExperience experience =
            DispatchProxy.Create<IVersionManagementExperience, WindowLifetimeStorageProxy>();
        ((WindowLifetimeStorageProxy)experience).Call = (method, args) =>
        {
            if (method == nameof(IVersionManagementExperience.CheckAsync))
            {
                _ = checkEntered.TrySetResult();
                return new ValueTask<VersionManagementSnapshot>(checkRelease.Task);
            }
            return typeof(IVersionManagementExperience).GetMethod(method)!.Invoke(inner, args);
        };
        var startup = new IgnoringReadyStartup();
        PresentationHostServices original = PresentationTestHost.CreateServices("0.10.5", experience);
        var services = new PresentationHostServices(
            original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions, original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader, original.LocalFiles, original.LocalStateDirectory,
            experience, startup, null);
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
            startup.Release(new(ApplicationReadySignalOutcome.Reported, inner.Current));
            await checkEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            window.Closed += (_, _) => _ = closed.TrySetResult();
            window.Close();
            checkRelease.SetException(new InvalidOperationException("late discovery fault"));
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.False(escaped);
            Assert.False(shell.IsSettingsModalOpen);
            Assert.False(shell.Settings.HasRetentionReview);
            Assert.Equal(WindowClosePhase.Closed, window.ClosePhase);
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= Capture;
        }
    }

    /// <summary>The real install command requests activation on its subscribed window before Close settles.</summary>
    [AvaloniaFact]
    public async Task ConfirmedInstallUsesWindowActivationRequestedPath()
    {
        VersionManagementSnapshot initial = Snapshot(retentionReviewDue: false);
        UpdateCatalogVersionSnapshot available = CatalogVersion("0.10.6");
        initial = initial with
        {
            Catalog = new([available]),
            VerifiedCandidate = new(available.Version, available.Identity, available.ReleaseNotes),
            SourceStatus = VersionSourceStatus.Connected,
        };
        var experience = new RecordingVersionExperience(initial);
        var handoff = new GatedWindowLifetimeHandoff();
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            PresentationTestHost.CreateServices("0.10.5", experience, handoff), ShellPreferenceSnapshot.Default);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        await window.StartupWork;
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        shell.Settings.ApplyVersionSnapshot(initial);
        SettingsVersionRowViewModel candidate = Assert.Single(shell.Settings.VersionRows,
            row => row.Version == available.Version);
        shell.Settings.RequestVersionPrimaryActionCommand.Execute(candidate);
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        await shell.Settings.ConfirmVersionActionCommand.ExecuteAsync(null);
        await handoff.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal([available.Version], experience.Installations);
        Assert.Equal([available.Version], experience.Activations);
        Assert.Equal(WindowClosePhase.HandingOff, window.ClosePhase);
        Assert.False(closed.Task.IsCompleted);
        handoff.Release(started: true);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(1, handoff.Attempts);
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
        Assert.Contains("could not be confirmed", shell.Settings.VersionOperationStatus, StringComparison.Ordinal);
        shell.OpenSettingsCommand.Execute(null);
        shell.SelectedLanguage = "Traditional Chinese";
        Assert.Contains("目前無法確認已儲存的待處理版本切換", shell.Settings.VersionOperationStatus,
            StringComparison.Ordinal);
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
