using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Loaded AB inputs exercise the shared memory overlay in the actual product window.</summary>
public sealed class AbMemoryLayoutControlTests
{
    /// <summary>Direct DP/TP cards fit the viewport and dismiss without creating a CtrlRAM endpoint tier.</summary>
    [AvaloniaTheory]
    [InlineData(1440, 900, false, false)]
    [InlineData(1440, 900, true, true)]
    [InlineData(980, 640, false, true)]
    [InlineData(980, 640, true, false)]
    public async Task LoadedAbCardsStayInsideTheRailAndCloseOnExit(int width, int height, bool dark, bool chinese)
    {
        using var workspace = TempWorkspace.Create("ab-memory-controls");
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
            shell.WorkflowSession.SelectedIc = "NT51929";
            shell.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
            if (chinese) { shell.SelectedLanguage = "Traditional Chinese"; }
            JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ab-merge", "nt51929-ab-t05-d06");
            JsonElement[] artifacts = [.. golden.GetProperty("artifacts").EnumerateArray()];
            string[] slots = [CompositionAddressSpaceIds.DpAbInput, CompositionAddressSpaceIds.TpAInput, CompositionAddressSpaceIds.TpBInput];
            foreach (string id in slots)
            {
                await shell.WorkflowSession.SetSlotFileAsync(id, CanonicalGoldenTestData.ArtifactPath(
                    artifacts.Single(artifact => artifact.GetProperty("artifactId").GetString() == id)),
                    TestContext.Current.CancellationToken);
            }
            Render();
            Assert.All(shell.Merge.AbMergeSlots, slot => Assert.True(slot.HasFile));
            (long?, long?, string?, bool)[] originalRanges = [.. shell.Merge.MergeCoverageSegments.Select(segment =>
                (segment.RangeStart, segment.RangeEndExclusive, segment.SourceSlotId, segment.IsSelectedForWrite))];
            MemoryCoverageBar rail = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(),
                control => control.IsEffectivelyVisible);
            Assert.Equal(34, rail.Bounds.Height);
            Assert.Null(rail.FocusPositions);
            AssertNoOverlay();
            rail.BringIntoView();
            Render();
            rail.ReducedMotion = true;
            // The canonical AB bank template is 0x40000 bytes, with TP at +0x7000.
            // TPB is copied from the relocated work buffer, not directly from its input slot.
            foreach ((string name, long start, long end) in new[]
            {
                ("dp-ab", 0L, 0x7000L), ("tp-a", 0x7000L, 0x40000L), ("tp-b", 0x47000L, 0x80000L),
            })
            {
                Border leaf = rail.GetVisualDescendants().OfType<Border>().First(control =>
                    control.Classes.Contains("memoryCoverageBarSegment") &&
                    control.DataContext is MemoryCoverageSegmentViewModel segment && segment.RangeStart == start);
                MemoryCoverageSegmentViewModel selected = Assert.IsType<MemoryCoverageSegmentViewModel>(leaf.DataContext);
                Assert.Equal(end, selected.RangeEndExclusive);
                Assert.True(leaf.Focus(NavigationMethod.Tab));
                Render();
                AssertCard();
                await SaveAsync(name);
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
                await Task.Delay(220, TestContext.Current.CancellationToken);
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
                    Assert.InRange(railBounds.Left, 0, window.ClientSize.Width - railBounds.Width);
                    Assert.InRange(bounds.Left, 0, window.ClientSize.Width - bounds.Width);
                    Assert.InRange(bounds.Left, railBounds.Left - 1, railBounds.Right - bounds.Width + 1);
                    Assert.InRange(bounds.Top, 0, window.ClientSize.Height - bounds.Height);
                    string?[] visible = [.. card.GetVisualDescendants().OfType<TextBlock>()
                        .Where(block => block.IsEffectivelyVisible).Select(block => block.Text)];
                    Assert.Contains(selected.AddressRangeLabel, visible);
                    Assert.Contains(selected.SizeValue, visible);
                    Assert.Contains(selected.SourceLabel, visible);
                }
            }
            Assert.Equal(originalRanges, shell.Merge.MergeCoverageSegments.Select(segment =>
                (segment.RangeStart, segment.RangeEndExclusive, segment.SourceSlotId, segment.IsSelectedForWrite)));
            Assert.All(shell.Merge.AbMergeSlots, slot => Assert.True(slot.HasFile));
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
        async Task SaveAsync(string slotId)
        {
            string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (string.IsNullOrWhiteSpace(directory)) { return; }
            // Let the shared Fluent disclosure finish before preserving visual evidence.
            for (int tick = 0; tick < 4; tick++)
            {
                await Task.Delay(80, TestContext.Current.CancellationToken);
                Render();
            }
            _ = Directory.CreateDirectory(directory);
            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(directory, $"ab-memory-{width}-{height}-{dark}-{chinese}-{slotId}.png"));
        }
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
