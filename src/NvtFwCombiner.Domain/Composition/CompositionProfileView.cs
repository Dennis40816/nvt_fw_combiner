using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Base value for one normalized logical-view selector.</summary>
internal abstract record CompositionProfileViewSelector;

/// <summary>Selects one complete canonical map region.</summary>
internal sealed record MapRegionViewSelector : CompositionProfileViewSelector
{
    internal MapRegionViewSelector(string regionId)
    {
        RegionId = CanonicalPolicyValueRules.RequireCanonicalId(regionId, nameof(regionId));
    }

    internal string RegionId { get; }
}

/// <summary>Selects one checked range relative to a canonical map region.</summary>
internal sealed record MapRegionSliceViewSelector : CompositionProfileViewSelector
{
    internal MapRegionSliceViewSelector(string regionId, ByteRange relativeRange)
    {
        RegionId = CanonicalPolicyValueRules.RequireCanonicalId(regionId, nameof(regionId));
        RelativeRange = CanonicalProfileValueRules.RequireRange(relativeRange, nameof(relativeRange));
    }

    internal string RegionId { get; }

    internal ByteRange RelativeRange { get; }
}

/// <summary>Selects one checked range directly in the owning profile space.</summary>
internal sealed record SpaceRangeViewSelector : CompositionProfileViewSelector
{
    internal SpaceRangeViewSelector(ByteRange range)
    {
        Range = CanonicalProfileValueRules.RequireRange(range, nameof(range));
    }

    internal ByteRange Range { get; }
}

/// <summary>Selects one template-relative range through an explicit region instance.</summary>
internal sealed record RegionTemplateRangeViewSelector : CompositionProfileViewSelector
{
    internal RegionTemplateRangeViewSelector(
        string regionInstanceId,
        string templateRegionId)
    {
        RegionInstanceId = CanonicalPolicyValueRules.RequireCanonicalId(
            regionInstanceId,
            nameof(regionInstanceId));
        TemplateRegionId = CanonicalPolicyValueRules.RequireCanonicalId(
            templateRegionId,
            nameof(templateRegionId));
    }

    internal string RegionInstanceId { get; }

    internal string TemplateRegionId { get; }
}

/// <summary>One named logical view over a profile address space.</summary>
internal sealed record CompositionProfileView
{
    internal CompositionProfileView(
        string viewId,
        string spaceId,
        CompositionProfileViewSelector selector)
    {
        ViewId = CanonicalPolicyValueRules.RequireCanonicalId(viewId, nameof(viewId));
        SpaceId = CanonicalPolicyValueRules.RequireCanonicalId(spaceId, nameof(spaceId));
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
    Inspection,
    Formatting,
    Copy,
    Relocation,
    Integrity,
    Processor,
    MemoryProjection,
    ReportClassification,
}

/// <summary>One canonical metadata structure and selected fields bound to a profile space.</summary>
internal sealed class CompositionProfileMetadataBinding
{
    private readonly string[] _fieldIds;
    private readonly FirmwareMetadataReferenceTarget[] _targetReferences;
    private readonly CompositionProfileMetadataPurpose[] _purposes;
    private readonly string[] _evidenceRefs;

    internal CompositionProfileMetadataBinding(
        string bindingId,
        string spaceId,
        string structureId,
        IEnumerable<string> fieldIds,
        IEnumerable<CompositionProfileMetadataPurpose> purposes)
        : this(
            bindingId,
            spaceId,
            structureId,
            fieldIds.Select(static fieldId =>
                new FirmwareMetadataReferenceTarget(
                    FirmwareMetadataReferenceTargetKind.Field,
                    fieldId)),
            purposes,
            [])
    {
    }

    internal CompositionProfileMetadataBinding(
        string bindingId,
        string spaceId,
        string structureId,
        IEnumerable<FirmwareMetadataReferenceTarget> targetReferences,
        IEnumerable<CompositionProfileMetadataPurpose> purposes,
        IEnumerable<string> evidenceRefs)
    {
        BindingId = CanonicalPolicyValueRules.RequireCanonicalId(bindingId, nameof(bindingId));
        SpaceId = CanonicalPolicyValueRules.RequireCanonicalId(spaceId, nameof(spaceId));
        StructureId = CanonicalPolicyValueRules.RequireCanonicalId(structureId, nameof(structureId));
        _targetReferences = ImmutableReferenceSnapshot.Create(
            targetReferences,
            "Metadata target references cannot contain null.");
        if (_targetReferences.Length == 0 ||
            _targetReferences.Distinct().Count() != _targetReferences.Length)
        {
            throw new ArgumentException(
                "Metadata target references must be nonempty and unique.",
                nameof(targetReferences));
        }

        Array.Sort(_targetReferences, static (left, right) =>
        {
            int kind = left.Kind.CompareTo(right.Kind);
            return kind != 0
                ? kind
                : StringComparer.Ordinal.Compare(left.TargetId, right.TargetId);
        });
        _fieldIds =
        [
            .. _targetReferences
                .Where(static target =>
                    target.Kind == FirmwareMetadataReferenceTargetKind.Field)
                .Select(static target => target.TargetId),
        ];

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
        _evidenceRefs = CanonicalPolicyValueRules.SnapshotCanonicalIds(
            evidenceRefs,
            nameof(evidenceRefs),
            requireValue: false);
        TargetReferences = Array.AsReadOnly(_targetReferences);
        FieldIds = Array.AsReadOnly(_fieldIds);
        Purposes = Array.AsReadOnly(_purposes);
        EvidenceRefs = Array.AsReadOnly(_evidenceRefs);
    }

    internal string BindingId { get; }

    internal string SpaceId { get; }

    internal string StructureId { get; }

    internal IReadOnlyList<FirmwareMetadataReferenceTarget> TargetReferences { get; }

    internal IReadOnlyList<string> FieldIds { get; }

    internal IReadOnlyList<CompositionProfileMetadataPurpose> Purposes { get; }

    internal IReadOnlyList<string> EvidenceRefs { get; }
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
        RegionId = CanonicalPolicyValueRules.RequireCanonicalId(regionId, nameof(regionId));
        if (!Enum.IsDefined(access))
        {
            throw new ArgumentOutOfRangeException(nameof(access), access, "Unknown region access kind.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        _allowedSubregionIds = CanonicalPolicyValueRules.SnapshotCanonicalIds(
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
