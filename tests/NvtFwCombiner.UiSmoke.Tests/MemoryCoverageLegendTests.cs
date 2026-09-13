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
            shell.WorkflowSession.SelectedIc = "NT51928";
            if (replace) { shell.Replace.SelectedReplaceMode = ExperienceIds.DpReplace; }
            else { shell.Merge.SelectedMergeMode = ExperienceIds.StandardMerge; }
            if (darkChinese) { shell.SelectedLanguage = "Traditional Chinese"; }
            (string, string)[] selectedInputs = replace
                ? [(CompositionSlotIds.ReplaceBase, "dp-input"), (CompositionSlotIds.ReplaceDp, "dp-input")]
                : [(CompositionSlotIds.MergeDp, "dp-input"), (CompositionSlotIds.MergeTp, "tp-input"), (CompositionSlotIds.MergeLdc, "ldc-input")];
            foreach ((string slot, string artifact) in selectedInputs)
            {
                await shell.WorkflowSession.SetSlotFileAsync(slot, golden.ManifestPath(inputs.GetProperty(artifact)),
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
                    .OrderBy(row => row.ContentRole == MemoryContentRole.Unmapped).ThenBy(row => row.RangeStart),
                    LegendTargets().Select(target => (MemoryCoverageSegmentViewModel)target.DataContext!));
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
