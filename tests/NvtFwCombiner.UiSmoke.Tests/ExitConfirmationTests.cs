using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Closing must ask before ending any selected-file session or its persistence queue.</summary>
public sealed partial class ExitConfirmationTests
{
    /// <summary>Replacing a pending page change with Exit returns focus to Cancel and never clears inputs.</summary>
    [AvaloniaFact]
    public async Task ExitSupersedesPendingNavigationWithSafeDefaultFocus()
    {
        using var workspace = TempWorkspace.Create("v114-exit-navigation");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        await AwaitHistoryReadyAsync(window);
        try
        {
            shell.ShowMergeCommand.Execute(null);
            shell.Merge.MergeDpSlot.FilePath = workspace.Write("selected.bin", [0x12]);
            shell.ShowHomeCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            NavigationClearConfirmationModal dialog = Assert.Single(window.GetVisualDescendants().OfType<NavigationClearConfirmationModal>());
            Button cancel = dialog.FindControl<Button>("CancelButton")!;
            Button confirm = Assert.Single(dialog.GetVisualDescendants().OfType<Button>(), button => button != cancel);
            Assert.True(confirm.Focus(NavigationMethod.Tab));
            window.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.True(shell.Navigation.IsExitConfirmationOpen);
            Assert.True(cancel.IsFocused);
            shell.Navigation.CancelNavigationClearCommand.Execute(null);
            Assert.True(shell.IsMergeVisible);
            Assert.True(shell.Merge.MergeDpSlot.HasFile);
        }
        finally
        {
            await CloseAndFlushAsync(window);
        }
    }

    /// <summary>A Report JSON alone counts, even behind its open modal; Cancel retains usable state.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ReportOnlyCloseWaitsForSafeConfirmation(bool dark, bool chinese)
    {
        using var workspace = TempWorkspace.Create("v114-exit-report");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        await AwaitHistoryReadyAsync(window);
        window.Width = 1024;
        window.Height = 850;
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        if (chinese)
        {
            shell.SelectedLanguage = "Traditional Chinese";
        }
        shell.Reports.LoadReportJson("{}", "selected.json");
        shell.Reports.ShowReportCommand.Execute(null);
        string report = shell.Reports.LoadedReportJson;
        ShellPage page = shell.SelectedPage;
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult();
        try
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.True(shell.Navigation.IsNavigationClearConfirmationOpen);
            Assert.True(window.IsEnabled);
            Assert.False(closed.Task.IsCompleted);
            NavigationClearConfirmationModal dialog = Assert.Single(window.GetVisualDescendants().OfType<NavigationClearConfirmationModal>());
            Button cancel = dialog.FindControl<Button>("CancelButton")!;
            Button confirm = Assert.Single(dialog.GetVisualDescendants().OfType<Button>(), button => button != cancel);
            Assert.True(cancel.IsFocused);
            Assert.True(dialog.IsEffectivelyVisible);
            Assert.Equal(chinese ? "取消" : "Cancel", cancel.Content);
            Assert.Equal(chinese ? "離開" : "Exit", confirm.Content);
            Assert.Contains("danger", confirm.Classes);
            Color foreground = Assert.IsType<SolidColorBrush>(confirm.Foreground, exactMatch: false).Color;
            Assert.True(foreground.R > foreground.G && foreground.R > foreground.B);
            Border surface = Assert.Single(dialog.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("modalSurface"));
            Assert.Equal(520, surface.Bounds.Width);
            Assert.True(Math.Abs(surface.TranslatePoint(default, window)!.Value.X - ((window.Bounds.Width - surface.Bounds.Width) / 2)) <= 0.5);
            Assert.True(window.FindControl<ContentControl>("NavigationClearConfirmationModalHost")!.ZIndex > window.FindControl<ContentControl>("ReportModalHost")!.ZIndex);
            window.Close();
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Assert.True(confirm.IsFocused);
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Assert.True(cancel.IsFocused);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(shell.Navigation.IsNavigationClearConfirmationOpen);
            Assert.True(window.IsEnabled);
            Assert.True(shell.Reports.IsReportModalOpen);
            Assert.Equal(report, shell.Reports.LoadedReportJson);
            Assert.Equal(page, shell.SelectedPage);
            // Exercise new queued work after cancellation, then persist it on the eventual close.
            shell.Reports.LoadReportJson("{\"afterCancel\":true}", "after-cancel.json");
            window.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.True(cancel.IsFocused);
            Assert.True(confirm.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            if (!closed.Task.IsCompleted)
            {
                window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            }
            Assert.False(shell.Navigation.IsNavigationClearConfirmationOpen);
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(page, shell.SelectedPage);
            Assert.Contains("afterCancel", File.ReadAllText(workspace.PathFor(Path.GetFileName(ReportHistoryFileStore.DefaultHistoryPath))), StringComparison.Ordinal);
        }
        finally
        {
            if (!closed.Task.IsCompleted)
            {
                window.Close();
                shell.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
                await closed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            }
        }
    }
}
