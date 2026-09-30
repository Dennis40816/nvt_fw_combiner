using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.MemoryLayout;

/// <summary>A declared DP-owned sub-field in the output address space.</summary>
public sealed record MemoryLayoutSectionField(FirmwareRegion CanonicalRegion, string AddressSpaceId, ByteRange Range);

/// <summary>Immutable location context from an already-admitted companion map, not an operation.</summary>
public sealed record MemoryLayoutSectionLocator
{
    internal MemoryLayoutSectionLocator(
        string addressSpaceId, ByteRange range, FirmwareImageMap? map, FirmwareRegion? region,
        MemoryLayoutBankLocator? bank = null, IReadOnlyList<MemoryLayoutSectionField>? fields = null)
    {
        AddressSpaceId = addressSpaceId;
        Range = range;
        MapId = map?.MapId;
        CanonicalRegion = region;
        Bank = bank;
        Fields = Array.AsReadOnly([.. fields ?? []]);
        IsImageContainer = region?.Kind == FirmwareRegionKind.Image;
        IsImageOverlay = region is not null && map is not null && region.Kind == FirmwareRegionKind.Code &&
            HasImageAncestor(region, map);
        ContentRole = region?.Kind == FirmwareRegionKind.Unmapped
            ? MemoryContentRole.Unmapped
            : region?.Owner == FirmwareRegionOwner.Tp ? MemoryContentRole.Tp
            : region?.Owner == FirmwareRegionOwner.Dp ? MemoryContentRole.Dp
            : MemoryContentRole.General;
    }

    /// <summary>Physical output address space.</summary>
    public string AddressSpaceId { get; }
    /// <summary>Exact half-open visible range.</summary>
    public ByteRange Range { get; }
    /// <summary>Exact companion map, or null when no unambiguous counterpart is available.</summary>
    public string? MapId { get; }
    /// <summary>Declared region reference, or null for neutral context.</summary>
    public FirmwareRegion? CanonicalRegion { get; }
    /// <summary>Canonical bank placement when the region belongs to a local companion map.</summary>
    public MemoryLayoutBankLocator? Bank { get; }
    /// <summary>TP, DP, explicit Unmapped, or neutral General context.</summary>
    public MemoryContentRole ContentRole { get; }
    /// <summary>The canonical section is an image container, not standalone firmware code.</summary>
    public bool IsImageContainer { get; }
    /// <summary>The section is code declared inside an image owned by another firmware component.</summary>
    public bool IsImageOverlay { get; }
    /// <summary>Exact declared DP sub-fields, including checked bank placement.</summary>
    public IReadOnlyList<MemoryLayoutSectionField> Fields { get; }

    private static bool HasImageAncestor(FirmwareRegion region, FirmwareImageMap map)
    {
        string? parentId = region.ParentRegionId;
        while (parentId is not null && map.Regions.FirstOrDefault(item => item.RegionId == parentId) is { } parent)
        {
            if (parent.Kind == FirmwareRegionKind.Image && parent.Owner == FirmwareRegionOwner.Dp &&
                region.Owner == FirmwareRegionOwner.Tp) { return true; }
            parentId = parent.ParentRegionId;
        }
        return false;
    }
}

public static partial class MemoryLayoutProjector
{
    private static IReadOnlyList<MemoryLayoutSectionLocator> ProjectSections(
        ResolvedCapability capability, string addressSpaceId, long capacity)
    {
        if (capability.CompiledComposition.V2Details.ExperienceId != ExperienceIds.CtrlRamReplace)
        {
            return [];
        }

        var output = new ByteRange(0, capacity);
        if (capability.CompiledComposition.V2Details.Provenance.Context is RuntimeReferenceBankReplaceV2CompilationContext bankProjection &&
            capability.MemoryLayoutContext is { } companion)
        {
            IReadOnlyList<MemoryLayoutSectionLocator> localSections = ProjectMapSections(
                companion.Map, addressSpaceId, companion.Map.CapacityBytes);
            return [.. ProjectBankLocators(bankProjection, addressSpaceId, capacity).SelectMany(bank =>
                localSections.Select(section => new MemoryLayoutSectionLocator(addressSpaceId,
                    new ByteRange(checked(bank.Range.Start + section.Range.Start), section.Range.Length),
                    companion.Map, section.CanonicalRegion, bank,
                    [.. section.Fields.Select(field => field with
                    {
                        Range = new ByteRange(checked(bank.Range.Start + field.Range.Start), field.Range.Length),
                    })]))),
                .. bankProjection.SourceEnvelope is { } envelope && envelope.ActualOutputLength > envelope.LayoutTemplateCapacity
                    ? new[] { new MemoryLayoutSectionLocator(addressSpaceId,
                        new ByteRange(envelope.LayoutTemplateCapacity,
                            envelope.ActualOutputLength - envelope.LayoutTemplateCapacity), null, null) }
                    : []];
        }
        FirmwareImageMap[] maps = capability.CompiledComposition.V2Details.Provenance.Context is RuntimeReferenceBankReplaceV2CompilationContext bankContext
            ? [bankContext.ResolvedMap.ImageMap]
            : capability.MemoryLayoutContext is { } explicitContext ? [explicitContext.Map] :
        [
            .. capability.MetadataPlan.Definition.Entries
                .Where(static entry => entry.Purposes.Contains(MetadataReferencePurpose.ReportClassification))
                .Select(static entry => entry.ResolvedMap.ImageMap).Distinct(),
        ];
        MemoryLayoutSectionLocator[] context = [new(addressSpaceId, output, null, null)];
        return maps.Length != 1 || maps[0].AddressSpaceId != addressSpaceId
            ? context : ProjectMapSections(maps[0], addressSpaceId, capacity);
    }

    internal static IReadOnlyList<MemoryLayoutSectionLocator> ProjectMapSections(
        FirmwareImageMap map, string addressSpaceId, long capacity)
    {
        var output = new ByteRange(0, capacity);
        MemoryLayoutSectionLocator[] context = [new(addressSpaceId, output, null, null)];
        FirmwareRegion[] codes =
        [
            .. map.Regions.Where(region => (region.Kind == FirmwareRegionKind.Code ||
                (region.Kind == FirmwareRegionKind.Image && region.Owner == FirmwareRegionOwner.Dp)) &&
                region.Owner is FirmwareRegionOwner.Tp or FirmwareRegionOwner.Dp &&
                output.Contains(region.Range)),
        ];
        Dictionary<string, FirmwareRegion> byId = map.Regions.ToDictionary(static region => region.RegionId);
        codes = [.. codes.Where(region => !HasAncestor(region, codes, byId, sameOwner: true))];
        FirmwareRegion[] fields = [.. map.Regions.Where(region => region.Owner == FirmwareRegionOwner.Dp &&
            region.Kind is FirmwareRegionKind.Command or FirmwareRegionKind.Header or FirmwareRegionKind.FirmwareConfig or
                FirmwareRegionKind.CustomerInformation or FirmwareRegionKind.Checksum && output.Contains(region.Range))];
        Dictionary<string, FirmwareRegion?> owners = codes.Concat(fields).ToDictionary(static region => region.RegionId,
            region => DeclaredSectionOwner(region, codes, byId), StringComparer.Ordinal);
        // Sibling fields share a declared parent and DP owner with code. A field with no declared
        // DP parent or code sibling stays neutral until its companion map supplies that authority.
        codes = [.. codes, .. fields.Where(field => owners[field.RegionId] is not null && !HasAncestor(field, codes, byId))];
        if (codes.Any(left => codes.Any(right => !ReferenceEquals(left, right) && left.Range.Overlaps(right.Range) &&
            !HasAncestor(left, [right], byId) && !HasAncestor(right, [left], byId))))
        {
            return context;
        }

        FirmwareRegion[] gaps = [.. map.Regions.Where(region =>
            region.Kind == FirmwareRegionKind.Unmapped && output.Contains(region.Range) &&
            !HasAncestor(region, codes, byId))];
        gaps = [.. gaps.Where(region => !HasAncestor(region, gaps, byId))];
        // Fine-grained descendants belong to the outer firmware section. An unrelated overlap is not a priority rule.
        if (gaps.Any(gap => codes.Any(code => gap.Range.Overlaps(code.Range)) ||
            gaps.Any(other => !ReferenceEquals(gap, other) && gap.Range.Overlaps(other.Range))))
        {
            return context;
        }
        long[] boundaries = [.. codes.Concat(gaps).SelectMany(static region =>
            new[] { region.Range.Start, region.Range.EndExclusive }).Append(0).Append(capacity).Distinct().Order()];
        var result = new List<MemoryLayoutSectionLocator>();
        for (int i = 1; i < boundaries.Length; i++)
        {
            var range = new ByteRange(boundaries[i - 1], boundaries[i] - boundaries[i - 1]);
            FirmwareRegion? region = gaps.FirstOrDefault(candidate => candidate.Range.Contains(range)) ??
                codes.FirstOrDefault(candidate => candidate.Range.Contains(range) &&
                    !codes.Any(child => child.Range.Contains(range) && HasAncestor(child, [candidate], byId)));
            if (region is not null && owners.TryGetValue(region.RegionId, out FirmwareRegion? owner)) { region = owner; }
            if (result.Count > 0 && ReferenceEquals(result[^1].CanonicalRegion, region))
            {
                range = new ByteRange(result[^1].Range.Start, range.EndExclusive - result[^1].Range.Start);
                region = result[^1].CanonicalRegion;
                result.RemoveAt(result.Count - 1);
            }
            MemoryLayoutSectionField[] sectionFields = region?.Owner == FirmwareRegionOwner.Dp
                ? [.. fields.Where(field => ReferenceEquals(owners[field.RegionId], region) &&
                    range.Contains(field.Range)).Select(field => new MemoryLayoutSectionField(field, addressSpaceId, field.Range))]
                : [];
            result.Add(new MemoryLayoutSectionLocator(addressSpaceId, range, map, region, fields: sectionFields));
        }
        return result;
    }

    private static FirmwareRegion? DeclaredSectionOwner(
        FirmwareRegion region, FirmwareRegion[] sections, Dictionary<string, FirmwareRegion> byId)
    {
        if (region.Owner != FirmwareRegionOwner.Dp || region.Kind == FirmwareRegionKind.Image) { return region; }
        FirmwareRegion? ancestor = sections.FirstOrDefault(section => section.Owner == FirmwareRegionOwner.Dp &&
            HasAncestor(region, [section], byId));
        if (ancestor is not null) { return DeclaredSectionOwner(ancestor, sections, byId); }
        // Keep separate banks and image containers distinct even when their ranges touch.
        return region.ParentRegionId is { } parent
            ? sections.Where(section => section.Owner == FirmwareRegionOwner.Dp && section.Kind == FirmwareRegionKind.Code &&
                section.ParentRegionId == parent).OrderBy(static section => section.Range.Start).FirstOrDefault()
            : region.Kind == FirmwareRegionKind.Code ? region : null;
    }

    private static bool HasAncestor(
        FirmwareRegion region, FirmwareRegion[] candidates, Dictionary<string, FirmwareRegion> byId, bool sameOwner = false)
    {
        string? parent = region.ParentRegionId;
        while (parent is not null && byId.TryGetValue(parent, out FirmwareRegion? ancestor))
        {
            if ((!sameOwner || ancestor.Owner == region.Owner) && candidates.Contains(ancestor))
            {
                return true;
            }
            parent = ancestor.ParentRegionId;
        }
        return false;
    }
}
