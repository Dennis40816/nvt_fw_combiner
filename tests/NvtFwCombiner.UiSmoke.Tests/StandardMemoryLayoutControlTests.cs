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

/// <summary>Loaded Standard inputs exercise direct memory cards without a CtrlRAM tier.</summary>
public sealed class StandardMemoryLayoutControlTests
{
    /// <summary>DP, TP and LDC cards preserve input state while opening and closing inside the viewport.</summary>
    [AvaloniaTheory]
    [InlineData(1440, 900, false, false)]
    [InlineData(1440, 900, true, true)]
    [InlineData(980, 640, false, true)]
    [InlineData(980, 640, true, false)]
    public async Task LoadedStandardCardsStayInsideTheRailAndCloseOnExit(int width, int height, bool dark, bool chinese)
    {
        using var workspace = TempWorkspace.Create("standard-memory-controls");
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement inputs = golden.CaseByIc("51928").GetProperty("inputs");
        PresentationHostServices services = await CreateServicesAsync(workspace, useRetainedDpReplacePolicy: false);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        { Width = width, Height = height, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            shell.ShowMergeCommand.Execute(null);
            shell.WorkflowSession.SelectedIc = "NT51928";
            shell.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
            shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
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
            Assert.Equal(3, shell.Merge.MergeSlots.Count(slot => slot.HasFile));
            CtrlRamSelectorLayoutTests.AssertMergePanelAlignment(window);
            string?[] originalPaths = [.. shell.Merge.MergeSlots.Select(slot => slot.FilePath)];
            (long?, long?, string?, bool)[] originalRanges = [.. shell.Merge.MergeCoverageSegments.Select(segment =>
                (segment.RangeStart, segment.RangeEndExclusive, segment.SourceSlotId, segment.IsSelectedForWrite))];
            MemoryCoverageBar rail = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(),
                control => control.IsEffectivelyVisible);
            Assert.Equal(34, Assert.Single(rail.GetVisualDescendants().OfType<ItemsControl>(), control => control.Name == "MemoryMainRail").Bounds.Height);
            Assert.Null(rail.FocusPositions);
            AssertNoOverlay();
            rail.BringIntoView();
            Render();
            rail.ReducedMotion = true;
            foreach (MemoryContentRole role in new[] { MemoryContentRole.Dp, MemoryContentRole.Tp, MemoryContentRole.Ldc })
            {
                Border leaf = rail.GetVisualDescendants().OfType<Border>().First(control =>
                    control.Classes.Contains("memoryCoverageBarSegment") &&
                    control.DataContext is MemoryCoverageSegmentViewModel segment && segment.ContentRole == role);
                MemoryCoverageSegmentViewModel selected = Assert.IsType<MemoryCoverageSegmentViewModel>(leaf.DataContext);
                Assert.True(selected.RangeEndExclusive > selected.RangeStart);
                Assert.True(leaf.Focus(NavigationMethod.Tab));
                Render();
                AssertCard();
                await SaveAsync(role);
                if (role == MemoryContentRole.Tp)
                {
                    Border detailCard = Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
                    Expander disclosure = Assert.Single(detailCard.GetVisualDescendants().OfType<Expander>());
                    Assert.False(disclosure.IsExpanded);
                    disclosure.IsExpanded = true;
                    Render();
                    AssertCard();
                    Assert.Contains(detailCard.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == shell.Text.MemoryAddressSpaceLabel && block.IsEffectivelyVisible);
                    await SaveAsync(role, "-technical-expanded");
                }
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                Render();
                AssertNoOverlay();
                window.MouseMove(Bounds(leaf).Center, RawInputModifiers.None);
                Render();
                AssertCard();
                var outside = new Point(4, 4);
                Border openCard = Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
                Assert.True(new Rect(window.ClientSize).Contains(outside));
                Assert.False(Bounds(rail).Contains(outside));
                Assert.False(Bounds(openCard).Contains(outside));
                window.MouseMove(outside, RawInputModifiers.None);
                await Task.Delay(400, TestContext.Current.CancellationToken);
                Render();
                AssertNoOverlay();

                void AssertCard()
                {
                    Assert.DoesNotContain(window.GetVisualDescendants(), control => control.Name == "MemoryLocalView");
                    Border card = Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
                    Assert.Same(selected, card.DataContext);
                    Rect bounds = Bounds(card);
                    Rect railBounds = Bounds(rail);
                    Assert.True(bounds.Width > 0 && bounds.Height > 0);
                    Assert.InRange(bounds.Left, 0, window.ClientSize.Width - bounds.Width);
                    Assert.InRange(bounds.Left, railBounds.Left - 1, railBounds.Right - bounds.Width + 1);
                    Assert.InRange(bounds.Top, 0, window.ClientSize.Height - bounds.Height);
                    WrapPanel legend = Assert.Single(rail.GetVisualDescendants().OfType<WrapPanel>(), panel => panel.Name == "MemoryLegend");
                    Assert.False(bounds.Intersects(Bounds(legend)), "The card must not cover its sibling legend targets.");
                    string?[] visible = [.. card.GetVisualDescendants().OfType<TextBlock>()
                        .Where(block => block.IsEffectivelyVisible).Select(block => block.Text)];
                    Assert.Contains(selected.AddressRangeLabel, visible);
                    Assert.Contains(selected.SizeValue, visible);
                    Assert.Contains(selected.SourceLabel, visible);
                }
            }
            Assert.Equal("NT51928", shell.WorkflowSession.SelectedIc);
            Assert.Equal(ExperienceIds.StandardMerge, shell.Merge.SelectedMergeMode);
            Assert.Equal(originalPaths, shell.Merge.MergeSlots.Select(slot => slot.FilePath));
            Assert.Equal(originalRanges, shell.Merge.MergeCoverageSegments.Select(segment =>
                (segment.RangeStart, segment.RangeEndExclusive, segment.SourceSlotId, segment.IsSelectedForWrite)));
        }
        finally { await CloseAndFlushAsync(window); }

        Rect Bounds(Control control)
        {
            return new(Assert.IsType<Point>(control.TranslatePoint(default, window)), control.Bounds.Size);
        }
        void AssertNoOverlay()
        {
            Assert.DoesNotContain(window.GetVisualDescendants(), control => control.Name is "MemoryLocalView" or "MemorySliceCard");
        }
        async Task SaveAsync(MemoryContentRole role, string suffix = "")
        {
            string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (string.IsNullOrWhiteSpace(directory)) { return; }
            for (int tick = 0; tick < 4; tick++)
            {
                await Task.Delay(80, TestContext.Current.CancellationToken);
                Render();
            }
            _ = Directory.CreateDirectory(directory);
            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            Assert.Equal(new PixelSize(width, height), frame.PixelSize);
            frame.Save(Path.Combine(directory, $"standard-memory-{width}-{height}-{dark}-{chinese}-{role}{suffix}.png"));
        }
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
