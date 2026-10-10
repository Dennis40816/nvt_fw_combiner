using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

[Collection(UiProcessWideObservationCollection.Name)]
public sealed partial class WindowLifetimeTests
{
    /// <summary>Repeated requests share the running drain and produce exactly one final close.</summary>
    [AvaloniaFact]
    public async Task RepeatedCloseRequestsShareAttemptAndFinalClose()
    {
        var held = new TaskCompletionSource<IEventBufferFormatConfigurationSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.5");
        using MainWindow window = CreateWindowWithHeldConfiguration(services, held);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        shell.OpenSettingsCommand.Execute(null);
        shell.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        Assert.True(shell.Settings.IsEventBufferFormatLoading);
        var deadline = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int deadlines = 0;
        window.CloseDeadlineFactory = duration =>
        {
            Assert.Equal(TimeSpan.FromSeconds(5), duration);
            deadlines++;
            return deadline.Task;
        };
        int finalClosings = 0;
        int closedEvents = 0;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closing += (_, _) =>
        {
            if (window.ClosePhase == WindowClosePhase.Closing)
            {
                finalClosings++;
            }
        };
        window.Closed += (_, _) =>
        {
            closedEvents++;
            _ = closed.TrySetResult();
        };
        try
        {
            window.Close();
            Task attempt = window.CloseAttempt;
            Assert.Equal(WindowClosePhase.Draining, window.ClosePhase);
            window.Close();
            Assert.Same(attempt, window.CloseAttempt);
            Assert.Equal(1, deadlines);
            Assert.False(attempt.IsCompleted);
            ShellPreloadSession preload = Assert.IsType<ShellPreloadSession>(
                window.FindControl<Border>("OptionalPreloadStatusHost")!.DataContext);
            _ = Assert.Throws<ObjectDisposedException>(() =>
            {
                _ = preload.RunCatalogAsync(services.CanonicalCatalogLoader,
                    _ => ValueTask.CompletedTask, retry: false, TestContext.Current.CancellationToken);
            });
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, finalClosings);
            held.SetException(new InvalidOperationException("Controlled configuration failure."));
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await attempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, finalClosings);
            Assert.Equal(1, closedEvents);
        }
        finally
        {
            _ = held.TrySetCanceled(TestContext.Current.CancellationToken);
            _ = deadline.TrySetResult();
        }
    }

    /// <summary>Both persistence tails finish, or their deadline expires, before the final Closing event.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task PersistenceSealsBeforeFinalClose(bool expire, bool preferencesFirst)
    {
        var historyStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var preferencesStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Release on the default scheduler so the persistence completion chain settles before the UI probe.
        var releaseHistory = new TaskCompletionSource();
        var releasePreferences = new TaskCompletionSource();
        var historyFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var preferencesFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new ConcurrentQueue<string>();
        int historyWrites = 0;
        int preferenceWrites = 0;
        string lastHistory = "";
        ILocalFileStore files = DispatchProxy.Create<ILocalFileStore, LifetimeReportStorageProxy>();
        ((LifetimeReportStorageProxy)files).Call = (name, args) => name switch
        {
            nameof(ILocalFileStore.ReadAsync) => throw new LocalFileNotFoundException("No saved history."),
            nameof(ILocalFileStore.WriteAsync) => new ValueTask(WriteAsync((string)args![0]!, (ReadOnlyMemory<byte>)args[1]!)),
            _ => throw new NotSupportedException(name),
        };
        async Task WriteAsync(string path, ReadOnlyMemory<byte> bytes)
        {
            bool history = Path.GetFileName(path) == "report-history.v1.json";
            bool first = (history ? Interlocked.Increment(ref historyWrites) : Interlocked.Increment(ref preferenceWrites)) == 1;
            _ = (history ? historyStarted : preferencesStarted).TrySetResult();
            await (history ? releaseHistory : releasePreferences).Task.ConfigureAwait(false);
            if (first)
            {
                order.Enqueue(history ? "history" : "preferences");
            }
            if (history)
            {
                Volatile.Write(ref lastHistory, Encoding.UTF8.GetString(bytes.Span));
            }
            _ = (history ? historyFinished : preferencesFinished).TrySetResult();
        }
        (PresentationHostServices services, _) = await PresentationTestHost.CreateServicesAsync(files);
        var held = new TaskCompletionSource<IEventBufferFormatConfigurationSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using MainWindow window = CreateWindowWithHeldConfiguration(services, held);
        window.Show();
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        shell.SelectedTheme = "Dark";
        shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "close-order"), "close-order.json");
        await Task.WhenAll(historyStarted.Task, preferencesStarted.Task)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        shell.OpenSettingsCommand.Execute(null);
        shell.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        var deadline = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sealing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.CloseDeadlineFactory = duration =>
        {
            Assert.Equal(TimeSpan.FromSeconds(5), duration);
            if (window.ClosePhase == WindowClosePhase.Sealing)
            {
                order.Enqueue("sealing");
                _ = sealing.TrySetResult();
            }
            return deadline.Task;
        };
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closing += (_, _) =>
        {
            if (window.ClosePhase == WindowClosePhase.Closing)
            {
                order.Enqueue("closing");
            }
        };
        window.Closed += (_, _) => _ = closed.TrySetResult();
        try
        {
            CloseConfirmed(window, shell);
            Assert.Equal(WindowClosePhase.Draining, window.ClosePhase);
            Assert.False(sealing.Task.IsCompleted);
            // Admission stays open while the drain runs: newer preference and history snapshots must still be queued and written.
            shell.SelectedTheme = "Light";
            shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "close-order-late"), "close-order-late.json");
            Dispatcher.UIThread.RunJobs();
            held.SetException(new InvalidOperationException("Controlled drain completion."));
            await sealing.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(WindowClosePhase.Sealing, window.ClosePhase);
            Assert.False(window.CloseAttempt.IsCompleted);
            if (expire)
            {
                order.Enqueue("deadline");
                deadline.SetResult();
            }
            else
            {
                await Task.Run(() => (preferencesFirst ? releasePreferences : releaseHistory).SetResult(),
                        TestContext.Current.CancellationToken)
                    .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                await (preferencesFirst ? preferencesFinished : historyFinished).Task
                    .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(WindowClosePhase.Sealing, window.ClosePhase);
                Assert.False(window.CloseAttempt.IsCompleted);
                (preferencesFirst ? releaseHistory : releasePreferences).SetResult();
            }
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await window.CloseAttempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            string[] expectedOrder = expire ? ["sealing", "deadline", "closing"] : preferencesFirst
                ? ["sealing", "preferences", "history", "closing"]
                : ["sealing", "history", "preferences", "closing"];
            Assert.Equal(expectedOrder, order.ToArray());
            if (!expire)
            {
                Assert.Equal(2, Volatile.Read(ref preferenceWrites));
                Assert.Contains("close-order-late.json", Volatile.Read(ref lastHistory), StringComparison.Ordinal);
            }
        }
        finally
        {
            _ = held.TrySetCanceled(TestContext.Current.CancellationToken);
            _ = releaseHistory.TrySetResult();
            _ = releasePreferences.TrySetResult();
            _ = deadline.TrySetResult();
            await Task.WhenAll(historyFinished.Task, preferencesFinished.Task)
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A deadline releases close while a late provider fault loses publication and run admission.</summary>
    [AvaloniaFact]
    public async Task DeadlinePreventsLateSaveFaultFromPublishingOrStartingRun()
    {
        const string fault = "Controlled late save failure.";
        var unobserved = new ConcurrentQueue<Exception>();
        void Capture(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            foreach (Exception exception in args.Exception.Flatten().InnerExceptions)
            {
                if (exception.Message == fault)
                {
                    unobserved.Enqueue(exception);
                    args.SetObserved();
                }
            }
        }
        TaskScheduler.UnobservedTaskException += Capture;
        using var stopCollect = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task collecting = Task.CompletedTask;
        try
        {
            WeakReference[] retired = await CloseBeforeLateSaveFaultAsync(fault);
            collecting = CollectRetiredTasksAsync(retired, stopCollect.Token);
            await collecting.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Empty(unobserved);
        }
        finally
        {
            stopCollect.Cancel();
            try
            {
                await collecting;
            }
            catch (OperationCanceledException) when (stopCollect.IsCancellationRequested)
            {
            }
            TaskScheduler.UnobservedTaskException -= Capture;
        }
    }

    private static async Task<WeakReference[]> CloseBeforeLateSaveFaultAsync(string fault)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Inline on the UI thread when released so the admitted discovery task faults before the GC probe.
        var discovery = new TaskCompletionSource<VersionManagementSnapshot>();
        IVersionManagementExperience experience =
            DispatchProxy.Create<IVersionManagementExperience, LifetimeReportStorageProxy>();
        ((LifetimeReportStorageProxy)experience).Call = (name, _) =>
        {
            Assert.Equal(nameof(IVersionManagementExperience.CheckAsync), name);
            entered.SetResult();
            return new ValueTask<VersionManagementSnapshot>(discovery.Task);
        };
        PresentationHostServices original = PresentationTestHost.CreateServices("0.10.5");
        var services = new PresentationHostServices(
            original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions, original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader, original.LocalFiles, original.LocalStateDirectory,
            experience, managedApplicationStartup: null, stableLauncherHandoff: null);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await ReportControlTestHost.AwaitHistoryReadyAsync(window);
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "late-fault"), "late-fault.json");
        string toast = shell.Reports.ReportToastText;
        var selected = new TaskCompletionSource<IStorageFile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        IStorageProvider picker = DispatchProxy.Create<IStorageProvider, LifetimeReportStorageProxy>();
        ((LifetimeReportStorageProxy)picker).Call = (name, _) => name == "SaveFilePickerAsync"
            ? selected.Task : throw new NotSupportedException(name);
        var modal = new ReportModal { DataContext = shell.Reports };
        Task saving = modal.SaveReportAsync(picker);
        var deadline = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.CloseDeadlineFactory = _ => deadline.Task;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => _ = closed.TrySetResult();
        int notifications = 0;
        shell.Reports.PropertyChanged += (_, _) =>
        {
            if (closed.Task.IsCompleted)
            {
                notifications++;
            }
        };
        try
        {
            CloseConfirmed(window, shell);
            Assert.Equal(WindowClosePhase.Draining, window.ClosePhase);
            Assert.False(saving.IsCompleted);
            deadline.SetResult();
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await window.CloseAttempt.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.False(saving.IsCompleted);
            Assert.False(discovery.Task.IsCompleted);
            discovery.SetException(new InvalidOperationException(fault));
            Assert.True(discovery.Task.IsFaulted);
            selected.SetException(new InvalidOperationException(fault));
            await saving.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await shell.Reports.WhenSavesIdleAsync()
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(toast, shell.Reports.ReportToastText);
            Assert.Equal(0, notifications);
            bool started = false;
            UiRunResultViewModel? result = await shell.RunSession.RunCompositionAsync(
                shell.Merge.CaptureRunContext(ExperienceIds.StandardMerge, build: true), true,
                (_, _) =>
                {
                    started = true;
                    throw new InvalidOperationException("A late operation regained admission.");
                }, (_, _) => { });
            Assert.Null(result);
            Assert.False(started);
            // Do not await or inspect the discovery fault; its window owners must observe the drain aggregates.
            return [new WeakReference(discovery.Task), new WeakReference(selected.Task), new WeakReference(saving)];
        }
        finally
        {
            _ = selected.TrySetResult(null);
            _ = discovery.TrySetCanceled(TestContext.Current.CancellationToken);
            _ = deadline.TrySetResult();
        }
    }

    private static async Task CollectRetiredTasksAsync(WeakReference[] retired, CancellationToken cancellationToken)
    {
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A pause lets dispatcher timers (the toast timer) release the retired window before the next probe.
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        while (retired.Any(static task => task.IsAlive));
    }

    private static void CloseConfirmed(MainWindow window, MainWindowViewModel shell)
    {
        window.Close();
        if (shell.Navigation.IsExitConfirmationOpen)
        {
            shell.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
        }
    }

    private static MainWindow CreateWindowWithHeldConfiguration(
        PresentationHostServices original,
        TaskCompletionSource<IEventBufferFormatConfigurationSession> held)
    {
        var services = new PresentationHostServices(
            original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions, original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader, original.LocalFiles, original.LocalStateDirectory,
            versionManagement: null, managedApplicationStartup: null, stableLauncherHandoff: null,
            eventBufferFormatConfigurationSessionFactory: _ => held.Task);
        return new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
    }
}
