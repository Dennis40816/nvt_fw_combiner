namespace NvtFwCombiner.Contracts.Profiles;

/// <summary>DTO for one schema-validated <c>composition-profile-v2</c> document.</summary>
public sealed record CompositionProfileDocument(
    string SchemaVersion,
    string ProfileId,
    string ProfileVersion,
    CompositionProfilePromotionDocument Promotion,
    string CompositionKind,
    string? IcNumberInputMode,
    CompositionProfileExperienceDocument Experience,
    CompositionProfileMapBindingDocument MapBinding,
    IReadOnlyList<CompositionProfileInputSlotDocument> InputSlots,
    IReadOnlyList<CompositionProfileSpaceDocument> Spaces,
    IReadOnlyList<CompositionProfileViewDocument> Views,
    IReadOnlyList<CompositionProfileMetadataBindingDocument> MetadataBindings,
    IReadOnlyList<CompositionProfileRegionAccessRuleDocument> RegionAccessRules,
    IReadOnlyList<CompositionProfileOperationDocument> Operations,
    IReadOnlyList<CompositionProfileValidationDocument> Validations,
    IReadOnlyList<CompositionProfileProcessorStageDocument> ProcessorStages,
    CompositionProfileOutputDocument Output,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>DTO for profile promotion state and its explicit blockers.</summary>
public sealed record CompositionProfilePromotionDocument(
    string Stage,
    IReadOnlyList<CompositionProfilePromotionBlockerDocument> Blockers);

/// <summary>DTO for one promotion blocker and its evidence.</summary>
public sealed record CompositionProfilePromotionBlockerDocument(
    string BlockerId,
    string Kind,
    string Reason,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>DTO for orthogonal authoring experience policy.</summary>
public sealed record CompositionProfileExperienceDocument(
    string ExperienceId,
    string Audience,
    string LayoutPolicy,
    string InputPolicy,
    string TopologyAuthoring,
    string DisplayNameKey);

/// <summary>DTO for the exact trusted family and candidate maps accepted by a profile.</summary>
public sealed record CompositionProfileMapBindingDocument(
    string FamilyId,
    string FamilyVersion,
    string FamilyContentHash,
    IReadOnlyList<string> MapIds,
    IReadOnlyList<string> RequiredRegionIds,
    IReadOnlyList<string> RequiredMetadataStructureIds,
    IReadOnlyList<string> RequiredCapabilityIds);
