using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Canonical DP Replace keeps planned intent distinct from observed byte changes.</summary>
    [Fact]
    public void MemoryCoveragePatternUsesTypedWorkbenchRole()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-memory-pattern");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51951";
        viewModel.Replace.SelectedReplaceMode = ExperienceIds.DpReplace;
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("reference.bin", new byte[0x80000]));
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceDp,
            workspace.Write("replacement.bin", new byte[0x80000]));

        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.FillRole == MemoryCoverageFillRole.Kept && segment.UsesKeptPattern);
        Assert.DoesNotContain(viewModel.Replace.ReplaceCoverageSegments, segment => segment.IsChanged);
        Assert.False(viewModel.Replace.HasObservedMemoryChanges);
        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment =>
            !segment.UsesKeptPattern && segment.ChangeLabel == "Will replace");
        Assert.DoesNotContain(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.SourceLabel.StartsWith("Changed ", StringComparison.Ordinal));

        var plannedRange = new MemoryCoverageSegmentViewModel(
            "0x100-0x10F",
            "DP BIN",
            "Planned replacement",
            MemoryCoverageFillRole.Dp,
            10,
            disposition: Application.MemoryLayout.MemoryWorkflowDisposition.WillReplace,
            sourceSlotId: "replace-dp",
            rangeStart: 0x100,
            rangeEndExclusive: 0x110);
        var changedRange = new MemoryCoverageSegmentViewModel(
            "0x110-0x11F",
            "DP BIN",
            "Observed replacement",
            MemoryCoverageFillRole.Dp,
            10,
            disposition: Application.MemoryLayout.MemoryWorkflowDisposition.WillReplace,
            observedChange: Application.MemoryLayout.MemoryObservedChange.Changed,
            sourceSlotId: "replace-dp",
            rangeStart: 0x110,
            rangeEndExclusive: 0x120);
        var logicalItem = new MemoryCoverageLogicalItemViewModel(
            "slot:replace-dp",
            [plannedRange, changedRange],
            ShellTextResources.For(ShellLanguage.English));

        Assert.True(Assert.Single(logicalItem.Ranges).IsChanged);
        Assert.Collection(
            logicalItem.Segments,
            segment => Assert.False(segment.IsChanged),
            segment => Assert.True(segment.IsChanged));
    }
}
