using System.Security.Cryptography;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static string ActionLabel(CompositionOperationKind kind)
    {
        return kind switch
        {
            CompositionOperationKind.CopyRange => "Copy",
            CompositionOperationKind.ReplaceRange => "Replace",
            CompositionOperationKind.FillRange => "Fill",
            CompositionOperationKind.PatchScalar => "Patch",
            CompositionOperationKind.RunExternalProcessor => "Postbuild",
            _ => kind.ToString(),
        };
    }

    private static string AddressSpaceLabel(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            "dp-input" => "DP BIN",
            "tp-input" => "TP BIN",
            "ld-input" => "LD BIN",
            "reference-base" => "Base flash",
            "dp-replacement" => "DP replacement",
            "ldc-replacement" => "LDC replacement",
            "output-image" => "Output",
            _ => addressSpaceId,
        };
    }

    private static bool IsPreservedRegion(TpFlashMapRegion region)
    {
        return region.Kind == TpFlashMapRegionKind.CustomerInfo ||
            region.Tags.Contains("preserve", StringComparer.OrdinalIgnoreCase);
    }

    private static string ActionSummaryForReplaceMode(string replaceMode)
    {
        return replaceMode switch
        {
            WorkbenchReplaceModes.Dp => "profile policy controls padding",
            WorkbenchReplaceModes.CtrlRam => "postbuild refreshes CRC/header",
            _ => "profile validation controls write access",
        };
    }

    private static CoverageSegment[] ApplyCoverageWrite(
        IReadOnlyList<CoverageSegment> current,
        CoverageSegment write)
    {
        List<CoverageSegment> next = [];
        foreach (CoverageSegment segment in current)
        {
            if (!segment.Range.Overlaps(write.Range))
            {
                next.Add(segment);
                continue;
            }

            if (segment.Range.Start < write.Range.Start)
            {
                next.Add(segment with
                {
                    Range = ByteRange.FromStartEndExclusive(segment.Range.Start, write.Range.Start),
                });
            }

            long overlapStart = Math.Max(segment.Range.Start, write.Range.Start);
            long overlapEnd = Math.Min(segment.Range.EndExclusive, write.Range.EndExclusive);
            next.Add(write with
            {
                Range = ByteRange.FromStartEndExclusive(overlapStart, overlapEnd),
            });

            if (write.Range.EndExclusive < segment.Range.EndExclusive)
            {
                next.Add(segment with
                {
                    Range = ByteRange.FromStartEndExclusive(write.Range.EndExclusive, segment.Range.EndExclusive),
                });
            }
        }

        return [.. MergeAdjacentCoverage(next.OrderBy(segment => segment.Range.Start))];
    }

    private static IEnumerable<CoverageSegment> MergeAdjacentCoverage(IEnumerable<CoverageSegment> ordered)
    {
        CoverageSegment? pending = null;
        foreach (CoverageSegment segment in ordered)
        {
            if (pending is null)
            {
                pending = segment;
                continue;
            }

            if (pending.Range.EndExclusive == segment.Range.Start &&
                string.Equals(pending.SourceLabel, segment.SourceLabel, StringComparison.Ordinal) &&
                string.Equals(pending.Detail, segment.Detail, StringComparison.Ordinal) &&
                string.Equals(pending.Fill, segment.Fill, StringComparison.Ordinal))
            {
                pending = pending with
                {
                    Range = ByteRange.FromStartEndExclusive(pending.Range.Start, segment.Range.EndExclusive),
                };
                continue;
            }

            yield return pending;
            pending = segment;
        }

        if (pending is not null)
        {
            yield return pending;
        }
    }

    private static string CoverageFill(string sourceLabel)
    {
        return sourceLabel switch
        {
            "DP BIN" => "#2563EB",
            "Changed DP BIN" => "#2563EB",
            "TP BIN" => "#16A34A",
            "LD BIN" => "#F97316",
            "Changed LDC BIN" => "#F97316",
            "CtrlRAM BIN" => "#16A34A",
            "Changed CtrlRAM BIN" => "#16A34A",
            "Source BIN" => "#0D9488",
            "Restored TP" => "#64748B",
            "Preserved customer info" => "#64748B",
            "Preserve" => "#64748B",
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

    private static double WidthForRange(ByteRange range, long capacity)
    {
        const double maxWidth = 300;
        return Math.Max(8, Math.Round(maxWidth * range.Length / capacity, 1));
    }

    private static string FormatFullRange(long capacity)
    {
        return capacity <= 0 ? "No range" : FormatDisplayRange(new ByteRange(0, capacity));
    }

    private static string FormatDisplayRange(ByteRange range)
    {
        return FormattableString.Invariant($"0x{range.Start:X5}-0x{range.EndExclusive - 1:X5} (len 0x{range.Length:X})");
    }

    private static IReadOnlyList<WorkbenchMemoryCoverageSegment> ToWorkbenchCoverageSegments(
        IEnumerable<CoverageSegment> segments,
        long capacity)
    {
        return
        [
            .. segments.Select(segment => new WorkbenchMemoryCoverageSegment(
                FormatDisplayRange(segment.Range),
                segment.SourceLabel,
                segment.Detail,
                segment.Fill,
                WidthForRange(segment.Range, capacity),
                segment.IsChanged)),
        ];
    }

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
