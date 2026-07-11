using System.Text.Json;
using System.Text.Json.Serialization;

namespace NvtFwCombiner.Contracts.Firmware;

/// <summary>DTO for one schema-validated <c>firmware-family-v1</c> document.</summary>
/// <param name="SchemaVersion">Exact family schema version.</param>
/// <param name="FamilyId">Stable family identifier.</param>
/// <param name="FamilyVersion">Family content version.</param>
/// <param name="Members">Declared IC family members.</param>
/// <param name="Capabilities">Evidence-backed technical capability facts.</param>
/// <param name="RegionSets">Canonical physical region fact sets.</param>
/// <param name="MetadataSets">Canonical metadata fact sets.</param>
/// <param name="ImageMaps">Candidate physical image maps.</param>
/// <param name="FactAliases">Explicit fact-scoped aliases.</param>
/// <param name="EvidenceRefs">Family-level evidence manifest references.</param>
public sealed record FirmwareFamilyDocument(
    string SchemaVersion,
    string FamilyId,
    string FamilyVersion,
    IReadOnlyList<FirmwareFamilyMemberDocument> Members,
    IReadOnlyList<FirmwareCapabilityDocument> Capabilities,
    IReadOnlyList<FirmwareRegionSetDocument> RegionSets,
    IReadOnlyList<FirmwareMetadataSetDocument> MetadataSets,
    IReadOnlyList<FirmwareImageMapDocument> ImageMaps,
    IReadOnlyList<FirmwareFactAliasDocument> FactAliases,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>DTO for one family member and its display label.</summary>
/// <param name="MemberId">Stable IC member identifier.</param>
/// <param name="DisplayName">Human-readable IC name.</param>
public sealed record FirmwareFamilyMemberDocument(string MemberId, string DisplayName);

/// <summary>DTO for one evidence-backed capability fact.</summary>
/// <param name="CapabilityId">Stable capability identifier.</param>
/// <param name="State">Closed source capability state token.</param>
/// <param name="MemberIds">Members covered by the fact.</param>
/// <param name="EvidenceRefs">Evidence manifest references.</param>
/// <param name="Reason">Optional source explanation.</param>
public sealed record FirmwareCapabilityDocument(
    string CapabilityId,
    string State,
    IReadOnlyList<string> MemberIds,
    IReadOnlyList<string> EvidenceRefs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Reason = null);

/// <summary>DTO for one canonical physical region fact set.</summary>
/// <param name="RegionSetId">Stable fact-set identifier.</param>
/// <param name="AddressSpaceId">Physical address-space identifier.</param>
/// <param name="Regions">Physical region declarations.</param>
/// <param name="EvidenceRefs">Evidence manifest references.</param>
public sealed record FirmwareRegionSetDocument(
    string RegionSetId,
    string AddressSpaceId,
    IReadOnlyList<FirmwareRegionDocument> Regions,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>DTO for one canonical physical firmware region.</summary>
/// <param name="RegionId">Stable map-local region identifier.</param>
/// <param name="Owner">Closed physical owner token.</param>
/// <param name="Kind">Closed physical kind token.</param>
/// <param name="Range">Half-open range represented as start plus length.</param>
/// <param name="WriteConstraint">Non-relaxable write-constraint token.</param>
/// <param name="Alignment">Positive physical alignment.</param>
/// <param name="ParentRegionId">Optional containing region identifier.</param>
public sealed record FirmwareRegionDocument(
    string RegionId,
    string Owner,
    string Kind,
    FirmwareByteRangeDocument Range,
    string WriteConstraint,
    JsonElement Alignment,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ParentRegionId = null);

/// <summary>DTO for one checked range represented as start plus positive length.</summary>
/// <param name="Start">Inclusive byte start.</param>
/// <param name="Length">Positive byte length.</param>
public sealed record FirmwareByteRangeDocument(JsonElement Start, JsonElement Length);

/// <summary>DTO for one checked range in a named address space.</summary>
/// <param name="AddressSpaceId">Address-space identifier.</param>
/// <param name="Start">Inclusive byte start.</param>
/// <param name="Length">Positive byte length.</param>
public sealed record FirmwareAddressedRangeDocument(
    string AddressSpaceId,
    JsonElement Start,
    JsonElement Length);

/// <summary>DTO for one closed topology requirement shape.</summary>
/// <param name="Kind">Closed topology kind token.</param>
/// <param name="MinimumChipCount">Cascade minimum when declared.</param>
/// <param name="MaximumChipCount">Optional cascade maximum.</param>
/// <param name="ChipCount">Exact count when declared.</param>
public sealed record FirmwareTopologyRequirementDocument(
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? MinimumChipCount = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? MaximumChipCount = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? ChipCount = null);

/// <summary>DTO for one candidate map applicability declaration.</summary>
/// <param name="MemberIds">Accepted IC members.</param>
/// <param name="ModeIds">Accepted firmware modes.</param>
/// <param name="TopologyRequirement">Static topology requirement.</param>
/// <param name="CapacityBytes">Exact candidate-map capacity.</param>
/// <param name="CommonFirmwareCategoryIds">Optional accepted Common FW categories.</param>
/// <param name="MetadataPredicates">Optional typed metadata predicates pending field-context conversion.</param>
public sealed record FirmwareMapApplicabilityDocument(
    IReadOnlyList<string> MemberIds,
    IReadOnlyList<string> ModeIds,
    FirmwareTopologyRequirementDocument TopologyRequirement,
    JsonElement CapacityBytes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? CommonFirmwareCategoryIds = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<FirmwareMetadataPredicateDocument>? MetadataPredicates = null);

/// <summary>DTO for one canonical candidate image map.</summary>
/// <param name="MapId">Stable image-map identifier.</param>
/// <param name="AddressSpaceId">Physical map address space.</param>
/// <param name="Applicability">Static candidate applicability.</param>
/// <param name="CoveragePolicy">Closed complete-coverage policy token.</param>
/// <param name="RegionSetIds">Referenced physical region-set identifiers.</param>
/// <param name="MetadataSetIds">Referenced metadata-set identifiers.</param>
/// <param name="EvidenceRefs">Evidence manifest references.</param>
public sealed record FirmwareImageMapDocument(
    string MapId,
    string AddressSpaceId,
    FirmwareMapApplicabilityDocument Applicability,
    string CoveragePolicy,
    IReadOnlyList<string> RegionSetIds,
    IReadOnlyList<string> MetadataSetIds,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>DTO for applicability attached to one fact-scoped alias.</summary>
/// <param name="ModeIds">Accepted firmware modes.</param>
/// <param name="TopologyRequirement">Static topology requirement.</param>
/// <param name="CapacityBytes">Exact applicable capacity.</param>
/// <param name="CommonFirmwareCategoryIds">Optional accepted Common FW categories.</param>
/// <param name="MetadataPredicates">Optional field-context metadata predicates.</param>
public sealed record FirmwareAliasApplicabilityDocument(
    IReadOnlyList<string> ModeIds,
    FirmwareTopologyRequirementDocument TopologyRequirement,
    JsonElement CapacityBytes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? CommonFirmwareCategoryIds = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<FirmwareMetadataPredicateDocument>? MetadataPredicates = null);

/// <summary>DTO for one explicit source-to-target fact alias.</summary>
/// <param name="AliasId">Stable alias identifier.</param>
/// <param name="FactKind">Closed aliased fact kind token.</param>
/// <param name="TargetMemberId">Member receiving the fact.</param>
/// <param name="TargetFactId">Target fact identifier.</param>
/// <param name="SourceMemberId">Member supplying the fact.</param>
/// <param name="SourceFactId">Source fact identifier.</param>
/// <param name="Applicability">Non-widening alias applicability declaration.</param>
/// <param name="Reason">Required source explanation.</param>
/// <param name="EvidenceRefs">Evidence manifest references.</param>
public sealed record FirmwareFactAliasDocument(
    string AliasId,
    string FactKind,
    string TargetMemberId,
    string TargetFactId,
    string SourceMemberId,
    string SourceFactId,
    FirmwareAliasApplicabilityDocument Applicability,
    string Reason,
    IReadOnlyList<string> EvidenceRefs);
