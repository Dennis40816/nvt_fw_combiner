using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

internal static class FirmwareRangeOrdering
{
    internal static int Compare(ByteRange left, ByteRange right)
    {
        int start = left.Start.CompareTo(right.Start);
        return start != 0 ? start : right.Length.CompareTo(left.Length);
    }
}
