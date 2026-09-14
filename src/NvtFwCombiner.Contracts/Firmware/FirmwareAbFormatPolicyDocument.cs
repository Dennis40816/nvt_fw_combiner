namespace NvtFwCombiner.Contracts.Firmware;

/// <summary>Optional profile-owned A/B format facts, without a runtime selection rule.</summary>
public sealed record FirmwareAbFormatPolicyDocument(
    string ScopeId,
    string CommonFormatId,
    string CommonDisplayName,
    IReadOnlyList<FirmwareAbFormatDefinitionDocument> Formats,
    FirmwareAbPrimaryBindingsDocument PrimaryBindings,
    IReadOnlyList<FirmwareAbFormatVariantDocument> Variants);

/// <summary>One configurable non-Common format and its byte recognition values.</summary>
public sealed record FirmwareAbFormatDefinitionDocument(
    string UniqueId,
    string DisplayName,
    IReadOnlyList<int> DefaultRecognitionValues);

/// <summary>Exact metadata fields shared by the declared A/B primary structures.</summary>
public sealed record FirmwareAbPrimaryBindingsDocument(
    string TpAStructureId,
    string TpBStructureId,
    string FieldId,
    string RelationId);

/// <summary>One member, format and exact existing map reference.</summary>
public sealed record FirmwareAbFormatVariantDocument(
    string MemberId,
    string FormatId,
    string MapId);
