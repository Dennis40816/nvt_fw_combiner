// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Detail content and missing-data seams preserve the existing coverage presentation contract.</summary>
public sealed class OutputLayoutPanelDetailsTests
{
    /// <summary>The first detail layer shows supplied initialization and unassigned values verbatim.</summary>
    [AvaloniaTheory]
    [InlineData(false, "Source", "Not assigned")]
    [InlineData(true, "Initialization", "0xFF")]
    public void UnwrittenRowsRetainSuppliedSourceOrInitialization(bool initialized, string label, string value)
    {
        var text = ShellTextResources.For(ShellLanguage.English);
        var segment = new MemoryCoverageSegmentViewModel("0x00000–0x00063", value,
            initialized ? text.MemoryNoPlannedWritesDetail : text.MemorySourceNotAssignedDetail,
            MemoryCoverageFillRole.Neutral, 100, disposition: MemoryWorkflowDisposition.Blank,
            rangeStart: 0, rangeEndExclusive: 100, addressSpaceId: "flash", sourceFieldLabel: label, displayTitle: "Unmapped");
        using var host = new OutputLayoutPanelTestHost(new([segment]));
        host.Click(host.Group(OutputLayoutGroupKind.NotWritten));
        host.Click(host.Row("1"));
        string?[] visible = [.. OutputLayoutPanelTestHost.Body(host.Row("1")).GetVisualDescendants().OfType<TextBlock>()
            .Where(static block => block.IsEffectivelyVisible).Select(static block => block.Text)];
        Assert.Contains(label, visible);
        Assert.Contains(value, visible);
        Assert.Contains(segment.PreservationSummary, visible);
        Assert.Contains(segment.SizeValue, visible);
    }

    /// <summary>Technical details keep processing facts and every per-IC preservation display field.</summary>
    [AvaloniaFact]
    public void TechnicalDisclosureRetainsProcessingAndPreservationContent()
    {
        var segment = new MemoryCoverageSegmentViewModel("range", "DP BIN", "Summary sentence.", MemoryCoverageFillRole.Dp, 100,
            disposition: MemoryWorkflowDisposition.WillWrite, rangeStart: 0, rangeEndExclusive: 100, addressSpaceId: "flash",
            processingFacts: [new("Operation", "copy-dp (Sequence 7)"), new("Region ID", "dp")],
            preservationDetails: [new MemoryLayoutPreservationDetail("kept", 0, MemoryEndpointIdentity.NotApplicable,
                "reference-base", new ByteRange(0, 4), new ByteRange(4, 4))]);
        using var host = new OutputLayoutPanelTestHost(new([segment]));
        host.Click(host.Row("1"));
        host.Click(host.Technical("1"));
        string[] visible = [.. OutputLayoutPanelTestHost.Body(host.Technical("1")).GetVisualDescendants().OfType<TextBlock>()
            .Where(static block => block.IsEffectivelyVisible).Select(static block => block.Text ?? string.Empty)];
        DiffDlmPreservationDetailViewModel preserved = Assert.Single(segment.PreservationDetails);
        Assert.Equal(["Address space", "flash", "Operation", "copy-dp (Sequence 7)", "Region ID", "dp",
            segment.PreservationSummary, preserved.IcLabel, preserved.BlockLabel, preserved.ArtifactRangeLabel,
            preserved.FlashRangeLabel, preserved.DispositionLabel], visible);
    }

    /// <summary>A legacy conflict without numerical coordinates or disposition uses only explicit supplemental facts.</summary>
    [AvaloniaFact]
    public void ExplicitConflictPlacementAndGroupEnableTheConflictDesignState()
    {
        var conflict = new MemoryCoverageSegmentViewModel("0x00190–0x00257", "Step 2 and step 3", "Existing conflict description.",
            MemoryCoverageFillRole.Conflict, 200, diagnosticSeverity: MemoryDiagnosticSeverity.Error, logicalCoverageGroupId: "conflict:one");
        using var host = new OutputLayoutPanelTestHost(new([
            OutputLayoutPanelTestHost.Slice(0, 400, "DP"), OutputLayoutPanelTestHost.Slice(600, 1000, "TP"), conflict], errorCount: 1,
            rowOptions: [new(OutputLayoutRowIdentity.For(conflict), OutputLayoutGroupKind.Write, new("flash", 400, 600))]));
        Assert.Equal("Conflict", Assert.Single(host.Row("2").GetVisualDescendants().OfType<TextBlock>(),
            static block => block.Classes.Contains("outputLayoutBadge")).Text);
        Assert.InRange(Math.Abs(host.Bar.Children[2].Bounds.Left - (host.Bar.Bounds.Width * 0.4)), 0, 0.5);
        Assert.InRange(Math.Abs(host.Bar.Children[2].Bounds.Width - (host.Bar.Bounds.Width * 0.2)), 0, 0.5);
    }

    /// <summary>Absent supplemental facts do not invent addresses or classify a resolved diagnostic as unwritten.</summary>
    [AvaloniaFact]
    public void MissingConflictFactsStayAbsentInsteadOfParsingItsText()
    {
        var conflict = new MemoryCoverageSegmentViewModel("0x00190–0x00257", "Will write", "0x190 means Conflict and blocked.",
            MemoryCoverageFillRole.Conflict, 200, diagnosticSeverity: MemoryDiagnosticSeverity.Error, logicalCoverageGroupId: "conflict:one");
        using var host = new OutputLayoutPanelTestHost(new([conflict], errorCount: 1));
        Assert.Empty(host.Panel.Model!.Rows);
        Assert.Empty(host.Bar.Children);
        Assert.Empty(host.Pins.Children);
        Assert.Equal("1 error", host.Panel.FindControl<TextBlock>("LayoutIssues")!.Text);
    }
}
