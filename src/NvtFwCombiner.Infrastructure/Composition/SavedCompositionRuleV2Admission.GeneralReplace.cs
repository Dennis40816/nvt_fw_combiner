using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Contracts;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>Exact Parent facts and canonical target regions admitted for General Replace.</summary>
internal sealed record SavedRuleV2GeneralReplaceAdmissionContext(
    SavedRuleV2ParentBinding ParentBinding,
    string PromotionStage,
    IReadOnlyList<SavedRuleV2ParentInputPolicy> InputPolicies,
    IReadOnlyList<string> ValidationRuleIds,
    IReadOnlyList<string> ProcessorStageIds,
    IReadOnlyDictionary<string, ByteRange> TargetRegions);

/// <summary>One hash-bound General Replace Parent projected from a trusted catalog.</summary>
internal sealed record SavedRuleV2GeneralReplaceExactParent(
    SavedRuleV2GeneralReplaceAdmissionContext Admission,
    GeneralReplaceRuntimeAuthority Runtime);

/// <summary>Projects Saved Rule and runtime authority from the same trusted catalog entry.</summary>
internal static class SavedRuleV2GeneralReplaceExactParentResolver
{
    internal static SavedRuleV2GeneralReplaceExactParent Resolve(
        TrustedProfileBundleCatalog catalog,
        string profileId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        TrustedCompositionProfileCatalogEntry entry =
            catalog.Profiles.Single(candidate =>
                StringComparer.Ordinal.Equals(
                    candidate.Profile.ProfileId,
                    profileId));
        CompositionProfileDefinition profile = entry.Profile;
        if (profile.Promotion.Stage !=
                CompiledProfilePromotionStage.ExecutableCandidate ||
            profile.CompositionKind != CompositionKind.Replace ||
            !StringComparer.Ordinal.Equals(
                profile.Header.ExperienceId,
                ExperienceIds.GeneralReplace))
        {
            throw new InvalidDataException(
                "General Replace exact Parent must be one executable-candidate General Replace profile.");
        }

        string mapId = profile.MapBinding.MapIds.Single();
        FirmwareImageMap map = entry.Family.Family.ImageMaps.Single(
            candidate => StringComparer.Ordinal.Equals(
                candidate.MapId,
                mapId));
        HashSet<string> writableRegionIds =
        [
            .. profile.RegionAccessRules
                .Where(static rule =>
                    rule.Access == RegionAccessKind.ExplicitRange)
                .Select(static rule => rule.RegionId),
        ];
        ProfileBundleIdentity bundle = catalog.BundleIdentity;
        var parent = new SavedRuleV2ParentBinding(
            bundle.BundleId,
            bundle.BundleVersion,
            bundle.ContentHash,
            profile.ProfileId,
            profile.ProfileVersion,
            entry.Identity.ContentHash,
            entry.Family.Family.FamilyId,
            entry.Family.Family.FamilyVersion,
            entry.Family.Family.FamilyContentHash,
            mapId);
        var admission = new SavedRuleV2GeneralReplaceAdmissionContext(
            parent,
            SavedRuleSchemaTokens.PromotionStageExecutableCandidate,
            [
                .. profile.InputSlots.Select(static slot =>
                    new SavedRuleV2ParentInputPolicy(
                        slot.SlotId,
                        slot.Role,
                        slot.Cardinality,
                        [.. slot.AcceptedExtensions])),
            ],
            [.. profile.Validations.Select(static validation => validation.RuleId)],
            [
                .. profile.ProcessorStages.Select(
                    static stage => stage.ProcessorStageId),
            ],
            map.Regions
                .Where(region => writableRegionIds.Contains(region.RegionId))
                .ToDictionary(
                    static region => region.RegionId,
                    static region => region.Range,
                    StringComparer.Ordinal));
        var runtime = new GeneralReplaceRuntimeAuthority(
            parent,
            admission.ProcessorStageIds,
            [
                .. profile.ProcessorStages
                    .OfType<LegacyCombinerProfileProcessorStage>()
                    .Select(static stage =>
                        new ExternalProcessorDependencyReference(
                            stage.InvocationProfileId,
                            stage.ToolBindingId)),
            ]);
        return new SavedRuleV2GeneralReplaceExactParent(
            admission,
            runtime);
    }
}

internal static partial class SavedCompositionRuleV2Admission
{
    internal static SavedCompositionRuleV2AdmissionResult ValidateGeneralReplace(
        JsonElement root,
        SavedRuleV2GeneralReplaceAdmissionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        List<SavedRuleValidationIssue> issues = [];
        if (!SavedCompositionRuleV2Schema.IsValid(root))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.V2ContractInvalid,
                "Saved Rule v2 does not satisfy the complete canonical contract schema.",
                "$"));
        }

        ValidateUniqueProperties(root, "$", issues);
        if (issues.Count != 0)
        {
            return new SavedCompositionRuleV2AdmissionResult(null, null, issues);
        }

        SavedRuleV2ParentBinding parent =
            NormalizeParentBinding(root.GetProperty("parentBinding"));
        if (parent != context.ParentBinding)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                "Saved Rule v2 parentBinding does not match the exact trusted General Replace parent.",
                "$.parentBinding"));
        }

        var common = new SavedRuleV2GeneralMergeAdmissionContext(
            context.ParentBinding,
            context.PromotionStage,
            context.InputPolicies,
            context.ValidationRuleIds,
            context.ProcessorStageIds);
        ValidatePromotion(root, common, issues);
        ValidateUniqueObjectIds(root, issues);
        ValidateSlotNarrowing(root, common, issues);
        ValidateParentReferences(root, common, issues);
        ValidateSourceSlotReferences(root, common, issues);
        ValidateAccessNarrowing(root, context.TargetRegions, issues);
        return new SavedCompositionRuleV2AdmissionResult(parent, null, issues);
    }
}
