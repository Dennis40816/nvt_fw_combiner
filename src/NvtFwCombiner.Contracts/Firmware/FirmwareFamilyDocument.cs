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
/// <param name="FamilyRelationships">Optional owner-declared perfect-like or shared-part relationships.</param>
public sealed record FirmwareFamilyDocument(
    string SchemaVersion,
    string FamilyId,
    string FamilyVersion,
    IReadOnlyList<FirmwareFamilyMemberDocument> Members,
    IReadOnlyList<FirmwareCapabilityFactDocument> Capabilities,
    IReadOnlyList<FirmwareRegionSetDocument> RegionSets,
    IReadOnlyList<FirmwareMetadataSetDocument> MetadataSets,
    IReadOnlyList<FirmwareImageMapDocument> ImageMaps,
    IReadOnlyList<FirmwareFactAliasDocument> FactAliases,
    IReadOnlyList<string> EvidenceRefs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<FirmwareFamilyRelationshipDocument>? FamilyRelationships = null);

/// <summary>DTO for one family member and its display label.</summary>
/// <param name="MemberId">Stable IC member identifier.</param>
/// <param name="DisplayName">Human-readable IC name.</param>
public sealed record FirmwareFamilyMemberDocument(string MemberId, string DisplayName);

/// <summary>
/// Shared DTO fields for one owner-declared family relationship. Relationship
/// declarations contain firmware-semantic scope only; support, publication,
/// evidence classification, workflow, processor, and topology authority are
/// intentionally not representable here.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "relationshipKind")]
[JsonDerivedType(typeof(FirmwarePerfectLikeFamilyRelationshipDocument), "perfect-like-family")]
[JsonDerivedType(
    typeof(FirmwareInitialCodeSharedFamilyRelationshipDocument),
    "initial-code-shared-family")]
[JsonDerivedType(typeof(FirmwareTpSharedFamilyRelationshipDocument), "tp-shared-family")]
public abstract record FirmwareFamilyRelationshipDocument(
    string RelationshipId,
    IReadOnlyList<string> MemberIds,
    string Reason,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>DTO for one complete perfect-like firmware-semantic relationship.</summary>
public sealed record FirmwarePerfectLikeFamilyRelationshipDocument(
    string RelationshipId,
    IReadOnlyList<string> MemberIds,
    string Reason,
    IReadOnlyList<string> EvidenceRefs)
    : FirmwareFamilyRelationshipDocument(RelationshipId, MemberIds, Reason, EvidenceRefs);

/// <summary>DTO for one Initial Code geometry and owned-metadata relationship.</summary>
public sealed record FirmwareInitialCodeSharedFamilyRelationshipDocument(
    string RelationshipId,
    IReadOnlyList<string> MemberIds,
    IReadOnlyList<string> SharedRegionIds,
    IReadOnlyList<string> MetadataDefinitionIds,
    string Reason,
    IReadOnlyList<string> EvidenceRefs)
    : FirmwareFamilyRelationshipDocument(RelationshipId, MemberIds, Reason, EvidenceRefs);

/// <summary>DTO for one TP geometry and owned-metadata relationship.</summary>
public sealed record FirmwareTpSharedFamilyRelationshipDocument(
    string RelationshipId,
    IReadOnlyList<string> MemberIds,
    IReadOnlyList<string> SharedRegionIds,
    IReadOnlyList<string> MetadataDefinitionIds,
    string Reason,
    IReadOnlyList<string> EvidenceRefs)
    : FirmwareFamilyRelationshipDocument(RelationshipId, MemberIds, Reason, EvidenceRefs);

/// <summary>DTO for one map-bound evidence-backed technical capability fact.</summary>
/// <param name="CapabilityFactId">Stable aliasable capability fact identifier.</param>
/// <param name="CapabilityId">Technical capability identifier.</param>
/// <param name="MemberId">Member covered by this fact.</param>
/// <param name="MapId">Physical map covered by this fact.</param>
/// <param name="Applicability">Static non-member applicability.</param>
/// <param name="State">Closed source capability state token.</param>
/// <param name="EvidenceRefs">Evidence manifest references.</param>
/// <param name="Reason">Required source explanation.</param>
public sealed record FirmwareCapabilityFactDocument(
    string CapabilityFactId,
    string CapabilityId,
    string MemberId,
    string MapId,
    FirmwareAliasApplicabilityDocument Applicability,
    string State,
    string Reason,
    IReadOnlyList<string> EvidenceRefs);

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

/// <summary>Shared DTO fields for one map-bound source-to-target fact alias.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "factKind")]
[JsonDerivedType(typeof(FirmwareRegionSetAliasDocument), "region-set")]
[JsonDerivedType(typeof(FirmwareMetadataSetAliasDocument), "metadata-set")]
[JsonDerivedType(typeof(FirmwareCapabilityAliasDocument), "capability")]
public abstract record FirmwareFactAliasDocument(
    string AliasId,
    string TargetMemberId,
    string TargetMapId,
    string SourceMemberId,
    string SourceMapId,
    FirmwareAliasApplicabilityDocument Applicability,
    string Reason,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>DTO for one map-bound region-set alias.</summary>
public sealed record FirmwareRegionSetAliasDocument(
    string AliasId,
    string TargetMemberId,
    string TargetMapId,
    string TargetRegionSetId,
    string SourceMemberId,
    string SourceMapId,
    string SourceRegionSetId,
    FirmwareAliasApplicabilityDocument Applicability,
    string Reason,
    IReadOnlyList<string> EvidenceRefs)
    : FirmwareFactAliasDocument(
        AliasId,
        TargetMemberId,
        TargetMapId,
        SourceMemberId,
        SourceMapId,
        Applicability,
        Reason,
        EvidenceRefs);

/// <summary>DTO for one map-bound metadata-set alias.</summary>
public sealed record FirmwareMetadataSetAliasDocument(
    string AliasId,
    string TargetMemberId,
    string TargetMapId,
    string TargetMetadataSetId,
    string SourceMemberId,
    string SourceMapId,
    string SourceMetadataSetId,
    FirmwareAliasApplicabilityDocument Applicability,
    string Reason,
    IReadOnlyList<string> EvidenceRefs)
    : FirmwareFactAliasDocument(
        AliasId,
        TargetMemberId,
        TargetMapId,
        SourceMemberId,
        SourceMapId,
        Applicability,
        Reason,
        EvidenceRefs);

/// <summary>DTO for one map-bound capability fact alias.</summary>
public sealed record FirmwareCapabilityAliasDocument(
    string AliasId,
    string TargetMemberId,
    string TargetMapId,
    string TargetCapabilityFactId,
    string SourceMemberId,
    string SourceMapId,
    string SourceCapabilityFactId,
    FirmwareAliasApplicabilityDocument Applicability,
    string Reason,
    IReadOnlyList<string> EvidenceRefs)
    : FirmwareFactAliasDocument(
        AliasId,
        TargetMemberId,
        TargetMapId,
        SourceMemberId,
        SourceMapId,
        Applicability,
        Reason,
        EvidenceRefs);
