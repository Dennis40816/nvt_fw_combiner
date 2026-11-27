// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledRegionAccessContract
{
    /// <summary>
    /// Returns bytes that no profile-authorized target can cover. Whole-region and Parts
    /// grants retain their atomicity; processor-specific authority does not grant authoring access.
    /// The caller supplies the exact canonical map retained by the compiled composition.
    /// </summary>
    public IReadOnlyList<CompiledReadOnlyRange> GetReadOnlyRanges(FirmwareImageMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var regions = map.Regions.ToDictionary(static region => region.RegionId, StringComparer.Ordinal);
        if (Requirements.Any(requirement => requirement.GoverningRegionChain.Any(region =>
            !regions.TryGetValue(region.RegionId, out FirmwareRegion? actual) || !ReferenceEquals(region, actual))))
        {
            throw new ArgumentException("Read-only projection requires the exact compiled physical map.", nameof(map));
        }

        List<ByteRange> candidates = [];
        foreach (FirmwareRegion region in map.Regions)
        {
            candidates.Add(region.Range);
            if (GetAlignedInterior(region.Range, regions) is { } aligned)
            {
                candidates.Add(aligned);
            }
        }
        ByteRange[] writable = [.. candidates.Distinct().Where(range => GetTargetWriteRestriction(range, map.AddressSpaceId, regions) is null)];
        long[] boundaries = [.. map.Regions.SelectMany(static region => new[] { region.Range.Start, region.Range.EndExclusive })
            .Concat(writable.SelectMany(static range => new[] { range.Start, range.EndExclusive })).Distinct().Order()];
        List<CompiledReadOnlyRange> readOnly = [];
        for (int index = 1; index < boundaries.Length; index++)
        {
            var range = ByteRange.FromStartEndExclusive(boundaries[index - 1], boundaries[index]);
            if (!writable.Any(candidate => candidate.Contains(range)) &&
                GetTargetWriteRestriction(range, map.AddressSpaceId, regions) is { } denial)
            {
                readOnly.Add(denial);
            }
        }
        return Array.AsReadOnly(readOnly.ToArray());
    }

    internal CompiledReadOnlyRange? GetTargetWriteRestriction(ByteRange range, string addressSpaceId,
        IReadOnlyDictionary<string, FirmwareRegion> regions)
    {
        _ = TryResolveGoverningRegionChain(range, regions, out FirmwareRegion[] chain);
        FirmwareRegion region = chain[^1];
        CompiledRegionAccessRequirement[] applicable = [.. Requirements
            .Where(rule => rule.GoverningRegionChain[^1].Range.Contains(range))
            .OrderBy(static rule => rule.GoverningRegionChain[^1].Range.Length)
            .ThenBy(static rule => rule.RegionId, StringComparer.Ordinal)];
        if (applicable.Length == 0)
        {
            return new(region, new FirmwareAddressedRange(addressSpaceId, range),
                CompiledReadOnlyReasonKind.NoAccessRule, region);
        }
        foreach (CompiledRegionAccessRequirement rule in applicable)
        {
            if (!AllowsProfileTarget(rule, range, regions))
            {
                return new(region, new FirmwareAddressedRange(addressSpaceId, range),
                    CompiledReadOnlyReasonKind.ProfileAccess, rule.GoverningRegionChain[^1], rule);
            }
        }
        foreach (FirmwareRegion physical in chain)
        {
            if (!AllowsPhysicalTarget(physical, range, regions))
            {
                return new(region, new FirmwareAddressedRange(addressSpaceId, range),
                    CompiledReadOnlyReasonKind.PhysicalWriteConstraint, physical);
            }
        }
        return null;
    }

    private ByteRange? GetAlignedInterior(ByteRange range, IReadOnlyDictionary<string, FirmwareRegion> regions)
    {
        _ = TryResolveGoverningRegionChain(range, regions, out FirmwareRegion[] chain);
        IEnumerable<int> alignments = chain.Where(static region => region.WriteConstraint == FirmwareWriteConstraint.ExplicitRange)
            .Select(static region => region.Alignment).Concat(Requirements.Where(rule => rule.Access == RegionAccessKind.ExplicitRange &&
                rule.GoverningRegionChain[^1].Range.Contains(range)).Select(static rule => rule.GoverningRegionChain[^1].Alignment));
        long alignment = 1;
        foreach (int next in alignments)
        {
            long left = alignment;
            long right = next;
            while (right != 0)
            {
                (left, right) = (right, left % right);
            }
            long factor = next / left;
            if (alignment > range.EndExclusive / factor)
            {
                return null;
            }
            alignment *= factor;
        }
        long startRemainder = range.Start % alignment;
        long padding = startRemainder == 0 ? 0 : alignment - startRemainder;
        if (padding >= range.Length)
        {
            return null;
        }
        long start = checked(range.Start + padding);
        long end = range.EndExclusive - (range.EndExclusive % alignment);
        return end > start ? ByteRange.FromStartEndExclusive(start, end) : null;
    }

    private static bool AllowsProfileTarget(CompiledRegionAccessRequirement rule, ByteRange targetRange,
        IReadOnlyDictionary<string, FirmwareRegion> regions)
    {
        FirmwareRegion region = rule.GoverningRegionChain[^1];
        return rule.Access switch
        {
            RegionAccessKind.Hidden or RegionAccessKind.ReadOnly => false,
            RegionAccessKind.Whole => region.Range == targetRange,
            RegionAccessKind.Parts => rule.AllowedSubregionIds.Any(id => regions[id].Range == targetRange &&
                StringComparer.Ordinal.Equals(regions[id].ParentRegionId, region.RegionId)),
            RegionAccessKind.ExplicitRange => region.Range.Contains(targetRange) && IsAligned(targetRange, region.Alignment),
            _ => throw new InvalidOperationException("Validated V2 lowering encountered an unknown region access kind."),
        };
    }

    internal static bool AllowsPhysicalTarget(FirmwareRegion region, ByteRange targetRange,
        IReadOnlyDictionary<string, FirmwareRegion> regions)
    {
        return region.WriteConstraint switch
        {
            FirmwareWriteConstraint.Forbidden => false,
            FirmwareWriteConstraint.WholeRegion => region.Range == targetRange,
            FirmwareWriteConstraint.DeclaredSubregions => regions.Values.Any(child =>
                StringComparer.Ordinal.Equals(child.ParentRegionId, region.RegionId) && child.Range == targetRange),
            FirmwareWriteConstraint.ExplicitRange => region.Range.Contains(targetRange) && IsAligned(targetRange, region.Alignment),
            _ => throw new InvalidOperationException("Validated V2 lowering encountered an unknown firmware write constraint."),
        };
    }

    private static bool IsAligned(ByteRange range, int alignment)
    {
        return range.Start % alignment == 0 && range.Length % alignment == 0;
    }

    internal static bool TryResolveGoverningRegionChain(ByteRange range,
        IReadOnlyDictionary<string, FirmwareRegion> regions, out FirmwareRegion[] chain)
    {
        FirmwareRegion? deepest = regions.Values.Where(region => region.Range.Contains(range))
            .OrderBy(static region => region.Range.Length).ThenBy(static region => region.RegionId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (deepest is null)
        {
            chain = [];
            return false;
        }
        List<FirmwareRegion> reverse = [];
        for (FirmwareRegion? current = deepest; current is not null;)
        {
            reverse.Add(current);
            current = current.ParentRegionId is { } parentId ? regions[parentId] : null;
        }
        reverse.Reverse();
        chain = [.. reverse];
        return true;
    }
}
