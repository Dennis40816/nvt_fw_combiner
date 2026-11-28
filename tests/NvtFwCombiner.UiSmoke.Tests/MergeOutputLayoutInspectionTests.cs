// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Inspection completion publishes current input diagnostics to the production Merge panel.</summary>
public sealed class MergeOutputLayoutInspectionTests
{
    /// <summary>Either input can fail first and publish its count and complete reason once while retaining the planned layout.</summary>
    [AvaloniaTheory]
    [InlineData(CompositionSlotIds.MergeTp, false)]
    [InlineData(CompositionSlotIds.MergeTp, true)]
    [InlineData(CompositionSlotIds.MergeDp, false)]
    [InlineData(CompositionSlotIds.MergeDp, true)]
    public async Task SingleInvalidInputPublishesItsErrorAndReasonOnce(string slotId, bool chinese)
    {
        using TempWorkspace workspace = TempWorkspace.Create("merge-layout-single-invalid-input");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace, chinese: chinese);
        host.Shell.WorkflowSession.SelectedIc = "NT51926";
        host.Shell.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        host.Render();
        Assert.All(host.Shell.Merge.MergeSlots, static slot => Assert.False(slot.HasFile));
        Assert.Equal(0, host.Model.ErrorCount);
        Task inspection = host.Shell.WorkflowSession.SetSlotFileAsync(slotId,
            workspace.Write("one-byte.bin", [0]), TestContext.Current.CancellationToken);
        FirmwareSlotViewModel selected = Assert.Single(host.Shell.Merge.MergeSlots, static slot => slot.HasFile);
        Assert.Equal(slotId, selected.SlotId);
        Assert.True(selected.IsInputInspectionPending);
        OutputLayoutPublication pending = host.Shell.Merge.OutputLayout.Current;
        List<OutputLayoutPublication> completions = [];
        // Observe after synchronous selection/pending publication, before the asynchronous inspection completes.
        host.Shell.Merge.OutputLayout.PropertyChanged += (_, args) =>
        {
            Assert.Equal(nameof(OutputLayoutPresentationViewModel.Current), args.PropertyName);
            completions.Add(host.Shell.Merge.OutputLayout.Current);
        };

        await inspection;
        await host.SettleAsync();

        Assert.True(selected.IsInputInspectionBlocking);
        Assert.True(selected.IsSemanticStateError);
        Assert.False(selected.IsInputInspectionPending);
        OutputLayoutPublication completed = Assert.Single(completions);
        Assert.NotSame(pending, completed);
        Assert.Same(completed.Panel, host.Model);
        Assert.Equal(1, host.Model.ErrorCount);
        Assert.Null(host.Model.Notice);
        Assert.NotNull(completed.Map);
        Assert.NotEmpty(completed.Rows);
        Assert.NotEmpty(host.Model.Rows);
        Assert.Equal(pending.CoverageSegments.Select(static segment => (segment.RangeStart, segment.RangeEndExclusive)),
            completed.CoverageSegments.Select(static segment => (segment.RangeStart, segment.RangeEndExclusive)));
        Assert.False(host.Panel.FindControl<StackPanel>("LayoutNotice")!.IsVisible);
        Assert.True(host.Panel.FindControl<StackPanel>("LayoutMap")!.IsVisible);
        Assert.NotEmpty(host.Panel.FindControl<Canvas>("LayoutBar")!.Children);
        string reason = Assert.Single(host.Model.InputErrors);
        Assert.Equal(selected.SemanticStateAutomationText, reason);
        IssueCardViewModel issue = Assert.IsType<IssueCardViewModel>(selected.IssueCard);
        Assert.NotEmpty(issue.Summary);
        Assert.Contains(issue.Summary, reason, StringComparison.Ordinal);
        Assert.NotEmpty(reason);
        Assert.True(host.Panel.FindControl<StackPanel>("LayoutInputErrors")!.IsEffectivelyVisible);
        _ = Assert.Single(host.Panel.GetVisualDescendants().OfType<TextBlock>(),
            block => block.IsEffectivelyVisible && block.Text == reason);
        TextBlock issues = host.Panel.FindControl<TextBlock>("LayoutIssues")!;
        Assert.True(issues.IsEffectivelyVisible);
        Assert.Equal(chinese ? "1 則錯誤" : "1 error", issues.Text);
        Assert.False(host.Shell.Merge.CanBuildMerge);
        Assert.All(host.Shell.Merge.MergeSlots.Where(slot => slot.SlotId != slotId), static slot => Assert.False(slot.HasFile));

        host.Shell.SelectedLanguage = chinese ? "English" : "Traditional Chinese";
        host.Render();
        string translated = Assert.Single(host.Model.InputErrors);
        Assert.NotEqual(reason, translated);
        Assert.Equal(selected.SemanticStateAutomationText, translated);
        _ = Assert.Single(host.Panel.GetVisualDescendants().OfType<TextBlock>(),
            block => block.IsEffectivelyVisible && block.Text == translated);
        Assert.DoesNotContain(host.Panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == reason);
        Assert.Equal(1, host.Model.ErrorCount);

        await host.Shell.WorkflowSession.ClearSlotFileAsync(slotId, TestContext.Current.CancellationToken);
        await host.SettleAsync();
        Assert.Equal(0, host.Model.ErrorCount);
        Assert.Empty(host.Model.InputErrors);
        Assert.Empty(host.Panel.FindControl<StackPanel>("LayoutInputErrors")!.Children);
        Assert.False(host.Panel.FindControl<StackPanel>("LayoutInputErrors")!.IsVisible);
    }
}
