namespace NvtFwCombiner.Domain.Firmware;

/// <summary>One profile-declared A/B recognition format, independent of runtime selection.</summary>
public sealed class FirmwareAbFormatDefinition(
    string uniqueId,
    string displayName,
    IEnumerable<byte> defaultRecognitionValues)
{
    /// <summary>Stable format identifier.</summary>
    public string UniqueId { get; } = RequiredValue.NotBlank(uniqueId);

    /// <summary>Human-readable format label.</summary>
    public string DisplayName { get; } = RequiredValue.NotBlank(displayName);

    /// <summary>Recognition values reserved for this declared format.</summary>
    public IReadOnlyList<byte> DefaultRecognitionValues { get; } = Array.AsReadOnly(SnapshotValues(defaultRecognitionValues));

    private static byte[] SnapshotValues(IEnumerable<byte> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        byte[] snapshot = [.. values];
        DomainInvariant.Reject(
            snapshot.Length == 0,
            "A/B format definitions require at least one default recognition value.",
            nameof(values));
        DomainInvariant.Reject(
            snapshot.Distinct().Count() != snapshot.Length,
            "A/B format default recognition values must be unique.",
            nameof(values));
        Array.Sort(snapshot);
        return snapshot;
    }
}

/// <summary>Exact A/B metadata bindings shared by every format variant in one policy.</summary>
public sealed class FirmwareAbPrimaryBindings(
    string tpAStructureId,
    string tpBStructureId,
    string fieldId,
    string relationId)
{
    /// <summary>Selected TP-A metadata structure binding.</summary>
    public string TpAStructureId { get; } = RequiredValue.NotBlank(tpAStructureId);

    /// <summary>Selected TP-B metadata structure binding.</summary>
    public string TpBStructureId { get; } = RequiredValue.NotBlank(tpBStructureId);

    /// <summary>Unsigned one-byte field checked in both structures.</summary>
    public string FieldId { get; } = RequiredValue.NotBlank(fieldId);

    /// <summary>Bitwise-complement relation checked in both structures.</summary>
    public string RelationId { get; } = RequiredValue.NotBlank(relationId);
}

/// <summary>One member, format, and exact target-map declaration.</summary>
public sealed class FirmwareAbFormatVariant(string memberId, string formatId, string mapId)
{
    /// <summary>Declared family member.</summary>
    public string MemberId { get; } = RequiredValue.NotBlank(memberId);

    /// <summary>Declared non-Common format.</summary>
    public string FormatId { get; } = RequiredValue.NotBlank(formatId);

    /// <summary>Existing exact family image map.</summary>
    public string MapId { get; } = RequiredValue.NotBlank(mapId);
}

/// <summary>Immutable A/B format facts; Application owns terminal format and map selection.</summary>
public sealed class FirmwareAbFormatPolicy
{
    private const string AbMergeModeId = "ab-merge";
    private readonly FirmwareAbFormatDefinition[] _formats;
    private readonly FirmwareAbFormatVariant[] _variants;

    /// <summary>Creates immutable A/B format declarations.</summary>
    public FirmwareAbFormatPolicy(
        string scopeId,
        string commonFormatId,
        string commonDisplayName,
        IEnumerable<FirmwareAbFormatDefinition> formats,
        FirmwareAbPrimaryBindings primaryBindings,
        IEnumerable<FirmwareAbFormatVariant> variants)
    {
        ScopeId = RequiredValue.NotBlank(scopeId);
        CommonFormatId = RequiredValue.NotBlank(commonFormatId);
        CommonDisplayName = RequiredValue.NotBlank(commonDisplayName);
        PrimaryBindings = RequiredValue.NotNull(primaryBindings);
        _formats = SnapshotFormats(formats, CommonFormatId);
        _variants = SnapshotVariants(variants);
        ValidateRecognitionValues(_formats);
        Formats = Array.AsReadOnly(_formats);
        Variants = Array.AsReadOnly(_variants);
    }

    /// <summary>Opaque profile-owned scope shared by every target map.</summary>
    public string ScopeId { get; }

    /// <summary>Reserved Common format identifier; it is not configurable.</summary>
    public string CommonFormatId { get; }

    /// <summary>Display label for the reserved Common format.</summary>
    public string CommonDisplayName { get; }

    /// <summary>Configurable non-Common formats in ordinal id order.</summary>
    public IReadOnlyList<FirmwareAbFormatDefinition> Formats { get; }

    /// <summary>Canonical primary-structure binding facts.</summary>
    public FirmwareAbPrimaryBindings PrimaryBindings { get; }

    /// <summary>Exact member/format/map variants in deterministic declaration order.</summary>
    public IReadOnlyList<FirmwareAbFormatVariant> Variants { get; }

    internal void ValidateFamily(FirmwareFamilyResolutionDefinition family)
    {
        ArgumentNullException.ThrowIfNull(family);
        Dictionary<string, FirmwareImageMap> maps = family.ImageMaps.ToDictionary(
            static map => map.MapId,
            StringComparer.Ordinal);

        foreach (FirmwareAbFormatVariant variant in _variants)
        {
            DomainInvariant.Reject(
                !StringComparer.Ordinal.Equals(CommonFormatId, variant.FormatId) &&
                !_formats.Any(format => StringComparer.Ordinal.Equals(format.UniqueId, variant.FormatId)),
                $"A/B format variant references unknown format '{variant.FormatId}'.",
                nameof(Variants));
            FirmwareImageMap map = maps.TryGetValue(variant.MapId, out FirmwareImageMap? resolved)
                ? resolved
                : throw new ArgumentException(
                    $"A/B format variant references unknown image map '{variant.MapId}'.",
                    nameof(family));
            DomainInvariant.Reject(
                !map.Applicability.MemberIds.Contains(variant.MemberId, StringComparer.Ordinal),
                $"A/B format variant member '{variant.MemberId}' is not selected by map '{map.MapId}'.",
                nameof(Variants));
            DomainInvariant.Reject(
                !map.Applicability.ModeIds.Contains(AbMergeModeId, StringComparer.Ordinal),
                $"A/B format variant map '{map.MapId}' does not declare A/B mode '{AbMergeModeId}'.",
                nameof(Variants));
            DomainInvariant.Reject(
                map.Applicability.MetadataPredicates.Count != 0,
                $"A/B format variant map '{map.MapId}' cannot use metadata predicates.",
                nameof(Variants));
            ValidatePrimaryBindings(family, map);
        }

        foreach (IGrouping<(string MemberId, string FormatId), FirmwareAbFormatVariant> group in _variants.GroupBy(
                     static variant => (variant.MemberId, variant.FormatId)))
        {
            ValidateVariantTopologies(group, maps);
        }

        foreach (IGrouping<string, FirmwareAbFormatVariant> member in _variants.GroupBy(
                     static variant => variant.MemberId, StringComparer.Ordinal))
        {
            FirmwareImageMap anchor = maps[member.First().MapId];
            foreach (FirmwareAbFormatVariant variant in member)
            {
                family.ValidateAbPrimaryContext(anchor, maps[variant.MapId], PrimaryBindings.TpAStructureId);
                family.ValidateAbPrimaryContext(anchor, maps[variant.MapId], PrimaryBindings.TpBStructureId);
            }
        }
    }

    private static FirmwareAbFormatDefinition[] SnapshotFormats(
        IEnumerable<FirmwareAbFormatDefinition> formats,
        string commonFormatId)
    {
        FirmwareAbFormatDefinition[] snapshot = Composition.ImmutableReferenceSnapshot.CreateUnique(
            formats,
            static format => format.UniqueId,
            "A/B format policies cannot contain null formats.",
            "A/B format ids must be ordinally unique.",
            StringComparer.Ordinal,
            requireValue: true);
        DomainInvariant.Reject(
            snapshot.Any(format => StringComparer.Ordinal.Equals(format.UniqueId, commonFormatId)),
            "The reserved Common format cannot be configurable.",
            nameof(formats));
        Array.Sort(snapshot, static (left, right) =>
            StringComparer.Ordinal.Compare(left.UniqueId, right.UniqueId));
        return snapshot;
    }

    private static FirmwareAbFormatVariant[] SnapshotVariants(IEnumerable<FirmwareAbFormatVariant> variants)
    {
        FirmwareAbFormatVariant[] snapshot = Composition.ImmutableReferenceSnapshot.Create(
            variants,
            "A/B format policies require at least one non-null variant.",
            requireValue: true);
        DomainInvariant.Reject(
            snapshot.Select(static variant => (variant.MemberId, variant.FormatId, variant.MapId))
                .Distinct().Count() != snapshot.Length,
            "A/B format variants must be unique by member, format, and map.",
            nameof(variants));
        return snapshot;
    }

    private static void ValidateRecognitionValues(IEnumerable<FirmwareAbFormatDefinition> formats)
    {
        byte[] values = [.. formats.SelectMany(static format => format.DefaultRecognitionValues)];
        DomainInvariant.Reject(
            values.Distinct().Count() != values.Length,
            "A/B format default recognition values must be unique across formats.",
            nameof(formats));
    }

    private void ValidatePrimaryBindings(
        FirmwareFamilyResolutionDefinition family,
        FirmwareImageMap map)
    {
        FirmwareMetadataStructure tpA = ResolveStructure(family, map, PrimaryBindings.TpAStructureId);
        FirmwareMetadataStructure tpB = ResolveStructure(family, map, PrimaryBindings.TpBStructureId);
        DomainInvariant.Reject(
            !ReferenceEquals(tpA.Definition, tpB.Definition),
            "A/B primary structures must share one canonical metadata definition.",
            nameof(PrimaryBindings));
        DomainInvariant.Reject(
            StringComparer.Ordinal.Equals(tpA.ArtifactBindingId, tpB.ArtifactBindingId),
            "A/B primary structures must bind different artifacts.",
            nameof(PrimaryBindings));

        FirmwareMetadataField fieldA = ResolveField(tpA, PrimaryBindings.FieldId);
        FirmwareMetadataField fieldB = ResolveField(tpB, PrimaryBindings.FieldId);
        DomainInvariant.Reject(
            !ReferenceEquals(fieldA, fieldB) ||
            fieldA.Encoding != FirmwareMetadataEncoding.UnsignedInteger ||
            fieldA.WidthBytes != 1 ||
            fieldA.BitSlice is not null,
            "A/B primary bindings require one shared unsliced unsigned one-byte field.",
            nameof(PrimaryBindings));

        FirmwareMetadataFieldRelation relationA = ResolveRelation(tpA, PrimaryBindings.RelationId);
        FirmwareMetadataFieldRelation relationB = ResolveRelation(tpB, PrimaryBindings.RelationId);
        DomainInvariant.Reject(
            !ReferenceEquals(relationA, relationB) ||
            relationA.Kind != FirmwareMetadataFieldRelationKind.BitwiseComplement,
            "A/B primary bindings require one shared bitwise-complement relation.",
            nameof(PrimaryBindings));
    }

    private static FirmwareMetadataStructure ResolveStructure(
        FirmwareFamilyResolutionDefinition family,
        FirmwareImageMap map,
        string structureId)
    {
        return family.GetStructuresForMap(map.MapId).FirstOrDefault(structure =>
                StringComparer.Ordinal.Equals(structure.StructureId, structureId))
            ?? throw new ArgumentException(
                $"A/B primary binding structure '{structureId}' is not selected by map '{map.MapId}'.",
                nameof(family));
    }

    private static FirmwareMetadataField ResolveField(FirmwareMetadataStructure structure, string fieldId)
    {
        return structure.Fields.FirstOrDefault(field =>
                StringComparer.Ordinal.Equals(field.FieldId, fieldId))
            ?? throw new ArgumentException(
                $"A/B primary binding field '{fieldId}' is not defined by '{structure.StructureId}'.",
                nameof(structure));
    }

    private static FirmwareMetadataFieldRelation ResolveRelation(
        FirmwareMetadataStructure structure,
        string relationId)
    {
        return structure.Relations.FirstOrDefault(relation =>
                StringComparer.Ordinal.Equals(relation.RelationId, relationId))
            ?? throw new ArgumentException(
                $"A/B primary binding relation '{relationId}' is not defined by '{structure.StructureId}'.",
                nameof(structure));
    }

    private static void ValidateVariantTopologies(
        IGrouping<(string MemberId, string FormatId), FirmwareAbFormatVariant> variants,
        IReadOnlyDictionary<string, FirmwareImageMap> maps)
    {
        FirmwareAbFormatVariant[] none = [.. variants.Where(variant =>
            GetTopology(variant, maps).Kind == TopologyRequirementKind.None)];
        FirmwareAbFormatVariant[] single = [.. variants.Where(variant =>
            GetTopology(variant, maps).Kind == TopologyRequirementKind.SingleChip)];
        FirmwareAbFormatVariant[] cascade = [.. variants.Where(variant =>
            GetTopology(variant, maps).Kind == TopologyRequirementKind.Cascade)];
        FirmwareAbFormatVariant[] exact = [.. variants.Where(variant =>
            GetTopology(variant, maps).Kind == TopologyRequirementKind.ExactCount)];

        DomainInvariant.Reject(
            none.Length > 1 || single.Length > 1 || cascade.Length > 1,
            "A/B format variants allow at most one baseline for each topology kind.",
            nameof(Variants));
        DomainInvariant.Reject(
            none.Length != 0 && (single.Length != 0 || cascade.Length != 0 || exact.Length != 0),
            "A topology-independent A/B format variant cannot mix with topology-specific variants.",
            nameof(Variants));
        if (cascade.Length == 1)
        {
            TopologyRequirement requirement = GetTopology(cascade[0], maps);
            DomainInvariant.Reject(
                requirement.MinimumChipCount != 2 || requirement.MaximumChipCount is not null,
                "A/B Cascade baseline variants require the generic minimum-two unbounded topology.",
                nameof(Variants));
        }

        int[] exactCounts = [.. exact.Select(variant => GetTopology(variant, maps).ExactChipCount!.Value)];
        DomainInvariant.Reject(
            exactCounts.Distinct().Count() != exactCounts.Length,
            "A/B exact-count overrides must use unique chip counts per member and format.",
            nameof(Variants));
        foreach (int count in exactCounts)
        {
            bool compatible = count == 1
                ? single.Length == 1
                : cascade.Length == 1;
            DomainInvariant.Reject(
                !compatible,
                "A/B exact-count overrides require a compatible Single or generic Cascade baseline.",
                nameof(Variants));
        }
    }

    private static TopologyRequirement GetTopology(
        FirmwareAbFormatVariant variant,
        IReadOnlyDictionary<string, FirmwareImageMap> maps)
    {
        return maps[variant.MapId].Applicability.TopologyRequirement;
    }
}
