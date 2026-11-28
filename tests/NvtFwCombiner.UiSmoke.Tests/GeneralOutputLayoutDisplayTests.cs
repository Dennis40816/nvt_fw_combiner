// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>General output displays distinguish unwritten space and identify the selected source.</summary>
public sealed class GeneralOutputLayoutDisplayTests
{
    /// <summary>Large blank ranges stay neutral and unlabelled while a four-byte mapping keeps its navigation pin.</summary>
    [AvaloniaTheory]
    [InlineData(960, false, false)]
    [InlineData(1280, true, true)]
    public async Task BlankRangesStayNeutralAndTinyWrittenRangeRemainsFindable(int width, bool dark, bool chinese)
    {
        using TempWorkspace workspace = TempWorkspace.Create("general-layout-blank-colour");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(
            workspace, width: width, dark: dark, chinese: chinese);
        await host.LoadGeneralAsync(workspace);
        Canvas bar = host.Panel.FindControl<Canvas>("LayoutBar")!;
        MemoryCoverageSegmentViewModel[] blank = [.. host.Model.Segments.Where(
            static segment => segment.Disposition == MemoryWorkflowDisposition.Blank)];
        Assert.Equal(2, blank.Length);
        Button group = Assert.Single(host.Panel.GetVisualDescendants().OfType<Button>(),
            static button => Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "OutputLayout.Group.NotWritten");
        group.Command!.Execute(null);
        host.Render();
        Border groupSwatch = Assert.Single(group.GetVisualDescendants().OfType<Border>(),
            static border => border.Width == 16 && border.Height == 10);
        AssertNeutral(groupSwatch);
        foreach (MemoryCoverageSegmentViewModel segment in blank)
        {
            Border block = Assert.IsType<Border>(Assert.Single(bar.Children,
                control => ReferenceEquals(control.DataContext, segment)));
            AssertNeutral(Assert.Single(block.GetVisualDescendants().OfType<Border>(),
                static border => border.Classes.Contains("memoryCoverageFill")));
            Assert.Empty(block.GetVisualDescendants().OfType<TextBlock>());
            Assert.InRange(Math.Abs(block.Bounds.Width - (bar.Bounds.Width *
                (segment.RangeEndExclusive!.Value - segment.RangeStart!.Value) / 0x40000)), 0, 1);
            AssertNeutral(Assert.Single(host.RowFor(segment).GetVisualDescendants().OfType<Border>(),
                static border => border.Classes.Contains("memoryCoverageFill")));
        }

        MemoryCoverageSegmentViewModel written = Assert.Single(host.Model.Segments,
            static segment => segment.IsSelectedForWrite);
        Assert.Equal(0x7000, written.RangeStart);
        Assert.Equal(0x7004, written.RangeEndExclusive);
        Control writtenBlock = Assert.Single(bar.Children, control => ReferenceEquals(control.DataContext, written));
        Assert.True(writtenBlock.Bounds.Width > 0);
        Assert.Contains(writtenBlock.GetVisualDescendants().OfType<Border>(),
            static border => border.Classes.Contains("source"));
        Canvas pins = host.Panel.FindControl<Canvas>("LayoutPins")!;
        Control pin = Assert.Single(pins.Children, control => control.DataContext is OutputLayoutPanelRow row &&
            row.Members.Contains(written));
        Assert.True(pin.IsEffectivelyVisible);
        Assert.True(pin.Focusable);
        Assert.True(pin.Bounds.Width >= 16);
        host.Open(written);
    }

    /// <summary>The row uses the selected file name in either language instead of the internal source-space ID.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WrittenRowTitleUsesTheKnownSourceFile(bool chinese)
    {
        using TempWorkspace workspace = TempWorkspace.Create("general-layout-source-title");
        await using MergeOutputLayoutTestHost host = await MergeOutputLayoutTestHost.CreateAsync(workspace, chinese: chinese);
        await host.LoadGeneralAsync(workspace);
        MemoryCoverageSegmentViewModel written = Assert.Single(host.Model.Rows,
            static row => row.Group == OutputLayoutGroupKind.Write).Members[0];
        Assert.Equal("source.bin", written.DisplayTitle);
        Button row = host.RowFor(written);
        _ = Assert.Single(row.GetVisualDescendants().OfType<TextBlock>(),
            static text => text.Text == "source.bin");
        Assert.DoesNotContain(row.GetVisualDescendants().OfType<TextBlock>(),
            static text => text.Text == "general-merge-map-1-input");
        Assert.Contains("source.bin", Avalonia.Automation.AutomationProperties.GetName(row), StringComparison.Ordinal);
    }

    /// <summary>General Replace receives the same source-file title through the shared memory projection.</summary>
    [Fact]
    public void GeneralReplaceSharesTheKnownSourceFileTitle()
    {
        using TempWorkspace workspace = TempWorkspace.Create("general-replace-source-title");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        MainWindowViewModelTestExtensions.OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        viewModel.SetSlotFile("replace-base",
            workspace.Write("base.bin", FirmwareByteTestData.CreatePattern(0x40000, 0x26)));
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x3E020";
        mapping.Length = "0x2";
        viewModel.SetSlotFile(mapping.MappingId, workspace.Write("replacement.bin", [0xA5, 0x5A]));

        MemoryCoverageSegmentViewModel written = Assert.Single(viewModel.Replace.ReplaceCoverageSegments,
            static segment => segment.IsSelectedForWrite);
        Assert.Equal("replacement.bin", written.DisplayTitle);
        Assert.NotEqual("general-replace-map-1-input", written.DisplayTitle);
    }

    /// <summary>Typed disposition and content role determine neutral rendering regardless of misleading labels or fill roles.</summary>
    [AvaloniaTheory]
    [InlineData(MemoryWorkflowDisposition.Blank, MemoryContentRole.General, true)]
    [InlineData(MemoryWorkflowDisposition.Resolved, MemoryContentRole.Unmapped, true)]
    [InlineData(MemoryWorkflowDisposition.Resolved, MemoryContentRole.CustomerInformation, true)]
    [InlineData(MemoryWorkflowDisposition.Resolved, MemoryContentRole.Reserved, true)]
    [InlineData(MemoryWorkflowDisposition.WillWrite, MemoryContentRole.General, false)]
    public void NotWrittenRenderingUsesTypedFacts(MemoryWorkflowDisposition disposition, MemoryContentRole role, bool notWritten)
    {
        string misleadingTitle = notWritten ? "Data" : "Not written";
        MemoryCoverageSegmentViewModel segment = new("0x00000-0x00063", misleadingTitle, "Will write data.",
            MemoryCoverageFillRole.Source, 100, disposition, rangeStart: 0, rangeEndExclusive: 100,
            addressSpaceId: "flash", contentRole: role, displayTitle: misleadingTitle);
        using OutputLayoutPanelTestHost host = new(new([segment]));
        if (!notWritten)
        {
            Assert.Contains(host.Bar.GetVisualDescendants().OfType<Border>(),
                static border => border.Classes.Contains("source"));
            _ = Assert.Single(host.Bar.GetVisualDescendants().OfType<TextBlock>(),
                static text => text.Text == "Not written" && text.IsEffectivelyVisible);
            Assert.True(host.Group(OutputLayoutGroupKind.Write).IsEffectivelyVisible);
            return;
        }

        AssertNeutral(Assert.Single(host.Bar.GetVisualDescendants().OfType<Border>(),
            static border => border.Classes.Contains("memoryCoverageFill")));
        Assert.Empty(host.Bar.GetVisualDescendants().OfType<TextBlock>());
        host.Click(host.Group(OutputLayoutGroupKind.NotWritten));
        AssertNeutral(Assert.Single(host.Row("1").GetVisualDescendants().OfType<Border>(),
            static border => border.Classes.Contains("memoryCoverageFill")));
    }

    /// <summary>Compares the actual themed brush and excludes the data-fill class.</summary>
    private static void AssertNeutral(Border fill)
    {
        Assert.DoesNotContain("source", fill.Classes);
        Assert.True(fill.TryFindResource("NfcMemoryNeutralFillBrush", fill.ActualThemeVariant, out object? expected));
        Assert.Equal(Assert.IsType<ISolidColorBrush>(expected, exactMatch: false).Color,
            Assert.IsType<ISolidColorBrush>(fill.Background, exactMatch: false).Color);
    }
}
