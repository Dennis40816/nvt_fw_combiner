using System.Collections.Specialized;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>
/// Board decision 112: Merge/Replace memory-coverage publication replaces
/// <see cref="MergePresentationViewModel.MergeCoverageSegments"/>,
/// <see cref="ReplacePresentationViewModel.ReplaceCoverageSegments"/> and
/// <see cref="ReplacePresentationViewModel.CtrlRamOverview"/> with one collection Reset instead of a
/// Clear followed by one Add per segment.
/// </summary>
public sealed class MemoryCoveragePublicationTests
{
    /// <summary>Setting one CtrlRAM slot, and separately switching language, each publish
    /// <c>ReplaceCoverageSegments</c> and <c>CtrlRamOverview</c> with a small, bounded number of Resets
    /// (never a per-item Add) even though the pending/inspected lifecycle republishes more than once.</summary>
    [Fact]
    public async Task CtrlRamReplacePublicationReplacesCoverageWithOneReset()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("ctrlram-replace-coverage-publication");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51923";
        viewModel.WorkflowSession.SelectedNumber = "single";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-base",
            golden.ExpectedOutputPath(golden.CaseByIc("51923")),
            TestContext.Current.CancellationToken);

        FirmwareSlotViewModel normalSlot = Assert.Single(
            viewModel.Replace.ReplaceSlots,
            slot => slot.ReplaceInputRole == ReplaceInputRole.CtrlRam &&
                slot.CtrlRamDescriptionFacts?.TitleStem == "Normal CtrlRAM");
        long normalLength = Assert.Single(normalSlot.CtrlRamDescriptionFacts!.Sections).MaximumLength;

        List<NotifyCollectionChangedAction> coverageActions = [];
        List<NotifyCollectionChangedAction> overviewActions = [];
        viewModel.Replace.ReplaceCoverageSegments.CollectionChanged += (_, e) => coverageActions.Add(e.Action);
        viewModel.Replace.CtrlRamOverview.CollectionChanged += (_, e) => overviewActions.Add(e.Action);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            normalSlot.SlotId,
            workspace.Write($"{normalSlot.SlotId}.bin", new byte[checked((int)normalLength)]),
            TestContext.Current.CancellationToken);

        // Setting one slot republishes through the pending/inspected lifecycle (measured: 2 Resets for
        // ReplaceCoverageSegments); the bound stays far below the segment count so per-item Add fan-out
        // (one notification per segment) would still fail it.
        AssertOnlyResetsWithinBounds(coverageActions, maxNotifications: 2);
        AssertOnlyResetsWithinBounds(overviewActions, maxNotifications: 2);
        Assert.True(
            viewModel.Replace.ReplaceCoverageSegments.Count > 2,
            "The fixture must keep enough segments that per-item Add fan-out would be detected.");

        coverageActions.Clear();
        overviewActions.Clear();

        viewModel.SelectedLanguage = "Traditional Chinese";

        AssertOnlyResetsWithinBounds(coverageActions, maxNotifications: 2);
        AssertOnlyResetsWithinBounds(overviewActions, maxNotifications: 2);
    }

    /// <summary>Standard Merge with DP, TP and LDC inputs publishes <c>MergeCoverageSegments</c> with a
    /// small, bounded number of Resets (never a per-item Add), even though the pending/inspected
    /// lifecycle republishes more than once.</summary>
    [Fact]
    public async Task MergePublicationReplacesCoverageSegmentsWithOneReset()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement inputs = golden.CaseByIc("51928").GetProperty("inputs");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51928";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            golden.ManifestPath(inputs.GetProperty("dp-input")),
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeTp,
            golden.ManifestPath(inputs.GetProperty("tp-input")),
            TestContext.Current.CancellationToken);

        List<NotifyCollectionChangedAction> coverageActions = [];
        viewModel.Merge.MergeCoverageSegments.CollectionChanged += (_, e) => coverageActions.Add(e.Action);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeLdc,
            golden.ManifestPath(inputs.GetProperty("ldc-input")),
            TestContext.Current.CancellationToken);

        // Setting one slot republishes through the pending/inspected lifecycle (measured: 3 Resets for
        // MergeCoverageSegments); the bound stays far below the segment count so per-item Add fan-out
        // (one notification per segment) would still fail it.
        AssertOnlyResetsWithinBounds(coverageActions, maxNotifications: 3);
        Assert.True(
            viewModel.Merge.MergeCoverageSegments.Count > 2,
            "The fixture must keep enough segments that per-item Add fan-out would be detected.");

        coverageActions.Clear();

        viewModel.SelectedLanguage = "Traditional Chinese";

        AssertOnlyResetsWithinBounds(coverageActions, maxNotifications: 3);
    }

    /// <summary>One <see cref="ResettableObservableCollection{T}.ReplaceAll"/> rebuilds the rail's
    /// <c>MemoryLegend</c> children exactly once, instead of once per replaced segment.</summary>
    [AvaloniaFact]
    public void ReplaceAllRebuildsMemoryLegendExactlyOnce()
    {
        var collection = new ResettableObservableCollection<MemoryCoverageSegmentViewModel>();
        var bar = new MemoryCoverageBar
        {
            ItemsSource = collection,
            Labels = ShellTextResources.For(ShellLanguage.English),
            ShowLegend = true,
        };
        var window = new Window
        {
            Width = 420,
            Height = 640,
            Content = new Border { Padding = new Thickness(16), Child = bar },
        };
        var sharedTemplatesUri = new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowSharedTemplates.axaml");
        window.Resources.MergedDictionaries.Add(new ResourceInclude(sharedTemplatesUri) { Source = sharedTemplatesUri });
        window.Show();
        try
        {
            Render();

            StackPanel legend = Assert.IsType<StackPanel>(
                bar.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "MemoryLegend"));
            int addedRowCount = 0;
            legend.Children.CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    addedRowCount += e.NewItems!.Count;
                }
            };

            collection.ReplaceAll(MemoryCoverageBarProjectionTests.Example());
            Render();

            Assert.True(legend.Children.Count > 2, "The fixture must keep enough rows that per-item Add fan-out would be detected.");
            Assert.Equal(legend.Children.Count, addedRowCount);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertOnlyResetsWithinBounds(
        List<NotifyCollectionChangedAction> actions,
        int maxNotifications)
    {
        Assert.All(actions, action => Assert.Equal(NotifyCollectionChangedAction.Reset, action));
        Assert.True(
            actions.Count <= maxNotifications,
            $"Expected at most {maxNotifications} Reset notification(s), observed {actions.Count}.");
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
