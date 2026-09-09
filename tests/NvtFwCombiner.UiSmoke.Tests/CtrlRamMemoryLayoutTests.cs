using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>The real input-loaded window keeps parent firmware and physical CtrlRAM positions readable.</summary>
public sealed class CtrlRamMemoryLayoutTests
{
    /// <summary>Eight physical inputs project twelve targets into three distinct continuous endpoint lanes.</summary>
    [AvaloniaFact]
    public async Task ThreeChipWindowShowsFirmwareOverviewAndSeparatePhysicalLanes()
    {
        using var workspace = TempWorkspace.Create("ctrlram-layout-threechip");
        PresentationHostServices services = await CreateServicesAsync(workspace, useRetainedDpReplacePolicy: false);
        using var window = new MainWindow(UiLaunchOptions.Parse([]), StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        { Width = 1180, Height = 1040 };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            await MainWindow.ApplyCtrlRamLaunchAsync(shell, ThreeChipArguments().CtrlRam!, TestContext.Current.CancellationToken);
            Render();
            Assert.Equal("NT51927", shell.WorkflowSession.SelectedIc);
            Assert.Equal("3", shell.WorkflowSession.SelectedNumber);
            Assert.Equal(8, shell.Replace.ReplaceSlots.Count(slot => slot.SlotKind == FirmwareSlotKind.CtrlRam && slot.HasFile));
            Assert.Empty(shell.Reports.ReportHistoryEntries);
            Capture(window, "nt51927-threechip-current.png");

            Control overview = Assert.Single(window.GetVisualDescendants().OfType<Control>(),
                control => control.Name == "CtrlRamFlashOverview" && control.IsEffectivelyVisible);
            string[] overviewText = VisibleText(overview);
            Assert.Contains("TP FW", overviewText);
            Assert.Contains("DP", overviewText);
            Assert.Contains("0x00000-0x34FFF", overviewText);
            Assert.Contains("0x3C000-0x3FFFF", overviewText);
            Control[] lanes = [.. window.GetVisualDescendants().OfType<Control>()
                .Where(control => control.Name == "CtrlRamFocusLane" && control.IsEffectivelyVisible)];
            Assert.Equal(3, lanes.Length);
            string[][] expected = [["Master", "0x16800-0x1E22F"],
                ["Slave R", "0x1F800-0x2722F"], ["Slave L", "0x28800-0x3022F"]];
            string[] expectedRoles = ["NF", "Normal", "MP", "VN"];
            for (int index = 0; index < lanes.Length; index++)
            {
                string[] labels = VisibleText(lanes[index]);
                Assert.All(expected[index], label => Assert.Contains(label, labels));
                Assert.All(expectedRoles, label => Assert.Contains(label, labels));
                Assert.InRange(lanes[index].Bounds.Width, 300, 430);
                MemoryCoverageBar rail = Assert.Single(lanes[index].GetVisualDescendants().OfType<MemoryCoverageBar>());
                Control[] cells = [.. rail.GetVisualDescendants().OfType<Control>().Where(control => control.Focusable && control.Classes.Contains("memoryFocusSlice"))];
                Assert.Equal(4, cells.Length);
                foreach (Control cell in cells)
                {
                    MemoryCoverageSegmentViewModel segment = Assert.IsType<MemoryCoverageSegmentViewModel>(cell.DataContext);
                    double expectedWidth = rail.Bounds.Width * (segment.RangeEndExclusive!.Value - segment.RangeStart!.Value) / 31280d;
                    Assert.InRange(Math.Abs(cell.Bounds.Width - expectedWidth), 0, 1);
                }
                if (index > 0)
                {
                    Point previous = lanes[index - 1].TranslatePoint(new Point(), window)!.Value;
                    Point current = lanes[index].TranslatePoint(new Point(), window)!.Value;
                    Assert.InRange(Math.Abs(previous.X - current.X), 0, 1);
                    Assert.True(current.Y >= previous.Y + lanes[index - 1].Bounds.Height);
                }
            }
            Capture(window, "nt51927-threechip-actual.png");
            Control nfRight = lanes[1].GetVisualDescendants().OfType<Control>().Single(control =>
                control.Focusable && control.DataContext is MemoryCoverageSegmentViewModel { CtrlRamRegionRole: CtrlRamRegionRole.Nf });
            Assert.True(nfRight.Focus(NavigationMethod.Tab));
            Render();
            Border card = Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
            MemoryCoverageSegmentViewModel shown = Assert.IsType<MemoryCoverageSegmentViewModel>(card.DataContext);
            Assert.Equal("0x1F800-0x207CF", shown.AddressRangeLabel);
            Assert.Equal("NF CtrlRAM · Slave R", shown.DisplayTitle);
            Assert.Same(nfRight.DataContext, shown);
            Assert.False(shell.Replace.CtrlRamFocusLanes[0].Ranges[0].Interaction.IsActive);
            Assert.False(shell.Replace.CtrlRamFocusLanes[2].Ranges[0].Interaction.IsActive);
            await Task.Delay(250, TestContext.Current.CancellationToken);
            Render();
            Capture(window, "nt51927-threechip-hover.png");
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Render();
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
            window.RequestedThemeVariant = ThemeVariant.Dark;
            shell.SelectedLanguage = "Traditional Chinese";
            Render();
            Assert.Equal(["主 IC", "右從 IC", "左從 IC"], shell.Replace.CtrlRamFocusLanes.Select(lane => lane.Title));
            Capture(window, "nt51927-threechip-dark-zh.png");
            await shell.WorkflowSession.ClearSlotFileAsync(shell.Replace.ReplaceBaseSlot.SlotId, TestContext.Current.CancellationToken);
            Render();
            Assert.Empty(shell.Replace.CtrlRamOverview);
            Assert.Empty(shell.Replace.CtrlRamFocusLanes);
            Assert.False(shell.Replace.HasCtrlRamFocusLayout);
        }
        finally { await CloseAndFlushAsync(window); }
    }

    private static UiLaunchOptions ThreeChipArguments()
    {
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace", "nt51927-3chip-self-20260705");
        var arguments = new List<string> { "--workflow", "ctrlram-replace", "--ic", "NT51927", "--ic-num", "3" };
        foreach (JsonElement artifact in fixture.GetProperty("artifacts").EnumerateArray())
        {
            string slot = artifact.GetProperty("slotId").GetString()!;
            string path = CanonicalGoldenTestData.ArtifactPath(artifact);
            arguments.Add(slot == "replace-base" ? "--base" : "--ctrlram");
            arguments.Add(slot == "replace-base" ? path : $"{slot}={path}");
        }
        return UiLaunchOptions.Parse([.. arguments]);
    }

    private static string[] VisibleText(Control control)
    {
        return [.. control.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible).Select(text => text.Text ?? string.Empty)];
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static void Capture(Window window, string name)
    {
        string directory = Path.Combine(Assert.IsType<string>(Environment.GetEnvironmentVariable("NFC_TEST_AREA_ROOT")),
            "evidence", "v114-ctrlram-layout49");
        _ = Directory.CreateDirectory(directory);
        using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, name));
    }
}
