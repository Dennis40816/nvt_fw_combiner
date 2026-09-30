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

    /// <summary>Repeated primary titles are numbered in rail order; a title that occurs once stays plain.</summary>
    [Fact]
    public void RepeatedTitlesGetOrdinalsInPhysicalOrderAndSingletonsStayPlain()
    {
        MemoryCoverageSegmentViewModel dp1 = Slice(0, 4, "file-1", title: "DP AB", source: "DP AB");
        MemoryCoverageSegmentViewModel tp = Slice(4, 8, "file-2", title: "TP", source: "TP");
        MemoryCoverageSegmentViewModel dp2 = Slice(8, 12, "file-3", title: "DP AB", source: "DP AB");
        MemoryCoverageSegmentViewModel dp3 = Slice(12, 16, "file-4", title: "DP AB", source: "DP AB");
        MemoryCoverageSegmentViewModel[] rail = [dp1, tp, dp2, dp3];
        Assert.Equal(rail, MemoryCoverageBarProjection.CoalesceContent(rail, Text));
        Assert.Equal(["DP AB #1", "TP", "DP AB #2", "DP AB #3"], rail.Select(slice => slice.DisplayTitle));
        Assert.StartsWith("DP AB #1. ", dp1.AccessibleDetail, StringComparison.Ordinal);
        Assert.StartsWith("TP. ", tp.AccessibleDetail, StringComparison.Ordinal);
        Assert.Equal(["DP AB", "TP", "DP AB", "DP AB"], rail.Select(slice => slice.SourceLabel));
    }

    /// <summary>Numbering counts display rows: a coalesced run is one row and its raw parts keep their title.</summary>
    [Fact]
    public void OrdinalsCountCoalescedRunsAsOneRow()
    {
        MemoryCoverageSegmentViewModel a1 = Slice(0, 4, "file-1", title: "DP AB", source: "DP AB");
        MemoryCoverageSegmentViewModel a2 = Slice(4, 8, "file-1", title: "DP AB", source: "DP AB");
        MemoryCoverageSegmentViewModel tp = Slice(8, 12, "file-2", title: "TP", source: "TP");
        MemoryCoverageSegmentViewModel b = Slice(12, 16, "file-3", title: "DP AB", source: "DP AB");
        IReadOnlyList<MemoryCoverageSegmentViewModel> rows = MemoryCoverageBarProjection.CoalesceContent([a1, a2, tp, b], Text);
        Assert.Equal(["DP AB #1", "TP", "DP AB #2"], rows.Select(row => row.DisplayTitle));
        Assert.Equal([a1, a2], rows[0].DisplayParts);
        Assert.All(rows[0].DisplayParts, part => Assert.Equal("DP AB", part.DisplayTitle));
    }

    /// <summary>Reserved, kept, CtrlRAM and differently captioned rows are never numbered; a re-run resets a stale ordinal.</summary>
    [Fact]
    public void OrdinalsSkipNonPrimaryKeptCtrlRamAndCaptionedRowsAndResetOnRerun()
    {
        MemoryCoverageSegmentViewModel dp1 = Slice(0, 4, "file-1", title: "DP AB", source: "DP AB");
        MemoryCoverageSegmentViewModel dp2 = Slice(8, 12, "file-2", title: "DP AB", source: "DP AB");
        var reserved = new MemoryCoverageSegmentViewModel("r", "Reserved", "detail", MemoryCoverageFillRole.Neutral, 4,
            rangeStart: 16, rangeEndExclusive: 20, addressSpaceId: "output", isPrimaryContent: false, contentArtifactIdentity: "file-3");
        var reserved2 = new MemoryCoverageSegmentViewModel("r", "Reserved", "detail", MemoryCoverageFillRole.Neutral, 4,
            rangeStart: 24, rangeEndExclusive: 28, addressSpaceId: "output", isPrimaryContent: false, contentArtifactIdentity: "file-4");
        var kept1 = new MemoryCoverageSegmentViewModel("k", "Base firmware", "detail", MemoryCoverageFillRole.Kept, 4,
            usesBaseFirmwarePattern: true, rangeStart: 32, rangeEndExclusive: 36, addressSpaceId: "output", contentArtifactIdentity: "file-5");
        var kept2 = new MemoryCoverageSegmentViewModel("k", "Base firmware", "detail", MemoryCoverageFillRole.Kept, 4,
            usesBaseFirmwarePattern: true, rangeStart: 40, rangeEndExclusive: 44, addressSpaceId: "output", contentArtifactIdentity: "file-6");
        var ctrl1 = new MemoryCoverageSegmentViewModel("c", "Normal CtrlRAM", "detail", MemoryCoverageFillRole.CtrlRamNormal, 4,
            rangeStart: 48, rangeEndExclusive: 52, contentRole: MemoryContentRole.CtrlRam, addressSpaceId: "output", contentArtifactIdentity: "file-7");
        var ctrl2 = new MemoryCoverageSegmentViewModel("c", "Normal CtrlRAM", "detail", MemoryCoverageFillRole.CtrlRamNormal, 4,
            rangeStart: 56, rangeEndExclusive: 60, contentRole: MemoryContentRole.CtrlRam, addressSpaceId: "output", contentArtifactIdentity: "file-8");
        MemoryCoverageSegmentViewModel captioned1 = Slice(64, 68, "file-9", title: "Customer information", source: "customer.bin");
        MemoryCoverageSegmentViewModel captioned2 = Slice(72, 76, "file-10", title: "Customer information", source: "other.bin");
        MemoryCoverageSegmentViewModel[] rail = [dp1, dp2, reserved, reserved2, kept1, kept2, ctrl1, ctrl2, captioned1, captioned2];
        _ = MemoryCoverageBarProjection.CoalesceContent(rail, Text);
        Assert.Equal(["DP AB #1", "DP AB #2"], new[] { dp1, dp2 }.Select(slice => slice.DisplayTitle));
        Assert.Equal(rail.Skip(2).Select(static slice => slice.SourceLabel).Take(6),
            rail.Skip(2).Take(6).Select(static slice => slice.DisplayTitle));
        Assert.Equal(["Customer information", "Customer information"], new[] { captioned1, captioned2 }.Select(slice => slice.DisplayTitle));
        _ = MemoryCoverageBarProjection.CoalesceContent([dp1, kept1], Text);
        Assert.Equal("DP AB", dp1.DisplayTitle);
        Assert.StartsWith("DP AB. ", dp1.AccessibleDetail, StringComparison.Ordinal);
    }

    private static readonly long[] GapStarts = [0, 8];

    /// <summary>Primary unmapped or reserved sections of an overview never take ordinals, even with a plain title.</summary>
    [Theory]
    [InlineData(MemoryContentRole.Unmapped, "Unmapped")]
    [InlineData(MemoryContentRole.Reserved, "Reserved")]
    public void OrdinalsSkipPrimaryUnmappedAndReservedSections(MemoryContentRole role, string title)
    {
        MemoryCoverageSegmentViewModel[] rail = [.. GapStarts.Select(start => new MemoryCoverageSegmentViewModel("gap", title,
            "detail", MemoryCoverageFillRole.Neutral, 4, rangeStart: start, rangeEndExclusive: start + 4, contentRole: role,
            addressSpaceId: "flash", contentArtifactIdentity: $"gap-{start}"))];
        _ = MemoryCoverageBarProjection.CoalesceContent(rail, Text);
        Assert.All(rail, slice => Assert.Equal(title, slice.DisplayTitle));
    }

    private static MemoryCoverageSegmentViewModel Slice(long start, long end, string? identity,
        string slot = "slot", string title = "BIN", string? space = "output", bool primary = true, string source = "BIN")
    {
        return new("range", source, "detail", MemoryCoverageFillRole.Source, end - start,
            sourceSlotId: slot, rangeStart: start, rangeEndExclusive: end, displayTitle: title,
            addressSpaceId: space, isPrimaryContent: primary, contentArtifactIdentity: identity);
    }
}
