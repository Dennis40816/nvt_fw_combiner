using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    private static MemoryMapRowViewModel ToMemoryMapRow(WorkbenchMemoryMapRow row)
    {
        return new MemoryMapRowViewModel(
            row.RangeLabel,
            row.BeforeSource,
            row.ActionLabel,
            row.AfterSource,
            row.Detail);
    }

    private static MemoryCoverageSegmentViewModel ToMemoryCoverageSegment(WorkbenchMemoryCoverageSegment segment)
    {
        return new(
            segment.RangeLabel,
            segment.SourceLabel,
            segment.Detail,
            segment.Fill,
            segment.BarWidth,
            segment.IsChanged);
    }

    private static string ToRange(long start, long length)
    {
        return FormattableString.Invariant($"0x{start:X5}-0x{start + length - 1:X5}");
    }

    private static string ToLength(long length)
    {
        return FormattableString.Invariant($"len 0x{length:X}");
    }
}
