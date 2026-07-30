using System.Text.Json;
using System.Text.Json.Serialization;

namespace NvtFwCombiner.Contracts.Profiles;

/// <summary>DTO for one immutable input artifact slot.</summary>
public sealed record CompositionProfileInputSlotDocument(
    string SlotId,
    string Role,
    string ArtifactClass,
    bool Required,
    string Cardinality,
    IReadOnlyList<string> AcceptedExtensions,
    CompositionProfileInputAcceptanceDocument Acceptance,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? NotApplicableReason = null);

/// <summary>DTO for one checked selection constraint across existing optional input slots.</summary>
public sealed record CompositionProfileInputSelectionGroupDocument(
    string GroupId,
    IReadOnlyList<string> MemberSlotIds,
    int MinimumSelected,
    int MaximumSelected);

/// <summary>DTO for input length acceptance and transient normalization policy.</summary>
public sealed record CompositionProfileInputAcceptanceDocument(
    CompositionProfileLengthRuleDocument LengthRule,
    CompositionProfileInputNormalizationDocument Normalization);

/// <summary>DTO for one closed input-length rule shape.</summary>
public sealed record CompositionProfileLengthRuleDocument(
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? Bytes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? MinimumBytes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? MaximumBytes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? IssueCode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<JsonElement>? ExpectedInputLengths = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? RequiredEndExclusive = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<JsonElement>? ExpectedOuterLengths = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ShortInputIssueCode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? UnexpectedOuterLengthIssueCode = null);

/// <summary>DTO for one closed transient input-normalization shape.</summary>
public sealed record CompositionProfileInputNormalizationDocument(
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? FillByte = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? WarningIssueCode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? EvidenceRef = null);
