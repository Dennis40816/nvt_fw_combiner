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
    CompositionProfileInputAcceptanceDocument Acceptance);

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
    string? IssueCode = null);

/// <summary>DTO for one closed transient input-normalization shape.</summary>
public sealed record CompositionProfileInputNormalizationDocument(
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? FillByte = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? WarningIssueCode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? EvidenceRef = null);
