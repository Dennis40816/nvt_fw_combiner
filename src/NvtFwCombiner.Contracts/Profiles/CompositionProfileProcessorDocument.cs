using System.Text.Json.Serialization;

namespace NvtFwCombiner.Contracts.Profiles;

/// <summary>DTO for one immutable source view staged into a processor target view.</summary>
public sealed record CompositionProfileStagedSourceBindingDocument(
    string SourceViewId,
    string TargetViewId);

/// <summary>DTO for one closed CRC-worker or legacy-combiner processor stage.</summary>
public sealed record CompositionProfileProcessorStageDocument(
    string ProcessorStageId,
    string Kind,
    string TargetSpaceId,
    string Authority,
    string Purpose,
    string IntegrityDisposition,
    IReadOnlyList<string> AllowedReadViewIds,
    IReadOnlyList<string> AllowedWriteViewIds,
    string FailurePolicy,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ContractVersion = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CalculationSetId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ToolBindingId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? InvocationProfileId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CompositionProfileStagedSourceBindingDocument>? StagedSourceBindings = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? EvidenceRef = null);

/// <summary>DTO for profile-controlled output naming policy.</summary>
public sealed record CompositionProfileOutputDocument(
    string FileNameTemplate,
    bool AllowOverride,
    string InvalidCharacterPolicy,
    IReadOnlyList<string> RequiredTokenIds);
