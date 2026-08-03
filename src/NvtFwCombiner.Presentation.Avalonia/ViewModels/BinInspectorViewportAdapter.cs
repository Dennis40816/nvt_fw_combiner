using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One resolved structure paired with a private copy of its exact bytes.</summary>
public sealed class BinInspectorStructureSource
{
    private readonly byte[] _bytes;

    /// <summary>Accepts only Application-resolved structure facts and their exact byte range.</summary>
    public BinInspectorStructureSource(
        FormattedMetadataStructure metadata,
        ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.AddressedRange is not { } range)
        {
            throw new ArgumentException(
                "BIN inspection requires resolved structure geometry.",
                nameof(metadata));
        }

        if (metadata.State != MetadataInspectionState.Value ||
            metadata.Readiness != ResolvedChildReadiness.Ready ||
            metadata.ArtifactIdentity is not { } identity ||
            !StringComparer.Ordinal.Equals(identity.ArtifactId, metadata.ArtifactBindingId))
        {
            throw new ArgumentException(
                "BIN inspection accepts only ready, identity-bound metadata structures.",
                nameof(metadata));
        }

        if (range.Range.Length != bytes.Length)
        {
            throw new ArgumentException(
                "BIN inspection bytes must exactly cover the resolved structure range.",
                nameof(bytes));
        }

        if (metadata.Fields.Any(field =>
            !StringComparer.Ordinal.Equals(
                field.AddressedRange.AddressSpaceId,
                range.AddressSpaceId) ||
            !range.Range.Contains(field.AddressedRange.Range)))
        {
            throw new ArgumentException(
                "BIN inspection fields must remain inside their resolved structure range.",
                nameof(metadata));
        }

        Metadata = metadata;
        _bytes = bytes.ToArray();
    }

    /// <summary>Application-owned names, values, state, identity, and exact geometry.</summary>
    public FormattedMetadataStructure Metadata { get; }

    internal ReadOnlySpan<byte> Bytes => _bytes;
}

/// <summary>Projects one exact resolved metadata structure into the shared read-only viewport.</summary>
internal static class BinInspectorViewportAdapter
{
    internal static HexViewportSnapshot Create(
        BinInspectorStructureSource source,
        int firstStructureRow,
        long? selectedAddress)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(firstStructureRow);
        if (source.Metadata.AddressedRange is not { } addressedRange)
        {
            throw new InvalidOperationException("BIN inspection source lost its resolved geometry.");
        }

        if (selectedAddress is long selected && !addressedRange.Range.Contains(selected))
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectedAddress),
                "BIN inspection selection must remain inside the resolved structure.");
        }

        int totalRows = checked((source.Bytes.Length + HexViewportSnapshot.BytesPerRow - 1) /
            HexViewportSnapshot.BytesPerRow);
        int visibleRows = Math.Min(HexViewportCapabilityProfile.BinInspector.InitialRows, totalRows);
        int maximumStartRow = Math.Max(0, totalRows - visibleRows);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(firstStructureRow, maximumStartRow);

        int byteStart = checked(firstStructureRow * HexViewportSnapshot.BytesPerRow);
        long startAddress = checked(addressedRange.Range.Start + byteStart);
        int bytesToProject = Math.Min(
            source.Bytes.Length - byteStart,
            visibleRows * HexViewportSnapshot.BytesPerRow);
        ReadOnlySpan<byte> bytes = source.Bytes.Slice(byteStart, bytesToProject);
        var rows = new HexViewportRow[visibleRows];
        for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            int rowByteStart = rowIndex * HexViewportSnapshot.BytesPerRow;
            int rowLength = Math.Min(HexViewportSnapshot.BytesPerRow, bytes.Length - rowByteStart);
            long rowAddress = checked(startAddress + rowByteStart);
            var cells = new HexViewportCell[rowLength];
            for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                long address = checked(rowAddress + cellIndex);
                cells[cellIndex] = new HexViewportCell(
                    address,
                    bytes[rowByteStart + cellIndex],
                    ComparisonValue: null,
                    HexViewportCellDecoration.None);
            }

            rows[rowIndex] = HexViewportRow.CreateOwned(rowAddress, cells);
        }

        return HexViewportSnapshot.CreateOwned(
            HexViewportCapabilityProfile.BinInspector,
            addressedRange.AddressSpaceId,
            addressedRange.Range.EndExclusive,
            startAddress,
            rows,
            selectedAddress,
            showComparisonRows: false,
            decorationVersion: 0);
    }
}
