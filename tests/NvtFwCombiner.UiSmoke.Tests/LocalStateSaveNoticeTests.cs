using System.Globalization;
using System.Reflection;
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

    /// <summary>
    /// A Retry whose in-flight write is superseded by a newer snapshot reports nothing, even when that write ignores
    /// the cancellation and succeeds: the notice stays until the latest snapshot is saved.
    /// </summary>
    [AvaloniaFact]
    public async Task SupersededRetryThatStillSucceedsKeepsNoticeUntilLatestSnapshotSaves()
    {
        using var workspace = TempWorkspace.Create("f08-save-notice-superseded");
        (PresentationHostServices services, ScriptedStateFiles files) = await CreateScriptedServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services,
            ShellPreferenceSnapshot.Default);
        WriteHold? retryWrite = null;
        WriteHold? latestWrite = null;
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            var shell = (MainWindowViewModel)window.DataContext!;
            LocalStateSaveNoticeViewModel notice = Notice(window);
            Border host = window.FindControl<Border>(NoticeHostName)!;
            files.Fail(PreferencesPath, static () => new UnauthorizedAccessException("synthetic access denied"));
            shell.ExpandInputDetailsByDefault = !shell.ExpandInputDetailsByDefault;
            await WaitUntilAsync(() => notice.IsVisible);
            ShellPreferenceSnapshot retried = shell.ExportShellPreferences();

            // Retry starts a write that ignores its cancellation; a newer preference change then supersedes it.
            files.Fail(PreferencesPath, null);
            retryWrite = files.HoldNextWrite(PreferencesPath, ignoresCancellation: true);
            notice.RetryCommand.Execute(null);
            await retryWrite.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            latestWrite = files.HoldNextWrite(PreferencesPath);
            shell.IsReducedMotionEnabled = !shell.IsReducedMotionEnabled;
            ShellPreferenceSnapshot latest = shell.ExportShellPreferences();
            Assert.NotEqual(retried, latest);

            // The superseded write still succeeds, yet the latest snapshot is unsaved, so the notice stays.
            retryWrite.Released.SetResult();
            await latestWrite.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(retried, await ShellPreferenceFileStore.LoadAsync(files, PreferencesPath));
            Assert.True(notice.IsVisible);
            Assert.True(host.IsVisible);

            latestWrite.Released.SetResult();
            await WaitUntilAsync(() => !notice.IsVisible);
            Assert.False(host.IsVisible);
            Assert.Equal(latest, await ShellPreferenceFileStore.LoadAsync(files, PreferencesPath));
        }
        finally
        {
            // A failed assertion must not leave a held write stalling the close flush.
            _ = retryWrite?.Released.TrySetResult();
            _ = latestWrite?.Released.TrySetResult();
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>
    /// Independent review finding F-1 (P1) on the supersession fix itself: the coordinator can already own a save
    /// as latest and report it, yet that report only reaches the notice after an <c>await</c> back to the UI
    /// thread. A newer snapshot queued in that gap must still make the earlier, already-terminal success stale:
    /// the notice keeps the original failure until the newer snapshot's own save finishes.
    /// </summary>
    /// <remarks>
    /// Independent review finding F-3 (P2) on the first version of this test: a fixed sleep cannot prove A's
    /// coordinator-level report already ran before B is queued. Under a slow background, B could queue first,
    /// which would make the coordinator's own (already-fixed, 9f9d7f837) supersession check reject A instead --
    /// keeping the notice correct for an unrelated reason and letting this test pass even against the pre-F-1
    /// coordinator. This version blocks synchronously (never <c>await</c>, so the dispatcher queue stays
    /// untouched) on the shell-preference coordinator's own <c>WaitForIdleAsync()</c>, which by construction
    /// completes only once A's whole <c>PersistAfterAsync</c> -- the isLatest check and the report to
    /// <c>PostLocalStateSaveOutcome</c> included -- has already run. B is queued only after that wait returns.
    /// </remarks>
    [AvaloniaFact]
    public async Task StaleSuccessDeliveredAfterNewerSnapshotQueuedDoesNotClearNotice()
    {
        using var workspace = TempWorkspace.Create("f08-save-notice-stale-outcome");
        (PresentationHostServices services, ScriptedStateFiles files) = await CreateScriptedServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services,
            ShellPreferenceSnapshot.Default);
        LatestSnapshotPersistenceCoordinator<ShellPreferenceSnapshot> coordinator = ShellPreferenceCoordinator(window);
        WriteHold? latestWrite = null;
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            var shell = (MainWindowViewModel)window.DataContext!;
            LocalStateSaveNoticeViewModel notice = Notice(window);
            Border host = window.FindControl<Border>(NoticeHostName)!;
            files.Fail(PreferencesPath, static () => new UnauthorizedAccessException("synthetic access denied"));
            shell.ExpandInputDetailsByDefault = !shell.ExpandInputDetailsByDefault;
            await WaitUntilAsync(() => notice.IsVisible);

            // A succeeds on an unheld write. Block (never `await`) on the coordinator's own idle signal: this is
            // provably waiting for A's coordinator-level report -- including the isLatest check and the post to
            // the UI thread -- to have already run, not merely for the underlying write to finish. An `await`
            // here would let this headless test host drain the dispatcher queue in the background and apply A's
            // outcome before B is even queued, which is exactly the gap this test needs to hold open.
            files.Fail(PreferencesPath, null);
            shell.IsReducedMotionEnabled = !shell.IsReducedMotionEnabled;
            ShellPreferenceSnapshot a = shell.ExportShellPreferences();
            bool aReported = coordinator.WaitForIdleAsync().Wait(TimeSpan.FromSeconds(10));
            Assert.True(aReported, "Timed out waiting for A's coordinator-level report.");

            // B is queued, and held, only now: strictly after A's own report already ran, proven above rather
            // than assumed from elapsed time.
            latestWrite = files.HoldNextWrite(PreferencesPath);
            shell.ExpandInputDetailsByDefault = !shell.ExpandInputDetailsByDefault;
            ShellPreferenceSnapshot latest = shell.ExportShellPreferences();
            Assert.NotEqual(a, latest);

            // Draining the dispatcher now applies A's outcome. It must be recognized as stale and discarded: the
            // original failure stays, even though the access-denied write itself never ran again.
            Dispatcher.UIThread.RunJobs();
            Assert.True(notice.IsVisible, $"Detail='{notice.Detail}', ToolTip='{notice.DetailToolTip}'");
            Assert.True(host.IsVisible);
            Assert.Equal("Preferences: synthetic access denied", notice.DetailToolTip);
            Assert.Equal(a, await ShellPreferenceFileStore.LoadAsync(files, PreferencesPath));

            await latestWrite.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            latestWrite.Released.SetResult();
            await WaitUntilAsync(() => !notice.IsVisible);
            Assert.False(host.IsVisible);
            Assert.Equal(latest, await ShellPreferenceFileStore.LoadAsync(files, PreferencesPath));
        }
        finally
        {
            // A failed assertion must not leave a held write stalling the close flush.
            _ = latestWrite?.Released.TrySetResult();
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
    /// with the content, icon, bold title, separator, one-line detail and a round icon-only Retry with the
    /// reference refresh glyph and hover state, with the startup status row's geometry and 13 px text; both
    /// status rows stack without overlap.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task NoticeMatchesApprovedStatusRowGeometry(bool dark, bool chinese)
    {
        string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        using var workspace = TempWorkspace.Create("f08-save-notice-visual");
        (PresentationHostServices services, ScriptedStateFiles files) = await CreateScriptedServicesAsync(workspace);
        using var window = new MainWindow(ReferenceMergeLaunch(), StartupTraceSession.Disabled, services,
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
            // The reference sets the title, separator and detail in one amber warning text colour.
            foreach (TextBlock part in new[] { title, separator, detail })
            {
                AssertBrush(part, part.Foreground, "NfcWarningTextMutedBrush");
                AssertContrast(part.Foreground, host.Background, minimum: 4.5);
            }
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
            AssertApprovedRefreshGlyph(refresh);
            // Sample inside the round button's ring (the 16 px icon area) and inside the 18 px warning icon.
            Assert.True(CountInkPixels(window, retry, inset: 9) >= 20, "The Retry icon did not render.");
            Assert.True(CountInkPixels(window, icon, inset: 1) >= 20, "The warning icon did not render.");
            Save(chinese ? "zh" : "en", dark ? "dark" : "light", "single");

            // The reference's hover state is the shared secondary hover: accent surface and border, an accent
            // refresh icon and the bilingual tooltip beside the button (owner decision 42), clear of the button so
            // the hover state persists while the tooltip is open; leaving restores the resting icon.
            window.MouseMove(new Point(4, 4), RawInputModifiers.None);
            window.MouseMove(Bounds(retry, window).Center, RawInputModifiers.None);
            await WaitUntilAsync(() => ToolTip.GetIsOpen(retry), TimeSpan.FromSeconds(5));
            Render(window);
            Assert.True(retry.IsPointerOver);
            AssertBrush(retry, retryPresenter.Background, "NfcAccentSurfaceBrush");
            AssertBrush(retry, retryPresenter.BorderBrush, "NfcAccentBorderBrush");
            AssertBrush(retry, refresh.Stroke, "NfcAccentStrongBrush");
            Assert.Equal(chinese ? "重試" : "Retry", ToolTip.GetTip(retry));
            ToolTip tip = AssertRetryTooltipBesideButton(window, retry, host);
            await WaitUntilAsync(() =>
            {
                Render(window);
                return tip.Opacity >= 1;
            });
            Assert.True(retry.IsPointerOver);
            Save(chinese ? "zh" : "en", dark ? "dark" : "light", "hover");
            window.MouseMove(new Point(4, 4), RawInputModifiers.None);
            await WaitUntilAsync(() => !retry.IsPointerOver);
            AssertBrush(retry, refresh.Stroke, "NfcTextBrush");

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

    /// <summary>
    /// Owner decision 42 at the supported window sizes in both languages: with the longest detail sentence the
    /// Retry tooltip opens beside the button inside the strip, clear of the status text and the composition rail,
    /// and the hover state persists while it is open.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(980, 640, false)]
    [InlineData(980, 640, true)]
    [InlineData(1280, 800, false)]
    [InlineData(1280, 800, true)]
    [InlineData(1920, 1080, false)]
    [InlineData(1920, 1080, true)]
    public async Task RetryTooltipStaysClearOfStatusTextAndRail(double width, double height, bool chinese)
    {
        using var workspace = TempWorkspace.Create("f08-save-notice-tooltip");
        (PresentationHostServices services, ScriptedStateFiles files) = await CreateScriptedServicesAsync(workspace);
        using var window = new MainWindow(ReferenceMergeLaunch(), StartupTraceSession.Disabled, services,
            ShellPreferenceSnapshot.Default)
        { Width = width, Height = height };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            var shell = (MainWindowViewModel)window.DataContext!;
            var preload = (ShellPreloadSession)window.FindControl<Border>("OptionalPreloadStatusHost")!.DataContext!;
            await WaitUntilAsync(() => !preload.HasOptionalStatus &&
                window.FindControl<Grid>("ShellInteractionHost")!.IsEnabled, TimeSpan.FromSeconds(45));
            if (chinese)
            {
                shell.SelectedLanguage = "Traditional Chinese";
                await WaitUntilAsync(() => files.Completed(PreferencesPath) >= 1);
            }

            // Layout only: present the longest detail sentence, the report-history size failure, directly.
            LocalStateSaveNoticeViewModel notice = Notice(window);
            notice.ObserveSave(LocalStateSaveTarget.ReportHistory, new ReportHistoryPersistenceException(
                ReportHistoryPersistenceFailure.EntryTooLargeToPersist, "synthetic too large"));
            Render(window);
            Border host = window.FindControl<Border>(NoticeHostName)!;
            Button retry = window.FindControl<Button>(RetryButtonName)!;
            Assert.True(host.IsEffectivelyVisible);
            Assert.True(window.FindControl<StackPanel>("CompositionBuildActionRail")!.IsEffectivelyVisible);
            Assert.Equal(new Size(width, height), window.Bounds.Size);

            window.MouseMove(new Point(4, 4), RawInputModifiers.None);
            window.MouseMove(Bounds(retry, window).Center, RawInputModifiers.None);
            await WaitUntilAsync(() => ToolTip.GetIsOpen(retry), TimeSpan.FromSeconds(5));
            Render(window);
            Assert.True(retry.IsPointerOver);
            ToolTip tip = AssertRetryTooltipBesideButton(window, retry, host);
            await WaitUntilAsync(() =>
            {
                Render(window);
                return tip.Opacity >= 1;
            });
            Assert.True(retry.IsPointerOver);
            SaveFrame(window, $"f08-save-failure-tooltip-{width:0}x{height:0}-{(chinese ? "zh" : "en")}.png");
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>
    /// The coordinator reports finished and failed saves in queue order and never a superseded one, including an
    /// in-flight write that ignores its cancellation and finishes after a newer snapshot was queued.
    /// </summary>
    [Fact]
    public async Task CoordinatorReportsTerminalSavesButNotSupersededOnes()
    {
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> saved = [];
        List<string> outcomes = [];
        var coordinator = new LatestSnapshotPersistenceCoordinator<string>(
            async (snapshot, _) =>
            {
                lock (saved)
                {
                    saved.Add(snapshot);
                }

                if (snapshot == "first")
                {
                    firstStarted.SetResult();
                    // The write ignores its cancellation and finishes after it was superseded.
                    await releaseFirst.Task;
                }
                else if (snapshot == "failed")
                {
                    throw new IOException("synthetic failure");
                }
            },
            static snapshot => snapshot,
            (failure, _) =>
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

        Assert.Equal(["first", "failed"], saved);
        Assert.Equal(["synthetic failure"], outcomes);
        _ = Assert.IsType<IOException>(coordinator.LastFailure);

        coordinator.Queue("latest");
        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["first", "failed", "latest"], saved);
        Assert.Equal(["synthetic failure", "saved"], outcomes);
    }

    /// <summary>
    /// Independent review finding F-1 (P1): the coordinator can already own a save as latest (queue order, not
    /// superseded while writing) and report its outcome, yet a caller that applies that outcome later — after an
    /// <c>await</c> back to a UI thread, for example — must re-check freshness at that later point, because a
    /// newer snapshot can be queued in between. The reported generation, checked again through
    /// <see cref="LatestSnapshotPersistenceCoordinator{TSnapshot}.IsCurrentGeneration"/> once that newer snapshot
    /// exists, must then read as stale even though the coordinator itself already reported the outcome as terminal
    /// and latest.
    /// </summary>
    [Fact]
    public async Task GenerationStaysCurrentOnlyUntilANewerSnapshotIsQueued()
    {
        TaskCompletionSource bStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseB = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<(Exception? Failure, long Generation)> reported = [];
        var coordinator = new LatestSnapshotPersistenceCoordinator<string>(
            async (snapshot, _) =>
            {
                if (snapshot == "B")
                {
                    bStarted.SetResult();
                    await releaseB.Task;
                }
            },
            static snapshot => snapshot,
            (failure, generation) =>
            {
                lock (reported)
                {
                    reported.Add((failure, generation));
                }
            });

        coordinator.Queue("A");
        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);
        (Exception? aFailure, long aGeneration) = Assert.Single(reported);
        Assert.Null(aFailure);
        // Nothing newer is queued yet: applying A's outcome right now would still be correct.
        Assert.True(coordinator.IsCurrentGeneration(aGeneration));

        // B is queued only after A's own completion already reported — the gap a UI dispatcher post leaves open.
        coordinator.Queue("B");
        await bStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Applying A's outcome now must recognize it as stale, even though the coordinator already reported it as
        // a terminal, non-superseded save; only B's own future report may still clear a notice for this state.
        Assert.False(coordinator.IsCurrentGeneration(aGeneration));

        releaseB.SetResult();
        await coordinator.WaitForIdleAsync().WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, reported.Count);
        (Exception? bFailure, long bGeneration) = reported[1];
        Assert.Null(bFailure);
        Assert.True(coordinator.IsCurrentGeneration(bGeneration));
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
            (failure, _) => outcomes.Add(failure));

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
            (_, _) =>
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

    /// <summary>The approved reference state: the NT51923 standard Merge page with both inputs loaded.</summary>
    private static UiLaunchOptions ReferenceMergeLaunch()
    {
        return UiLaunchOptions.Parse(["--workflow", "standard-merge", "--ic", "NT51923", "--ic-num", "single",
            "--dp", RepositoryPaths.FromRepositoryRoot(ReferenceCase + "nt51923-dp-input.bin"),
            "--tp", RepositoryPaths.FromRepositoryRoot(ReferenceCase + "nt51923-tp-input.bin")]);
    }

    /// <summary>
    /// Owner decision 42: the open Retry tooltip sits beside the button on its left, centred on it inside the
    /// strip, and covers neither the button, the status text nor the composition rail.
    /// </summary>
    private static ToolTip AssertRetryTooltipBesideButton(Window window, Button retry, Border host)
    {
        ToolTip tip = Assert.Single(window.GetVisualDescendants().OfType<ToolTip>());
        Rect tipBounds = Bounds(tip, window);
        Rect button = Bounds(retry, window);
        Rect strip = Bounds(host, window);
        Assert.InRange(button.Left - tipBounds.Right, 0, 16);
        Assert.Equal(button.Center.Y, tipBounds.Center.Y, tolerance: 1);
        Assert.True(tipBounds.Top >= strip.Top && tipBounds.Bottom <= strip.Bottom,
            $"The Retry tooltip {tipBounds} leaves the strip {strip}.");
        TextBlock[] texts = [.. host.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible && block.FindAncestorOfType<Button>() is null)];
        Assert.Equal(3, texts.Length);
        foreach (TextBlock text in texts)
        {
            Rect ink = TextExtent(text, window);
            Assert.False(tipBounds.Intersects(ink), $"The Retry tooltip {tipBounds} covers '{text.Text}' at {ink}.");
        }

        StackPanel rail = window.FindControl<StackPanel>("CompositionBuildActionRail")!;
        if (rail.IsEffectivelyVisible)
        {
            Rect railBounds = Bounds(rail, window);
            Assert.False(tipBounds.Intersects(railBounds), $"The Retry tooltip {tipBounds} covers the rail {railBounds}.");
        }

        return tip;
    }

    /// <summary>Saves the rendered window for owner review when NFC_VISUAL_OUTPUT_DIR is set.</summary>
    private static void SaveFrame(Window window, string fileName)
    {
        string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        Render(window);
        _ = Directory.CreateDirectory(outputDirectory);
        using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(System.IO.Path.Combine(outputDirectory, fileName));
    }

    /// <summary>The laid-out text of a text block, not its stretched arrange slot.</summary>
    private static Rect TextExtent(TextBlock block, Visual window)
    {
        Point origin = block.TranslatePoint(default, window)!.Value;
        return new Rect(
            origin.X + block.Padding.Left,
            origin.Y + block.Padding.Top,
            block.TextLayout.WidthIncludingTrailingWhitespace,
            block.TextLayout.Height);
    }

    private static LocalStateSaveNoticeViewModel Notice(Window window)
    {
        return Assert.IsType<LocalStateSaveNoticeViewModel>(window.FindControl<Border>(NoticeHostName)!.DataContext);
    }

    /// <summary>
    /// The window's own preference-persistence coordinator, reached through reflection since it is a private
    /// field. Used only as a deterministic synchronization point (<c>WaitForIdleAsync</c>,
    /// <c>IsCurrentGeneration</c>): the field is internal-visible-to-tests <see cref="LatestSnapshotPersistenceCoordinator{TSnapshot}"/>
    /// state, not a production seam added for this test.
    /// </summary>
    private static LatestSnapshotPersistenceCoordinator<ShellPreferenceSnapshot> ShellPreferenceCoordinator(
        MainWindow window)
    {
        FieldInfo field = typeof(MainWindow).GetField("_shellPreferencePersistence",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (LatestSnapshotPersistenceCoordinator<ShellPreferenceSnapshot>)field.GetValue(window)!;
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

    /// <summary>
    /// The approved reference's refresh glyph fills the 16 px icon: a ring open on its right side between the free
    /// end at the lower right and a right-angle arrowhead whose corner sits on the ring at the upper right.
    /// </summary>
    private static void AssertApprovedRefreshGlyph(ShapePath refresh)
    {
        Geometry glyph = Assert.IsType<Geometry>(refresh.Data, exactMatch: false);
        var pen = new Pen(Brushes.Black, refresh.StrokeThickness, lineCap: refresh.StrokeLineCap,
            lineJoin: refresh.StrokeJoin);
        Rect extent = glyph.GetRenderBounds(pen);
        Assert.Equal(new Size(16, 16), new Size(refresh.Width, refresh.Height));
        Assert.InRange(extent.Width, 14, 16.5);
        Assert.InRange(extent.Height, 14, 16.5);
        Assert.True(extent.Left >= -0.25 && extent.Top >= -0.25 && extent.Right <= 16.25 && extent.Bottom <= 16.25,
            $"The refresh glyph {extent} leaves its 16 px icon.");
        (Point Anchor, bool IsInked, string Part)[] anatomy =
        [
            (new Point(1.5, 8), true, "ring at 9 o'clock"),
            (new Point(8, 1.5), true, "ring at 12 o'clock"),
            (new Point(8, 14.5), true, "ring at 6 o'clock"),
            (new Point(14.3, 6.3), true, "arrowhead corner on the ring at the upper right"),
            (new Point(14.3, 2), true, "arrowhead arm rising from the corner"),
            (new Point(10, 6.3), true, "arrowhead arm running left from the corner"),
            (new Point(14.6, 8.6), false, "ring opening at 3 o'clock"),
            (new Point(8, 8), false, "hollow centre"),
        ];
        foreach ((Point anchor, bool isInked, string part) in anatomy)
        {
            Assert.True(glyph.StrokeContains(pen, anchor) == isInked, $"Refresh glyph {part} at {anchor}.");
        }
    }

    /// <summary>The WCAG 2 contrast ratio between two solid brushes meets the minimum.</summary>
    private static void AssertContrast(IBrush? foreground, IBrush? background, double minimum)
    {
        Color text = Assert.IsType<ISolidColorBrush>(foreground, exactMatch: false).Color;
        Color surface = Assert.IsType<ISolidColorBrush>(background, exactMatch: false).Color;
        double lighter = Math.Max(Luminance(text), Luminance(surface));
        double darker = Math.Min(Luminance(text), Luminance(surface));
        double ratio = (lighter + 0.05) / (darker + 0.05);
        Assert.True(ratio >= minimum, $"{text} on {surface} has contrast {ratio:F2}:1; expected at least {minimum:F1}:1.");

        static double Luminance(Color color)
        {
            return (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));
        }

        static double Linear(byte component)
        {
            double value = component / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
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

    /// <summary>
    /// One held write: the test observes its start and decides when it continues; a write that ignores
    /// cancellation still completes after its save was superseded.
    /// </summary>
    private sealed class WriteHold(string path, bool ignoresCancellation)
    {
        internal string Path { get; } = path;

        internal bool IgnoresCancellation { get; } = ignoresCancellation;

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

        internal WriteHold HoldNextWrite(string path, bool ignoresCancellation = false)
        {
            var hold = new WriteHold(path, ignoresCancellation);
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

                await inner.WriteAsync(path, bytes, hold?.IgnoresCancellation == true
                    ? CancellationToken.None
                    : cancellationToken).ConfigureAwait(false);
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
