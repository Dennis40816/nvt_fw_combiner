using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises the real Raw viewport and both clipboard event consumers.</summary>
public sealed class ReportRawCopyTests
{
    /// <summary>The existing command block uses current exact text; empty text does not erase the clipboard.</summary>
    [AvaloniaFact]
    public async Task ExistingCommandCopyRetainsTextChangesAndEmptyNoOp()
    {
        using var workspace = TempWorkspace.Create("raw-command-copy");
        using MainWindow window = await CreateReportWindowAsync(workspace);
        var block = new ReportCodeBlockView { Text = "python checksum.py  --input staged.bin\r\n" };
        window.Content = block;
        try
        {
            Dispatcher.UIThread.RunJobs();
            Button copy = Assert.Single(block.GetVisualDescendants().OfType<Button>());
            IClipboard clipboard = window.Clipboard!;
            Assert.Equal("Copy command evidence", AutomationProperties.GetName(copy));
            copy.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(block.Text, await clipboard.TryGetTextAsync());
            block.Text = "\tsecond command\r\n\r\n";
            copy.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(block.Text, await clipboard.TryGetTextAsync());
            block.Text = string.Empty;
            copy.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("\tsecond command\r\n\r\n", await clipboard.TryGetTextAsync());
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>Copy stays inside Raw, preserves its viewport, and copies exact unselected text in both languages/themes.</summary>
    [AvaloniaTheory]
    [InlineData(false, false, 1920)]
    [InlineData(true, true, 1920)]
    [InlineData(false, true, 1024)]
    [InlineData(true, false, 1024)]
    public async Task RawCopyIsAnchoredInsideTextBoxAndCopiesExactPayload(bool dark, bool chinese, double width)
    {
        using var workspace = TempWorkspace.Create("raw-copy-controls");
        using MainWindow window = await CreateReportWindowAsync(workspace);
        MainWindowViewModel shell = (MainWindowViewModel)window.DataContext!;
        shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        window.WindowState = WindowState.Normal;
        window.Width = width;
        window.Height = width == 1920 ? 1008 : 744;
        string json = "\r\n" + ReportJsonSamples.Succeeded(runId: "exact-raw-copy") +
            " \t\r\n" + string.Join("\r\n", Enumerable.Repeat(new string(' ', 400), 70));
        shell.Reports.LoadReportJson(json, "copy.json");
        try
        {
            OpenRaw(window, shell);
            TextBox raw = RawText(window);
            Button copy = RawCopy(window);
            Assert.True(raw.IsReadOnly);
            Assert.Equal(json, raw.Text);
            string label = chinese ? "複製完整 JSON" : "Copy full JSON";
            Assert.Equal(label, AutomationProperties.GetName(copy));
            Assert.Equal(label, ToolTip.GetTip(copy));
            Point anchor = copy.TranslatePoint(default, raw)!.Value;
            Assert.InRange(anchor.Y, 11, 13);
            Assert.InRange(raw.Bounds.Width - anchor.X - copy.Bounds.Width, 31, 33);
            Assert.Equal(new Size(32, 32), copy.Bounds.Size);
            Assert.True(raw.Bounds.Height > 200);
            Assert.True(anchor.X > 0 && anchor.Y + copy.Bounds.Height < raw.Bounds.Height);
            ScrollViewer scroll = Assert.Single(raw.GetVisualDescendants().OfType<ScrollViewer>());
            raw.SelectionStart = 3;
            raw.SelectionEnd = 19;
            scroll.Offset = new Vector(35, 100);
            Dispatcher.UIThread.RunJobs();
            Vector offset = scroll.Offset;
            Assert.True(offset.Y > 0);
            Assert.True(copy.Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            Assert.True(ToolTip.GetIsOpen(copy));
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            Dispatcher.UIThread.RunJobs();
            Assert.False(ToolTip.GetIsOpen(copy));
            Assert.True(shell.Reports.IsReportModalOpen);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(json, await window.Clipboard!.TryGetTextAsync());
            Assert.Equal(3, raw.SelectionStart);
            Assert.Equal(19, raw.SelectionEnd);
            Assert.Equal(offset, scroll.Offset);
            Assert.Equal(anchor, copy.TranslatePoint(default, raw));
            Assert.True(shell.Reports.IsReportModalOpen);

            shell.Reports.ShowReportHistoryCommand.Execute(null);
            await shell.Reports.OpenReportHistoryEntryAsyncCommand.ExecuteAsync(shell.Reports.ReportHistoryEntries[0]);
            OpenRaw(window, shell);
            Button reopenedCopy = RawCopy(window);
            Point center = reopenedCopy.TranslatePoint(new Point(16, 16), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(json, await window.Clipboard!.TryGetTextAsync());
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>A rejected clipboard write cannot escape either actual button event or close Report.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ClipboardFailureIsContainedByBothReportActions(bool rawAction, bool unavailable)
    {
        using var workspace = TempWorkspace.Create("raw-copy-failure");
        using MainWindow window = await CreateReportWindowAsync(workspace);
        MainWindowViewModel shell = (MainWindowViewModel)window.DataContext!;
        try
        {
            OpenRaw(window, shell);
            Button button;
            if (rawAction)
            {
                button = RawCopy(window);
            }
            else
            {
                var block = new ReportCodeBlockView { Text = "python checksum.py --input staged.bin" };
                window.Content = block;
                Dispatcher.UIThread.RunJobs();
                button = Assert.Single(block.GetVisualDescendants().OfType<Button>());
            }

            bool escaped = false;
            void Capture(object sender, DispatcherUnhandledExceptionEventArgs args)
            {
                escaped = true;
                args.Handled = true;
            }
            Dispatcher.UIThread.UnhandledException += Capture;
            try
            {
                IClipboard clipboard = DispatchProxy.Create<IClipboard, RejectingClipboard>();
                // Avalonia 12 exposes clipboard only through the native TopLevel feature port.
                // Replace just that port for the click, then restore it before closing the real window.
                FieldInfo platformField = typeof(TopLevel).GetField("<PlatformImpl>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var original = (IWindowImpl)platformField.GetValue(window)!;
                using IWindowImpl replacement = DispatchProxy.Create<IWindowImpl, ClipboardWindowProxy>();
                var proxy = (ClipboardWindowProxy)replacement;
                proxy.Inner = original;
                proxy.Clipboard = unavailable ? null : clipboard;
                platformField.SetValue(window, replacement);
                try
                {
                    button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    Dispatcher.UIThread.RunJobs();
                }
                finally
                {
                    platformField.SetValue(window, original);
                }
                Assert.False(escaped);
                Assert.True(shell.Reports.IsReportModalOpen);
                Assert.True(shell.Reports.HasReportToast);
                Assert.Equal("Copy failed", shell.Reports.ShellToastTitle);
                Assert.Equal("Clipboard unavailable. Try again or save the report.", shell.Reports.ReportToastText);
            }
            finally
            {
                Dispatcher.UIThread.UnhandledException -= Capture;
            }
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    private static async Task<MainWindow> CreateReportWindowAsync(TempWorkspace workspace)
    {
        PresentationHostServices services = await CreateServicesAsync(workspace);
        var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
        }
        catch
        {
            await CloseAndFlushAsync(window);
            window.Dispose();
            throw;
        }
        var shell = (MainWindowViewModel)window.DataContext!;
        shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "raw-copy"), "copy.json");
        return window;
    }

    private static void OpenRaw(Window window, MainWindowViewModel shell)
    {
        shell.Reports.ShowReportCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        TabControl tabs = Assert.Single(window.GetVisualDescendants().OfType<TabControl>(), item => item.Classes.Contains("reportTabs"));
        tabs.SelectedItem = Assert.Single(tabs.Items.OfType<TabItem>(), item => Equals(item.Header, shell.Text.ReportTabRaw));
        Dispatcher.UIThread.RunJobs();
    }

    private static TextBox RawText(Window window)
    {
        return Assert.Single(window.GetVisualDescendants().OfType<TextBox>(), item => item.Classes.Contains("readOnlyRaw"));
    }

    private static Button RawCopy(Window window)
    {
        return Assert.Single(window.GetVisualDescendants().OfType<Button>(), item => item.Name == "RawReportCopyButton");
    }

    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass of this test port.")]
    private class ClipboardWindowProxy : DispatchProxy
    {
        public IWindowImpl Inner { get; set; } = null!;

        public IClipboard? Clipboard { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IDisposable.Dispose))
            {
                return null; // The real Window retains ownership of its restored native platform.
            }

            return targetMethod.Name == "TryGetFeature" && Equals(args![0], typeof(IClipboard))
                ? Clipboard : targetMethod.Invoke(Inner, args);
        }
    }

    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass of this test clipboard.")]
    private class RejectingClipboard : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.Equal(nameof(IClipboard.SetDataAsync), targetMethod!.Name);
            (Assert.Single(args!) as IDisposable)?.Dispose();
            return Task.FromException(new IOException("Clipboard is busy."));
        }
    }
}
