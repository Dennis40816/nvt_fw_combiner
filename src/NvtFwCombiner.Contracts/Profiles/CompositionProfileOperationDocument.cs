using System.Text.Json;
using System.Text.Json.Serialization;

namespace NvtFwCombiner.Contracts.Profiles;

/// <summary>DTO for one ordered closed composition operation shape.</summary>
public sealed record CompositionProfileOperationDocument(
    string OperationId,
    JsonElement Sequence,
    string OverlapPolicy,
    string Reason,
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SourceViewId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? TargetViewId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? FillByte = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ValueHex = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? WidthBytes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ByteOrder = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ValueInterpretation = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? Addend = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? ExpectedBefore = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? OverflowPolicy = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ProcessorStageId = null);

/// <summary>DTO for an addend resolved as target-region-instance base minus source base.</summary>
public sealed record CompositionProfileRegionInstanceDeltaAddendDocument(
    string Kind,
    string SourceRegionInstanceId,
    string TargetRegionInstanceId);

/// <summary>DTO for one metadata field reached through a profile metadata binding.</summary>
public sealed record CompositionProfileMetadataFieldReferenceDocument(string BindingId, string FieldId);

/// <summary>DTO for one closed profile validation shape before typed scalar conversion.</summary>
public sealed record CompositionProfileValidationDocument(
    string RuleId,
    string Stage,
    string Severity,
    string IssueCode,
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CompositionProfileMetadataFieldReferenceDocument? Field = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Operator = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<JsonElement>? ExpectedValues = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CompositionProfileMetadataFieldReferenceDocument? Left = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CompositionProfileMetadataFieldReferenceDocument? Right = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? RejectedPatterns = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ViewId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ExpectedHex = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? MaskHex = null);
