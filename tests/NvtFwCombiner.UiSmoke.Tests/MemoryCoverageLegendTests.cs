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

/// <summary>All primary ranges remain inspectable without persistent supporting cards.</summary>
public sealed class MemoryCoverageLegendTests
{
    /// <summary>The canonical AB example exposes five content runs while retaining exact raw operation facts.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nt51950AbLegendCoalescesBanksAndPostbuildImports(bool darkChinese)
    {
        using var workspace = TempWorkspace.Create("ab-content-legend");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        { Width = 1920, Height = 1032, RequestedThemeVariant = darkChinese ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            string folder = RepositoryPaths.FromRepositoryRoot("testdata/golden/canonical/NT51950/ab-merge/boe-d82t80/topology-unscoped/nt51950-ab-boe-d82t80/inputs");
            string dp = Path.Combine(folder, "NT51950TT_Initial Code_BOE_AS172QD0-B00 2560x1600_BOE only_PD fixed pixelonoff_D82_20260616.bin");
            string tp = Path.Combine(folder, "nt51950_fw_T80.bin");
            UiLaunchOptions options = UiLaunchOptions.Parse(["--workflow", "ab-merge", "--ic", "NT51950", "--ic-num", "single", "--dp", dp, "--tp-a", tp, "--tp-b", tp]);
            await MainWindow.ApplyAbMergeLaunchAsync(shell, options.AbMerge!, TestContext.Current.CancellationToken);
            if (darkChinese) { shell.SelectedLanguage = "Traditional Chinese"; }
            Render();
            MemoryCoverageBar bar = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(), control => control.IsEffectivelyVisible);
            MemoryCoverageSegmentViewModel[] raw = [.. (IEnumerable<MemoryCoverageSegmentViewModel>)bar.ItemsSource!];
            MemoryCoverageSegmentViewModel[] rows = [.. bar.GetVisualDescendants().OfType<Border>()
                .Where(border => border.Name == "MemoryLegendTarget").Select(border => (MemoryCoverageSegmentViewModel)border.DataContext!)];
            Assert.Equal([(0L, 0xA000L), (0xA000L, 0x37000L), (0x37000L, 0x4A000L), (0x4A000L, 0x77000L), (0x77000L, 0x80000L)],
                rows.Select(row => (row.RangeStart!.Value, row.RangeEndExclusive!.Value)));
            Assert.Equal([MemoryCoverageFillRole.Dp, MemoryCoverageFillRole.Tp, MemoryCoverageFillRole.Dp, MemoryCoverageFillRole.TpBackup, MemoryCoverageFillRole.Dp],
                rows.Select(row => row.FillRole));
            Assert.Equal(rows[1].ContentArtifactIdentity, rows[3].ContentArtifactIdentity);
            Assert.NotEqual(rows[0].ContentArtifactIdentity, rows[1].ContentArtifactIdentity);
            Assert.Equal(raw, rows.SelectMany(row => row.DisplayParts.Count == 0 ? [row] : row.DisplayParts));
            Assert.Equal(raw.SelectMany(row => row.ProcessingFacts).Distinct(), rows.SelectMany(row => row.ProcessingFacts).Distinct());
            Assert.Empty(shell.Reports.ReportHistoryEntries);
            SaveFrame(window, $"nt51950-ab-content-{darkChinese}.png");
        }
        finally { await CloseAndFlushAsync(window); }
    }

    /// <summary>Actual CtrlRAM keeps section context, partial focus and exact raw content ownership under the shared legend.</summary>
    [AvaloniaFact]
    public async Task Nt51950CtrlRamRetainsSectionOverviewAndDistinctReplacementBins()
    {
        using var workspace = TempWorkspace.Create("ctrlram-content-legend");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
        { Width = 1440, Height = 1032 };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            await XamlControlStyleContractTests.LoadNt51950GoldenCtrlRamInputsAsync(shell, TestContext.Current.CancellationToken);
            Render();
            IReadOnlyList<MemoryCoverageSegmentViewModel> raw = [.. shell.Replace.ReplaceCoverageSegments];
            IReadOnlyList<MemoryCoverageSegmentViewModel> runs = MemoryCoverageBarProjection.CoalesceContent(raw, shell.Text);
            Assert.Equal(raw, runs.SelectMany(run => run.DisplayParts.Count > 0 ? run.DisplayParts : [run]));
            Assert.Contains(raw, part => part.IsSelectedForWrite && part.ContentArtifactIdentity is not null);
            Assert.Contains(raw, part => part.UsesKeptPattern && part.ContentArtifactIdentity is not null);
            foreach (MemoryCoverageSegmentViewModel run in runs.Where(run => run.DisplayParts.Count > 0))
            {
                _ = Assert.Single(run.DisplayParts.Select(part => part.ContentArtifactIdentity).Distinct());
            }
            Assert.Contains(shell.Replace.CtrlRamFocusLanes.SelectMany(lane => lane.Ranges), row =>
                row.DisplayParts.Any(part => part.IsSelectedForWrite) && row.DisplayParts.Any(part => part.UsesKeptPattern));
            MemoryCoverageBar overview = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(), control => control.IsEffectivelyVisible);
            Border[] legend = [.. overview.GetVisualDescendants().OfType<Border>().Where(border => border.Name == "MemoryLegendTarget")];
            ItemsControl rail = Assert.Single(overview.GetVisualDescendants().OfType<ItemsControl>(), control => control.Name == "MemoryMainRail");
            Assert.True(legend[0].TranslatePoint(default, window)!.Value.Y >= rail.TranslatePoint(default, window)!.Value.Y + rail.Bounds.Height);
            Assert.All((IEnumerable<MemoryCoverageSegmentViewModel>)overview.ItemsSource!, section => Assert.Null(section.ContentArtifactIdentity));
            SaveFrame(window, "nt51950-ctrlram-content.png");
        }
        finally { await CloseAndFlushAsync(window); }
    }

    private static void SaveFrame(Window window, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrEmpty(directory)) { return; }
        _ = Directory.CreateDirectory(directory);
        using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, name));
    }

    /// <summary>Real NT51928 inputs keep exact DP/TP/LDC facts across workflows and relocalization.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task StandardLegendKeepsEveryRangeWithoutDefaultCards(bool darkChinese, bool replace)
    {
        using var workspace = TempWorkspace.Create("memory-legend");
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
            shell.WorkflowSession.SelectedIc = replace ? "NT51950" : "NT51928";
            if (replace) { shell.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace; }
            else { shell.Merge.SelectedMergeMode = ExperienceIds.StandardMerge; }
            if (darkChinese) { shell.SelectedLanguage = "Traditional Chinese"; }
            (string, string)[] selectedInputs = replace
                ? [(CompositionSlotIds.ReplaceBase, "reference"), ("replace-ctrlram-normal", "normal")]
                : [(CompositionSlotIds.MergeDp, "dp-input"), (CompositionSlotIds.MergeTp, "tp-input"), (CompositionSlotIds.MergeLdc, "ldc-input")];
            foreach ((string slot, string artifact) in selectedInputs)
            {
                string path = replace
                    ? workspace.Write($"{artifact}.bin", artifact == "reference"
                        ? ShellViewModelTestBase.ReadCtrlRamReference() : ShellViewModelTestBase.ReadCtrlRamNormalSource())
                    : golden.ManifestPath(inputs.GetProperty(artifact));
                await shell.WorkflowSession.SetSlotFileAsync(slot, path,
                    TestContext.Current.CancellationToken);
            }
            Render();
            MemoryCoverageBar rail = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(), control => control.IsEffectivelyVisible);
            MemoryCoverageSegmentViewModel[] original = [.. (IEnumerable<MemoryCoverageSegmentViewModel>)rail.ItemsSource!];
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), border => border.IsEffectivelyVisible && border.Classes.Contains("memoryInfoRow"));
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), control => control.Name == "MemorySliceCard");
            AssertLegendReferences();
            Assert.Contains(original, row => row.ContentRole == MemoryContentRole.Dp);
            Assert.Contains(original, row => row.ContentRole == MemoryContentRole.Tp);
            Border target = LegendTargets().First();
            MemoryCoverageSegmentViewModel expected = Assert.IsType<MemoryCoverageSegmentViewModel>(target.DataContext);
            Assert.True(target.Focus(NavigationMethod.Tab));
            Render();
            Border card = Assert.Single(window.GetVisualDescendants().OfType<Border>(), border => border.Name == "MemorySliceCard");
            Assert.Same(expected, card.DataContext);
            Assert.False(Assert.Single(card.GetVisualDescendants().OfType<Expander>()).IsExpanded);
            Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == expected.AddressRangeLabel);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Render();
            shell.SelectedLanguage = darkChinese ? "English" : "Traditional Chinese";
            Render();
            AssertLegendReferences();
            Assert.Equal(original.Select(row => (row.RangeStart, row.RangeEndExclusive, row.ContentRole)),
                ((IEnumerable<MemoryCoverageSegmentViewModel>)rail.ItemsSource!).Select(row => (row.RangeStart, row.RangeEndExclusive, row.ContentRole)));
            shell.SelectedLanguage = darkChinese ? "Traditional Chinese" : "English";
            Render();
            string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrEmpty(directory))
            {
                using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(directory, $"nt51928-legend-{replace}-{darkChinese}.png"));
            }
            // Clearing an input must publish current facts, not retain stale legend targets.
            await shell.WorkflowSession.ClearSlotFileAsync(selectedInputs[^1].Item1, TestContext.Current.CancellationToken);
            Render();
            AssertLegendReferences();

            Border[] LegendTargets()
            {
                return [.. rail.GetVisualDescendants().OfType<Border>().Where(border => border.Name == "MemoryLegendTarget")];
            }
            void AssertLegendReferences()
            {
                Assert.Equal(((IEnumerable<MemoryCoverageSegmentViewModel>)rail.ItemsSource!).Where(row => row.IsPrimaryContent)
                    .OrderBy(row => row.RangeStart),
                    LegendTargets().Select(target => (MemoryCoverageSegmentViewModel)target.DataContext!)
                        .SelectMany(row => row.DisplayParts.Count > 0 ? row.DisplayParts : [row]));
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
