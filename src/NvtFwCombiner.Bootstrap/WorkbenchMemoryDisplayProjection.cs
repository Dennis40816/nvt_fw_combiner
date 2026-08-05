using System.Security.Cryptography;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static class WorkbenchMemoryDisplayProjection
{
    internal static string GeneralMergeSourceLabel(GeneralMappingDraftRow mapping)
    {
        return string.IsNullOrWhiteSpace(mapping.Source.Reference)
            ? "Source BIN"
            : Path.GetFileName(mapping.Source.Reference);
    }

    internal static string ActionLabel(CompositionOperationKind kind)
    {
        return kind switch
        {
            CompositionOperationKind.CopyRange => "Copy",
            CompositionOperationKind.ReplaceRange => "Replace",
            CompositionOperationKind.FillRange => "Fill",
            CompositionOperationKind.PatchScalar => "Patch",
            CompositionOperationKind.TransformScalar => "Transform",
            CompositionOperationKind.RunExternalProcessor => "Postbuild",
            _ => kind.ToString(),
        };
    }

    internal static string AddressSpaceLabel(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            CompositionAddressSpaceIds.DpInput => "DP BIN",
            CompositionAddressSpaceIds.TpInput => "TP BIN",
            CompositionAddressSpaceIds.LdcInput => "LDC BIN",
            CompositionAddressSpaceIds.ReferenceBase => "Base flash",
            CompositionAddressSpaceIds.DpReplacement => "DP replacement",
            CompositionAddressSpaceIds.LdcReplacement => "LDC replacement",
            CompositionAddressSpaceIds.DpAbInput => "DP_AB BIN",
            CompositionAddressSpaceIds.TpAInput => "TPA BIN",
            CompositionAddressSpaceIds.TpBInput => "TPB BIN",
            CompositionAddressSpaceIds.TpBWork => "TPB work buffer",
            "a-bank-work" => "A bank work",
            "b-bank-work" => "B bank work",
            "ab-combiner-work" => "Postbuild AB work",
            CompositionAddressSpaceIds.OutputImage => "Output",
            _ => addressSpaceId,
        };
    }

    internal static CoverageSegment[] ApplyCoverageWrite(
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
                string.Equals(pending.Detail, segment.Detail, StringComparison.Ordinal))
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

    internal static string FormatFullRange(long capacity)
    {
        return capacity <= 0 ? "No range" : FormatDisplayRange(new ByteRange(0, capacity));
    }

    internal static string FormatDisplayRange(ByteRange range)
    {
        return FormattableString.Invariant($"0x{range.Start:X5}-0x{range.EndExclusive - 1:X5} (len 0x{range.Length:X})");
    }

    internal static IReadOnlyList<WorkbenchMemoryCoverageSegment> ToWorkbenchCoverageSegments(
        IEnumerable<CoverageSegment> segments,
        long capacity)
    {
        return
        [
            .. segments.Select(segment => new WorkbenchMemoryCoverageSegment(
                segment.Range,
                UnresolvedRangeLabel: null,
                segment.SourceLabel,
                segment.Detail,
                capacity,
                segment.IsChanged,
                segment.Role,
                segment.RegionId,
                segment.IsDiffDlm,
                segment.PreservationDetails,
                segment.RegionGroup)),
        ];
    }

    internal static WorkbenchMemoryDisplay CreateMessageDisplay(
        string rangeLabel,
        (string Range, string Before, string Action, string After, string Detail) row,
        (string Range, string Source, string Detail)? coverage)
    {
        return new(
            rangeLabel,
            [new WorkbenchMemoryMapRow(row.Range, row.Before, row.Action, row.After, row.Detail)],
            coverage is { } item
                ? [new WorkbenchMemoryCoverageSegment(
                    Range: null,
                    item.Range,
                    item.Source,
                    item.Detail,
                    DisplayCapacity: 0,
                    false,
                    WorkbenchMemoryCoverageRole.Standard)]
                : []);
    }

}

internal static class WorkbenchArtifactIdentity
{
    internal static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

internal sealed record CoverageSegment(
    ByteRange Range,
    string SourceLabel,
    string Detail,
    bool IsChanged,
    WorkbenchMemoryCoverageRole Role,
    string? RegionId = null,
    bool IsDiffDlm = false,
    IReadOnlyList<MemoryLayoutPreservationDetail>? PreservationDetails = null,
    WorkbenchReplaceRegionGroup RegionGroup = WorkbenchReplaceRegionGroup.Common);
