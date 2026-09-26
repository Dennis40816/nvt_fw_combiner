using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>F08: a failed local-state save stays visible and retryable in the status row until a save succeeds.</summary>
public sealed class LocalStateSaveNoticeTests
{
    private const string NoticeHostName = "LocalStateSaveNoticeHost";
    private const string RetryButtonName = "LocalStateSaveRetryButton";
    private const string ReferenceCase = "testdata/golden/canonical/NT51923/standard-merge/gen-flash/" +
        "topology-unscoped/nt51923-gen-flash/inputs/";
    private static readonly string PreferencesPath = ShellPreferenceFileStore.DefaultPreferencesPath;
    private static readonly string HistoryPath = ReportHistoryFileStore.DefaultHistoryPath;

    /// <summary>
    /// A failed preference save shows the notice without blocking the shell; a failed retry keeps it, a
    /// successful retry through the same coordinator removes it and persists the latest preferences.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedSaveStaysVisibleUntilRetrySucceeds(bool chinese)
    {
        using var workspace = TempWorkspace.Create("f08-save-notice-retry");
        (PresentationHostServices services, ScriptedStateFiles files) = await CreateScriptedServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services,
            ShellPreferenceSnapshot.Default)
        { Width = 1280, Height = 860 };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            var shell = (MainWindowViewModel)window.DataContext!;
            LocalStateSaveNoticeViewModel notice = Notice(window);
            if (chinese)
            {
                shell.SelectedLanguage = "Traditional Chinese";
                await WaitUntilAsync(() => files.Completed(PreferencesPath) >= 1);
            }
            List<bool> notifiedOnUiThread = [];
            int publications = 0;
            notice.PropertyChanged += (_, e) =>
            {
                notifiedOnUiThread.Add(Dispatcher.UIThread.CheckAccess());
                if (e.PropertyName == nameof(LocalStateSaveNoticeViewModel.IsVisible))
                {
                    publications++;
                }
            };
            Border host = window.FindControl<Border>(NoticeHostName)!;
            Button retry = window.FindControl<Button>(RetryButtonName)!;
            Assert.False(notice.IsVisible);
            Assert.False(host.IsVisible);

            files.Fail(PreferencesPath, static () => new UnauthorizedAccessException("synthetic access denied"));
            shell.ExpandInputDetailsByDefault = !shell.ExpandInputDetailsByDefault;
            await WaitUntilAsync(() => notice.IsVisible);
            window.UpdateLayout();

            Assert.True(host.IsEffectivelyVisible);
            Assert.Equal(chinese ? "最近的工作未儲存" : "Recent work not saved", notice.Title);
            Assert.Equal(
                chinese
                    ? "無法儲存最近的工作狀態：存取遭拒。目前的工作不受影響。"
                    : "Couldn't save your recent work state: access denied. Your current work isn't affected.",
                notice.Detail);
            Assert.Equal(
                (chinese ? "偏好設定" : "Preferences") + ": synthetic access denied",
                notice.DetailToolTip);
            Assert.Equal($"{notice.Title} — {notice.Detail}", notice.AccessibleStatus);
            Assert.Equal(notice.AccessibleStatus, AutomationProperties.GetName(host));
            Assert.Equal(chinese ? "重試" : "Retry", AutomationProperties.GetName(retry));
            Assert.Equal(chinese ? "重試" : "Retry", ToolTip.GetTip(retry));
            Assert.True(retry.IsEffectivelyEnabled);
            Assert.True(window.FindControl<Grid>("ShellInteractionHost")!.IsEnabled);
            Assert.True(shell.ShowMergeCommand.CanExecute(null));

            // Enter on the focused icon button retries; the save fails again and the notice stays.
            int attempts = files.Completed(PreferencesPath);
            int published = publications;
            Assert.True(retry.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            await WaitUntilAsync(() => files.Completed(PreferencesPath) > attempts && publications > published);
            Assert.True(notice.IsVisible);
            Assert.True(host.IsEffectivelyVisible);
            Assert.True(retry.IsEffectivelyEnabled);

            // Space retries again; this time the same coordinator saves the latest snapshot and the notice clears.
            files.Fail(PreferencesPath, null);
            Assert.True(retry.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            await WaitUntilAsync(() => !notice.IsVisible);
            Assert.False(host.IsVisible);
            Assert.Equal(string.Empty, notice.Detail);
            Assert.False(notice.RetryCommand.CanExecute(null));
            ShellPreferenceSnapshot persisted = await ShellPreferenceFileStore.LoadAsync(files, PreferencesPath);
            Assert.Equal(shell.ExportShellPreferences(), persisted);
            Assert.NotEmpty(notifiedOnUiThread);
            Assert.All(notifiedOnUiThread, Assert.True);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>
    /// The next automatic save of a failed state clears that state only; the notice follows the latest
    /// unresolved reason and disappears when every failed state has saved.
    /// </summary>
    [AvaloniaFact]
    public async Task NextAutomaticSaveClearsOnlyItsOwnFailure()
    {
        using var workspace = TempWorkspace.Create("f08-save-notice-automatic");
        (PresentationHostServices services, ScriptedStateFiles files) = await CreateScriptedServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services,
            ShellPreferenceSnapshot.Default);
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            var shell = (MainWindowViewModel)window.DataContext!;
            LocalStateSaveNoticeViewModel notice = Notice(window);
            Border host = window.FindControl<Border>(NoticeHostName)!;

            files.Fail(HistoryPath, static () => new IOException("synthetic disk full", unchecked((int)0x80070070)));
            shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "first"), "first.json");
            await WaitUntilAsync(() => notice.IsVisible);
            ShellTextResources english = shell.Text;
            Assert.Equal(Detail(english, english.LocalStateSaveStorageFullReason), notice.Detail);
            Assert.Equal("Report history: synthetic disk full", notice.DetailToolTip);

            // A language change while the notice is shown relocalizes it through the window's Text relay.
            shell.SelectedLanguage = "Traditional Chinese";
            await WaitUntilAsync(() => notice.Title == "最近的工作未儲存");
            ShellTextResources text = shell.Text;
            Assert.Equal("無法儲存最近的工作狀態：磁碟空間不足。目前的工作不受影響。", notice.Detail);
            Assert.Equal("報告記錄: synthetic disk full", notice.DetailToolTip);
            Assert.Equal(notice.AccessibleStatus, AutomationProperties.GetName(host));
            Assert.Equal("重試", AutomationProperties.GetName(window.FindControl<Button>(RetryButtonName)!));

            files.Fail(PreferencesPath, static () => new IOException("synthetic in use", unchecked((int)0x80070020)));
            shell.IsReducedMotionEnabled = !shell.IsReducedMotionEnabled;
            await WaitUntilAsync(() => notice.DetailToolTip.Contains(text.LocalStatePreferencesLabel, StringComparison.Ordinal));
            Assert.Equal(Detail(text, text.LocalStateSaveFileInUseReason), notice.Detail);
            Assert.Equal(
                $"報告記錄: synthetic disk full{Environment.NewLine}偏好設定: synthetic in use",
                notice.DetailToolTip);

            files.Fail(HistoryPath, null);
            shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "second"), "second.json");
            await WaitUntilAsync(() => !notice.DetailToolTip.Contains(text.LocalStateReportHistoryLabel, StringComparison.Ordinal));
            Assert.True(notice.IsVisible);
            Assert.Equal("偏好設定: synthetic in use", notice.DetailToolTip);
            Assert.Equal(Detail(text, text.LocalStateSaveFileInUseReason), notice.Detail);

            files.Fail(PreferencesPath, null);
            shell.IsReducedMotionEnabled = !shell.IsReducedMotionEnabled;
            await WaitUntilAsync(() => !notice.IsVisible);
            Assert.False(host.IsVisible);
            Assert.Equal(2, (await ReportHistoryFileStore.LoadAsync(files, HistoryPath, CancellationToken.None)).Count);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>Once the window is disposed, a later save outcome and Retry no longer change the notice.</summary>
    [AvaloniaFact]
    public async Task DisposedWindowIgnoresLaterSaveOutcomes()
    {
        using var workspace = TempWorkspace.Create("f08-save-notice-dispose");
        (PresentationHostServices services, ScriptedStateFiles files) = await CreateScriptedServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services,
            ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        LocalStateSaveNoticeViewModel notice = Notice(window);
        int changes = 0;
        notice.PropertyChanged += (_, _) => changes++;
        files.Fail(PreferencesPath, static () => new UnauthorizedAccessException("late failure"));
        WriteHold late = files.HoldNextWrite(PreferencesPath);
        shell.ExpandInputDetailsByDefault = !shell.ExpandInputDetailsByDefault;
        await late.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        window.Dispose();
        late.Released.SetResult();
        // Saves are serialized, so the next write starts only after the late failure was reported.
        files.Fail(PreferencesPath, null);
        WriteHold next = files.HoldNextWrite(PreferencesPath);
        shell.ExpandInputDetailsByDefault = !shell.ExpandInputDetailsByDefault;
        await next.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.False(notice.IsVisible);
        Assert.Equal(0, changes);
        Assert.False(notice.RetryCommand.CanExecute(null));
        next.Released.SetResult();
        await WaitUntilAsync(() => files.Completed(PreferencesPath) == 2);
        Dispatcher.UIThread.RunJobs();
        Assert.False(notice.IsVisible);
        Assert.Equal(0, changes);
    }

    /// <summary>
    /// The notice keeps the approved reference hierarchy in the Merge page status row: warning strip aligned
    /// with the content, icon, bold title, separator, one-line detail and a round icon-only Retry, with the
    /// startup status row's geometry and 13 px text; both status rows stack without overlap.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task NoticeMatchesApprovedStatusRowGeometry(bool dark, bool chinese)
    {
        string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        using var workspace = TempWorkspace.Create("f08-save-notice-visual");
        (PresentationHostServices services, ScriptedStateFiles files) = await CreateScriptedServicesAsync(workspace);
        string[] arguments = ["--workflow", "standard-merge", "--ic", "NT51923", "--ic-num", "single",
            "--dp", RepositoryPaths.FromRepositoryRoot(ReferenceCase + "nt51923-dp-input.bin"),
            "--tp", RepositoryPaths.FromRepositoryRoot(ReferenceCase + "nt51923-tp-input.bin")];
        using var window = new MainWindow(UiLaunchOptions.Parse(arguments), StartupTraceSession.Disabled, services,
            ShellPreferenceSnapshot.Default)
        { Width = 1440, Height = 1100 };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            var shell = (MainWindowViewModel)window.DataContext!;
            Border preloadHost = window.FindControl<Border>("OptionalPreloadStatusHost")!;
            var preload = (ShellPreloadSession)preloadHost.DataContext!;
            await WaitUntilAsync(() => !preload.HasOptionalStatus &&
                window.FindControl<Grid>("ShellInteractionHost")!.IsEnabled, TimeSpan.FromSeconds(45));
            int saved = 0;
            if (dark)
            {
                shell.SelectedTheme = "Dark";
                await WaitUntilAsync(() => files.Completed(PreferencesPath) > saved);
                saved = files.Completed(PreferencesPath);
            }
            if (chinese)
            {
                shell.SelectedLanguage = "Traditional Chinese";
                await WaitUntilAsync(() => files.Completed(PreferencesPath) > saved);
            }
            files.Fail(PreferencesPath, static () => new UnauthorizedAccessException("synthetic access denied"));
            shell.IsReducedMotionEnabled = !shell.IsReducedMotionEnabled;
            LocalStateSaveNoticeViewModel notice = Notice(window);
            await WaitUntilAsync(() => notice.IsVisible);
            Render(window);

            Border host = window.FindControl<Border>(NoticeHostName)!;
            Button retry = window.FindControl<Button>(RetryButtonName)!;
            Rect strip = Bounds(host, window);
            Rect content = Bounds(
                Assert.Single(window.GetVisualDescendants().OfType<ScrollViewer>(),
                    viewer => viewer.Classes.Contains("contentScrollSurface")),
                window);
            Assert.Equal(preloadHost.Margin, host.Margin);
            Assert.Equal(preloadHost.Padding, host.Padding);
            Assert.Equal(preloadHost.BorderThickness, host.BorderThickness);
            Assert.Equal(preloadHost.CornerRadius, host.CornerRadius);
            Assert.Equal(content.Left + 28, strip.Left, tolerance: 1);
            Assert.Equal(window.Bounds.Width - 28, strip.Right, tolerance: 1);
            Assert.Equal(window.Bounds.Height - 12, strip.Bottom, tolerance: 1);
            Assert.True(content.Bottom <= strip.Top + 0.5, $"{content} overlaps {strip}");
            double contentHeight = strip.Height - host.Padding.Top - host.Padding.Bottom -
                host.BorderThickness.Top - host.BorderThickness.Bottom;
            Assert.Equal(34, contentHeight, tolerance: 1);
            AssertBrush(host, host.Background, "NfcWarningSurfaceBrush");
            AssertBrush(host, host.BorderBrush, "NfcWarningBorderBrush");

            TextBlock[] texts = [.. host.GetVisualDescendants().OfType<TextBlock>()
                .Where(block => block.FindAncestorOfType<Button>() is null)];
            TextBlock title = Assert.Single(texts, block => block.Text == notice.Title);
            TextBlock separator = Assert.Single(texts, block => block.Text == "·");
            TextBlock detail = Assert.Single(texts, block => block.Text == notice.Detail);
            Assert.All(texts, block => Assert.Equal(13, block.FontSize));
            Assert.Equal(FontWeight.Bold, title.FontWeight);
            AssertBrush(title, title.Foreground, "NfcWarningTextStrongBrush");
            AssertBrush(separator, separator.Foreground, "NfcWarningTextStrongBrush");
            AssertBrush(detail, detail.Foreground, "NfcWarningTextBrush");
            Assert.Equal(TextTrimming.CharacterEllipsis, detail.TextTrimming);
            Assert.Equal(notice.DetailToolTip, ToolTip.GetTip(detail));
            Panel icon = Assert.Single(host.GetVisualDescendants().OfType<Panel>(),
                panel => panel.Children.Count == 2 && panel.Children.All(static child => child is ShapePath));
            Control[] ordered = [icon, title, separator, detail, retry];
            for (int index = 1; index < ordered.Length; index++)
            {
                Assert.True(Bounds(ordered[index - 1], window).Right <= Bounds(ordered[index], window).Left + 0.5,
                    $"{ordered[index - 1].GetType().Name} overlaps {ordered[index].GetType().Name}");
            }
            foreach (Control part in ordered)
            {
                Assert.Equal(strip.Center.Y, Bounds(part, window).Center.Y, tolerance: 1);
            }
            Assert.Equal(strip.Right - host.BorderThickness.Right - host.Padding.Right, Bounds(retry, window).Right, tolerance: 1);
            Assert.Equal(34, retry.Bounds.Width, tolerance: 1);
            Assert.Equal(34, retry.Bounds.Height, tolerance: 1);
            ContentPresenter retryPresenter = Assert.Single(retry.GetVisualDescendants().OfType<ContentPresenter>());
            Assert.True(retryPresenter.CornerRadius.TopLeft >= 17);
            Assert.Empty(retry.GetVisualDescendants().OfType<TextBlock>());
            ShapePath refresh = Assert.Single(retry.GetVisualDescendants().OfType<ShapePath>());
            Assert.Equal(
                Assert.IsType<ISolidColorBrush>(retry.Foreground, exactMatch: false).Color,
                Assert.IsType<ISolidColorBrush>(refresh.Stroke, exactMatch: false).Color);
            // Sample inside the round button's ring (the 16 px icon area) and inside the 18 px warning icon.
            Assert.True(CountInkPixels(window, retry, inset: 9) >= 20, "The Retry icon did not render.");
            Assert.True(CountInkPixels(window, icon, inset: 1) >= 20, "The warning icon did not render.");
            Save(chinese ? "zh" : "en", dark ? "dark" : "light", "single");

            // With the startup status also present, both rows stack in the same status area without overlap.
            preloadHost.IsVisible = true;
            Render(window);
            Rect stackedNotice = Bounds(host, window);
            Rect stackedPreload = Bounds(preloadHost, window);
            Assert.True(stackedNotice.Bottom + host.Margin.Bottom <= stackedPreload.Top + 0.5);
            Assert.Equal(stackedNotice.Left, stackedPreload.Left, tolerance: 1);
            Assert.Equal(stackedNotice.Width, stackedPreload.Width, tolerance: 1);
            Assert.Equal(window.Bounds.Height - 12, stackedPreload.Bottom, tolerance: 1);
            Save(chinese ? "zh" : "en", dark ? "dark" : "light", "stacked");
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }

        void Save(string language, string theme, string state)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                return;
            }

            Render(window);
            _ = Directory.CreateDirectory(outputDirectory);
            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(System.IO.Path.Combine(outputDirectory, $"f08-save-failure-{theme}-{language}-{state}.png"));
        }
    }

    /// <summary>The coordinator reports finished and failed saves in order and never a superseded one.</summary>
    [Fact]
    public async Task CoordinatorReportsTerminalSavesButNotSupersededOnes()
    {
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> outcomes = [];
        var coordinator = new LatestSnapshotPersistenceCoordinator<string>(
            async (snapshot, _) =>
            {
                if (snapshot == "first")
                {
                    firstStarted.SetResult();
                    await releaseFirst.Task;
                }
                else if (snapshot == "failed")
                {
                    throw new IOException("synthetic failure");
                }
            },
            static snapshot => snapshot,
            failure =>
            {
                lock (outcomes)
                {
                    outcomes.Add(failure?.Message ?? "saved");
                }
            });

        coordinator.Queue("first");
        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        coordinator.Queue("superseded");
        coordinator.Queue("failed");
        releaseFirst.SetResult();
        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["saved", "synthetic failure"], outcomes);
        _ = Assert.IsType<IOException>(coordinator.LastFailure);
    }

    /// <summary>Retry re-queues the latest captured snapshot through the same serialized coordinator.</summary>
    [Fact]
    public async Task CoordinatorRetryRequeuesLatestCapturedSnapshot()
    {
        int captures = 0;
        List<string> saved = [];
        List<Exception?> outcomes = [];
        bool fail = true;
        var coordinator = new LatestSnapshotPersistenceCoordinator<string>(
            (snapshot, _) =>
            {
                saved.Add(snapshot);
                return fail ? Task.FromException(new IOException("synthetic failure")) : Task.CompletedTask;
            },
            snapshot =>
            {
                captures++;
                return snapshot + "#captured";
            },
            outcomes.Add);

        Assert.False(coordinator.TryRetry());
        coordinator.Queue("latest");
        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(coordinator.TryRetry());
        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);
        fail = false;
        Assert.True(coordinator.TryRetry());
        await coordinator.CompleteAsync().WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["latest#captured", "latest#captured", "latest#captured"], saved);
        Assert.Equal(1, captures);
        Assert.Equal(3, outcomes.Count);
        _ = Assert.IsType<IOException>(outcomes[0]);
        _ = Assert.IsType<IOException>(outcomes[1]);
        Assert.Null(outcomes[2]);
        Assert.False(coordinator.TryRetry());
    }

    /// <summary>A failing outcome observer cannot stop later saves from running and being observed.</summary>
    [Fact]
    public async Task CoordinatorObserverFailureDoesNotPoisonLaterSaves()
    {
        List<string> saved = [];
        int observed = 0;
        var coordinator = new LatestSnapshotPersistenceCoordinator<string>(
            (snapshot, _) =>
            {
                saved.Add(snapshot);
                return Task.CompletedTask;
            },
            static snapshot => snapshot,
            _ =>
            {
                observed++;
                throw new InvalidOperationException("synthetic observer failure");
            });

        coordinator.Queue("first");
        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);
        coordinator.Queue("second");
        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], saved);
        Assert.Equal(2, observed);
        Assert.Null(coordinator.LastFailure);
    }

    /// <summary>A preference save failure reaches its coordinator instead of being swallowed by the store.</summary>
    [Fact]
    public async Task PreferenceSaveFailureReachesCaller()
    {
        var files = new ScriptedStateFiles(new UnreachableFiles());
        files.Fail(PreferencesPath, static () => new UnauthorizedAccessException("synthetic access denied"));

        UnauthorizedAccessException failure = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            ShellPreferenceFileStore.SaveAsync(
                files,
                PreferencesPath,
                ShellPreferenceSnapshot.Default,
                TestContext.Current.CancellationToken));

        Assert.Equal("synthetic access denied", failure.Message);
    }

    /// <summary>The reason follows the failure category; unknown failures stay generic.</summary>
    [Theory]
    [InlineData("access", "access denied")]
    [InlineData("disk-full", "the disk is full")]
    [InlineData("handle-disk-full", "the disk is full")]
    [InlineData("sharing", "the file is in use")]
    [InlineData("lock", "the file is in use")]
    [InlineData("too-large", "the newest report is too large to keep")]
    [InlineData("io", "an unexpected error occurred")]
    [InlineData("other", "an unexpected error occurred")]
    public void NoticeReasonFollowsFailureCategory(string category, string reason)
    {
        Exception failure = category switch
        {
            "access" => new UnauthorizedAccessException("denied"),
            "disk-full" => new IOException("full", unchecked((int)0x80070070)),
            "handle-disk-full" => new IOException("full", unchecked((int)0x80070027)),
            "sharing" => new IOException("in use", unchecked((int)0x80070020)),
            "lock" => new IOException("locked", unchecked((int)0x80070021)),
            "too-large" => new ReportHistoryPersistenceException(
                ReportHistoryPersistenceFailure.EntryTooLargeToPersist, "too large"),
            "io" => new IOException("generic"),
            _ => new InvalidOperationException("other"),
        };
        var notice = new LocalStateSaveNoticeViewModel(() => ShellTextResources.For(ShellLanguage.English));

        notice.ObserveSave(LocalStateSaveTarget.ReportHistory, failure);

        Assert.Equal(
            $"Couldn't save your recent work state: {reason}. Your current work isn't affected.",
            notice.Detail);
        Assert.Equal($"Report history: {failure.Message}", notice.DetailToolTip);
    }

    /// <summary>A language change republishes the notice text; success of an unfailed state changes nothing.</summary>
    [Fact]
    public void NoticeRelocalizesAndIgnoresUnrelatedSuccess()
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        var notice = new LocalStateSaveNoticeViewModel(() => text);
        List<string?> changed = [];
        notice.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        notice.ObserveSave(LocalStateSaveTarget.Preferences, null);
        Assert.Empty(changed);
        notice.ObserveSave(LocalStateSaveTarget.Preferences, new UnauthorizedAccessException("denied"));
        changed.Clear();
        text = ShellTextResources.For(ShellLanguage.ChineseTraditional);
        notice.ApplyLanguageChanged();

        Assert.Contains(nameof(LocalStateSaveNoticeViewModel.Detail), changed);
        Assert.Contains(nameof(LocalStateSaveNoticeViewModel.RetryLabel), changed);
        Assert.Equal("重試", notice.RetryLabel);
        Assert.Equal("最近的工作未儲存", notice.Title);
        Assert.Equal("無法儲存最近的工作狀態：存取遭拒。目前的工作不受影響。", notice.Detail);
        Assert.Equal("偏好設定: denied", notice.DetailToolTip);
        notice.ObserveSave(LocalStateSaveTarget.ReportHistory, null);
        Assert.True(notice.IsVisible);
    }

    /// <summary>Retry reaches every failed state's owner once; a detached notice ignores outcomes and Retry.</summary>
    [Fact]
    public void RetryReachesFailedOwnersUntilDetached()
    {
        var notice = new LocalStateSaveNoticeViewModel(() => ShellTextResources.For(ShellLanguage.English));
        List<LocalStateSaveTarget> retried = [];
        notice.Attach(LocalStateSaveTarget.ReportHistory, () =>
        {
            retried.Add(LocalStateSaveTarget.ReportHistory);
            return true;
        });
        notice.Attach(LocalStateSaveTarget.Preferences, () =>
        {
            retried.Add(LocalStateSaveTarget.Preferences);
            return true;
        });
        Assert.False(notice.RetryCommand.CanExecute(null));

        notice.ObserveSave(LocalStateSaveTarget.Preferences, new IOException("failed"));
        notice.RetryCommand.Execute(null);
        Assert.Equal([LocalStateSaveTarget.Preferences], retried);
        Assert.True(notice.IsVisible);

        notice.Detach();
        Assert.False(notice.RetryCommand.CanExecute(null));
        notice.RetryCommand.Execute(null);
        notice.ObserveSave(LocalStateSaveTarget.Preferences, null);
        notice.ObserveSave(LocalStateSaveTarget.ReportHistory, new IOException("late"));
        Assert.Equal([LocalStateSaveTarget.Preferences], retried);
        Assert.Equal("Preferences: failed", notice.DetailToolTip);
    }

    private static LocalStateSaveNoticeViewModel Notice(Window window)
    {
        return Assert.IsType<LocalStateSaveNoticeViewModel>(window.FindControl<Border>(NoticeHostName)!.DataContext);
    }

    private static string Detail(ShellTextResources text, string reason)
    {
        return string.Format(CultureInfo.CurrentCulture, text.LocalStateSaveFailedDetailFormat, reason);
    }

    private static Rect Bounds(Visual control, Visual window)
    {
        Point origin = control.TranslatePoint(default, window)!.Value;
        return new Rect(origin, control.Bounds.Size);
    }

    private static void AssertBrush(Control control, IBrush? actual, string resourceKey)
    {
        Assert.True(control.TryFindResource(resourceKey, control.ActualThemeVariant, out object? expected),
            $"Missing resource '{resourceKey}'.");
        Assert.Equal(
            Assert.IsType<ISolidColorBrush>(expected, exactMatch: false).Color,
            Assert.IsType<ISolidColorBrush>(actual, exactMatch: false).Color);
    }

    /// <summary>Counts rendered pixels in a control's inset area that clearly differ from the area's corner.</summary>
    private static int CountInkPixels(Window window, Control control, double inset)
    {
        Render(window);
        using Avalonia.Media.Imaging.Bitmap frame = Assert.IsType<Avalonia.Media.Imaging.Bitmap>(
            window.GetLastRenderedFrame(), exactMatch: false);
        double scale = frame.PixelSize.Width / window.Bounds.Width;
        Rect bounds = Bounds(control, window);
        var region = new PixelRect(
            (int)Math.Ceiling((bounds.X + inset) * scale),
            (int)Math.Ceiling((bounds.Y + inset) * scale),
            (int)Math.Floor((bounds.Width - (2 * inset)) * scale),
            (int)Math.Floor((bounds.Height - (2 * inset)) * scale));
        int stride = region.Width * 4;
        byte[] pixels = new byte[stride * region.Height];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            frame.CopyPixels(region, handle.AddrOfPinnedObject(), pixels.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        // The corner pixel is the background; ink is any pixel whose channels differ from it by a clear margin.
        (byte b, byte g, byte r) = (pixels[0], pixels[1], pixels[2]);
        int ink = 0;
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            int difference = Math.Abs(pixels[offset] - b) + Math.Abs(pixels[offset + 1] - g) +
                Math.Abs(pixels[offset + 2] - r);
            if (difference > 96)
            {
                ink++;
            }
        }

        return ink;
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        wait.CancelAfter(timeout ?? TimeSpan.FromSeconds(10));
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
            {
                return;
            }

            await Task.Delay(10, wait.Token);
        }
    }

    private static async Task<(PresentationHostServices Services, ScriptedStateFiles Files)> CreateScriptedServicesAsync(
        TempWorkspace workspace)
    {
        PresentationHostServices services = await CreateServicesAsync(workspace);
        var files = new ScriptedStateFiles(services.LocalFiles);
        return (new PresentationHostServices(services.Composition, services.FileReveal, services.SupportMatrix,
            services.SystemInformation, services.SystemDiagnosticsExporter, services.RawBinaryEditorFileSessions,
            services.CanonicalCatalogLoader, services.ExternalEnvironmentLoader, files), files);
    }

    /// <summary>One held write: the test observes its start and decides when it continues.</summary>
    private sealed class WriteHold(string path)
    {
        internal string Path { get; } = path;

        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Scripted write failures and holds over the isolated real local-state store.</summary>
    private sealed class ScriptedStateFiles(ILocalFileStore inner) : ILocalFileStore
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<string, Func<Exception>?> _failures = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _completed = new(StringComparer.Ordinal);
        private WriteHold? _hold;

        internal void Fail(string path, Func<Exception>? failure)
        {
            lock (_gate)
            {
                _failures[path] = failure;
            }
        }

        internal int Completed(string path)
        {
            lock (_gate)
            {
                return _completed.GetValueOrDefault(path);
            }
        }

        internal WriteHold HoldNextWrite(string path)
        {
            var hold = new WriteHold(path);
            lock (_gate)
            {
                _hold = hold;
            }

            return hold;
        }

        public ValueTask<T> ReadAsync<T>(string path, long maximumBytes,
            Func<Stream, CancellationToken, ValueTask<T>> project, CancellationToken cancellationToken)
        {
            return inner.ReadAsync(path, maximumBytes, project, cancellationToken);
        }

        public ValueTask<string> ReadTextAsync(string path, long maximumBytes, CancellationToken cancellationToken,
            Action<LocalFileReadProgress>? progress = null)
        {
            return inner.ReadTextAsync(path, maximumBytes, cancellationToken, progress);
        }

        public ValueTask<string> ReadTextAsync(Func<CancellationToken, ValueTask<Stream>> openReadAsync,
            long maximumBytes, CancellationToken cancellationToken)
        {
            return inner.ReadTextAsync(openReadAsync, maximumBytes, cancellationToken);
        }

        public async ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            WriteHold? hold;
            Func<Exception>? failure;
            lock (_gate)
            {
                hold = _hold?.Path == path ? _hold : null;
                if (hold is not null)
                {
                    _hold = null;
                }

                failure = _failures.GetValueOrDefault(path);
            }

            try
            {
                // The failure is fixed when the write starts, even if the test rearms it while the write is held.
                if (hold is not null)
                {
                    hold.Started.SetResult();
                    await hold.Released.Task.ConfigureAwait(false);
                }

                if (failure is not null)
                {
                    throw failure();
                }

                await inner.WriteAsync(path, bytes, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lock (_gate)
                {
                    _completed[path] = _completed.GetValueOrDefault(path) + 1;
                }
            }
        }
    }

    private sealed class UnreachableFiles : ILocalFileStore
    {
        public ValueTask<T> ReadAsync<T>(string path, long maximumBytes,
            Func<Stream, CancellationToken, ValueTask<T>> project, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Unexpected read.");
        }

        public ValueTask<string> ReadTextAsync(string path, long maximumBytes, CancellationToken cancellationToken,
            Action<LocalFileReadProgress>? progress = null)
        {
            throw new InvalidOperationException("Unexpected read.");
        }

        public ValueTask<string> ReadTextAsync(Func<CancellationToken, ValueTask<Stream>> openReadAsync,
            long maximumBytes, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Unexpected read.");
        }

        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Unexpected write.");
        }
    }
}
