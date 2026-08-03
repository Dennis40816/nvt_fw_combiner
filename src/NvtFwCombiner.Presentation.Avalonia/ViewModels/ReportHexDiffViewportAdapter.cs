using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Projects one exact report replay segment into the shared read-only viewport.</summary>
internal static class ReportHexDiffViewportAdapter
{
    internal static HexViewportSnapshot Create(
        string outputSpaceId,
        long documentLength,
        long differenceStart,
        long differenceLength,
        OutputDifferenceReplaySegment replay,
        int firstReplayRow,
        long? selectedAddress,
        bool showOriginalRows = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputSpaceId);
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentOutOfRangeException.ThrowIfNegative(documentLength);
        ArgumentOutOfRangeException.ThrowIfNegative(firstReplayRow);
        ArgumentOutOfRangeException.ThrowIfNegative(differenceStart);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(differenceLength);
        if (replay.Range.Start % HexViewportSnapshot.BytesPerRow != 0 ||
            replay.Range.EndExclusive > documentLength ||
            differenceLength > replay.Range.Length ||
            differenceStart < replay.Range.Start ||
            differenceStart > replay.Range.EndExclusive - differenceLength)
        {
            throw new ArgumentException(
                "Report replay bytes must be aligned, in bounds, and contain the selected difference.",
                nameof(replay));
        }

        int totalRows = checked((int)(
            (replay.Range.Length + HexViewportSnapshot.BytesPerRow - 1) /
            HexViewportSnapshot.BytesPerRow));
        int visibleRows = Math.Min(HexViewportCapabilityProfile.ReportDiff.InitialRows, totalRows);
        int maximumStartRow = Math.Max(0, totalRows - visibleRows);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(firstReplayRow, maximumStartRow);

        long startAddress = checked(
            replay.Range.Start + ((long)firstReplayRow * HexViewportSnapshot.BytesPerRow));
        int replayStart = checked((int)(startAddress - replay.Range.Start));
        int remainingBytes = checked((int)replay.Range.Length - replayStart);
        int bytesToProject = Math.Min(
            remainingBytes,
            visibleRows * HexViewportSnapshot.BytesPerRow);
        ReadOnlySpan<byte> before = replay.BeforeBytes.Span.Slice(replayStart, bytesToProject);
        ReadOnlySpan<byte> after = replay.AfterBytes.Span.Slice(replayStart, bytesToProject);
        long differenceEndExclusive = checked(differenceStart + differenceLength);
        var rows = new HexViewportRow[visibleRows];
        for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            int byteOffset = rowIndex * HexViewportSnapshot.BytesPerRow;
            int rowLength = Math.Min(HexViewportSnapshot.BytesPerRow, bytesToProject - byteOffset);
            long rowAddress = checked(startAddress + byteOffset);
            var cells = new HexViewportCell[rowLength];
            for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                long address = checked(rowAddress + cellIndex);
                byte beforeValue = before[byteOffset + cellIndex];
                byte afterValue = after[byteOffset + cellIndex];
                HexViewportCellDecoration decorations =
                    address >= differenceStart &&
                    address < differenceEndExclusive &&
                    beforeValue != afterValue
                        ? HexViewportCellDecoration.DataChange
                        : HexViewportCellDecoration.None;
                cells[cellIndex] = new HexViewportCell(
                    address,
                    afterValue,
                    beforeValue,
                    decorations);
            }

            rows[rowIndex] = HexViewportRow.CreateOwned(rowAddress, cells);
        }

        return HexViewportSnapshot.CreateOwned(
            HexViewportCapabilityProfile.ReportDiff,
            outputSpaceId,
            documentLength,
            startAddress,
            rows,
            selectedAddress,
            showComparisonRows: showOriginalRows,
            decorationVersion: 0);
    }
}
