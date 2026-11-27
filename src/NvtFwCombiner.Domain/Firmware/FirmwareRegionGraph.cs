// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Structural input to graph validation, without physical classification or write authority.</summary>
/// <param name="RegionId">Unique region identifier, admitted by the caller's id index.</param>
/// <param name="ParentRegionId">Optional parent identifier in the same address space.</param>
/// <param name="Range">Half-open range in the containing map's address space.</param>
internal sealed record FirmwareRegionGraphNode(string RegionId, string? ParentRegionId, ByteRange Range);

/// <summary>Rules enforced by the canonical firmware image-map region graph.</summary>
internal enum FirmwareRegionGraphViolationKind
{
    /// <summary>A region ends beyond the map capacity.</summary>
    ExceedsCapacity,

    /// <summary>A parent identifier is absent from the graph.</summary>
    UnknownParent,

    /// <summary>A parent edge belongs to a cycle.</summary>
    ParentCycle,

    /// <summary>A parent does not contain a strictly smaller child range.</summary>
    ParentDoesNotProperlyContainChild,

    /// <summary>A partition contains an overlap or an implicit gap.</summary>
    PartitionDiscontinuity,

    /// <summary>A partition is empty or does not end at its containing range's end.</summary>
    PartitionIncomplete,
}

/// <summary>One graph violation, with the offending region and its parent when applicable.</summary>
/// <param name="Kind">Canonical violated rule.</param>
/// <param name="RegionId">Offending region; null only for an empty root partition.</param>
/// <param name="RelatedId">Parent identifier, or null for the map root.</param>
internal sealed record FirmwareRegionGraphViolation(
    FirmwareRegionGraphViolationKind Kind, string? RegionId, string? RelatedId)
{
    internal ArgumentException CreateException()
    {
        string subject = RelatedId is null ? "the map root" : $"region '{RelatedId}'";
        string message = Kind switch
        {
            FirmwareRegionGraphViolationKind.ExceedsCapacity =>
                $"Firmware region '{RegionId}' exceeds the map capacity.",
            FirmwareRegionGraphViolationKind.UnknownParent =>
                $"Firmware region '{RegionId}' references unknown parent '{RelatedId}'.",
            FirmwareRegionGraphViolationKind.ParentCycle =>
                "Firmware region parent relationships cannot contain cycles.",
            FirmwareRegionGraphViolationKind.ParentDoesNotProperlyContainChild =>
                $"Firmware parent '{RelatedId}' must properly contain child '{RegionId}'.",
            FirmwareRegionGraphViolationKind.PartitionDiscontinuity =>
                $"Firmware children of {subject} must partition its range without overlap or implicit gaps.",
            FirmwareRegionGraphViolationKind.PartitionIncomplete =>
                $"Firmware children of {subject} must cover its exact range.",
            _ => throw new InvalidOperationException("Unknown firmware region graph violation."),
        };
        return new ArgumentException(message, "regions");
    }
}

/// <summary>Single owner of capacity, parent-graph and complete-partition validation.</summary>
internal static class FirmwareRegionGraph
{
    /// <summary>
    /// Returns every violation in legacy rule order from a canonically ordered graph.
    /// The caller admits unique non-null ids and positive ranges in one named address space.
    /// Unknown parents prevent only checks that require that missing parent's range.
    /// </summary>
    internal static IReadOnlyList<FirmwareRegionGraphViolation> Validate(
        IReadOnlyList<FirmwareRegionGraphNode> regions, long capacityBytes)
    {
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityBytes);
        FirmwareRegionGraphNode[] ordered = [.. regions];
        Array.Sort(ordered, CompareNodes);
        Dictionary<string, FirmwareRegionGraphNode> regionsById = ordered.ToDictionary(
            static region => region.RegionId, StringComparer.Ordinal);
        List<FirmwareRegionGraphViolation> violations = [];
        foreach (FirmwareRegionGraphNode region in ordered)
        {
            if (region.Range.EndExclusive > capacityBytes)
            {
                violations.Add(new(FirmwareRegionGraphViolationKind.ExceedsCapacity, region.RegionId, null));
            }

            if (region.ParentRegionId is { } parentId && !regionsById.ContainsKey(parentId))
            {
                violations.Add(new(FirmwareRegionGraphViolationKind.UnknownParent, region.RegionId, parentId));
            }
        }

        AddParentCycles(ordered, regionsById, violations);
        foreach (FirmwareRegionGraphNode region in ordered)
        {
            if (region.ParentRegionId is { } parentId &&
                regionsById.TryGetValue(parentId, out FirmwareRegionGraphNode? parent) &&
                (parent.Range == region.Range || !parent.Range.Contains(region.Range)))
            {
                violations.Add(new(FirmwareRegionGraphViolationKind.ParentDoesNotProperlyContainChild,
                    region.RegionId, parentId));
            }
        }

        AddCompletePartitions(ordered, regionsById, capacityBytes, violations);
        return violations.AsReadOnly();
    }

    private static int CompareNodes(FirmwareRegionGraphNode left, FirmwareRegionGraphNode right)
    {
        int rangeComparison = FirmwareRangeOrdering.Compare(left.Range, right.Range);
        return rangeComparison != 0
            ? rangeComparison
            : StringComparer.Ordinal.Compare(left.RegionId, right.RegionId);
    }

    private static void AddParentCycles(
        FirmwareRegionGraphNode[] regions,
        Dictionary<string, FirmwareRegionGraphNode> regionsById,
        List<FirmwareRegionGraphViolation> violations)
    {
        HashSet<string> visited = new(StringComparer.Ordinal);
        foreach (FirmwareRegionGraphNode region in regions)
        {
            List<FirmwareRegionGraphNode> path = [];
            Dictionary<string, int> positions = new(StringComparer.Ordinal);
            FirmwareRegionGraphNode? current = region;
            while (current is not null && !visited.Contains(current.RegionId))
            {
                if (positions.TryGetValue(current.RegionId, out int cycleStart))
                {
                    foreach (FirmwareRegionGraphNode member in path.Skip(cycleStart))
                    {
                        violations.Add(new(FirmwareRegionGraphViolationKind.ParentCycle,
                            member.RegionId, member.ParentRegionId));
                    }

                    break;
                }

                positions.Add(current.RegionId, path.Count);
                path.Add(current);
                current = current.ParentRegionId is { } parentId
                    ? regionsById.GetValueOrDefault(parentId)
                    : null;
            }

            visited.UnionWith(path.Select(static member => member.RegionId));
        }
    }

    private static void AddCompletePartitions(
        FirmwareRegionGraphNode[] regions,
        Dictionary<string, FirmwareRegionGraphNode> regionsById,
        long capacityBytes,
        List<FirmwareRegionGraphViolation> violations)
    {
        List<FirmwareRegionGraphNode> roots = [];
        Dictionary<string, List<FirmwareRegionGraphNode>> childrenByParent = new(StringComparer.Ordinal);
        foreach (FirmwareRegionGraphNode region in regions)
        {
            if (region.ParentRegionId is not { } parentId)
            {
                roots.Add(region);
                continue;
            }

            if (!childrenByParent.TryGetValue(parentId, out List<FirmwareRegionGraphNode>? children))
            {
                children = [];
                childrenByParent.Add(parentId, children);
            }

            children.Add(region);
        }

        AddPartition(roots, new ByteRange(0, capacityBytes), null, violations);
        foreach (KeyValuePair<string, List<FirmwareRegionGraphNode>> entry in childrenByParent)
        {
            if (regionsById.TryGetValue(entry.Key, out FirmwareRegionGraphNode? parent))
            {
                AddPartition(entry.Value, parent.Range, entry.Key, violations);
            }
        }
    }

    private static void AddPartition(
        List<FirmwareRegionGraphNode> regions,
        ByteRange expectedRange,
        string? parentId,
        List<FirmwareRegionGraphViolation> violations)
    {
        long coveredUntil = expectedRange.Start;
        foreach (FirmwareRegionGraphNode region in regions)
        {
            if (region.Range.Start != coveredUntil)
            {
                violations.Add(new(FirmwareRegionGraphViolationKind.PartitionDiscontinuity, region.RegionId, parentId));
            }

            coveredUntil = region.Range.EndExclusive;
        }

        if (regions.Count == 0 || coveredUntil != expectedRange.EndExclusive)
        {
            violations.Add(new(FirmwareRegionGraphViolationKind.PartitionIncomplete,
                regions.Count == 0 ? null : regions[^1].RegionId, parentId));
        }
    }
}
