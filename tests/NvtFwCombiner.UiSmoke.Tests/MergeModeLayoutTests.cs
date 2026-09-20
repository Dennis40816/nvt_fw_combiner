using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Checks mode anchors through production page layout changes.</summary>
public sealed class MergeModeLayoutTests
{
    /// <summary>Scrollbar visibility and mode changes preserve the horizontal mode anchor.</summary>
    [AvaloniaTheory]
    [InlineData(980, false)]
    [InlineData(1440, false)]
    [InlineData(1440, true)]
    public async Task ModeAnchorStaysFixedWhenContentStartsScrolling(int width, bool dark)
    {
        using var workspace = TempWorkspace.Create("merge-mode-layout");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        { Width = width, Height = 2200, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            shell.ShowMergeCommand.Execute(null);
            shell.WorkflowSession.SelectedIc = "NT51950";
            shell.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
            Render();
            Grid header = Assert.Single(window.GetVisualDescendants().OfType<Grid>(), x => x.Name == "MergePageHeader");
            ComboBox mode = Assert.Single(header.GetVisualDescendants().OfType<ComboBox>());
            ScrollViewer scroll = header.GetVisualAncestors().OfType<ScrollViewer>().First();
            Assert.True(scroll.Extent.Height <= scroll.Viewport.Height);
            double initialX = mode.TranslatePoint(default, window)!.Value.X;
            window.Height = 640;
            Render();
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            Assert.InRange(Math.Abs(mode.TranslatePoint(default, window)!.Value.X - initialX), 0, 0.5);
            foreach (string choice in shell.Merge.MergeModeChoices.ToArray())
            {
                shell.Merge.SelectedMergeMode = choice;
                Render();
                Assert.InRange(Math.Abs(mode.TranslatePoint(default, window)!.Value.X - initialX), 0, 0.5);
            }
        }
        finally { await CloseAndFlushAsync(window); }
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
