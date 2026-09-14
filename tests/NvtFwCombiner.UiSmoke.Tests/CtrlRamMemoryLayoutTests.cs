using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>The real input-loaded window keeps parent firmware and physical CtrlRAM positions readable.</summary>
public sealed class CtrlRamMemoryLayoutTests
{
    /// <summary>Real Shared NF/VN cards retain complete guidance after selection, clearing, reselection and language changes.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedCtrlRamCardsKeepSizeAndEveryTargetAfterLoading(bool darkChinese)
    {
        using var workspace = TempWorkspace.Create("ctrlram-shared-guidance");
        PresentationHostServices services = await CreateServicesAsync(workspace, useRetainedDpReplacePolicy: false);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        { Width = 1180, Height = 1040 };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            await MainWindow.ApplyCtrlRamLaunchAsync(shell, ThreeChipArguments().CtrlRam!, TestContext.Current.CancellationToken);
            window.RequestedThemeVariant = darkChinese ? ThemeVariant.Dark : ThemeVariant.Light;
            Render();
            string[] shared = [.. shell.Replace.ReplaceSlots.Where(slot =>
                slot.HasFile && slot.CtrlRamDescriptionFacts is { IsShared: true }).Select(static slot => slot.SlotId)];
            Assert.Equal(2, shared.Length);
            foreach (string slotId in shared)
            {
                FirmwareSlotViewModel currentSlot = Assert.Single(shell.Replace.ReplaceSlots, candidate => candidate.SlotId == slotId);
                string path = Assert.IsType<string>(currentSlot.FilePath);
                for (int step = 0; step < 3; step++)
                {
                    if (step == 1)
                    {
                        await shell.WorkflowSession.ClearSlotFileAsync(slotId, TestContext.Current.CancellationToken);
                    }
                    else if (step == 2)
                    {
                        await shell.WorkflowSession.SetSlotFileAsync(slotId, path, TestContext.Current.CancellationToken);
                    }
                    // Refresh can replace ViewModels and controls: assert against the current rendered card.
                    foreach (bool chinese in new[] { false, true })
                    {
                        shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
                        Render();
                        FirmwareSlotViewModel slot = Assert.Single(shell.Replace.ReplaceSlots, candidate => candidate.SlotId == slotId);
                        Assert.Equal(step != 1, slot.HasFile);
                        Assert.Equal(step == 1, slot.IsGuidanceVisible);
                        FirmwareSlotCard card = Assert.Single(window.GetVisualDescendants().OfType<FirmwareSlotCard>(),
                            control => ReferenceEquals(control.DataContext, slot));
                        card.BringIntoView();
                        Render();
                        AssertSharedCtrlRamGuidance(card, slot, chinese);
                    }
                }
                Capture(window, $"nt51927-shared-{slotId}-{darkChinese}.png");
            }
        }
        finally { await CloseAndFlushAsync(window); }
    }

    private static void AssertSharedCtrlRamGuidance(FirmwareSlotCard card, FirmwareSlotViewModel slot, bool chinese)
    {
        // Independent expectations from this Golden's provenance and approved three-chip target map.
        bool nf = slot.SlotId == "replace-ctrlram-nf";
        Assert.True(nf || slot.SlotId == "replace-ctrlram-vn");
        string targets = nf
            ? chinese ? "主 IC: 0x16800\n右從 IC: 0x1F800\n左從 IC: 0x28800" : "Master: 0x16800\nSlave R: 0x1F800\nSlave L: 0x28800"
            : chinese ? "主 IC: 0x1CBD0\n右從 IC: 0x25BD0\n左從 IC: 0x2EBD0" : "Master: 0x1CBD0\nSlave R: 0x25BD0\nSlave L: 0x2EBD0";
        string[] expected = [chinese ? "大小上限" : "Max Size", nf ? "12,112\u00a0B" : "5,728\u00a0B",
            chinese ? "目標位址" : "Target Addr", targets];
        foreach (string text in expected)
        {
            TextBlock block = Assert.Single(card.GetVisualDescendants().OfType<TextBlock>(), candidate => candidate.Text == text);
            Assert.True(block.IsEffectivelyVisible, $"{slot.SlotId}: '{text}' disappeared (HasFile={slot.HasFile}).");
            Assert.Equal(text.Split('\n').Length, block.TextLayout.TextLines.Count);
            Assert.All(block.TextLayout.TextLines, line => Assert.False(line.HasCollapsed));
            Assert.True(block.TextLayout.Height <= block.Bounds.Height + 1, $"{slot.SlotId}: '{text}' is clipped vertically.");
            Point position = block.TranslatePoint(default, card)!.Value;
            Assert.InRange(position.X, 0, card.Bounds.Width - block.Bounds.Width + 1);
            Assert.InRange(position.Y, 0, card.Bounds.Height - block.Bounds.Height + 1);
        }
        Assert.DoesNotContain(VisibleText(card), text => text.Contains("16\u00a0B", StringComparison.Ordinal));
        Assert.Equal(3, slot.CtrlRamDescriptionFacts!.InputGuidanceTargets.Count);
        Assert.Equal(nf ? 5 : 3, slot.CtrlRamDescriptionFacts.Sections.Count);
    }

    /// <summary>CtrlRAM endpoint details stay hidden until their own position is explored.</summary>
    [AvaloniaTheory]
    [InlineData(CtrlRamRegionRole.Mp)]
    [InlineData(CtrlRamRegionRole.Normal)]
    public async Task LoadedCtrlRamWindowStartsWithOnlyTheOverview(CtrlRamRegionRole role)
    {
        using var workspace = TempWorkspace.Create("ctrlram-layout-collapsed");
        PresentationHostServices services = await CreateServicesAsync(workspace, useRetainedDpReplacePolicy: false);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        { Width = 1180, Height = 1040 };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            await MainWindow.ApplyCtrlRamLaunchAsync(shell, ThreeChipArguments().CtrlRam!, TestContext.Current.CancellationToken);
            Render();
            Assert.Equal(3, shell.Replace.CtrlRamFocusLanes.Count);
            _ = Assert.Single(window.GetVisualDescendants().OfType<Control>(),
                control => control.Name == "CtrlRamFlashOverview" && control.IsEffectivelyVisible);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(),
                control => control.Name == "CtrlRamFocusLane" && control.IsEffectivelyVisible);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(),
                control => control.Name is "MemoryLocalView" or "MemorySliceCard");
            Control overview = window.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "CtrlRamFlashOverview");
            Control mainRail = overview.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "MemoryMainRail");
            double railTop = mainRail.TranslatePoint(default, window)!.Value.Y;
            foreach (string address in new[] { "0x00000", shell.Replace.CtrlRamEndAddress })
            {
                TextBlock label = overview.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == address);
                Assert.True(label.TranslatePoint(default, window)!.Value.Y + label.Bounds.Height <= railTop,
                    "Overview addresses belong above the main rail.");
            }
            Capture(window, "nt51927-hover60-collapsed.png");
            Control position = Positions(window)[0];
            window.MouseMove(Center(position, window), RawInputModifiers.None);
            await SettleAsync();
            _ = Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemoryLocalView");
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
            Capture(window, "nt51927-hover60-master.png");
            Control leaf = LocalCells(window).Single(control => control.DataContext is MemoryCoverageSegmentViewModel
            segment && segment.CtrlRamRegionRole == role);
            window.MouseMove(Center(leaf, window), RawInputModifiers.None);
            await SettleAsync();
            Border card = Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
            Assert.Same(leaf.DataContext, card.DataContext);
            AssertLiftIsNotClipped(leaf);
            Capture(window, role == CtrlRamRegionRole.Mp ? "nt51927-hover60-master-mp.png" : "nt51927-master-normal-compact-gap.png");
            window.MouseMove(Center(card, window), RawInputModifiers.None);
            await SettleAsync(400);
            Assert.Same(card, Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard"));
            window.MouseMove(new Point(20, 20), RawInputModifiers.None);
            await SettleAsync(400);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), control => control.Name is "MemoryLocalView" or "MemorySliceCard");

            // Clicking an endpoint is not a pin: mouse-origin focus must not defeat exit dismissal.
            window.MouseMove(Center(position, window), RawInputModifiers.None);
            window.MouseDown(Center(position, window), MouseButton.Left);
            window.MouseUp(Center(position, window), MouseButton.Left);
            await SettleAsync();
            window.MouseMove(new Point(20, 20), RawInputModifiers.None);
            await SettleAsync(400);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), control => control.Name is "MemoryLocalView" or "MemorySliceCard");
        }
        finally { await CloseAndFlushAsync(window); }
    }

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
            foreach (string range in new[] { "0x00000-0x34FFF", "0x3C000-0x3FFFF" })
            {
                Border legend = Assert.Single(overview.GetVisualDescendants().OfType<Border>(),
                    control => control.Name == "MemoryLegendTarget" && control.DataContext is MemoryCoverageSegmentViewModel slice && slice.AddressRangeLabel == range);
                Assert.True(legend.Focus(NavigationMethod.Tab));
                Render();
                Border overviewCard = Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
                Assert.Contains(range, VisibleText(overviewCard));
            }
            Assert.Equal(3, Positions(window).Length);
            string[][] expected = [["Master", "0x16800", "0x1E22F"],
                ["Slave R", "0x1F800", "0x2722F"], ["Slave L", "0x28800", "0x3022F"]];
            string[] expectedRoles = ["NF", "Normal", "MP", "VN"];
            for (int index = 0; index < expected.Length; index++)
            {
                ProportionalStackPanel strip = OpenLane(window, shell.Replace.CtrlRamFocusLanes[index]);
                Border local = Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemoryLocalView");
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
                string[] labels = VisibleText(local);
                Assert.All(expected[index], label => Assert.Contains(label, labels));
                Assert.All(expectedRoles, label => Assert.Contains(label, labels));
                Assert.InRange(local.Bounds.Width, 300, 430);
                Control[] cells = LocalCells(window);
                Assert.Equal(4, cells.Length);
                foreach (Control cell in cells)
                {
                    MemoryCoverageSegmentViewModel segment = Assert.IsType<MemoryCoverageSegmentViewModel>(cell.DataContext);
                    double expectedWidth = strip.Bounds.Width * (segment.RangeEndExclusive!.Value - segment.RangeStart!.Value) / 31280d;
                    Assert.InRange(Math.Abs(cell.Bounds.Width - expectedWidth), 0, 1);
                }
                await SettleAsync();
                Border activePosition = Assert.IsType<Border>(Positions(window)[index]);
                Assert.Equal(default, activePosition.BorderThickness);
                Assert.Equal(default, activePosition.BoxShadow);
                Assert.Equal(Matrix.Identity, activePosition.RenderTransform?.Value ?? Matrix.Identity);
                Assert.Equal(global::Avalonia.Media.Brushes.Transparent, activePosition.Background);
                Point[] labelCenters = [.. Positions(window).Select(position =>
                    Center(Assert.Single(position.GetVisualDescendants().OfType<TextBlock>()), window))];
                Assert.All(labelCenters, center => Assert.InRange(Math.Abs(center.Y - labelCenters[0].Y), 0, 0.5));
                Capture(window, $"nt51927-hover60-endpoint-{index}.png");
            }
            Capture(window, "nt51927-threechip-actual.png");
            _ = OpenLane(window, shell.Replace.CtrlRamFocusLanes[1]);
            Control nfRight = LocalCells(window).Single(control =>
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
            AssertLiftIsNotClipped(nfRight);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Render();
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
            await AssertLaneEdgesAsync(window);
            window.RequestedThemeVariant = ThemeVariant.Dark;
            shell.SelectedLanguage = "Traditional Chinese";
            Render();
            Assert.Equal(["主 IC", "右從 IC", "左從 IC"], shell.Replace.CtrlRamFocusLanes.Select(lane => lane.Title));
            Capture(window, "nt51927-threechip-dark-zh.png");
            await AssertLaneEdgesAsync(window);
            await AssertSupportedMemoryViewportsAsync(window, "nt51927-threechip");
            await shell.WorkflowSession.ClearSlotFileAsync(shell.Replace.ReplaceBaseSlot.SlotId, TestContext.Current.CancellationToken);
            Render();
            Assert.Empty(shell.Replace.CtrlRamOverview);
            Assert.Empty(shell.Replace.CtrlRamFocusLanes);
            Assert.False(shell.Replace.HasCtrlRamFocusLayout);
        }
        finally { await CloseAndFlushAsync(window); }
    }

    /// <summary>The real NT51928 Standard inputs retain DP/TP/LDC geometry through the shared renderer.</summary>
    [AvaloniaFact]
    public async Task Nt51928StandardWindowKeepsDpTpAndLdcCoverage()
    {
        using var workspace = TempWorkspace.Create("memory-standard-928");
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement inputs = golden.CaseByIc("51928").GetProperty("inputs");
        PresentationHostServices services = await CreateServicesAsync(workspace, useRetainedDpReplacePolicy: false);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        { Width = 1180, Height = 1040 };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            shell.ShowMergeCommand.Execute(null);
            shell.WorkflowSession.SelectedIc = "NT51928";
            shell.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
            foreach ((string slot, string artifact) in new[]
            {
                (CompositionSlotIds.MergeDp, "dp-input"), (CompositionSlotIds.MergeTp, "tp-input"),
                (CompositionSlotIds.MergeLdc, "ldc-input"),
            })
            {
                await shell.WorkflowSession.SetSlotFileAsync(slot, golden.ManifestPath(inputs.GetProperty(artifact)),
                    TestContext.Current.CancellationToken);
            }
            Render();
            Assert.Equal("NT51928", shell.WorkflowSession.SelectedIc);
            Assert.True(shell.Merge.IsNormalMergeModeSelected);
            Assert.Equal(3, shell.Merge.MergeSlots.Count(slot => slot.HasFile));
            Assert.Empty(shell.Reports.ReportHistoryEntries);
            Assert.Contains(shell.Merge.MergeCoverageSegments, segment => segment.ContentRole == MemoryContentRole.Dp);
            Assert.Contains(shell.Merge.MergeCoverageSegments, segment => segment.ContentRole == MemoryContentRole.Tp);
            Assert.Contains(shell.Merge.MergeCoverageSegments, segment => segment.ContentRole == MemoryContentRole.Ldc);
            MemoryCoverageBar rail = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(),
                control => control.IsEffectivelyVisible);
            Assert.False(rail.ClipToBounds);
            Assert.Equal(34, Assert.Single(rail.GetVisualDescendants().OfType<ItemsControl>(), control => control.Name == "MemoryMainRail").Bounds.Height);
            Assert.InRange(rail.Bounds.Width, 300, 430);
            Border[] slices = [.. rail.GetVisualDescendants().OfType<Border>()
                .Where(border => border.Classes.Contains("memoryExplorerSlice"))];
            Assert.NotEmpty(slices);
            Assert.All(slices, slice => Assert.Equal(default, slice.BorderThickness));
            Capture(window, "nt51928-standard-actual.png");
            await AssertSupportedMemoryViewportsAsync(window, "nt51928-standard");
        }
        finally { await CloseAndFlushAsync(window); }
    }

    /// <summary>Characterizes supported full-window sizes without treating vertical scrolling as clipping.</summary>
    internal static async Task AssertSupportedMemoryViewportsAsync(Window window, string scenario)
    {
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        double originalWidth = window.Width;
        double originalHeight = window.Height;
        ThemeVariant? originalTheme = window.RequestedThemeVariant;
        string originalLanguage = shell.SelectedLanguage;
        string ic = shell.WorkflowSession.SelectedIc;
        string number = shell.WorkflowSession.SelectedNumber;
        (long?, long?, MemoryContentRole, bool)[] ranges = Ranges();
        try
        {
            foreach ((int width, int height) in new[] { (980, 640), (1180, 760), (1440, 900) })
            {
                foreach (bool darkChinese in new[] { false, true })
                {
                    window.Width = width;
                    window.Height = height;
                    window.RequestedThemeVariant = darkChinese ? ThemeVariant.Dark : ThemeVariant.Light;
                    shell.SelectedLanguage = darkChinese ? "Traditional Chinese" : "English";
                    Render();
                    await Task.Delay(180, TestContext.Current.CancellationToken);
                    Render();
                    Assert.Equal(width, window.ClientSize.Width);
                    Assert.Equal(height, window.ClientSize.Height);
                    MemoryCoverageBar[] rails = [.. window.GetVisualDescendants().OfType<MemoryCoverageBar>()
                        .Where(rail => rail.IsEffectivelyVisible)];
                    Assert.NotEmpty(rails);
                    foreach (MemoryCoverageBar rail in rails)
                    {
                        Point origin = rail.TranslatePoint(default, window)!.Value;
                        Assert.True(rail.Bounds.Width > 0);
                        Assert.InRange(origin.X, -1, window.ClientSize.Width - rail.Bounds.Width + 1);
                        ItemsControl main = Assert.Single(rail.GetVisualDescendants().OfType<ItemsControl>(),
                            control => control.Name == "MemoryMainRail");
                        Assert.InRange(main.Bounds.Height, 33.5, 34.5);
                        WrapPanel legend = Assert.Single(rail.GetVisualDescendants().OfType<WrapPanel>(), panel => panel.Name == "MemoryLegend");
                        Assert.True(legend.IsEffectivelyVisible);
                        Point legendOrigin = legend.TranslatePoint(default, rail)!.Value;
                        Point mainOrigin = main.TranslatePoint(default, rail)!.Value;
                        Assert.True(legendOrigin.Y >= 0);
                        Assert.True(legendOrigin.Y + legend.Bounds.Height <= mainOrigin.Y + 0.5);
                        Assert.InRange(Math.Abs(legendOrigin.X + legend.Bounds.Width - rail.Bounds.Width), 0, 0.5);
                    }
                    foreach (FirmwareSlotCard card in window.GetVisualDescendants().OfType<FirmwareSlotCard>()
                        .Where(card => card.IsEffectivelyVisible))
                    {
                        Border surface = Assert.Single(card.GetVisualDescendants().OfType<Border>(),
                            border => border.Classes.Contains("firmwareSlot"));
                        foreach (string name in new[] { "BrowseButton", "ClearButton" })
                        {
                            Button action = card.FindControl<Button>(name)!;
                            Point origin = action.TranslatePoint(default, surface)!.Value;
                            Assert.InRange(Math.Abs(origin.Y + (action.Bounds.Height / 2) -
                                (surface.Bounds.Height / 2)), 0, 0.5);
                            Assert.InRange(origin.X, 0, surface.Bounds.Width - action.Bounds.Width);
                        }
                    }
                    Assert.Equal(ic, shell.WorkflowSession.SelectedIc);
                    Assert.Equal(number, shell.WorkflowSession.SelectedNumber);
                    Assert.Equal(ranges, Ranges());
                    Capture(window, $"{scenario}-{width}x{height}-{(darkChinese ? "dark-zh" : "light-en")}.png");
                }
            }
        }
        finally
        {
            window.Width = originalWidth;
            window.Height = originalHeight;
            window.RequestedThemeVariant = originalTheme;
            shell.SelectedLanguage = originalLanguage;
            Render();
        }

        (long?, long?, MemoryContentRole, bool)[] Ranges()
        {
            return [.. shell.Merge.MergeCoverageSegments.Concat(shell.Replace.ReplaceCoverageSegments)
                .Select(segment => (segment.RangeStart, segment.RangeEndExclusive, segment.ContentRole, segment.IsSelectedForWrite))];
        }
    }

    private static async Task AssertLaneEdgesAsync(Window window)
    {
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.NotEmpty(shell.Replace.CtrlRamFocusLanes);
        foreach (MemoryFocusLaneViewModel lane in shell.Replace.CtrlRamFocusLanes)
        {
            foreach (bool last in new[] { false, true })
            {
                _ = OpenLane(window, lane);
                await SettleAsync();
                Control[] cells = LocalCells(window);
                Control target = last ? cells[^1] : cells[0];
                Rect resting = target.Bounds;
                Point center = target.TranslatePoint(new Point(resting.Width / 2, resting.Height / 2), window)!.Value;
                window.MouseMove(center, RawInputModifiers.None);
                await SettleAsync();
                AssertLiftIsNotClipped(target);
                Assert.Equal(resting, target.Bounds);
                shell.IsReducedMotionEnabled = true;
                Render();
                Assert.True(target.Classes.Contains("reducedMotion"), string.Join(",", target.Classes));
                Assert.Null(target.Transitions);
                Assert.Equal(Matrix.Identity, target.RenderTransform?.Value ?? Matrix.Identity);
                shell.IsReducedMotionEnabled = false;
                window.MouseMove(new Point(20, 20), RawInputModifiers.None);
                await Task.Delay(400, TestContext.Current.CancellationToken);
                Render();
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), border => border.Name == "MemorySliceCard");
            }
        }
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

    private static void AssertLiftIsNotClipped(Control target)
    {
        Assert.NotNull(target.RenderTransform);
        Assert.InRange(target.RenderTransform.Value.M22, 1.179, 1.181);
        Assert.NotEqual(default, Assert.IsType<Border>(target).BoxShadow);
        foreach (Visual ancestor in target.GetVisualAncestors().Where(ancestor => ancestor.ClipToBounds))
        {
            Matrix transform = target.TransformToVisual(ancestor)!.Value;
            Rect visible = new Rect(target.Bounds.Size).TransformToAABB(transform);
            // Proportional child widths snap independently; the existing rail contract allows one horizontal pixel.
            Assert.True(visible.Top >= -0.5 && visible.Bottom <= ancestor.Bounds.Height + 0.5 &&
                visible.Left >= -1 && visible.Right <= ancestor.Bounds.Width + 1,
                $"Lift {visible} clipped by {ancestor.GetType().Name} {(ancestor as Control)?.Name}: {ancestor.Bounds.Size}");
        }
    }

    internal static ProportionalStackPanel OpenLane(Window window, MemoryFocusLaneViewModel lane)
    {
        Control position = Positions(window).Single(control => control.DataContext is MemoryFocusPositionViewModel model && ReferenceEquals(model.Lane, lane));
        // A previous mouse exit intentionally leaves focus without pinning its popup.
        // This helper models a fresh keyboard focus entry, not focusing the same element twice.
        if (ReferenceEquals(window.FocusManager?.GetFocusedElement(), position))
        {
            Button otherTarget = window.GetVisualDescendants().OfType<Button>().First(button =>
                button.IsEffectivelyVisible && button.IsEffectivelyEnabled && button.Focusable);
            Assert.True(otherTarget.Focus(NavigationMethod.Tab));
        }
        Assert.True(position.Focus(NavigationMethod.Tab));
        Render();
        return Assert.Single(window.GetVisualDescendants().OfType<ProportionalStackPanel>(), panel => panel.Name == "MemoryLocalStrip");
    }

    private static Control[] Positions(Window window)
    {
        return [.. window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.Name == "MemoryFocusPosition" && control.IsEffectivelyVisible)];
    }

    private static Control[] LocalCells(Window window)
    {
        return [.. window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.Classes.Contains("memoryLocalSlice") && control.Focusable)];
    }

    private static Point Center(Control control, Window window)
    {
        return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
    }

    private static async Task SettleAsync(int delayMilliseconds = 220)
    {
        await Task.Delay(delayMilliseconds, TestContext.Current.CancellationToken);
        Render();
    }

    private static void Capture(Window window, string name)
    {
        string directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR") ??
            Path.Combine(Assert.IsType<string>(Environment.GetEnvironmentVariable("NFC_TEST_AREA_ROOT")),
                "evidence", "v114-memory-lift51");
        _ = Directory.CreateDirectory(directory);
        using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, name));
    }
}
