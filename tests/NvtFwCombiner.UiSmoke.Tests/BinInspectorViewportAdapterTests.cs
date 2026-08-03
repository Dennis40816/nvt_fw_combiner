using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Closed resolved-metadata projection into the shared read-only Hex viewport.</summary>
public sealed class BinInspectorViewportAdapterTests
{
    /// <summary>The source snapshots exact structure bytes and exposes no inferred IC input.</summary>
    [Fact]
    public void SourceCopiesExactResolvedBytesWithoutIcInference()
    {
        byte[] bytes = CreatePattern(0x40, 0x20);
        BinInspectorStructureSource source = CreateSource(
            "header",
            new ByteRange(0x80, bytes.Length),
            bytes,
            [Field("version", "Version", 0x82, 2, "513")]);
        bytes[2] = 0xFF;

        var inspector = new BinInspectorViewModel([source]);
        HexViewportCell versionCell = inspector.ViewportSnapshot.Rows
            .SelectMany(static row => row.Cells)
            .Single(static cell => cell.Address == 0x82);

        Assert.Equal((byte)0x22, versionCell.PrimaryValue);
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
        Assert.DoesNotContain(
            typeof(BinInspectorViewModel).GetConstructors()
                .SelectMany(static constructor => constructor.GetParameters()),
            static parameter => parameter.Name?.Contains("ic", StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>Field navigation keeps selection and scrolling inside the resolved structure range.</summary>
    [Fact]
    public void FieldSelectionRevealsExactAbsoluteRangeWithBoundedLocalRows()
    {
        byte[] bytes = CreatePattern(0x200, 0x40);
        FormattedMetadataField first = Field("first", "First", 0x104, 1, "68");
        FormattedMetadataField distant = Field("tail", "Tail", 0x2F0, 4, "F0 F1 F2 F3");
        BinInspectorStructureSource source = CreateSource(
            "parameters",
            new ByteRange(0x100, bytes.Length),
            bytes,
            [first, distant]);
        var inspector = new BinInspectorViewModel([source]);

        inspector.SelectFieldCommand.Execute(distant);

        Assert.Same(distant, inspector.SelectedField);
        Assert.Equal(4, distant.AddressedRange.Range.Length);
        Assert.Equal(distant.AddressedRange.Range.Start, inspector.ViewportSnapshot.SelectedAddress);
        Assert.InRange(inspector.ViewportSnapshot.Rows.Count, 1, 12);
        Assert.True(inspector.RangeScrollRow > 0);
        Assert.True(inspector.RangeScrollMaximum > 0);
        Assert.Contains(
            inspector.ViewportSnapshot.Rows.SelectMany(static row => row.Cells),
            cell => cell.Address == distant.AddressedRange.Range.Start);

        inspector.RangeScrollRow = int.MaxValue;
        Assert.Equal(inspector.RangeScrollMaximum, inspector.RangeScrollRow);
        Assert.True(inspector.ViewportSnapshot.Rows[^1].Cells[^1].Address < 0x300);
    }

    /// <summary>Only sources owned by the inspector and fields owned by its active structure can navigate.</summary>
    [Fact]
    public void SemanticCommandsRejectForeignResolvedObjects()
    {
        BinInspectorStructureSource first = CreateSource(
            "first",
            new ByteRange(0x20, 0x20),
            CreatePattern(0x20, 0x10),
            [Field("first-value", "First value", 0x20, 1, "16")]);
        BinInspectorStructureSource second = CreateSource(
            "second",
            new ByteRange(0x80, 0x20),
            CreatePattern(0x20, 0x80),
            [Field("second-value", "Second value", 0x80, 1, "128")]);
        BinInspectorStructureSource foreign = CreateSource(
            "foreign",
            new ByteRange(0xC0, 0x20),
            CreatePattern(0x20, 0xC0),
            [Field("foreign-value", "Foreign value", 0xC0, 1, "192")]);
        var inspector = new BinInspectorViewModel([first, second]);

        Assert.False(inspector.SelectStructureCommand.CanExecute(foreign));
        Assert.False(inspector.SelectFieldCommand.CanExecute(second.Metadata.Fields[0]));
        inspector.SelectStructureCommand.Execute(second);

        Assert.Same(second, inspector.SelectedStructure);
        Assert.Same(second.Metadata.Fields[0], inspector.SelectedField);
        Assert.Equal(0x80, inspector.ViewportSnapshot.StartAddress);
        Assert.Equal(0x80, inspector.ViewportSnapshot.SelectedAddress);
    }

    /// <summary>Pending or partial metadata cannot be promoted into a BIN inspector source.</summary>
    [Fact]
    public void SourceRejectsUnresolvedOrMismatchedGeometry()
    {
        byte[] bytes = CreatePattern(3, 0x20);
        FirmwareArtifactIdentity identity = new FirmwareArtifactPayload("dp-input", new byte[0x100]).Identity;
        var pending = new FormattedMetadataStructure(
            "pending",
            "map",
            "dp-input",
            "dpcmi",
            MetadataInspectionState.WaitingForArtifact,
            ResolvedChildReadiness.PendingInput,
            FirmwareMetadataStructureResolutionFailure.MissingArtifact,
            artifactIdentity: null,
            addressedRange: null,
            fields: []);
        var resolved = new FormattedMetadataStructure(
            "resolved",
            "map",
            "dp-input",
            "dpcmi",
            MetadataInspectionState.Value,
            ResolvedChildReadiness.Ready,
            failure: null,
            identity,
            new FirmwareAddressedRange("flash", new ByteRange(0x36, 3)),
            [Field("major", "DP major", 0x37, 1, "3")]);

        _ = Assert.Throws<ArgumentException>(() => new BinInspectorStructureSource(pending, bytes));
        _ = Assert.Throws<ArgumentException>(() =>
            new BinInspectorStructureSource(resolved, bytes.AsMemory(0, 2)));
    }

    private static BinInspectorStructureSource CreateSource(
        string bindingId,
        ByteRange range,
        byte[] bytes,
        IReadOnlyList<FormattedMetadataField> fields)
    {
        byte[] artifactBytes = new byte[checked((int)range.EndExclusive)];
        bytes.CopyTo(artifactBytes, checked((int)range.Start));
        FirmwareArtifactIdentity identity = new FirmwareArtifactPayload("dp-input", artifactBytes).Identity;
        var metadata = new FormattedMetadataStructure(
            bindingId,
            "dp-map",
            "dp-input",
            bindingId,
            MetadataInspectionState.Value,
            ResolvedChildReadiness.Ready,
            failure: null,
            identity,
            new FirmwareAddressedRange("flash", range),
            fields);
        return new BinInspectorStructureSource(metadata, bytes);
    }

    private static FormattedMetadataField Field(
        string id,
        string displayName,
        long start,
        long length,
        string value)
    {
        return new FormattedMetadataField(
            id,
            displayName,
            new FirmwareAddressedRange("flash", new ByteRange(start, length)),
            FirmwareMetadataFieldApplicabilityState.Active,
            FirmwareMetadataValueKind.UnsignedInteger,
            value);
    }

    private static byte[] CreatePattern(int length, byte seed)
    {
        return [.. Enumerable.Range(0, length).Select(index => unchecked((byte)(seed + index)))];
    }
}
