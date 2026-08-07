using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Closed profile access vocabulary retained independently from Profiles normalization types.</summary>
public enum CompiledRegionAccessKind
{
    /// <summary>The region is not authorable.</summary>
    Hidden,

    /// <summary>The region is inspectable but never writable.</summary>
    ReadOnly,

    /// <summary>Only one exact whole-region target is authorable.</summary>
    Whole,

    /// <summary>Only explicitly named direct child regions are authorable.</summary>
    Parts,

    /// <summary>Checked, aligned subranges are authorable.</summary>
    ExplicitRange,
}

/// <summary>One resolved physical constraint retained without duplicating its map range.</summary>
public sealed class CompiledPhysicalRegionConstraint
{
    internal CompiledPhysicalRegionConstraint(
        string regionId,
        FirmwareWriteConstraint writeConstraint,
        int alignment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        ClosedEnum.ThrowIfUndefined(writeConstraint, "Unknown firmware write constraint.");

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alignment);
        RegionId = regionId;
        WriteConstraint = writeConstraint;
        Alignment = alignment;
    }

    /// <summary>Canonical region id resolved from the selected map.</summary>
    public string RegionId { get; }

    /// <summary>Non-relaxable physical write constraint selected from the canonical map.</summary>
    public FirmwareWriteConstraint WriteConstraint { get; }

    /// <summary>Physical write alignment selected from the canonical map.</summary>
    public int Alignment { get; }
}

/// <summary>One logical profile view and the canonical physical ancestor chain governing its resolved range.</summary>
public sealed class CompiledResolvedPhysicalView
{
    private readonly CompiledPhysicalRegionConstraint[] _governingRegionChain;

    internal CompiledResolvedPhysicalView(
        string viewId,
        string addressSpaceId,
        ByteRange range,
        IEnumerable<CompiledPhysicalRegionConstraint> governingRegionChain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewId);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(range.Length);
        _governingRegionChain = ImmutableReferenceSnapshot.Create(
            governingRegionChain,
            "Resolved physical views require one non-empty, non-repeating region ancestor chain.",
            requireValue: true);
        DomainInvariant.Reject(
            _governingRegionChain.Select(static region => region.RegionId).Distinct(StringComparer.Ordinal).Count() !=
            _governingRegionChain.Length,
            "Resolved physical views require one non-empty, non-repeating region ancestor chain.",
            nameof(governingRegionChain));

        ViewId = viewId;
        AddressSpaceId = addressSpaceId;
        Range = range;
        GoverningRegionChain = Array.AsReadOnly(_governingRegionChain);
    }

    /// <summary>Stable profile view id.</summary>
    public string ViewId { get; }

    /// <summary>Plan address space that owns the logical view bytes.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Resolved logical half-open range validated against the plan space and canonical physical chain.</summary>
    public ByteRange Range { get; }

    /// <summary>Physical ancestor chain in root-to-leaf order; canonical map regions remain the physical range authority.</summary>
    public IReadOnlyList<CompiledPhysicalRegionConstraint> GoverningRegionChain { get; }
}

/// <summary>One profile-owned access rule resolved against the selected canonical physical map.</summary>
public sealed class CompiledRegionAccessRequirement
{
    private readonly string[] _allowedSubregionIds;
    private readonly CompiledPhysicalRegionConstraint[] _governingRegionChain;

    internal CompiledRegionAccessRequirement(
        string regionId,
        CompiledRegionAccessKind access,
        string reason,
        IEnumerable<string> allowedSubregionIds,
        IEnumerable<CompiledPhysicalRegionConstraint> governingRegionChain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        ClosedEnum.ThrowIfUndefined(access, "Unknown compiled region access kind.");

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        _allowedSubregionIds = ImmutableStringSnapshot.Create(
            allowedSubregionIds,
            nameof(allowedSubregionIds),
            access == CompiledRegionAccessKind.Parts ? "Identifiers must be non-empty values." : null,
            "Identifiers must be non-empty values.",
            "Identifiers must be ordinally unique.");
        DomainInvariant.Reject(
            access != CompiledRegionAccessKind.Parts && _allowedSubregionIds.Length != 0,
            "Only parts access can declare allowed subregions.", nameof(allowedSubregionIds));

        _governingRegionChain = ImmutableReferenceSnapshot.Create(
            governingRegionChain,
            "Compiled region access requires one non-empty, non-repeating ancestor chain ending at the target region.",
            requireValue: true);
        DomainInvariant.Reject(
            _governingRegionChain.Select(static region => region.RegionId).Distinct(StringComparer.Ordinal).Count() !=
            _governingRegionChain.Length ||
            !StringComparer.Ordinal.Equals(_governingRegionChain[^1].RegionId, regionId),
            "Compiled access rules require the complete root-to-declared-region physical chain.",
            nameof(governingRegionChain));

        RegionId = regionId;
        Access = access;
        Reason = reason;
        AllowedSubregionIds = Array.AsReadOnly(_allowedSubregionIds);
        GoverningRegionChain = Array.AsReadOnly(_governingRegionChain);
    }

    /// <summary>Canonical direct region named by the profile rule.</summary>
    public string RegionId { get; }

    /// <summary>Closed authoring access mode.</summary>
    public CompiledRegionAccessKind Access { get; }

    /// <summary>Profile-owned reason retained for report and approval evidence.</summary>
    public string Reason { get; }

    /// <summary>Canonical direct child regions allowed only for <see cref="CompiledRegionAccessKind.Parts"/>.</summary>
    public IReadOnlyList<string> AllowedSubregionIds { get; }

    /// <summary>Physical ancestor chain that governed this profile declaration.</summary>
    public IReadOnlyList<CompiledPhysicalRegionConstraint> GoverningRegionChain { get; }
}

/// <summary>Complete immutable V2 region-access policy and logical-to-physical view provenance.</summary>
public sealed class CompiledRegionAccessContract
{
    private readonly CompiledRegionAccessRequirement[] _requirements;
    private readonly CompiledResolvedPhysicalView[] _resolvedViews;

    internal CompiledRegionAccessContract(
        IEnumerable<CompiledRegionAccessRequirement> requirements,
        IEnumerable<CompiledResolvedPhysicalView> resolvedViews)
    {
        _requirements = ImmutableReferenceSnapshot.CreateUnique(
            requirements,
            static requirement => requirement.RegionId,
            "Compiled region access requirements must be non-null with ordinally unique region ids.",
            "Compiled region access requirements must be non-null with ordinally unique region ids.",
            StringComparer.Ordinal);
        _resolvedViews = ImmutableReferenceSnapshot.CreateUnique(
            resolvedViews,
            static view => view.ViewId,
            "Compiled resolved views must be non-null with ordinally unique view ids.",
            "Compiled resolved views must be non-null with ordinally unique view ids.",
            StringComparer.Ordinal);

        Array.Sort(_requirements, static (left, right) => StringComparer.Ordinal.Compare(left.RegionId, right.RegionId));
        Array.Sort(_resolvedViews, static (left, right) => StringComparer.Ordinal.Compare(left.ViewId, right.ViewId));
        Requirements = Array.AsReadOnly(_requirements);
        ResolvedViews = Array.AsReadOnly(_resolvedViews);
    }

    /// <summary>Complete profile-owned access rules resolved against canonical physical constraints.</summary>
    public IReadOnlyList<CompiledRegionAccessRequirement> Requirements { get; }

    /// <summary>All map-backed profile logical views and their governing physical region chains.</summary>
    public IReadOnlyList<CompiledResolvedPhysicalView> ResolvedViews { get; }
}
