using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Source-specific projection into the shared read-only Hex viewport.</summary>
public sealed class ReportHexDiffViewportAdapterTests
{
    /// <summary>Only changed bytes inside the selected semantic range receive diff decoration.</summary>
    [Fact]
    public void AdapterKeepsContextVisibleWithoutBorrowingAdjacentRangeVerdicts()
    {
        byte[] before = new byte[0x80];
        byte[] after = (byte[])before.Clone();
        after[0x10] = 0xA0;
        after[0x21] = 0xB1;
        var range = new ByteRange(0x21, 0x23);
        var replay = OutputDifferenceReplaySegment.CreateWithAlignedContext(
            before,
            after,
            range);

        HexViewportSnapshot snapshot = ReportHexDiffViewportAdapter.Create(
            "output-image",
            before.Length,
            range.Start,
            range.Length,
            replay,
            firstReplayRow: 0,
            selectedAddress: range.Start);

        Assert.Same(HexViewportCapabilityProfile.ReportDiff, snapshot.Profile);
        Assert.False(snapshot.ShowComparisonRows);
        Assert.Equal(replay.Range.Start, snapshot.StartAddress);
        Assert.Equal(replay.Range.Length / HexViewportSnapshot.BytesPerRow, snapshot.Rows.Count);
        HexViewportCell contextChange = snapshot.Rows.SelectMany(static row => row.Cells)
            .Single(cell => cell.Address == 0x10);
        HexViewportCell selectedChange = snapshot.Rows.SelectMany(static row => row.Cells)
            .Single(cell => cell.Address == 0x21);
        Assert.False(contextChange.IsDataChanged);
        Assert.True(selectedChange.IsDataChanged);
        Assert.Equal((byte?)0, selectedChange.ComparisonValue);
        Assert.Equal((byte)0xB1, selectedChange.PrimaryValue);
    }

    /// <summary>A long range materializes only the requested bounded range-local window.</summary>
    [Fact]
    public void AdapterMaterializesAtMostTheNamedReportRowBudget()
    {
        byte[] before = new byte[0x200];
        byte[] after = (byte[])before.Clone();
        after.AsSpan(0x40, 0x140).Fill(0x5A);
        var range = new ByteRange(0x40, 0x140);
        var replay = OutputDifferenceReplaySegment.CreateWithAlignedContext(
            before,
            after,
            range);

        HexViewportSnapshot snapshot = ReportHexDiffViewportAdapter.Create(
            "output-image",
            before.Length,
            range.Start,
            range.Length,
            replay,
            firstReplayRow: 12,
            selectedAddress: 0x140);

        Assert.Equal(HexViewportCapabilityProfile.ReportDiff.InitialRows, snapshot.Rows.Count);
        Assert.Equal(replay.Range.Start + (12 * HexViewportSnapshot.BytesPerRow), snapshot.StartAddress);
        Assert.Equal(0x140, snapshot.SelectedAddress);
        Assert.All(
            snapshot.Rows.SelectMany(static row => row.Cells).Where(static cell => cell.IsDataChanged),
            cell => Assert.True(range.Contains(cell.Address)));
    }
}
