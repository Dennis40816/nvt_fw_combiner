using Avalonia;
using Avalonia.Automation;
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
    /// <summary>The AB panel coalesces both banks while retaining every original operation and processing fact.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nt51950AbRowsCoalesceBanksAndPostbuildImports(bool darkChinese)
    {
        using var workspace = TempWorkspace.Create("ab-content-legend");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace, dark: darkChinese, chinese: darkChinese);
        await host.LoadAbAsync("NT51950", "nt51950-ab-boe-d82t80");
        MemoryCoverageSegmentViewModel[] raw = [.. host.Model.Segments];
        MemoryCoverageSegmentViewModel[] rows = [.. host.Model.Rows.SelectMany(static row => row.Members)];
        Assert.Equal([(0L, 0xA000L), (0xA000L, 0x37000L), (0x37000L, 0x4A000L), (0x4A000L, 0x77000L), (0x77000L, 0x80000L)],
            rows.Select(static row => (row.RangeStart!.Value, row.RangeEndExclusive!.Value)));
        Assert.Equal([MemoryCoverageFillRole.Dp, MemoryCoverageFillRole.Tp, MemoryCoverageFillRole.Dp, MemoryCoverageFillRole.TpBackup, MemoryCoverageFillRole.Dp],
            rows.Select(static row => row.FillRole));
        Assert.Equal(rows[1].ContentArtifactIdentity, rows[3].ContentArtifactIdentity);
        Assert.NotEqual(rows[0].ContentArtifactIdentity, rows[1].ContentArtifactIdentity);
        Assert.Equal(raw, rows.SelectMany(static row => row.DisplayParts.Count == 0 ? [row] : row.DisplayParts));
        Assert.Equal(raw.SelectMany(static row => row.ProcessingFacts).Distinct(), rows.SelectMany(static row => row.ProcessingFacts).Distinct());
        Assert.Empty(host.Shell.Reports.ReportHistoryEntries);
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
            OutputLayoutPanel panel = Assert.Single(window.GetVisualDescendants().OfType<OutputLayoutPanel>(),
                static control => control.IsEffectivelyVisible);
            OutputLayoutPanelModel model = Assert.IsType<OutputLayoutPanelModel>(shell.Replace.OutputLayout.Current.Panel);
            Assert.Same(model, panel.Model);
            Assert.All(model.Segments, static section => Assert.Null(section.ContentArtifactIdentity));
            Assert.Contains(model.Rows.SelectMany(static row => row.Members),
                static segment => segment.IsSelectedForWrite && segment.CtrlRamRegionRole == CtrlRamRegionRole.Nf);
            Assert.Contains(model.Rows.SelectMany(static row => row.Members),
                static segment => segment.UsesKeptPattern && segment.CtrlRamRegionRole == CtrlRamRegionRole.Nf);
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
        frame.SavePng(Path.Combine(directory, name));
    }

    /// <summary>Real NT51928 inputs keep exact DP/TP/LDC facts across workflows and relocalization.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task StandardLegendKeepsEveryRangeWithoutDefaultCards(bool darkChinese, bool replace)
    {
        if (!replace)
        {
            using var mergeWorkspace = TempWorkspace.Create("merge-numbered-rows");
            await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(mergeWorkspace, dark: darkChinese, chinese: darkChinese);
            await host.LoadStandardAsync("NT51928");
            OutputLayoutPanelModel originalModel = host.Model;
            Assert.NotEmpty(originalModel.Rows);
            Assert.All(originalModel.Rows.SelectMany(static row => row.Members), segment =>
            {
                Button row = host.RowFor(segment);
                Assert.NotEmpty(AutomationProperties.GetName(row) ?? string.Empty);
                Assert.False(OutputLayoutPanelTestHost.Body(row).IsVisible);
            });
            host.Shell.SelectedLanguage = darkChinese ? "English" : "Traditional Chinese";
            host.Render();
            Assert.Equal(originalModel.Rows.SelectMany(static row => row.Members).Select(static row => (row.RangeStart, row.RangeEndExclusive, row.ContentRole)),
                host.Model.Rows.SelectMany(static row => row.Members).Select(static row => (row.RangeStart, row.RangeEndExclusive, row.ContentRole)));
            Assert.Contains(host.Model.Segments, static row => row.ContentRole == MemoryContentRole.Dp);
            Assert.Contains(host.Model.Segments, static row => row.ContentRole == MemoryContentRole.Tp);
            Assert.Contains(host.Model.Segments, static row => row.ContentRole == MemoryContentRole.Ldc);
            return;
        }
        using var workspace = TempWorkspace.Create("memory-legend");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace,
            width: 1180, height: 1040, dark: darkChinese, chinese: darkChinese);
        host.Shell.WorkflowSession.SelectedIc = "NT51950";
        host.Shell.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;
        (string Slot, string Artifact)[] selectedInputs =
            [(CompositionSlotIds.ReplaceBase, "reference"), ("replace-ctrlram-normal", "normal")];
        foreach ((string slot, string artifact) in selectedInputs)
        {
            string path = workspace.Write($"{artifact}.bin", artifact == "reference"
                ? ShellViewModelTestBase.ReadCtrlRamReference() : ShellViewModelTestBase.ReadCtrlRamNormalSource());
            await host.Shell.WorkflowSession.SetSlotFileAsync(slot, path, TestContext.Current.CancellationToken);
        }
        await host.SettleAsync();
        OutputLayoutPanelModel original = host.Model;
        Assert.Same(host.Shell.Replace.OutputLayout.Current.Panel, host.Panel.Model);
        Assert.Contains(original.RowSegments, static row => row.ContentRole == MemoryContentRole.Dp);
        Assert.Contains(original.RowSegments, static row => row.ContentRole == MemoryContentRole.Tp);
        Assert.All(original.Rows.SelectMany(static row => row.Members), segment =>
        {
            Button disclosure = host.RowFor(segment);
            Assert.False(OutputLayoutPanelTestHost.Body(disclosure).IsVisible);
        });
        MemoryCoverageSegmentViewModel selected = original.Rows.SelectMany(static row => row.Members)
            .First(static segment => segment.IsSelectedForWrite);
        host.AssertDetails(selected);
        host.Shell.SelectedLanguage = darkChinese ? "English" : "Traditional Chinese";
        host.Render();
        Assert.Equal(original.RowSegments.Select(static row => (row.RangeStart, row.RangeEndExclusive, row.ContentRole)),
            host.Model.RowSegments.Select(static row => (row.RangeStart, row.RangeEndExclusive, row.ContentRole)));
        host.Capture($"nt51928-inline-replace-{darkChinese}");
        await host.Shell.WorkflowSession.ClearSlotFileAsync(selectedInputs[^1].Slot, TestContext.Current.CancellationToken);
        await host.SettleAsync();
        Assert.Same(host.Shell.Replace.OutputLayout.Current.Panel, host.Panel.Model);
        Assert.DoesNotContain(host.Model.RowSegments, static row => row.SourceSlotId == "replace-ctrlram-normal" &&
            row.IsSelectedForWrite);
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
