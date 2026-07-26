using System.Text.Json;
using System.Text.Json.Serialization;

namespace NvtFwCombiner.Contracts.Firmware;

/// <summary>DTO for one evidence-backed metadata fact set.</summary>
/// <param name="MetadataSetId">Stable metadata-set identifier.</param>
/// <param name="Structures">Metadata structure declarations.</param>
/// <param name="EvidenceRefs">Evidence manifest references.</param>
public sealed record FirmwareMetadataSetDocument(
    string MetadataSetId,
    IReadOnlyList<FirmwareMetadataStructureDocument> Structures,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>DTO for one located firmware metadata structure.</summary>
/// <param name="StructureId">Family-global structure identifier.</param>
/// <param name="ArtifactBindingId">Exact runtime artifact binding.</param>
/// <param name="Length">Exact structure byte length.</param>
/// <param name="Locator">Closed locator declaration shape.</param>
/// <param name="Fields">Typed field declarations.</param>
/// <param name="Assertions">Structure-relative byte assertions.</param>
/// <param name="Relations">Optional typed relationships between declared fields.</param>
public sealed record FirmwareMetadataStructureDocument(
    string StructureId,
    string ArtifactBindingId,
    JsonElement Length,
    FirmwareMetadataLocatorDocument Locator,
    IReadOnlyList<FirmwareMetadataFieldDocument> Fields,
    IReadOnlyList<FirmwareByteAssertionDocument> Assertions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<FirmwareMetadataFieldRelationDocument>? Relations = null);

/// <summary>DTO for one typed relationship between fields in a metadata structure.</summary>
/// <param name="RelationId">Stable relation identifier.</param>
/// <param name="Kind">Closed relation-kind token.</param>
/// <param name="SourceFieldId">Canonical source-field identifier.</param>
/// <param name="RelatedFieldId">Canonical related-field identifier.</param>
public sealed record FirmwareMetadataFieldRelationDocument(
    string RelationId,
    string Kind,
    string SourceFieldId,
    string RelatedFieldId);

/// <summary>DTO for one closed metadata field declaration.</summary>
/// <param name="FieldId">Stable field identifier.</param>
/// <param name="Offset">Structure-relative byte offset.</param>
/// <param name="WidthBytes">Exact carrier byte width.</param>
/// <param name="Encoding">Closed field encoding token.</param>
/// <param name="ByteOrder">Integer byte-order token when declared.</param>
/// <param name="BitSlice">Optional unsigned integer projection.</param>
/// <param name="SourceName">Optional retained source declaration name.</param>
public sealed record FirmwareMetadataFieldDocument(
    string FieldId,
    JsonElement Offset,
    JsonElement WidthBytes,
    string Encoding,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ByteOrder = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FirmwareMetadataBitSliceDocument? BitSlice = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SourceName = null);

/// <summary>DTO for one unsigned integer bit projection.</summary>
/// <param name="LeastSignificantBit">First selected normalized carrier bit.</param>
/// <param name="BitCount">Positive selected bit count.</param>
public sealed record FirmwareMetadataBitSliceDocument(
    JsonElement LeastSignificantBit,
    JsonElement BitCount);

/// <summary>DTO for one exact or partial-mask structure byte assertion.</summary>
/// <param name="Offset">Structure-relative assertion offset.</param>
/// <param name="ExpectedHex">Canonical expected bytes.</param>
/// <param name="MaskHex">Optional canonical nontrivial mask.</param>
public sealed record FirmwareByteAssertionDocument(
    JsonElement Offset,
    string ExpectedHex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? MaskHex = null);

/// <summary>DTO for one closed metadata locator shape after schema validation.</summary>
/// <param name="Kind">Closed locator kind token.</param>
/// <param name="AllowedResultRegionId">Candidate region that must contain the complete result.</param>
/// <param name="Range">Absolute addressed range when declared.</param>
/// <param name="RegionId">Region-relative base when declared.</param>
/// <param name="Offset">Nonnegative region-relative offset when declared.</param>
/// <param name="SearchRange">Bounded marker search range when declared.</param>
/// <param name="MarkerHex">Exact marker bytes when declared.</param>
/// <param name="Selection">Marker cardinality and terminal selection when declared.</param>
/// <param name="ResultOffset">Signed marker-relative result offset when declared.</param>
public sealed record FirmwareMetadataLocatorDocument(
    string Kind,
    string AllowedResultRegionId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FirmwareAddressedRangeDocument? Range = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? RegionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? Offset = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FirmwareAddressedRangeDocument? SearchRange = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? MarkerHex = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FirmwareMarkerSelectionDocument? Selection = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? ResultOffset = null);

/// <summary>DTO for one unique or evidenced terminal marker selection.</summary>
/// <param name="Kind">Closed marker-selection kind token.</param>
/// <param name="Terminal">Optional terminal direction token.</param>
/// <param name="ExpectedMatchCount">Optional exact evidenced match count.</param>
public sealed record FirmwareMarkerSelectionDocument(
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Terminal = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? ExpectedMatchCount = null);

/// <summary>DTO for one metadata predicate before field-context scalar conversion.</summary>
/// <param name="MetadataStructureId">Exact referenced metadata structure.</param>
/// <param name="FieldId">Exact field inside the referenced structure.</param>
/// <param name="Operator">Closed comparison token.</param>
/// <param name="ExpectedValues">Schema-validated JSON scalar values retained without coercion.</param>
public sealed record FirmwareMetadataPredicateDocument(
    string MetadataStructureId,
    string FieldId,
    string Operator,
    IReadOnlyList<JsonElement> ExpectedValues);
