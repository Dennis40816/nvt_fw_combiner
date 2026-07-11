using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Closed logical-view selector kind.</summary>
internal enum CompositionProfileViewSelectorKind
{
    MapRegion,
    MapRegionSlice,
    SpaceRange,
}

/// <summary>Base value for one normalized logical-view selector.</summary>
internal abstract record CompositionProfileViewSelector
{
    protected CompositionProfileViewSelector(CompositionProfileViewSelectorKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown profile view selector kind.");
        }

        Kind = kind;
    }

    internal CompositionProfileViewSelectorKind Kind { get; }
}

/// <summary>Selects one complete canonical map region.</summary>
internal sealed record MapRegionViewSelector : CompositionProfileViewSelector
{
    internal MapRegionViewSelector(string regionId)
        : base(CompositionProfileViewSelectorKind.MapRegion)
    {
        RegionId = CompositionProfileValueRules.RequireId(regionId, nameof(regionId));
    }

    internal string RegionId { get; }
}

/// <summary>Selects one checked range relative to a canonical map region.</summary>
internal sealed record MapRegionSliceViewSelector : CompositionProfileViewSelector
{
    internal MapRegionSliceViewSelector(string regionId, ByteRange relativeRange)
        : base(CompositionProfileViewSelectorKind.MapRegionSlice)
    {
        RegionId = CompositionProfileValueRules.RequireId(regionId, nameof(regionId));
        RelativeRange = CompositionProfileValueRules.RequireRange(relativeRange, nameof(relativeRange));
    }

    internal string RegionId { get; }

    internal ByteRange RelativeRange { get; }
}

/// <summary>Selects one checked range directly in the owning profile space.</summary>
internal sealed record SpaceRangeViewSelector : CompositionProfileViewSelector
{
    internal SpaceRangeViewSelector(ByteRange range)
        : base(CompositionProfileViewSelectorKind.SpaceRange)
    {
        Range = CompositionProfileValueRules.RequireRange(range, nameof(range));
    }

    internal ByteRange Range { get; }
}

/// <summary>One named logical view over a profile address space.</summary>
internal sealed record CompositionProfileView
{
    internal CompositionProfileView(
        string viewId,
        string spaceId,
        CompositionProfileViewSelector selector)
    {
        ViewId = CompositionProfileValueRules.RequireId(viewId, nameof(viewId));
        SpaceId = CompositionProfileValueRules.RequireId(spaceId, nameof(spaceId));
        ArgumentNullException.ThrowIfNull(selector);
        Selector = selector;
    }

    internal string ViewId { get; }

    internal string SpaceId { get; }

    internal CompositionProfileViewSelector Selector { get; }
}

/// <summary>Closed purpose for one profile metadata binding.</summary>
internal enum CompositionProfileMetadataPurpose
{
    MapResolution,
    Validation,
    OutputNaming,
    Display,
    Version,
}

/// <summary>One canonical metadata structure and selected fields bound to a profile space.</summary>
internal sealed class CompositionProfileMetadataBinding
{
    private readonly string[] _fieldIds;
    private readonly CompositionProfileMetadataPurpose[] _purposes;

    internal CompositionProfileMetadataBinding(
        string bindingId,
        string spaceId,
        string structureId,
        IEnumerable<string> fieldIds,
        IEnumerable<CompositionProfileMetadataPurpose> purposes)
    {
        BindingId = CompositionProfileValueRules.RequireId(bindingId, nameof(bindingId));
        SpaceId = CompositionProfileValueRules.RequireId(spaceId, nameof(spaceId));
        StructureId = CompositionProfileValueRules.RequireId(structureId, nameof(structureId));
        _fieldIds = CompositionProfileValueRules.SnapshotIds(fieldIds, nameof(fieldIds), requireValue: true);

        ArgumentNullException.ThrowIfNull(purposes);
        _purposes = [.. purposes];
        if (_purposes.Length == 0)
        {
            throw new ArgumentException("Metadata bindings require a purpose.", nameof(purposes));
        }

        if (_purposes.Any(static purpose => !Enum.IsDefined(purpose)))
        {
            throw new ArgumentOutOfRangeException(nameof(purposes), "Unknown metadata binding purpose.");
        }

        if (_purposes.Distinct().Count() != _purposes.Length)
        {
            throw new ArgumentException("Metadata binding purposes must be unique.", nameof(purposes));
        }

        Array.Sort(_purposes);
        FieldIds = Array.AsReadOnly(_fieldIds);
        Purposes = Array.AsReadOnly(_purposes);
    }

    internal string BindingId { get; }

    internal string SpaceId { get; }

    internal string StructureId { get; }

    internal IReadOnlyList<string> FieldIds { get; }

    internal IReadOnlyList<CompositionProfileMetadataPurpose> Purposes { get; }
}

/// <summary>One deny-by-default authoring access rule for a canonical map region.</summary>
internal sealed class CompositionProfileRegionAccess
{
    private readonly string[] _allowedSubregionIds;

    internal CompositionProfileRegionAccess(
        string regionId,
        RegionAccessKind access,
        string reason,
        IEnumerable<string>? allowedSubregionIds = null)
    {
        RegionId = CompositionProfileValueRules.RequireId(regionId, nameof(regionId));
        if (!Enum.IsDefined(access))
        {
            throw new ArgumentOutOfRangeException(nameof(access), access, "Unknown region access kind.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        _allowedSubregionIds = CompositionProfileValueRules.SnapshotIds(
            allowedSubregionIds ?? [],
            nameof(allowedSubregionIds),
            requireValue: access == RegionAccessKind.Parts);
        if (access != RegionAccessKind.Parts && _allowedSubregionIds.Length != 0)
        {
            throw new ArgumentException(
                "Only parts access can declare allowed subregions.",
                nameof(allowedSubregionIds));
        }

        Access = access;
        Reason = reason;
        AllowedSubregionIds = Array.AsReadOnly(_allowedSubregionIds);
    }

    internal string RegionId { get; }

    internal RegionAccessKind Access { get; }

    internal string Reason { get; }

    internal IReadOnlyList<string> AllowedSubregionIds { get; }
}
