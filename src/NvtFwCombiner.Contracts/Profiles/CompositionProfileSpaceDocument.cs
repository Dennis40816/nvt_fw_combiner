using System.Text.Json;
using System.Text.Json.Serialization;

namespace NvtFwCombiner.Contracts.Profiles;

/// <summary>DTO for one checked range relative to a named profile space.</summary>
public sealed record CompositionProfileRelativeRangeDocument(JsonElement Start, JsonElement Length);

/// <summary>DTO for one resolved-map or fixed space-capacity shape.</summary>
public sealed record CompositionProfileCapacityDocument(
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? Bytes = null);

/// <summary>DTO for one engine-owned mutable-space initializer shape.</summary>
public sealed record CompositionProfileInitializerDocument(
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? FillByte = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SourceSlotId = null);

/// <summary>DTO for one immutable input or mutable work/output address space.</summary>
public sealed record CompositionProfileSpaceDocument(
    string SpaceId,
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SlotId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? InstancePolicy = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CompositionProfileCapacityDocument? Capacity = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CompositionProfileInitializerDocument? Initializer = null);

/// <summary>DTO for one map-region, map-region-slice, or space-range selector.</summary>
public sealed record CompositionProfileViewSelectorDocument(
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? RegionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? Offset = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? Length = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CompositionProfileRelativeRangeDocument? Range = null);

/// <summary>DTO for one named logical view over a profile space.</summary>
public sealed record CompositionProfileViewDocument(
    string ViewId,
    string SpaceId,
    CompositionProfileViewSelectorDocument Selector);

/// <summary>DTO for one canonical metadata structure bound to a profile space.</summary>
public sealed record CompositionProfileMetadataBindingDocument(
    string BindingId,
    string SpaceId,
    string StructureId,
    IReadOnlyList<string> FieldIds,
    IReadOnlyList<string> Purposes);

/// <summary>DTO for deny-by-default authoring access to one canonical region.</summary>
public sealed record CompositionProfileRegionAccessRuleDocument(
    string RegionId,
    string Access,
    string Reason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? AllowedSubregionIds = null);
