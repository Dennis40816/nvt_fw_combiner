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

/// <summary>The supporting list is compact without altering the physical memory rail.</summary>
public sealed class MemoryCoverageListTests
{
    /// <summary>Actual NT51928 DP/TP/LDC cards precede gaps until the list is expanded by keyboard.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task StandardListShowsThreeContentsThenAllAddresses(bool darkChinese, bool replace)
    {
        using var workspace = TempWorkspace.Create("memory-list");
        using var golden = StandardMergeGoldenManifest.Load();
        System.Text.Json.JsonElement inputs = golden.CaseByIc("51928").GetProperty("inputs");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        { Width = 1180, Height = 1040, RequestedThemeVariant = darkChinese ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            if (replace) { shell.ShowReplaceCommand.Execute(null); }
            else { shell.ShowMergeCommand.Execute(null); }
            shell.WorkflowSession.SelectedIc = "NT51928";
            if (replace) { shell.Replace.SelectedReplaceMode = ExperienceIds.DpReplace; }
            else { shell.Merge.SelectedMergeMode = ExperienceIds.StandardMerge; }
            if (darkChinese) { shell.SelectedLanguage = "Traditional Chinese"; }
            (string, string)[] selectedInputs = replace
                ? [(CompositionSlotIds.ReplaceBase, "dp-input"), (CompositionSlotIds.ReplaceDp, "dp-input")]
                : [
                (CompositionSlotIds.MergeDp, "dp-input"), (CompositionSlotIds.MergeTp, "tp-input"),
                (CompositionSlotIds.MergeLdc, "ldc-input"),
                ];
            foreach ((string slot, string artifact) in selectedInputs)
            {
                await shell.WorkflowSession.SetSlotFileAsync(slot, golden.ManifestPath(inputs.GetProperty(artifact)),
                    TestContext.Current.CancellationToken);
            }
            Render();
            IReadOnlyList<MemoryCoverageSegmentViewModel> railRows = replace
                ? shell.Replace.ReplaceCoverageSegments : shell.Merge.MergeCoverageSegments;
            MemoryCoverageSegmentViewModel[] original = [.. railRows];
            Assert.Equal(3, Cards().Length);
            Assert.Equal([MemoryContentRole.Tp, MemoryContentRole.Dp, MemoryContentRole.Ldc],
                Cards().Select(card => ((MemoryCoverageSegmentViewModel)card.DataContext!).ContentRole));
            Button toggle = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => button.Name == "MemoryCoverageListToggle" && button.IsEffectivelyVisible);
            Assert.True(toggle.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            Render();
            IReadOnlyList<MemoryCoverageSegmentViewModel> detailRows = replace
                ? shell.Replace.ReplaceCoverageSegments : shell.Merge.MergeCoverageRows;
            Assert.Equal(detailRows.Where(row => row.IsPrimaryContent).OrderBy(row => row.RangeStart),
                Cards().Select(card => (MemoryCoverageSegmentViewModel)card.DataContext!));
            Assert.True(Cards().Length > 3);
            Assert.True(toggle.IsFocused);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            Render();
            Assert.Equal(3, Cards().Length);
            Assert.True(toggle.IsFocused);
            Assert.Equal(original, railRows);
            MemoryCoverageBar rail = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(),
                control => control.IsEffectivelyVisible);
            Assert.Same(railRows, rail.ItemsSource);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            Render();
            shell.SelectedLanguage = darkChinese ? "English" : "Traditional Chinese";
            Render();
            MemoryCoverageListViewModel list = replace ? shell.Replace.CoverageDetails : shell.Merge.CoverageDetails;
            Assert.True(list.IsExpanded);
            Assert.Equal(darkChinese ? "Show fewer regions" : "收合區域列表", toggle.Content);
            Assert.True(Cards().Length > 3);
            shell.SelectedLanguage = darkChinese ? "Traditional Chinese" : "English";
            list.ToggleCommand.Execute(null);
            Render();
            string directory = Path.Combine(Environment.GetEnvironmentVariable("NFC_TEST_AREA_ROOT")!,
                "evidence", "v114-memory-list53");
            _ = Directory.CreateDirectory(directory);
            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(directory, $"nt51928-{replace}-{darkChinese}.png"));
            // A genuinely changed selection publishes a fresh list with its default collapsed state.
            list.ToggleCommand.Execute(null);
            Assert.True(list.IsExpanded);
            await shell.WorkflowSession.SetSlotFileAsync(
                replace ? CompositionSlotIds.ReplaceDp : CompositionSlotIds.MergeTp,
                golden.ManifestPath(inputs.GetProperty(replace ? "dp-input" : "tp-input")),
                TestContext.Current.CancellationToken);
            Assert.False(list.IsExpanded);
        }
        finally { await CloseAndFlushAsync(window); }

        Border[] Cards()
        {
            return [.. window.GetVisualDescendants().OfType<Border>()
                .Where(border => border.IsEffectivelyVisible && border.Classes.Contains("memoryInfoRow"))];
        }
    }

    /// <summary>Typed roles and stable address ordering control visibility, not labels or filenames.</summary>
    [Fact]
    public void ListPreservesReferencesAndDisconnectedRangesInBothOrders()
    {
        MemoryCoverageSegmentViewModel gap = Row(0, MemoryContentRole.Unmapped);
        MemoryCoverageSegmentViewModel first = Row(10, MemoryContentRole.Tp);
        MemoryCoverageSegmentViewModel second = Row(20, MemoryContentRole.Dp);
        MemoryCoverageSegmentViewModel disconnected = Row(30, MemoryContentRole.Tp);
        MemoryCoverageSegmentViewModel unknown = Row(null, MemoryContentRole.General);
        MemoryCoverageSegmentViewModel technical = Row(5, MemoryContentRole.General, primary: false);
        MemoryCoverageSegmentViewModel[] rows = [unknown, disconnected, gap, second, technical, first];
        MemoryCoverageInteractionState interaction = first.Interaction;
        var list = new MemoryCoverageListViewModel();
        list.Update(rows, ShellTextResources.For(ShellLanguage.English));
        Assert.Equal([first, second, disconnected], list.VisibleRows);
        Assert.Equal("Show all 5 regions", list.ToggleLabel);
        list.ToggleCommand.Execute(null);
        Assert.Equal([gap, first, second, disconnected, unknown], list.VisibleRows);
        Assert.Same(interaction, first.Interaction);
        Assert.NotSame(first.Interaction, disconnected.Interaction);
        list.Update(rows, ShellTextResources.For(ShellLanguage.ChineseTraditional), resetExpansion: false);
        Assert.True(list.IsExpanded);
        Assert.Equal("收合區域列表", list.ToggleLabel);
        list.Update(rows, ShellTextResources.For(ShellLanguage.English));
        Assert.False(list.IsExpanded);
    }

    /// <summary>Small and empty lists have no pointless toggle; Unmapped still trails mapped contents.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AtMostThreeRowsNeedNoDisclosure(int count)
    {
        MemoryCoverageSegmentViewModel[] rows = [.. Enumerable.Range(0, count)
            .Select(index => Row(index, index == 0 ? MemoryContentRole.Unmapped : MemoryContentRole.Dp))];
        var list = new MemoryCoverageListViewModel();
        list.Update(rows, ShellTextResources.For(ShellLanguage.English));
        Assert.False(list.HasMoreRows);
        Assert.False(list.IsExpanded);
        Assert.Equal(rows.OrderBy(row => row.ContentRole == MemoryContentRole.Unmapped), list.VisibleRows);
        list.Update([], ShellTextResources.For(ShellLanguage.English));
        Assert.Empty(list.VisibleRows);
    }

    private static MemoryCoverageSegmentViewModel Row(long? start, MemoryContentRole role, bool primary = true)
    {
        return new("range", "same source", "detail", MemoryCoverageFillRole.Neutral, 1,
            rangeStart: start, rangeEndExclusive: start + 1, contentRole: role, isPrimaryContent: primary);
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
