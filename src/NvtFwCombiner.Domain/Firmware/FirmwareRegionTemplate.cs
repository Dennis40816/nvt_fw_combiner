using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>One physical region whose range is relative to a named instance base.</summary>
public sealed record FirmwareRelativeRegion
{
    /// <summary>Creates one checked instance-relative physical region.</summary>
    public FirmwareRelativeRegion(
        string regionId,
        string? parentRegionId,
        FirmwareRegionOwner owner,
        FirmwareRegionKind kind,
        ByteRange range,
        FirmwareWriteConstraint writeConstraint,
        int alignment = 1)
    {
        var validated = new FirmwareRegion(
            regionId,
            parentRegionId,
            owner,
            kind,
            range,
            writeConstraint,
            alignment);
        RegionId = validated.RegionId;
        ParentRegionId = validated.ParentRegionId;
        Owner = validated.Owner;
        Kind = validated.Kind;
        Range = validated.Range;
        WriteConstraint = validated.WriteConstraint;
        Alignment = validated.Alignment;
    }

    /// <summary>Template-local region identifier.</summary>
    public string RegionId { get; }

    /// <summary>Optional template-local parent identifier.</summary>
    public string? ParentRegionId { get; }

    /// <summary>Physical byte owner.</summary>
    public FirmwareRegionOwner Owner { get; }

    /// <summary>Physical region kind.</summary>
    public FirmwareRegionKind Kind { get; }

    /// <summary>Half-open range relative to the template instance base.</summary>
    public ByteRange Range { get; }

    /// <summary>Non-relaxable physical write constraint.</summary>
    public FirmwareWriteConstraint WriteConstraint { get; }

    /// <summary>Required start and length alignment.</summary>
    public int Alignment { get; }
}

/// <summary>
/// One canonical instance-relative region definition that may be placed more
/// than once without repeating its internal firmware geometry.
/// </summary>
public sealed class FirmwareRegionTemplate
{
    private readonly FirmwareRelativeRegion[] _regions;

    /// <summary>Creates one checked relative region definition.</summary>
    public FirmwareRegionTemplate(
        string templateId,
        long capacity,
        IEnumerable<FirmwareRelativeRegion> regions)
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
        Dictionary<string, FirmwareRelativeRegion> regionsById = _regions.ToDictionary(
            static region => region.RegionId,
            StringComparer.Ordinal);
        foreach (FirmwareRelativeRegion region in _regions)
        {
            DomainInvariant.Reject(
                region.Range.EndExclusive > capacity,
                $"Relative region '{region.RegionId}' exceeds template '{templateId}'.",
                nameof(regions));

            if (region.ParentRegionId is { } parentId)
            {
                DomainInvariant.Reject(
                    !regionsById.TryGetValue(parentId, out FirmwareRelativeRegion? parent) ||
                    !parent.Range.Contains(region.Range),
                    $"Relative region '{region.RegionId}' requires one containing template-local parent.",
                    nameof(regions));
            }

            ValidateParentChain(region, regionsById);
        }

        Array.Sort(_regions, CompareRegions);
        TemplateId = templateId;
        Capacity = capacity;
        Regions = Array.AsReadOnly(_regions);
    }

    /// <summary>Stable template identifier.</summary>
    public string TemplateId { get; }

    /// <summary>Exact instance-relative template capacity.</summary>
    public long Capacity { get; }

    /// <summary>Relative regions in deterministic range order.</summary>
    public IReadOnlyList<FirmwareRelativeRegion> Regions { get; }

    private static void ValidateParentChain(
        FirmwareRelativeRegion start,
        Dictionary<string, FirmwareRelativeRegion> regionsById)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (FirmwareRelativeRegion? current = start; current?.ParentRegionId is { } parentId;)
        {
            DomainInvariant.Reject(
                !visited.Add(current.RegionId),
                $"Relative region '{start.RegionId}' has a cyclic parent chain.",
                nameof(regionsById));

            current = regionsById[parentId];
        }
    }

    private static int CompareRegions(FirmwareRelativeRegion left, FirmwareRelativeRegion right)
    {
        int range = FirmwareRangeOrdering.Compare(left.Range, right.Range);
        return range != 0
            ? range
            : StringComparer.Ordinal.Compare(left.RegionId, right.RegionId);
    }
}

/// <summary>One placement of a canonical relative-region template.</summary>
public sealed class FirmwareRegionInstance
{
    private readonly ReadOnlyDictionary<string, string> _resolvedRegionIds;

    /// <summary>Creates one checked template placement and its stable resolved ids.</summary>
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
        string[] expectedIds =
        [
            .. template.Regions
                .Select(static region => region.RegionId)
                .Order(StringComparer.Ordinal),
        ];
        string[] actualIds =
        [
            .. resolvedRegionIds.Keys.Order(StringComparer.Ordinal),
        ];
        DomainInvariant.Reject(
            !expectedIds.SequenceEqual(actualIds, StringComparer.Ordinal) ||
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

    /// <summary>Stable placement identifier.</summary>
    public string InstanceId { get; }

    /// <summary>Exact canonical relative definition used by this placement.</summary>
    public FirmwareRegionTemplate Template { get; }

    /// <summary>Absolute base in the owning region-set address space.</summary>
    public long BaseOffset { get; }

    /// <summary>Optional external parent applied to template-root regions.</summary>
    public string? ParentRegionId { get; }

    /// <summary>Template-local to map-resolved region id bindings.</summary>
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
