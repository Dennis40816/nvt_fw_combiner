using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Every Memory Layout bar shows its outer start and end address above the rail.</summary>
public sealed class MemoryOuterAddressTests
{
    /// <summary>The AB Merge bar shows the image's outer addresses and keeps its range box.</summary>
    [AvaloniaFact]
    public async Task AbMergeBarShowsOuterAddresses()
    {
        using var workspace = TempWorkspace.Create("outer-address-merge");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
        { Width = 1440, Height = 1040, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ab-merge", "nt51929-ab-t05-d06");
            await MainWindow.ApplyAbMergeLaunchAsync(shell, new AbMergeLaunchRequest("NT51929", "single",
                PathFor(golden, "dp-ab-input"), PathFor(golden, "tp-a-input"), PathFor(golden, "tp-b-input")),
                TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("0x00000", shell.Merge.MergeStartAddress);
            Assert.Equal("0x7FFFF", shell.Merge.MergeEndAddress);
            Assert.False(string.IsNullOrEmpty(shell.Merge.MergeMemoryRangeLabel));
            MemoryCoverageBar bar = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(), static bar => bar.IsEffectivelyVisible);
            Grid addresses = Assert.Single(bar.GetVisualDescendants().OfType<Grid>(), static grid => grid.Name == "MemoryOverviewAddresses");
            Assert.True(addresses.IsEffectivelyVisible);
            Assert.Equal(["0x00000", "0x7FFFF"], addresses.Children.OfType<TextBlock>().Select(static text => text.Text));
        }
        finally { await CloseAndFlushAsync(window); }
    }

    /// <summary>The Replace flash bar carries the same outer addresses as the CtrlRAM overview of the image.</summary>
    [AvaloniaFact]
    public async Task ReplaceFlashBarCarriesOuterAddresses()
    {
        using var workspace = TempWorkspace.Create("outer-address-replace");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
        { Width = 1440, Height = 1040, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", "nt51950-fw200-single-auto-prj-676-20260717");
            UiLaunchOptions options = UiLaunchOptions.Parse(["--workflow", "ctrlram-replace", "--ic", "NT51950", "--ic-num", "single",
                "--base", PathFor(fixture, "expected-output"), "--ctrlram", "replace-ctrlram-nf=" + PathFor(fixture, "postbuild-nf-ctrlram")]);
            await MainWindow.ApplyCtrlRamLaunchAsync(shell, options.CtrlRam!, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("0x00000", shell.Replace.ReplaceStartAddress);
            Assert.Equal("0x3FFFF", shell.Replace.ReplaceEndAddress);
            Assert.Equal(shell.Replace.CtrlRamStartAddress, shell.Replace.ReplaceStartAddress);
            Assert.Equal(shell.Replace.CtrlRamEndAddress, shell.Replace.ReplaceEndAddress);
            MemoryCoverageBar flash = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(),
                bar => ReferenceEquals(bar.ItemsSource, shell.Replace.ReplaceCoverageSegments));
            Assert.Equal("0x00000", flash.StartAddress);
            Assert.Equal("0x3FFFF", flash.EndAddress);
            shell.Replace.ClearCtrlRamInspectionDisplay();
            Dispatcher.UIThread.RunJobs();
            Assert.True(string.IsNullOrEmpty(flash.StartAddress), "A cleared display keeps no outer start address.");
            Assert.True(string.IsNullOrEmpty(flash.EndAddress), "A cleared display keeps no outer end address.");
        }
        finally { await CloseAndFlushAsync(window); }
    }

    private static string PathFor(JsonElement fixture, string id)
    {
        return CanonicalGoldenTestData.ArtifactPath(fixture.GetProperty("artifacts").EnumerateArray()
            .Single(artifact => artifact.GetProperty("artifactId").GetString() == id));
    }
}
