using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>
/// One canonical instance-relative region definition that may be placed more
/// than once without repeating its internal firmware geometry.
/// </summary>
internal sealed class FirmwareRegionTemplate
{
    private readonly FirmwareRegion[] _regions;

    public FirmwareRegionTemplate(
        string templateId,
        long capacity,
        IEnumerable<FirmwareRegion> regions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _regions = ImmutableReferenceSnapshot.CreateUnique(
            regions,
            static region => region.RegionId,
            "Firmware region templates require non-null regions.",
            "Firmware region ids must be ordinally unique within a template.",
            StringComparer.Ordinal,
            requireValue: true);
        Dictionary<string, FirmwareRegion> regionsById = _regions.ToDictionary(
            static region => region.RegionId,
            StringComparer.Ordinal);
        foreach (FirmwareRegion region in _regions)
        {
            DomainInvariant.Reject(
                region.Range.EndExclusive > capacity,
                $"Relative region '{region.RegionId}' exceeds template '{templateId}'.",
                nameof(regions));

            if (region.ParentRegionId is { } parentId)
            {
                DomainInvariant.Reject(
                    !regionsById.TryGetValue(parentId, out FirmwareRegion? parent) ||
                    !parent.Range.Contains(region.Range),
                    $"Relative region '{region.RegionId}' requires one containing template-local parent.",
                    nameof(regions));
            }

        }

        _ = AcyclicDependencyGraph.Sort(
            _regions,
            region => region.ParentRegionId is { } parentId ? [regionsById[parentId]] : [],
            (region, _) => new ArgumentException(
                $"Relative region '{region.RegionId}' has a cyclic parent chain.",
                nameof(regions)));
        Array.Sort(_regions, FirmwareRangeOrdering.Compare);
        TemplateId = templateId;
        Capacity = capacity;
        Regions = Array.AsReadOnly(_regions);
    }

    public string TemplateId { get; }

    public long Capacity { get; }

    public IReadOnlyList<FirmwareRegion> Regions { get; }

}

internal sealed class FirmwareRegionInstance
{
    private readonly ReadOnlyDictionary<string, string> _resolvedRegionIds;

    public FirmwareRegionInstance(
        string instanceId,
        FirmwareRegionTemplate template,
        long baseOffset,
        string? parentRegionId,
        IReadOnlyDictionary<string, string> resolvedRegionIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentOutOfRangeException.ThrowIfNegative(baseOffset);
        if (parentRegionId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(parentRegionId);
        }

        ArgumentNullException.ThrowIfNull(resolvedRegionIds);
        var resolvedIds = resolvedRegionIds.Keys.ToHashSet(StringComparer.Ordinal);
        DomainInvariant.Reject(
            resolvedIds.Count != template.Regions.Count ||
            template.Regions.Any(region => !resolvedIds.Contains(region.RegionId)) ||
            resolvedRegionIds.Values.Any(string.IsNullOrWhiteSpace) ||
            resolvedRegionIds.Values.Distinct(StringComparer.Ordinal).Count() != resolvedRegionIds.Count,
            "A region instance requires exactly one resolved id for every template region.",
            nameof(resolvedRegionIds));

        _ = checked(baseOffset + template.Capacity);
        _resolvedRegionIds = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(resolvedRegionIds, StringComparer.Ordinal));
        InstanceId = instanceId;
        Template = template;
        BaseOffset = baseOffset;
        ParentRegionId = parentRegionId;
        ResolvedRegionIds = _resolvedRegionIds;
    }

    public string InstanceId { get; }

    public FirmwareRegionTemplate Template { get; }

    public long BaseOffset { get; }

    public string? ParentRegionId { get; }

    public IReadOnlyDictionary<string, string> ResolvedRegionIds { get; }

    internal FirmwareRegion[] ExpandRegions()
    {
        return
        [
            .. Template.Regions.Select(relative => new FirmwareRegion(
                _resolvedRegionIds[relative.RegionId],
                relative.ParentRegionId is { } relativeParentId
                    ? _resolvedRegionIds[relativeParentId]
                    : ParentRegionId,
                relative.Owner,
                relative.Kind,
                new ByteRange(
                    checked(BaseOffset + relative.Range.Start),
                    relative.Range.Length),
                relative.WriteConstraint,
                relative.Alignment)),
        ];
    }
}
