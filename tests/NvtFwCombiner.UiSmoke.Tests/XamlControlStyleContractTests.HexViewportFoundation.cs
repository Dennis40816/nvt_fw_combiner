using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Requires an immutable bounded source-neutral window rather than mutable editor cells.</summary>
    [Fact]
    public void HexViewportSnapshotDefensivelyCopiesAndValidatesRows()
    {
        HexViewportCell[] callerCells =
        [
            new(0, 0x10, 0x00, HexViewportCellDecoration.DataChange),
            new(1, 0x20, null, HexViewportCellDecoration.Search),
        ];
        var row = new HexViewportRow(0, callerCells);
        HexViewportRow[] callerRows = [row];
        var snapshot = new HexViewportSnapshot(
            HexViewportCapabilityProfile.RawEditor,
            "raw-binary-work-buffer",
            documentLength: 2,
            startAddress: 0,
            callerRows,
            selectedAddress: 1,
            showComparisonRows: true);

        callerCells[0] = new HexViewportCell(0, 0xFF, null, HexViewportCellDecoration.None);
        callerRows[0] = new HexViewportRow(0, [new HexViewportCell(0, 0xEE, null, HexViewportCellDecoration.None)]);

        Assert.Equal(0x10, snapshot.Rows[0].Cells[0].PrimaryValue);
        Assert.Equal(0x00, snapshot.Rows[0].Cells[0].ComparisonValue);
        Assert.Equal(1, snapshot.SelectedAddress);
        Assert.True(snapshot.Rows[0].HasDataChanges);
        Assert.True(snapshot.Rows[0].HasComparison);
        Assert.Throws<ArgumentOutOfRangeException>(() => new HexViewportRow(
            0,
            Enumerable.Range(0, 17)
                .Select(index => new HexViewportCell(index, 0, null, HexViewportCellDecoration.None))));
    }

    /// <summary>The shared renderer accepts only a snapshot and emits intents; editing remains in the host.</summary>
    [Fact]
    public void HexViewportControlHasNoEditorOrMutationAuthority()
    {
        string viewport = ReadPresentationFile("Views/HexViewportControl.cs");
        string panel = ReadPresentationFile("Views/HexEditorPanel.axaml");
        string codeBehind = ReadPresentationFile("Views/HexEditorPanel.axaml.cs");

        Assert.Contains("StyledProperty<HexViewportSnapshot?> SnapshotProperty", viewport, StringComparison.Ordinal);
        Assert.Contains("InteractionRequested", viewport, StringComparison.Ordinal);
        Assert.DoesNotContain("HexEditorWorkspaceViewModel", viewport, StringComparison.Ordinal);
        Assert.DoesNotContain("EditCommand", viewport, StringComparison.Ordinal);
        Assert.DoesNotContain("Save", viewport, StringComparison.Ordinal);
        Assert.Contains("<views:HexViewportControl", panel, StringComparison.Ordinal);
        Assert.Contains("Snapshot=\"{Binding ViewportSnapshot}\"", panel, StringComparison.Ordinal);
        Assert.Contains("HexViewport_OnInteractionRequested", codeBehind, StringComparison.Ordinal);
    }
}
