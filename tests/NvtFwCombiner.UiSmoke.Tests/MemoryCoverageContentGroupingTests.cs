using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Loaded BIN identity controls only the main display, never the raw CtrlRAM detail model.</summary>
public sealed class MemoryCoverageContentGroupingTests
{
    private static readonly ShellTextResources Text = ShellTextResources.For(ShellLanguage.English);

    /// <summary>One file selected in different slots remains one contiguous content run.</summary>
    [Fact]
    public void SameBinAcrossSlotsAndBankTitlesCoalescesWithoutLosingParts()
    {
        MemoryCoverageSegmentViewModel first = Slice(0, 4, "file-1", "slot-a", "Bank A");
        MemoryCoverageSegmentViewModel second = Slice(4, 8, "file-1", "slot-b", "Bank B");
        MemoryCoverageSegmentViewModel merged = Assert.Single(MemoryCoverageBarProjection.CoalesceContent([first, second], Text));
        Assert.Equal((0L, 8L), (merged.RangeStart, merged.RangeEndExclusive));
        Assert.Equal("BIN", merged.DisplayTitle);
        Assert.Equal(8, merged.BarWidth);
        Assert.Equal([first, second], merged.DisplayParts);
        Assert.Null(merged.SourceSlotId);
        Assert.Equal("file-1", merged.ContentArtifactIdentity);
    }

    /// <summary>Matching labels are insufficient: ownership and physical continuity must both be known.</summary>
    [Theory]
    [InlineData("different-bin")]
    [InlineData("missing-bin")]
    [InlineData("gap")]
    [InlineData("overlap")]
    [InlineData("space")]
    [InlineData("unknown-space")]
    [InlineData("trace")]
    public void DistinctOrUnprovenRangesStaySeparate(string boundary)
    {
        MemoryCoverageSegmentViewModel first = Slice(0, 4, "file-1");
        MemoryCoverageSegmentViewModel second = Slice(boundary == "gap" ? 5 : boundary == "overlap" ? 3 : 4, 8,
            boundary == "different-bin" ? "file-2" : boundary == "missing-bin" ? null : "file-1",
            space: boundary == "space" ? "other" : boundary == "unknown-space" ? null : "output",
            primary: boundary != "trace");
        Assert.Equal([first, second], MemoryCoverageBarProjection.CoalesceContent([first, second], Text));
    }

    /// <summary>Same-bin runs retain mixed written/kept fills, diagnostics and every operation fact.</summary>
    [Fact]
    public void SameBinPartialReplaceKeepsStateAndTechnicalEvidence()
    {
        var written = new MemoryCoverageSegmentViewModel("first", "BIN", "written", MemoryCoverageFillRole.CtrlRamNf, 4,
            disposition: MemoryWorkflowDisposition.WillReplace, observedChange: MemoryObservedChange.Changed,
            diagnosticSeverity: MemoryDiagnosticSeverity.Warning, rangeStart: 0, rangeEndExclusive: 4,
            contentRole: MemoryContentRole.CtrlRam, ctrlRamRegionRole: CtrlRamRegionRole.Nf,
            processingFacts: [new("operation", "copy-nf")], addressSpaceId: "output", contentArtifactIdentity: "file-1");
        var kept = new MemoryCoverageSegmentViewModel("second", "BIN", "retained", MemoryCoverageFillRole.CtrlRamNormal, 4,
            disposition: MemoryWorkflowDisposition.Kept, usesBaseFirmwarePattern: true, rangeStart: 4, rangeEndExclusive: 8,
            preservationDetails: [new MemoryLayoutPreservationDetail("kept", 0, MemoryEndpointIdentity.NotApplicable,
                "reference-base", new ByteRange(0, 4), new ByteRange(4, 4))],
            contentRole: MemoryContentRole.CtrlRam, ctrlRamRegionRole: CtrlRamRegionRole.Normal,
            processingFacts: [new("operation", "preserve-normal")], addressSpaceId: "output", contentArtifactIdentity: "file-1");
        MemoryCoverageSegmentViewModel merged = Assert.Single(MemoryCoverageBarProjection.CoalesceContent([written, kept], Text));
        Assert.Equal("Partially replaced", merged.ChangeLabel);
        Assert.Equal(MemoryDiagnosticSeverity.Warning, merged.DiagnosticSeverity);
        Assert.True(merged.IsChanged);
        Assert.Equal(MemoryContentRole.General, merged.ContentRole);
        Assert.Equal([false, true], merged.DisplayParts.Select(part => part.UsesKeptPattern));
        Assert.Equal(["copy-nf", "preserve-normal"], merged.ProcessingFacts.Select(fact => fact.Value));
        Assert.Contains("retained", merged.AccessibleDetail);
        Assert.Same(Assert.Single(kept.PreservationDetails), Assert.Single(merged.PreservationDetails));
    }

    /// <summary>CtrlRAM's partial supporting row still keeps a replacement BIN and reference BIN distinct on the main rail.</summary>
    [Fact]
    public void CtrlRamLogicalGroupingCannotMergeDifferentBinsOnMainRail()
    {
        var written = new MemoryCoverageSegmentViewModel("first", "BIN", "written", MemoryCoverageFillRole.CtrlRamNormal, 4,
            disposition: MemoryWorkflowDisposition.WillReplace, rangeStart: 0, rangeEndExclusive: 4,
            logicalCoverageGroupId: "ctrlram", contentRole: MemoryContentRole.CtrlRam, ctrlRamRegionRole: CtrlRamRegionRole.Normal,
            addressSpaceId: "output", contentArtifactIdentity: "replacement");
        var kept = new MemoryCoverageSegmentViewModel("second", "BIN", "kept", MemoryCoverageFillRole.CtrlRamNormal, 4,
            disposition: MemoryWorkflowDisposition.Kept, usesBaseFirmwarePattern: true, rangeStart: 4, rangeEndExclusive: 8,
            logicalCoverageGroupId: "ctrlram", contentRole: MemoryContentRole.CtrlRam, ctrlRamRegionRole: CtrlRamRegionRole.Normal,
            addressSpaceId: "output", contentArtifactIdentity: "reference");
        MemoryCoverageLogicalItemViewModel logical = Assert.Single(ReplaceRegionGroupBuilder.CreateLogicalItems([written, kept], Text));
        Assert.Equal("Partially replaced", Assert.Single(logical.Ranges).ChangeLabel);
        Assert.Equal([written, kept], MemoryCoverageBarProjection.CoalesceContent([written, kept], Text));
        Assert.Equal([written, kept], logical.Segments);
    }

    private static MemoryCoverageSegmentViewModel Slice(long start, long end, string? identity,
        string slot = "slot", string title = "BIN", string? space = "output", bool primary = true)
    {
        return new("range", "BIN", "detail", MemoryCoverageFillRole.Source, end - start,
            sourceSlotId: slot, rangeStart: start, rangeEndExclusive: end, displayTitle: title,
            addressSpaceId: space, isPrimaryContent: primary, contentArtifactIdentity: identity);
    }
}
