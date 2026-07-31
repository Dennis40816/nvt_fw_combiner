using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Contracts;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Exact Parent facts and canonical target regions admitted for General Replace.</summary>
internal sealed record SavedRuleV2GeneralReplaceAdmissionContext(
    SavedRuleV2ParentBinding ParentBinding,
    string PromotionStage,
    IReadOnlyList<SavedRuleV2ParentInputPolicy> InputPolicies,
    IReadOnlyList<string> ValidationRuleIds,
    IReadOnlyList<string> ProcessorStageIds,
    IReadOnlyDictionary<string, ByteRange> TargetRegions);

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
