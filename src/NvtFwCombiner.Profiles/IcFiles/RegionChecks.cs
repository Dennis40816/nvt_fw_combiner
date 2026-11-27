// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.IcFiles;

// Ranges share the containing layout's named address space; ids are admitted by IcFileIdIndex.
internal sealed record ResolvedRegion(string Id, ByteRange Range, string? ParentId, SourceMark Mark);

internal static class RegionChecks
{
    internal static ImmutableArray<IcFileError> Check(
        string layoutId, long layoutSize, IReadOnlyList<ResolvedRegion> regions)
    {
        Dictionary<string, SourceMark> marks = regions.ToDictionary(
            static region => region.Id, static region => region.Mark, StringComparer.Ordinal);
        IReadOnlyList<FirmwareRegionGraphViolation> violations = FirmwareRegionGraph.Validate(
            [.. regions.Select(static region => new FirmwareRegionGraphNode(region.Id, region.ParentId, region.Range))],
            layoutSize);
        return [.. violations.Select(violation => new IcFileError(
            ProjectCode(violation.Kind),
            violation.RegionId is { } regionId ? marks[regionId] : new SourceMark(1, 1),
            violation.RegionId ?? layoutId,
            violation.RelatedId ?? layoutId))];
    }

    private static IcFileErrorCode ProjectCode(FirmwareRegionGraphViolationKind kind)
    {
        return kind switch
        {
            FirmwareRegionGraphViolationKind.ExceedsCapacity => IcFileErrorCode.RegionOutsideLayout,
            FirmwareRegionGraphViolationKind.UnknownParent => IcFileErrorCode.DanglingReference,
            FirmwareRegionGraphViolationKind.ParentCycle => IcFileErrorCode.RegionParentCycle,
            FirmwareRegionGraphViolationKind.ParentDoesNotProperlyContainChild => IcFileErrorCode.RegionOutsideParent,
            FirmwareRegionGraphViolationKind.PartitionDiscontinuity => IcFileErrorCode.RegionPartitionDiscontinuity,
            FirmwareRegionGraphViolationKind.PartitionIncomplete => IcFileErrorCode.RegionPartitionIncomplete,
            _ => throw new InvalidOperationException("Unknown firmware region graph violation."),
        };
    }
}
