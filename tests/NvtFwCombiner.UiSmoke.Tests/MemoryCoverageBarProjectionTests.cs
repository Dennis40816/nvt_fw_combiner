using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Visual grouping preserves physical addresses, exact weights and original slice identities.</summary>
public sealed class MemoryCoverageBarProjectionTests
{
    /// <summary>The approved illustrative group has eight original slices and a 3.125% main span.</summary>
    [Fact]
    public void AdjacentSmallSlicesRetainOriginalReferencesAndInclusiveEndpoints()
    {
        MemoryCoverageSegmentViewModel[] slices = Example();
        IReadOnlyList<MemoryCoverageBarItem> items = MemoryCoverageBarProjection.Create(slices);
        Assert.Equal(3, items.Count);
        MemoryCoverageBarItem group = items[1];
        Assert.True(group.IsGroup);
        Assert.Equal(8, group.Slices.Count);
        Assert.Equal("output", group.AddressSpace);
        Assert.Equal("0x3F000", group.StartLabel);
        Assert.Equal("0x42FFF", group.EndLabel);
        Assert.Equal("16 KiB", group.SizeLabel);
        Assert.Equal(0.03125, group.BarWidth / items.Sum(static item => item.BarWidth));
        Assert.Equal(slices, items.SelectMany(static item => item.Slices));
        Assert.Equal(slices.Sum(static slice => slice.BarWidth), items.Sum(static item => item.BarWidth));
        for (int i = 0; i < 8; i++) { Assert.Same(slices[i + 1], group.Slices[i]); }
        Assert.Equal("0x41000–0x41FFF", $"{group.Slices[5].AddressRangeLabel}");
    }

    /// <summary>Gaps, overlaps, space boundaries, attention and unknown metadata are not hidden by grouping.</summary>
    [Theory]
    [InlineData("gap")]
    [InlineData("overlap")]
    [InlineData("space")]
    [InlineData("unknown")]
    [InlineData("warning")]
    [InlineData("threshold")]
    public void BoundaryOrIncompleteFactsPreventAggregation(string boundary)
    {
        MemoryCoverageSegmentViewModel first = Slice(0, 100);
        MemoryCoverageSegmentViewModel second = Slice(boundary == "gap" ? 101 : boundary == "overlap" ? 99 : 100,
            boundary == "threshold" ? 200 : 100, boundary == "space" ? "other" : boundary == "unknown" ? null : "output",
            boundary == "warning" ? MemoryDiagnosticSeverity.Warning : MemoryDiagnosticSeverity.None);
        // Total weight is exactly 10000: 200 is on, not below, the two-percent threshold.
        MemoryCoverageSegmentViewModel tail = Slice(1000, 10000 - first.BarWidth - second.BarWidth);
        IReadOnlyList<MemoryCoverageBarItem> items = MemoryCoverageBarProjection.Create([first, second, tail]);
        Assert.Equal(3, items.Count);
        Assert.All(items, static item => Assert.False(item.IsGroup));
    }

    /// <summary>Empty and degenerate layouts never create a fabricated group.</summary>
    [Fact]
    public void EmptyAndUnknownRangesStayUnaggregated()
    {
        Assert.Empty(MemoryCoverageBarProjection.Create([]));
        var unknown = new MemoryCoverageSegmentViewModel("unknown", "input", "detail", MemoryCoverageFillRole.Source, 0);
        Assert.False(Assert.Single(MemoryCoverageBarProjection.Create([unknown])).IsGroup);
    }

    /// <summary>A pair immediately below the threshold still groups, independently of its content color.</summary>
    [Fact]
    public void JustBelowTwoPercentAggregates()
    {
        MemoryCoverageSegmentViewModel first = Slice(0, 199);
        MemoryCoverageSegmentViewModel second = Slice(199, 199);
        IReadOnlyList<MemoryCoverageBarItem> items = MemoryCoverageBarProjection.Create([first, second, Slice(398, 9602)]);
        Assert.Equal(2, items.Count);
        Assert.Equal(398, items[0].BarWidth);
        Assert.Equal([first, second], items[0].Slices);
    }

    internal static MemoryCoverageSegmentViewModel[] Example()
    {
        var result = new List<MemoryCoverageSegmentViewModel> { Slice(0, 0x3F000) };
        MemoryCoverageFillRole[] roles = [MemoryCoverageFillRole.CtrlRamNf, MemoryCoverageFillRole.Tp,
            MemoryCoverageFillRole.CtrlRamVn, MemoryCoverageFillRole.TpBackup, MemoryCoverageFillRole.CtrlRamVector,
            MemoryCoverageFillRole.CtrlRamNormal, MemoryCoverageFillRole.CtrlRamMp, MemoryCoverageFillRole.DiffDlm];
        int index = 0;
        long address = 0x3F000;
        foreach (int kib in new[] { 1, 2, 1, 2, 2, 4, 2, 2 })
        {
            result.Add(Slice(address, kib * 1024, fillRole: roles[index++]));
            address += kib * 1024;
        }
        result.Add(Slice(0x43000, 0x3D000, fillRole: MemoryCoverageFillRole.Tp));
        return [.. result];
    }

    private static MemoryCoverageSegmentViewModel Slice(long start, double size, string? space = "output", MemoryDiagnosticSeverity severity = MemoryDiagnosticSeverity.None, MemoryCoverageFillRole fillRole = MemoryCoverageFillRole.Dp)
    {
        long end = checked(start + (long)size);
        string range = $"0x{start:X5}–0x{end - 1:X5}";
        return new MemoryCoverageSegmentViewModel(range, "DP BIN", "Typed display fixture, no firmware execution.",
            fillRole, size,
            rangeStart: start, rangeEndExclusive: end, addressSpaceId: space, addressRangeLabel: range,
            diagnosticSeverity: severity, displayTitle: $"Region 0x{start:X5}");
    }
}
