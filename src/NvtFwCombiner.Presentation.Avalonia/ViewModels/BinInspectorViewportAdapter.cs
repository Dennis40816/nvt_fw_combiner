using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Projects one exact resolved metadata structure into the shared read-only viewport.</summary>
internal static class BinInspectorViewportAdapter
{
    internal static HexViewportSnapshot Create(
        FirmwareBinInspectionStructure source,
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
        ReadOnlySpan<byte> bytes = source.Bytes.Span.Slice(byteStart, bytesToProject);
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
