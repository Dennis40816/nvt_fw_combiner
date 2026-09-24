namespace NvtFwCombiner.Contracts.Firmware;

/// <summary>One exact map/member metadata view supplied by the same captured full image.</summary>
public sealed record FirmwareFullImageMetadataViewDocument(
    string ViewId,
    string MapId,
    IReadOnlyList<string> MemberIds,
    IReadOnlyList<FirmwareFullImageMetadataBindingDocument> MetadataBindings,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>Canonical structure targets without geometry, remapping, or execution authority.</summary>
public sealed record FirmwareFullImageMetadataBindingDocument(
    string BindingId,
    string StructureId,
    IReadOnlyList<FirmwareFullImageMetadataTargetDocument> TargetReferences,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>One exact span, field, series, or group within a canonical definition.</summary>
public sealed record FirmwareFullImageMetadataTargetDocument(string TargetKind, string TargetId);
