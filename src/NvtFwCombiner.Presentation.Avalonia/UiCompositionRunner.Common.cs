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

    private static MemoryCoverageSegmentViewModel ToMemoryCoverageSegment(
        WorkbenchMemoryCoverageSegment segment,
        ShellTextResources? text = null)
    {
        string rangeLabel = segment.Range is { } range
            ? FormattableString.Invariant(
                $"0x{range.Start:X5}-0x{range.EndExclusive - 1:X5} (len 0x{range.Length:X})")
            : segment.UnresolvedRangeLabel ?? "No range";
        double barWidth = segment.Range is { } resolved && segment.DisplayCapacity > 0
            ? 300d * resolved.Length / segment.DisplayCapacity
            : 300d;
        return new(
            rangeLabel,
            segment.SourceLabel,
            segment.Detail,
            ResolveCoverageFill(segment.SourceLabel),
            barWidth,
            segment.IsChanged,
            segment.Role == WorkbenchMemoryCoverageRole.BaseFirmware,
            segment.RegionId,
            segment.IsDiffDlm,
            segment.PreservationDetails,
            text,
            segment.RegionGroup);
    }

    private static string ResolveCoverageFill(string sourceLabel)
    {
        return sourceLabel switch
        {
            "DP BIN" or "Changed DP BIN" or "DP_AB BIN" or "DP AB" => "#2563EB",
            "TP BIN" or "TPA BIN" or "TPA" or "A bank work" or
                "CtrlRAM BIN" or "Changed CtrlRAM BIN" => "#16A34A",
            "TPB work buffer" or "TPB" or "B bank work" or "Postbuild AB work" => "#7C3AED",
            "LDC BIN" or "Changed LDC BIN" => "#F97316",
            "Source BIN" => "#0D9488",
            "Restored TP" or "Preserved customer info" or "Preserve" => "#64748B",
            "Overlap error" => "#DC2626",
            string label when label.Contains("NF CtrlRAM", StringComparison.OrdinalIgnoreCase) => "#DC2626",
            string label when label.Contains("Normal CtrlRAM", StringComparison.OrdinalIgnoreCase) => "#0891B2",
            string label when label.Contains("MP CtrlRAM", StringComparison.OrdinalIgnoreCase) => "#7C3AED",
            string label when label.Contains("VN CtrlRAM", StringComparison.OrdinalIgnoreCase) => "#DB2777",
            string label when label.Contains("DIFF", StringComparison.OrdinalIgnoreCase) ||
                              label.Contains("DLM", StringComparison.OrdinalIgnoreCase) => "#D97706",
            string label when label.Contains("Vector", StringComparison.OrdinalIgnoreCase) => "#0D9488",
            _ => "#CBD5E1",
        };
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
