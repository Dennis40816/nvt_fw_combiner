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

/// <summary>Each production output panel shows its outer addresses and full output range.</summary>
public sealed class MemoryOuterAddressTests
{
    /// <summary>The AB panel retains outer endpoints and the full output-range fact in its collapsed context.</summary>
    [AvaloniaFact]
    public async Task AbMergePanelShowsOuterAddressesAndFullRange()
    {
        using var workspace = TempWorkspace.Create("outer-address-merge");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace);
        await host.LoadAbAsync();
        Assert.Equal("0x00000", host.Model.StartAddress);
        Assert.Equal("0x7FFFF", host.Model.EndAddress);
        Assert.Equal(host.Model.StartAddress, host.Panel.FindControl<TextBlock>("LayoutStart")!.Text);
        Assert.Equal(host.Model.EndAddress, host.Panel.FindControl<TextBlock>("LayoutEnd")!.Text);
        Expander context = host.Panel.FindControl<Expander>("LayoutContext")!;
        Assert.False(context.IsExpanded);
        context.IsExpanded = true;
        host.Render();
        TextBlock range = host.Panel.FindControl<TextBlock>("LayoutRange")!;
        Assert.Equal(host.Shell.Merge.OutputLayout.Current.RangeLabel, range.Text);
        Assert.True(range.IsEffectivelyVisible);
        Assert.Contains("0x7FFFF", range.Text!, StringComparison.Ordinal);
    }

    /// <summary>The Replace panel carries the exact outer addresses and clears them with its publication.</summary>
    [AvaloniaFact]
    public async Task ReplacePanelCarriesOuterAddresses()
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
            var options = UiLaunchOptions.Parse(["--workflow", "ctrlram-replace", "--ic", "NT51950", "--ic-num", "single",
                "--base", PathFor(fixture, "expected-output"), "--ctrlram", "replace-ctrlram-nf=" + PathFor(fixture, "postbuild-nf-ctrlram")]);
            await MainWindow.ApplyCtrlRamLaunchAsync(shell, options.CtrlRam!, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            OutputLayoutPanel panel = Assert.Single(window.GetVisualDescendants().OfType<OutputLayoutPanel>(),
                static candidate => candidate.IsEffectivelyVisible);
            OutputLayoutPanelModel model = Assert.IsType<OutputLayoutPanelModel>(shell.Replace.OutputLayout.Current.Panel);
            Assert.Same(model, panel.Model);
            Assert.Equal("0x00000", model.StartAddress);
            Assert.Equal("0x3FFFF", model.EndAddress);
            Assert.Equal(model.StartAddress, panel.FindControl<TextBlock>("LayoutStart")!.Text);
            Assert.Equal(model.EndAddress, panel.FindControl<TextBlock>("LayoutEnd")!.Text);
            Expander context = panel.FindControl<Expander>("LayoutContext")!;
            context.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(shell.Replace.OutputLayout.Current.RangeLabel, panel.FindControl<TextBlock>("LayoutRange")!.Text);
            shell.Replace.ClearCtrlRamInspectionDisplay();
            Dispatcher.UIThread.RunJobs();
            OutputLayoutPanelModel cleared = Assert.IsType<OutputLayoutPanelModel>(shell.Replace.OutputLayout.Current.Panel);
            Assert.Same(cleared, panel.Model);
            Assert.True(string.IsNullOrEmpty(cleared.StartAddress), "A cleared display keeps no outer start address.");
            Assert.True(string.IsNullOrEmpty(cleared.EndAddress), "A cleared display keeps no outer end address.");
        }
        finally { await CloseAndFlushAsync(window); }
    }

    private static string PathFor(JsonElement fixture, string id)
    {
        return CanonicalGoldenTestData.ArtifactPath(fixture.GetProperty("artifacts").EnumerateArray()
            .Single(artifact => artifact.GetProperty("artifactId").GetString() == id));
    }
}
