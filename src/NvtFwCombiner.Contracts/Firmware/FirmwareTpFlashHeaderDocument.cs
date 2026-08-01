using System.Text.Json;
using System.Text.Json.Serialization;

namespace NvtFwCombiner.Contracts.Firmware;

/// <summary>
/// DTO for one typed TP Flash Header specialization. Physical fields remain
/// declared exactly once by the containing common metadata structure.
/// </summary>
/// <param name="Spans">Named structure-relative spans, including reserved spans.</param>
/// <param name="FieldSemantics">Typed semantics referencing common physical fields.</param>
/// <param name="FieldSeries">Explicit logical-index-to-field series.</param>
/// <param name="FieldGroups">Reference-only semantic groups.</param>
public sealed record FirmwareTpFlashHeaderDocument(
    IReadOnlyList<FirmwareMetadataNamedSpanDocument> Spans,
    IReadOnlyList<FirmwareTpFlashHeaderFieldSemanticsDocument> FieldSemantics,
    IReadOnlyList<FirmwareMetadataFieldSeriesDocument> FieldSeries,
    IReadOnlyList<FirmwareMetadataFieldGroupDocument> FieldGroups);

/// <summary>DTO for one named structure-relative span.</summary>
/// <param name="SpanId">Stable span identity within the structure.</param>
/// <param name="Range">Exact half-open structure-relative range.</param>
public sealed record FirmwareMetadataNamedSpanDocument(
    string SpanId,
    FirmwareByteRangeDocument Range);

/// <summary>DTO for TP-specific meaning attached to one common physical field.</summary>
/// <param name="FieldId">Exact common physical-field reference.</param>
/// <param name="SpanId">Named span containing the physical field.</param>
/// <param name="Subject">Closed TP Header subject token.</param>
/// <param name="Role">Closed TP Header value-role token.</param>
/// <param name="LogicalIndex">Optional nonnegative repeated-record index.</param>
/// <param name="StoredAddress">Required value address-space/basis for address roles.</param>
public sealed record FirmwareTpFlashHeaderFieldSemanticsDocument(
    string FieldId,
    string SpanId,
    string Subject,
    string Role,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? LogicalIndex = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FirmwareTpFlashHeaderStoredAddressDocument? StoredAddress = null);

/// <summary>DTO for the meaning of an address integer stored in one Header field.</summary>
/// <param name="AddressSpaceId">Address space named by the encoded value.</param>
/// <param name="Basis">Closed origin/basis token for the encoded value.</param>
public sealed record FirmwareTpFlashHeaderStoredAddressDocument(
    string AddressSpaceId,
    string Basis);

/// <summary>DTO for one explicit repeated-field series.</summary>
/// <param name="SeriesId">Stable series identity within the structure.</param>
/// <param name="Members">Explicit logical-index-to-physical-field members.</param>
/// <param name="Applicability">Exact IC Count applicability rows.</param>
public sealed record FirmwareMetadataFieldSeriesDocument(
    string SeriesId,
    IReadOnlyList<FirmwareMetadataFieldSeriesMemberDocument> Members,
    IReadOnlyList<FirmwareMetadataFieldSeriesApplicabilityDocument> Applicability);

/// <summary>DTO for one explicit logical-index-to-physical-field member.</summary>
/// <param name="Index">Nonnegative owner-declared logical index.</param>
/// <param name="FieldId">Exact common physical-field reference.</param>
public sealed record FirmwareMetadataFieldSeriesMemberDocument(
    JsonElement Index,
    string FieldId);

/// <summary>DTO for exact active series indices at one IC Count.</summary>
/// <param name="IcCount">Positive owner-evidenced IC Count.</param>
/// <param name="ActiveIndices">Explicit active logical indices.</param>
public sealed record FirmwareMetadataFieldSeriesApplicabilityDocument(
    JsonElement IcCount,
    IReadOnlyList<JsonElement> ActiveIndices);

/// <summary>DTO for one semantic group over exact field and series references.</summary>
/// <param name="GroupId">Stable group identity within the structure.</param>
/// <param name="FieldIds">Exact scalar physical-field references.</param>
/// <param name="SeriesIds">Exact repeated-series references.</param>
public sealed record FirmwareMetadataFieldGroupDocument(
    string GroupId,
    IReadOnlyList<string> FieldIds,
    IReadOnlyList<string> SeriesIds);
