using Avalonia.Headless.XUnit;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Visible-cell lookup preserves row order, bounds and snapshot-owned values.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class HexViewportVisibleCellTests
{
    /// <summary>An empty viewport has no visible cells, including at extreme addresses.</summary>
    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    public void EmptySnapshotReturnsFalseAndDefault(long address)
    {
        HexViewportSnapshot snapshot = HexViewportSnapshot.Empty(
            HexViewportCapabilityProfile.BinInspector, "input");

        Assert.False(snapshot.TryGetVisibleCell(address, out HexViewportCell cell));
        Assert.Equal(default, cell);
    }

    /// <summary>Unsorted sparse rows admit only their actual cells, with half-open row bounds.</summary>
    [Theory]
    [InlineData(long.MinValue, false)]
    [InlineData(-1L, false)]
    [InlineData(0x0FL, false)]
    [InlineData(0x10L, true)]
    [InlineData(0x12L, true)]
    [InlineData(0x13L, false)]
    [InlineData(0x2FL, false)]
    [InlineData(0x30L, true)]
    [InlineData(0x3FL, true)]
    [InlineData(0x40L, false)]
    [InlineData(long.MaxValue, false)]
    public void SparseRowsUseTheirOwnAddressesAndLengths(long address, bool expectedFound)
    {
        HexViewportRow later = CreateRow(0x30, 16);
        HexViewportRow earlier = CreateRow(0x10, 3);
        var snapshot = new HexViewportSnapshot(
            HexViewportCapabilityProfile.BinInspector, "input", 0x80, 0x10,
            [later, earlier], null, false);

        Assert.Equal(expectedFound, snapshot.TryGetVisibleCell(address, out HexViewportCell cell));
        HexViewportCell expected = expectedFound
            ? address >= 0x30 ? later.Cells[(int)(address - 0x30)] : earlier.Cells[(int)(address - 0x10)]
            : default;
        Assert.Equal(expected, cell);
    }

    /// <summary>The final valid byte near the long address limit remains distinct from the exclusive end.</summary>
    [Fact]
    public void HighAddressRowKeepsUnsignedGuardAndExclusiveEnd()
    {
        HexViewportRow row = CreateRow(long.MaxValue - 2, 2);
        var snapshot = new HexViewportSnapshot(
            HexViewportCapabilityProfile.BinInspector, "input", long.MaxValue, row.Address,
            [row], null, false);

        Assert.True(snapshot.TryGetVisibleCell(row.Address, out HexViewportCell first));
        Assert.Equal(row.Cells[0], first);
        Assert.True(snapshot.TryGetVisibleCell(long.MaxValue - 1, out HexViewportCell last));
        Assert.Equal(row.Cells[1], last);
        Assert.False(snapshot.TryGetVisibleCell(row.Address - 1, out HexViewportCell before));
        Assert.Equal(default, before);
        Assert.False(snapshot.TryGetVisibleCell(long.MaxValue, out HexViewportCell end));
        Assert.Equal(default, end);
        Assert.False(snapshot.TryGetVisibleCell(long.MinValue, out HexViewportCell negative));
        Assert.Equal(default, negative);
    }

    /// <summary>Overlapping rows retain the first matching cell and every comparison field.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstMatchingRowPreservesComparisonDataWithoutChangingSnapshot(bool showComparisonRows)
    {
        var expected = new HexViewportCell(0x21, 0xA5, 0x11,
            HexViewportCellDecoration.DataChange | HexViewportCellDecoration.Search, 7);
        var first = new HexViewportRow(0x20,
            [new HexViewportCell(0x20, 0x10, null, HexViewportCellDecoration.None), expected]);
        var second = new HexViewportRow(0x21,
            [new HexViewportCell(0x21, 0xFF, 0x22, HexViewportCellDecoration.StructuralChange)]);
        var snapshot = new HexViewportSnapshot(
            HexViewportCapabilityProfile.ReportDiff, "output", 0x40, 0x20,
            [first, second], 0x20, showComparisonRows, decorationVersion: 3);
        IReadOnlyList<HexViewportRow> rows = snapshot.Rows;

        Assert.True(snapshot.TryGetVisibleCell(0x21, out HexViewportCell actual));
        Assert.Equal(expected, actual);
        Assert.Same(rows, snapshot.Rows);
        Assert.Equal(0x20L, snapshot.SelectedAddress);
        Assert.Equal(showComparisonRows, snapshot.ShowComparisonRows);
        Assert.Equal(3, snapshot.DecorationVersion);
    }

    /// <summary>The same address in separate snapshots cannot share a lookup result.</summary>
    [Fact]
    public void SameAddressReturnsEachSnapshotsOwnCell()
    {
        var inputCell = new HexViewportCell(0x10, 0x11, null, HexViewportCellDecoration.None);
        var outputCell = new HexViewportCell(0x10, 0x22, 0x33, HexViewportCellDecoration.DataChange);
        var input = new HexViewportSnapshot(
            HexViewportCapabilityProfile.BinInspector, "input", 0x20, 0x10,
            [new HexViewportRow(0x10, [inputCell])], null, false);
        var output = new HexViewportSnapshot(
            HexViewportCapabilityProfile.ReportDiff, "output", 0x20, 0x10,
            [new HexViewportRow(0x10, [outputCell])], null, true);

        Assert.True(input.TryGetVisibleCell(0x10, out HexViewportCell first));
        Assert.Equal(inputCell, first);
        Assert.True(output.TryGetVisibleCell(0x10, out HexViewportCell second));
        Assert.Equal(outputCell, second);
        Assert.True(input.TryGetVisibleCell(0x10, out HexViewportCell again));
        Assert.Equal(inputCell, again);
    }

    /// <summary>The editor's internal entry point follows its current viewport after load, edit and scroll.</summary>
    [AvaloniaFact]
    public async Task EditorForwardingUsesCurrentSnapshotAndRejectsNonvisibleAddresses()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-visible-cell");
        byte[] bytes = new byte[0x200];
        bytes[0] = 0x11;
        string path = workspace.Write("source.bin", bytes);
        HexEditorWorkspaceViewModel editor = PresentationTestHost.CreateViewModel().HexEditorWorkspace;
        Assert.False(editor.TryGetViewportCell(0, out HexViewportCell empty));
        Assert.Equal(default, empty);

        await editor.LoadAsync(path, TestContext.Current.CancellationToken);
        HexViewportSnapshot original = editor.ViewportSnapshot;
        Assert.True(editor.TryGetViewportCell(0, out HexViewportCell loaded));
        Assert.Equal(original.Rows[0].Cells[0], loaded);
        Assert.Equal((byte)0x11, loaded.PrimaryValue);

        editor.SetByteToFfCommand.Execute(0);
        Assert.True(editor.TryGetViewportCell(0, out HexViewportCell edited));
        Assert.Equal(editor.ViewportSnapshot.Rows[0].Cells[0], edited);
        Assert.Equal((byte)0xFF, edited.PrimaryValue);
        Assert.Equal((byte?)0x11, edited.ComparisonValue);
        Assert.True(edited.IsDataChanged);
        Assert.True(original.TryGetVisibleCell(0, out HexViewportCell retained));
        Assert.Equal(loaded, retained);

        editor.SetViewportStartRowCommand.Execute(editor.DocumentScrollMaximum);
        Assert.False(editor.TryGetViewportCell(0, out HexViewportCell hidden));
        Assert.Equal(default, hidden);
        Assert.True(editor.TryGetViewportCell(0x1FF, out HexViewportCell last));
        Assert.Equal(editor.ViewportSnapshot.Rows[^1].Cells[^1], last);
        Assert.False(editor.TryGetViewportCell(0x200, out HexViewportCell end));
        Assert.Equal(default, end);
    }

    private static HexViewportRow CreateRow(long address, int count)
    {
        return new HexViewportRow(address, Enumerable.Range(0, count).Select(index =>
            new HexViewportCell(address + index, (byte)(index + 1), null, HexViewportCellDecoration.None)));
    }
}
