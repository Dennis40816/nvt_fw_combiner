using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Closed formatter-rooted metadata projection into the shared read-only Hex viewport.</summary>
public sealed class BinInspectorViewportAdapterTests
{
    /// <summary>The host snapshots exact structure bytes and accepts no detached structure or IC input.</summary>
    [Fact]
    public void SnapshotCopiesExactResolvedBytesWithoutIcInference()
    {
        byte[] bytes = CreatePattern(0xC0);
        FirmwareBinInspectionSnapshot snapshot = CreateSnapshot(
            bytes,
            Structure("header", 0x80, 0x40, Field("version", "Version", 2, 2)));
        var inspector = new BinInspectorViewModel(snapshot, ShellLanguage.English);
        bytes[0x82] = 0xFF;

        HexViewportCell versionCell = inspector.ViewportSnapshot.Rows
            .SelectMany(static row => row.Cells)
            .Single(static cell => cell.Address == 0x82);

        Assert.Equal((byte)0x82, versionCell.PrimaryValue);
        Assert.Same(snapshot, inspector.Inspection);
        Assert.Same(HexViewportCapabilityProfile.BinInspector, inspector.ViewportSnapshot.Profile);
        Assert.Equal("flash", inspector.ViewportSnapshot.AddressSpaceId);
        Assert.False(inspector.ViewportSnapshot.ShowComparisonRows);
        Assert.All(
            inspector.ViewportSnapshot.Rows.SelectMany(static row => row.Cells),
            static cell =>
            {
                Assert.Null(cell.ComparisonValue);
                Assert.Equal(HexViewportCellDecoration.None, cell.Decorations);
            });
        Assert.Collection(
            typeof(BinInspectorViewModel).GetConstructors().Single().GetParameters(),
            parameter => Assert.Equal(typeof(FirmwareBinInspectionSnapshot), parameter.ParameterType),
            parameter => Assert.Equal(typeof(ShellLanguage), parameter.ParameterType));
    }

    /// <summary>Field navigation keeps selection, accessibility, and scrolling inside one resolved structure.</summary>
    [Fact]
    public void FieldSelectionRevealsExactAbsoluteRangeWithBoundedLocalRows()
    {
        FirmwareBinInspectionSnapshot snapshot = CreateSnapshot(
            CreatePattern(0x300),
            Structure(
                "parameters",
                0x100,
                0x200,
                Field("first", "First", 4, 1),
                Field("tail", "Tail", 0x1F0, 4)));
        var inspector = new BinInspectorViewModel(snapshot, ShellLanguage.English);
        FormattedMetadataField distant = inspector.Fields.Single(static field => field.FieldId == "tail");

        inspector.SelectedField = distant;

        Assert.Same(distant, inspector.SelectedField);
        Assert.Equal(distant.AddressedRange.Range.Start, inspector.ViewportSnapshot.SelectedAddress);
        Assert.InRange(inspector.ViewportSnapshot.Rows.Count, 1, 12);
        Assert.True(inspector.RangeScrollRow > 0);
        Assert.True(inspector.RangeScrollMaximum > 0);
        Assert.Contains("field Tail", inspector.SelectedByteAccessibleLabel, StringComparison.Ordinal);
        Assert.Contains(
            inspector.ViewportSnapshot.Rows.SelectMany(static row => row.Cells),
            cell => cell.Address == distant.AddressedRange.Range.Start);

        inspector.RangeScrollRow = int.MaxValue;
        Assert.Equal(inspector.RangeScrollMaximum, inspector.RangeScrollRow);
        Assert.True(inspector.ViewportSnapshot.Rows[^1].Cells[^1].Address < 0x300);
    }

    /// <summary>Semantic commands reject objects from another formatter-rooted inspection snapshot.</summary>
    [Fact]
    public void SemanticCommandsRejectForeignResolvedObjects()
    {
        FirmwareBinInspectionSnapshot snapshot = CreateSnapshot(
            CreatePattern(0x100),
            Structure("first", 0x20, 0x20, Field("first-value", "First value", 0, 1)),
            Structure("second", 0x80, 0x20, Field("second-value", "Second value", 0, 1)));
        FirmwareBinInspectionSnapshot foreignSnapshot = CreateSnapshot(
            CreatePattern(0x100),
            Structure("foreign", 0xC0, 0x20, Field("foreign-value", "Foreign value", 0, 1)));
        var inspector = new BinInspectorViewModel(snapshot, ShellLanguage.English);
        FirmwareBinInspectionStructure second = snapshot.Structures[1];
        FirmwareBinInspectionStructure foreign = Assert.Single(foreignSnapshot.Structures);

        Assert.False(inspector.SelectStructureCommand.CanExecute(foreign));
        Assert.False(inspector.SelectFieldCommand.CanExecute(second.Metadata.Fields[0]));
        inspector.SelectedStructure = second;

        Assert.Same(second, inspector.SelectedStructure);
        Assert.Same(second.Metadata.Fields[0], inspector.SelectedField);
        Assert.Equal(0x80, inspector.ViewportSnapshot.StartAddress);
        Assert.Equal(0x80, inspector.ViewportSnapshot.SelectedAddress);
    }

    /// <summary>Switching away from a scrolled structure publishes only state owned by the new structure.</summary>
    [Fact]
    public void ScrolledStructureSwitchDoesNotPublishThePriorSelection()
    {
        FirmwareBinInspectionSnapshot snapshot = CreateSnapshot(
            CreatePattern(0x400),
            Structure("long", 0x20, 0x200, Field("long-value", "Long value", 0, 1)),
            Structure("next", 0x300, 0x20, Field("next-value", "Next value", 0, 1)));
        var inspector = new BinInspectorViewModel(snapshot, ShellLanguage.English);
        inspector.RangeScrollRow = inspector.RangeScrollMaximum;
        Assert.True(inspector.RangeScrollRow > 0);

        inspector.SelectedStructure = snapshot.Structures[1];

        Assert.Same(snapshot.Structures[1], inspector.SelectedStructure);
        Assert.Equal(0, inspector.RangeScrollRow);
        Assert.Equal(0x300, inspector.ViewportSnapshot.SelectedAddress);
        Assert.Equal(0x300, inspector.SelectedField!.AddressedRange.Range.Start);
    }

    /// <summary>Direct byte navigation synchronizes the semantic field without snapping away from the chosen byte.</summary>
    [Fact]
    public void ByteSelectionSynchronizesTheContainingField()
    {
        FirmwareBinInspectionSnapshot snapshot = CreateSnapshot(
            CreatePattern(0x100),
            Structure(
                "header",
                0x20,
                0x40,
                Field("first", "First", 0, 2),
                Field("second", "Second", 0x10, 4)));
        var inspector = new BinInspectorViewModel(snapshot, ShellLanguage.English);
        FormattedMetadataField second = inspector.Fields.Single(static field => field.FieldId == "second");

        inspector.HandleViewportIntent(new HexViewportInteractionIntent(
            HexViewportInteractionTrigger.Select,
            0x32,
            default));

        Assert.Same(second, inspector.SelectedField);
        Assert.Equal(0x32, inspector.ViewportSnapshot.SelectedAddress);

        inspector.HandleViewportIntent(new HexViewportInteractionIntent(
            HexViewportInteractionTrigger.Select,
            0x28,
            default));

        Assert.Null(inspector.SelectedField);
        Assert.Equal(0x28, inspector.ViewportSnapshot.SelectedAddress);
    }

    /// <summary>Explicit selection retains the chosen semantic field when bit-slice fields share one byte.</summary>
    [Fact]
    public void ExplicitFieldSelectionRetainsAnOverlappingField()
    {
        FirmwareBinInspectionSnapshot snapshot = CreateSnapshot(
            CreatePattern(0x100),
            Structure(
                "dpcmi",
                0x20,
                0x40,
                Field("dp-minor", "DP minor", 0x10, 1),
                Field("jira-high", "Jira high", 0x10, 1)));
        var inspector = new BinInspectorViewModel(snapshot, ShellLanguage.English);
        FormattedMetadataField jiraHigh = inspector.Fields.Single(static field => field.FieldId == "jira-high");

        inspector.SelectedField = jiraHigh;

        Assert.Same(jiraHigh, inspector.SelectedField);
        Assert.Equal(0x30, inspector.ViewportSnapshot.SelectedAddress);
        Assert.Contains("field Jira high", inspector.SelectedByteAccessibleLabel, StringComparison.Ordinal);
    }

    private static FirmwareBinInspectionSnapshot CreateSnapshot(
        byte[] bytes,
        params FirmwareBinInspectionStructureFixture[] structures)
    {
        return FirmwareBinInspectionTestFixture.Create(bytes, structures);
    }

    private static FirmwareBinInspectionStructureFixture Structure(
        string id,
        long start,
        long length,
        params FirmwareBinInspectionFieldFixture[] fields)
    {
        return new FirmwareBinInspectionStructureFixture(id, new ByteRange(start, length), fields);
    }

    private static FirmwareBinInspectionFieldFixture Field(
        string id,
        string displayName,
        long offset,
        int width)
    {
        return new FirmwareBinInspectionFieldFixture(id, displayName, offset, width);
    }

    private static byte[] CreatePattern(int length)
    {
        return [.. Enumerable.Range(0, length).Select(static index => unchecked((byte)index))];
    }
}
