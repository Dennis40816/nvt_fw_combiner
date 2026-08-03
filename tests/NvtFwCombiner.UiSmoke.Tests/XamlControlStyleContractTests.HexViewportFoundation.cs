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

        Assert.Equal((byte)0x10, snapshot.Rows[0].Cells[0].PrimaryValue);
        Assert.Equal((byte?)0x00, snapshot.Rows[0].Cells[0].ComparisonValue);
        Assert.Equal(1, snapshot.SelectedAddress);
        Assert.True(snapshot.Rows[0].HasDataChanges);
        Assert.True(snapshot.Rows[0].HasComparison);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new HexViewportRow(
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
        Assert.Contains("new ControlAutomationPeer(this)", viewport, StringComparison.Ordinal);
        Assert.Contains("<views:HexViewportControl", panel, StringComparison.Ordinal);
        Assert.Contains("Snapshot=\"{Binding ViewportSnapshot}\"", panel, StringComparison.Ordinal);
        Assert.Contains("HexViewport_OnInteractionRequested", codeBehind, StringComparison.Ordinal);
    }

    /// <summary>Each approved host receives one closed profile without inheriting Raw Editor authority.</summary>
    [Fact]
    public void ReportAndBinInspectorProfilesRemainReadOnlyAndRangeBounded()
    {
        HexViewportCapabilityProfile report = HexViewportCapabilityProfile.ReportDiff;
        Assert.Equal(HexViewportInteraction.Inspect | HexViewportInteraction.Select, report.Interaction);
        Assert.Equal(HexViewportComparison.OptionalOriginalRows, report.Comparison);
        Assert.Equal(
            HexViewportNavigation.SemanticRanges | HexViewportNavigation.RangeScroll,
            report.Navigation);
        Assert.Equal(
            HexViewportDecorationCapability.DataChange | HexViewportDecorationCapability.SemanticVerdict,
            report.Decorations);
        Assert.Equal(12, report.InitialRows);
        Assert.Equal(28, report.MaximumRows);

        HexViewportCapabilityProfile inspector = HexViewportCapabilityProfile.BinInspector;
        Assert.Equal(HexViewportInteraction.Inspect | HexViewportInteraction.Select, inspector.Interaction);
        Assert.Equal(HexViewportComparison.None, inspector.Comparison);
        Assert.Equal(
            HexViewportNavigation.SemanticRanges | HexViewportNavigation.RangeScroll,
            inspector.Navigation);
        Assert.Equal(HexViewportDecorationCapability.None, inspector.Decorations);
        Assert.Equal(12, inspector.InitialRows);
        Assert.Equal(28, inspector.MaximumRows);

        foreach (HexViewportCapabilityProfile profile in new[] { report, inspector })
        {
            Assert.False(profile.Interaction.HasFlag(HexViewportInteraction.Overwrite));
            Assert.False(profile.Interaction.HasFlag(HexViewportInteraction.StructuralEdit));
            Assert.False(profile.Navigation.HasFlag(HexViewportNavigation.AddressJump));
            Assert.False(profile.Navigation.HasFlag(HexViewportNavigation.DocumentScroll));
            Assert.False(profile.Decorations.HasFlag(HexViewportDecorationCapability.Search));
        }
    }

    /// <summary>The reusable desktop BIN Inspector host binds the real read-only viewport contract.</summary>
    [Fact]
    public void BinInspectorHasAReusableAccessibleProductionHost()
    {
        string host = ReadPresentationFile("Views/BinInspectorPanel.axaml");

        Assert.Contains("x:DataType=\"vm:BinInspectorViewModel\"", host, StringComparison.Ordinal);
        Assert.Contains("<views:HexViewportControl", host, StringComparison.Ordinal);
        Assert.Contains("Snapshot=\"{Binding ViewportSnapshot}\"", host, StringComparison.Ordinal);
        Assert.Contains("InteractionCommand=\"{Binding ViewportInteractionCommand}\"", host, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AccessibilityView=\"Content\"", host, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.BinInspectorRangeScrollAutomationName}\"", host, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.BinInspectorResizeAutomationName}\"", host, StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.HelpText=\"{Binding SelectedByteAccessibleLabel}\"",
            host,
            StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedStructure, Mode=TwoWay}\"", host, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedField, Mode=TwoWay}\"", host, StringComparison.Ordinal);
        Assert.DoesNotContain("Save", host, StringComparison.Ordinal);
        Assert.DoesNotContain("Search", host, StringComparison.Ordinal);
        Assert.DoesNotContain("GoTo", host, StringComparison.Ordinal);
    }
}
