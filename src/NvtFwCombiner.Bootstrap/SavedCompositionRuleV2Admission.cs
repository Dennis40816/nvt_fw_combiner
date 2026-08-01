using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Contracts;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

/// <summary>One trusted parent input policy available to Saved Rule v2 narrowing.</summary>
internal sealed record SavedRuleV2ParentInputPolicy(
    string SlotId,
    string Role,
    CompositionProfileSlotCardinality Cardinality,
    IReadOnlyList<string> AcceptedExtensions);

/// <summary>
/// Immutable trusted-parent facts required before a Saved Rule v2 document may
/// materialize a General Merge draft.
/// </summary>
internal sealed record SavedRuleV2GeneralMergeAdmissionContext(
    SavedRuleV2ParentBinding ParentBinding,
    string PromotionStage,
    IReadOnlyList<SavedRuleV2ParentInputPolicy> InputPolicies,
    IReadOnlyList<string> ValidationRuleIds,
    IReadOnlyList<string> ProcessorStageIds);

/// <summary>Result of complete schema and exact-parent admission.</summary>
internal sealed record SavedCompositionRuleV2AdmissionResult(
    SavedRuleV2ParentBinding? ParentBinding,
    GeneralMergeOutputInitializer? Initializer,
    IReadOnlyList<SavedRuleValidationIssue> Issues)
{
    internal bool IsValid =>
        ParentBinding is not null &&
        Issues.Count == 0;
}

/// <summary>
/// Applies the canonical v2 schema and the parent-relative rules that JSON
/// Schema cannot express. Draft projection may run only after this succeeds.
/// </summary>
internal static partial class SavedCompositionRuleV2Admission
{
    internal static SavedCompositionRuleV2AdmissionResult ValidateGeneralMerge(
        JsonElement root,
        SavedRuleV2GeneralMergeAdmissionContext context)
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
            return new SavedCompositionRuleV2AdmissionResult(
                null,
                null,
                issues);
        }

        SavedRuleV2ParentBinding parentBinding =
            NormalizeParentBinding(root.GetProperty("parentBinding"));
        GeneralMergeOutputInitializer initializer =
            NormalizeInitializer(root.GetProperty("imageInitialization"));
        if (parentBinding != context.ParentBinding)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                "Saved Rule v2 parentBinding does not match the exact trusted General Merge parent.",
                "$.parentBinding"));
        }

        ValidatePromotion(root, context, issues);
        ValidateUniqueObjectIds(root, issues);
        ValidateSlotNarrowing(root, context, issues);
        ValidateParentReferences(root, context, issues);
        ValidateSourceSlotReferences(root, context, issues);
        ValidateAccessNarrowing(root, targetRegions: null, issues);

        return new SavedCompositionRuleV2AdmissionResult(
            parentBinding,
            initializer,
            issues);
    }

    private static SavedRuleV2ParentBinding NormalizeParentBinding(
        JsonElement parent)
    {
        return new SavedRuleV2ParentBinding(
            parent.GetProperty("bundleId").GetString()!,
            parent.GetProperty("bundleVersion").GetString()!,
            parent.GetProperty("bundleContentHash").GetString()!,
            parent.GetProperty("profileId").GetString()!,
            parent.GetProperty("profileVersion").GetString()!,
            parent.GetProperty("profileContentHash").GetString()!,
            parent.GetProperty("familyId").GetString()!,
            parent.GetProperty("familyVersion").GetString()!,
            parent.GetProperty("familyContentHash").GetString()!,
            parent.GetProperty("mapId").GetString()!);
    }

    private static GeneralMergeOutputInitializer NormalizeInitializer(
        JsonElement initialization)
    {
        long capacity = initialization.GetProperty("capacity").GetInt64();
        byte fillByte = initialization.TryGetProperty(
            "fillByte",
            out JsonElement fill)
            ? checked((byte)fill.GetInt32())
            : GeneralMergeOutputInitializer.DefaultFillByte;
        return new GeneralMergeOutputInitializer(capacity, fillByte);
    }

    private static void ValidatePromotion(
        JsonElement root,
        SavedRuleV2GeneralMergeAdmissionContext context,
        List<SavedRuleValidationIssue> issues)
    {
        JsonElement promotion = root.GetProperty("promotion");
        string stage = promotion.GetProperty("stage").GetString()!;
        if (!StringComparer.Ordinal.Equals(stage, context.PromotionStage))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                $"Saved Rule v2 promotion stage must match trusted parent stage '{context.PromotionStage}' for normal execution.",
                "$.promotion.stage"));
        }

        if (root.GetProperty("reviewers").GetArrayLength() == 0)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                "Normal Saved Rule v2 execution requires at least one declared reviewer.",
                "$.reviewers"));
        }

        int blockerIndex = 0;
        foreach (JsonElement blocker in promotion.GetProperty("blockers").EnumerateArray())
        {
            string kind = blocker.GetProperty("kind").GetString()!;
            if (kind is not (
                    SavedRuleSchemaTokens.PromotionBlockerGolden or
                    SavedRuleSchemaTokens.PromotionBlockerHumanReview))
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                    $"Executable-candidate Saved Rule v2 cannot run with unresolved '{kind}' promotion debt.",
                    $"$.promotion.blockers[{blockerIndex}].kind"));
            }

            blockerIndex++;
        }
    }

    private static void ValidateUniqueObjectIds(
        JsonElement root,
        List<SavedRuleValidationIssue> issues)
    {
        ValidateUniqueObjectIds(
            root.GetProperty("slotTemplates"),
            "slotTemplateId",
            "$.slotTemplates",
            issues);
        ValidateUniqueObjectIds(
            root.GetProperty("mappingFragments"),
            "fragmentId",
            "$.mappingFragments",
            issues);
        ValidateUniqueObjectIds(
            root.GetProperty("promotion").GetProperty("blockers"),
            "blockerId",
            "$.promotion.blockers",
            issues);
    }

    private static void ValidateUniqueObjectIds(
        JsonElement array,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            string id = item.GetProperty(propertyName).GetString()!;
            if (!ids.Add(id))
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.V2ContractInvalid,
                    $"Saved Rule v2 {propertyName} '{id}' is duplicated.",
                    $"{path}[{index}].{propertyName}"));
            }

            index++;
        }
    }

    private static void ValidateSlotNarrowing(
        JsonElement root,
        SavedRuleV2GeneralMergeAdmissionContext context,
        List<SavedRuleValidationIssue> issues)
    {
        int index = 0;
        foreach (JsonElement template in root.GetProperty("slotTemplates").EnumerateArray())
        {
            string path = $"$.slotTemplates[{index++}]";
            string role = template.GetProperty("role").GetString()!;
            SavedRuleV2ParentInputPolicy? parent = context.InputPolicies.SingleOrDefault(
                candidate => StringComparer.Ordinal.Equals(candidate.Role, role));
            if (parent is null)
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                    $"Saved Rule v2 slot role '{role}' is not declared by the trusted parent.",
                    $"{path}.role"));
                continue;
            }

            string cardinality = template.GetProperty("cardinality").GetString()!;
            if (!StringComparer.Ordinal.Equals(
                    cardinality,
                    SavedRuleSchemaTokens.InputSlotCardinalityOne) ||
                parent.Cardinality !=
                CompositionProfileSlotCardinality.OneOrMore)
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                    "Saved Rule v2 execution requires one concrete binding that narrows the trusted one-or-more parent slot.",
                    $"{path}.cardinality"));
            }

            foreach (JsonElement extensionElement in template
                         .GetProperty("acceptedExtensions")
                         .EnumerateArray())
            {
                string extension = extensionElement.GetString()!;
                if (!parent.AcceptedExtensions.Contains(
                        extension,
                        StringComparer.OrdinalIgnoreCase))
                {
                    issues.Add(Issue(
                        SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                        $"Saved Rule v2 extension '{extension}' broadens trusted parent slot '{parent.SlotId}'.",
                        $"{path}.acceptedExtensions"));
                }
            }
        }
    }

    private static void ValidateParentReferences(
        JsonElement root,
        SavedRuleV2GeneralMergeAdmissionContext context,
        List<SavedRuleValidationIssue> issues)
    {
        ValidateReferenceArray(
            root.GetProperty("validationRuleIds"),
            context.ValidationRuleIds,
            "$.validationRuleIds",
            "validation rule",
            issues);

        JsonElement processorStages = root.GetProperty("processorStageIds");
        ValidateExactReferenceArray(
            processorStages,
            context.ProcessorStageIds,
            "$.processorStageIds",
            "processor stage",
            issues);
        if (processorStages.GetArrayLength() != 0)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.ProcessorDependencyUnsupported,
                "Current Saved Rule v2 execution does not support processor stages.",
                "$.processorStageIds"));
        }
    }

    private static void ValidateExactReferenceArray(
        JsonElement values,
        IReadOnlyList<string> expected,
        string path,
        string referenceKind,
        List<SavedRuleValidationIssue> issues)
    {
        string[] actual =
        [
            .. values.EnumerateArray().Select(
                static item => item.GetString()!),
        ];
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                $"Saved Rule v2 must preserve the exact ordered Parent {referenceKind} list.",
                path));
        }
    }

    private static void ValidateReferenceArray(
        JsonElement values,
        IReadOnlyList<string> allowed,
        string path,
        string referenceKind,
        List<SavedRuleValidationIssue> issues)
    {
        int index = 0;
        foreach (JsonElement item in values.EnumerateArray())
        {
            string id = item.GetString()!;
            if (!allowed.Contains(id, StringComparer.Ordinal))
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                    $"Saved Rule v2 references unknown parent {referenceKind} '{id}'.",
                    $"{path}[{index}]"));
            }

            index++;
        }
    }

    private static void ValidateSourceSlotReferences(
        JsonElement root,
        SavedRuleV2GeneralMergeAdmissionContext context,
        List<SavedRuleValidationIssue> issues)
    {
        var ruleSlots = root.GetProperty("slotTemplates")
            .EnumerateArray()
            .Select(static template =>
                template.GetProperty("slotTemplateId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var parentSlots = context.InputPolicies
            .Select(static policy => policy.SlotId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string slotId in ruleSlots.Intersect(
                     parentSlots,
                     StringComparer.Ordinal))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                $"Saved Rule v2 rule-slot '{slotId}' collides with exact Parent slot '{slotId}'.",
                "$.slotTemplates"));
        }

        int index = 0;
        foreach (JsonElement fragment in root.GetProperty("mappingFragments").EnumerateArray())
        {
            JsonElement source = fragment.GetProperty("sourceSlot");
            string kind = source.GetProperty("kind").GetString()!;
            string id = kind == "rule-slot"
                ? source.GetProperty("slotTemplateId").GetString()!
                : source.GetProperty("slotId").GetString()!;
            bool exists = kind == "rule-slot"
                ? ruleSlots.Contains(id)
                : parentSlots.Contains(id);
            if (!exists)
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                    $"Saved Rule v2 mapping references unknown {kind} '{id}'.",
                    $"$.mappingFragments[{index}].sourceSlot"));
            }

            index++;
        }
    }

    private static void ValidateAccessNarrowing(
        JsonElement root,
        IReadOnlyDictionary<string, ByteRange>? targetRegions,
        List<SavedRuleValidationIssue> issues)
    {
        JsonElement envelope = root.GetProperty("accessEnvelope");
        var allowedRegions = envelope
            .GetProperty("allowedRegionIds")
            .EnumerateArray()
            .Select(static item => item.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        if (targetRegions is null &&
            !allowedRegions.SetEquals([WorkbenchGeneralMergeIds.OutputRegionId]))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.MappingRowTargetRegionUnsupported,
                $"General Merge Saved Rule v2 accessEnvelope must close over only '{WorkbenchGeneralMergeIds.OutputRegionId}'.",
                "$.accessEnvelope.allowedRegionIds"));
        }
        else if (targetRegions is not null)
        {
            foreach (string regionId in allowedRegions.Where(
                         regionId => !targetRegions.ContainsKey(regionId)))
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                    $"General Replace Saved Rule region '{regionId}' is not writable by its exact Parent.",
                    "$.accessEnvelope.allowedRegionIds"));
            }
        }

        JsonElement fragments = root.GetProperty("mappingFragments");
        long totalWriteBytes = 0;
        int index = 0;
        foreach (JsonElement fragment in fragments.EnumerateArray())
        {
            string path = $"$.mappingFragments[{index}]";
            string targetRegionId =
                fragment.GetProperty("targetRegionId").GetString()!;
            bool parentAllowsTarget = targetRegions is null ||
                targetRegions.ContainsKey(targetRegionId);
            if (!allowedRegions.Contains(targetRegionId) ||
                !parentAllowsTarget)
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.MappingRowTargetRegionUnsupported,
                    $"Saved Rule v2 mapping target '{targetRegionId}' is outside its exact Parent or accessEnvelope.",
                    $"{path}.targetRegionId"));
            }

            JsonElement lengthElement = fragment
                .GetProperty("sourceRange")
                .GetProperty("length");
            if (!lengthElement.TryGetInt64(out long length))
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.RangeOverflow,
                    "Saved Rule v2 source range length exceeds the supported address size.",
                    $"{path}.sourceRange.length"));
            }
            else
            {
                try
                {
                    if (targetRegions is not null &&
                        targetRegions.TryGetValue(
                            targetRegionId,
                            out ByteRange targetRegion))
                    {
                        long offset = fragment
                            .GetProperty("targetOffset")
                            .GetInt64();
                        ByteRange target = new(
                            checked(targetRegion.Start + offset),
                            length);
                        if (!targetRegion.Contains(target))
                        {
                            issues.Add(Issue(
                                SavedRuleIssueCodes.MappingRowTargetRegionUnsupported,
                                $"General Replace Saved Rule target range is outside canonical region '{targetRegionId}'.",
                                $"{path}.targetOffset"));
                        }
                    }

                    totalWriteBytes = checked(totalWriteBytes + length);
                }
                catch (Exception exception) when (
                    exception is ArgumentOutOfRangeException or
                    OverflowException)
                {
                    issues.Add(Issue(
                        SavedRuleIssueCodes.RangeOverflow,
                        "Saved Rule v2 mapping range or total write length overflows.",
                        path));
                }
            }

            index++;
        }

        if (!envelope.GetProperty("maximumMappingCount").TryGetInt32(
                out int maximumMappingCount) ||
            !envelope.GetProperty("maximumTotalWriteBytes").TryGetInt64(
                out long maximumTotalWriteBytes))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.RangeOverflow,
                "Saved Rule v2 accessEnvelope limits exceed the supported address size.",
                "$.accessEnvelope"));
        }
        else if (fragments.GetArrayLength() > maximumMappingCount ||
                 totalWriteBytes > maximumTotalWriteBytes)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                "Saved Rule v2 mappings exceed their closed accessEnvelope.",
                "$.accessEnvelope"));
        }
    }

    private static void ValidateUniqueProperties(
        JsonElement element,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                ValidateUniqueProperties(item, $"{path}[{index++}]", issues);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            string propertyPath = $"{path}.{property.Name}";
            if (!names.Add(property.Name))
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.V2ContractInvalid,
                    $"Saved Rule v2 property '{property.Name}' is duplicated.",
                    propertyPath));
            }

            ValidateUniqueProperties(property.Value, propertyPath, issues);
        }
    }

    private static SavedRuleValidationIssue Issue(
        string code,
        string message,
        string path)
    {
        return new SavedRuleValidationIssue(code, message, path);
    }
}
