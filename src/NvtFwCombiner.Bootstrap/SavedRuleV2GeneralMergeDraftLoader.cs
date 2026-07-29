using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>One executable General Merge draft closed over by a Saved Rule v2 document.</summary>
internal sealed record SavedRuleV2GeneralMergeDraftLoadResult(
    GeneralMergeDraftState? Draft,
    SavedRuleV2ParentBinding? ParentBinding,
    IReadOnlyList<SavedRuleValidationIssue> Issues)
{
    internal bool IsValid =>
        Draft is not null &&
        ParentBinding is not null &&
        Issues.Count == 0;
}

/// <summary>Exact trusted bundle/profile/family identity closed over by one v2 rule.</summary>
internal sealed record SavedRuleV2ParentBinding(
    string BundleId,
    string BundleVersion,
    string BundleContentHash,
    string ProfileId,
    string ProfileVersion,
    string ProfileContentHash,
    string FamilyId,
    string FamilyVersion,
    string FamilyContentHash,
    string MapId);

/// <summary>
/// Projects the already versioned Saved Rule v2 initializer and mapping fragments into the one
/// canonical General Merge draft used by manual CLI, Preview, and Build.
/// </summary>
internal static partial class SavedRuleV2GeneralMergeDraftLoader
{
    internal static SavedRuleV2GeneralMergeDraftLoadResult Load(
        string path,
        IReadOnlyDictionary<string, string> slotsById,
        SavedRuleV2GeneralMergeAdmissionContext admissionContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(slotsById);
        ArgumentNullException.ThrowIfNull(admissionContext);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return Failed(
                Issue(
                    SavedRuleIssueCodes.FileNotFound,
                    $"Saved Rule v2 JSON was not found: {fullPath}",
                    "$"));
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
            return Parse(
                document.RootElement,
                slotsById,
                admissionContext);
        }
        catch (JsonException exception)
        {
            return Failed(Issue(
                SavedRuleIssueCodes.JsonInvalid,
                $"Saved Rule v2 JSON is invalid: {exception.Message}",
                "$"));
        }
        catch (IOException exception)
        {
            return Failed(Issue(
                SavedRuleIssueCodes.FileReadFailed,
                $"Saved Rule v2 JSON could not be read: {exception.Message}",
                "$"));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failed(Issue(
                SavedRuleIssueCodes.FileReadFailed,
                $"Saved Rule v2 JSON could not be read: {exception.Message}",
                "$"));
        }
    }

    private static SavedRuleV2GeneralMergeDraftLoadResult Parse(
        JsonElement root,
        IReadOnlyDictionary<string, string> slotsById,
        SavedRuleV2GeneralMergeAdmissionContext admissionContext)
    {
        SavedCompositionRuleV2AdmissionResult admission =
            SavedCompositionRuleV2Admission.ValidateGeneralMerge(
                root,
                admissionContext);
        if (!admission.IsValid)
        {
            return new SavedRuleV2GeneralMergeDraftLoadResult(
                null,
                admission.ParentBinding,
                admission.Issues);
        }

        List<SavedRuleValidationIssue> issues = [];
        string ruleId = ReadRequiredString(root, "ruleId", "$.ruleId", issues);
        string ruleVersion = ReadRequiredString(
            root,
            "ruleVersion",
            "$.ruleVersion",
            issues);
        HashSet<string> slotTemplateIds = ReadSlotTemplateIds(root, issues);
        GeneralMappingDraftRow[] rows = ReadMappings(
            root,
            slotsById,
            slotTemplateIds,
            ruleId,
            ruleVersion,
            issues);

        if (issues.Count != 0)
        {
            return new SavedRuleV2GeneralMergeDraftLoadResult(
                null,
                admission.ParentBinding,
                issues);
        }

        try
        {
            return new SavedRuleV2GeneralMergeDraftLoadResult(
                new GeneralMergeDraftState(
                    admission.Initializer!,
                    new GeneralMappingDraftState(rows)),
                admission.ParentBinding,
                []);
        }
        catch (ArgumentException exception)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.MappingRowInvalid,
                $"Saved Rule v2 cannot form one typed General Merge draft: {exception.Message}",
                "$.mappingFragments"));
            return new SavedRuleV2GeneralMergeDraftLoadResult(
                null,
                admission.ParentBinding,
                issues);
        }
    }

    private static HashSet<string> ReadSlotTemplateIds(
        JsonElement root,
        List<SavedRuleValidationIssue> issues)
    {
        HashSet<string> result = new(StringComparer.Ordinal);
        if (!root.TryGetProperty("slotTemplates", out JsonElement templates) ||
            templates.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.InputSlotTemplatesInvalid,
                "Saved Rule v2 slotTemplates must be an array.",
                "$.slotTemplates"));
            return result;
        }

        int index = 0;
        foreach (JsonElement template in templates.EnumerateArray())
        {
            string path = $"$.slotTemplates[{index++}]";
            if (template.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.InputSlotTemplateInvalid,
                    "Saved Rule v2 slot template must be an object.",
                    path));
                continue;
            }

            string id = ReadRequiredString(
                template,
                "slotTemplateId",
                $"{path}.slotTemplateId",
                issues);
            if (id.Length > 0 && !result.Add(id))
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.InputSlotTemplateDuplicate,
                    $"Saved Rule v2 slot template '{id}' is duplicated.",
                    $"{path}.slotTemplateId"));
            }
        }

        return result;
    }

    private static GeneralMappingDraftRow[] ReadMappings(
        JsonElement root,
        IReadOnlyDictionary<string, string> slotsById,
        IReadOnlySet<string> slotTemplateIds,
        string ruleId,
        string ruleVersion,
        List<SavedRuleValidationIssue> issues)
    {
        List<GeneralMappingDraftRow> rows = [];
        if (!root.TryGetProperty(
                "mappingFragments",
                out JsonElement fragments) ||
            fragments.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.OperationFragmentsRequired,
                "Saved Rule v2 mappingFragments must be an array.",
                "$.mappingFragments"));
            return [];
        }

        int index = 0;
        foreach (JsonElement fragment in fragments.EnumerateArray())
        {
            string path = $"$.mappingFragments[{index++}]";
            if (fragment.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.OperationFragmentInvalid,
                    "Saved Rule v2 mapping fragment must be an object.",
                    path));
                continue;
            }

            TryAddMapping(
                fragment,
                path,
                slotsById,
                slotTemplateIds,
                ruleId,
                ruleVersion,
                rows,
                issues);
        }

        if (fragments.GetArrayLength() == 0)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.OperationFragmentsEmpty,
                "Saved Rule v2 requires at least one General Merge mapping fragment.",
                "$.mappingFragments"));
        }

        return [.. rows];
    }

    private static void TryAddMapping(
        JsonElement fragment,
        string path,
        IReadOnlyDictionary<string, string> slotsById,
        IReadOnlySet<string> slotTemplateIds,
        string ruleId,
        string ruleVersion,
        List<GeneralMappingDraftRow> rows,
        List<SavedRuleValidationIssue> issues)
    {
        string fragmentId = ReadRequiredString(
            fragment,
            "fragmentId",
            $"{path}.fragmentId",
            issues);
        string operationKind = ReadRequiredString(
            fragment,
            "operationKind",
            $"{path}.operationKind",
            issues);
        if (!StringComparer.Ordinal.Equals(
                operationKind,
                SavedRuleSchemaTokens.OperationKindCopyRange))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.OperationFragmentKindUnsupported,
                "General Merge Saved Rule v2 mappings must use copy-range.",
                $"{path}.operationKind"));
        }

        string overlapPolicy = ReadRequiredString(
            fragment,
            "overlapPolicy",
            $"{path}.overlapPolicy",
            issues);
        if (!StringComparer.Ordinal.Equals(
                overlapPolicy,
                SavedRuleSchemaTokens.MappingOverlapReject))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.MappingRowOverlapPolicyUnsupported,
                "General Merge Saved Rule v2 mappings must reject overlap.",
                $"{path}.overlapPolicy"));
        }

        string targetRegionId = ReadRequiredString(
            fragment,
            "targetRegionId",
            $"{path}.targetRegionId",
            issues);
        if (!StringComparer.Ordinal.Equals(
                targetRegionId,
                WorkbenchGeneralMergeIds.OutputRegionId))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.MappingRowTargetRegionUnsupported,
                $"Current General Merge supports only target region '{WorkbenchGeneralMergeIds.OutputRegionId}'.",
                $"{path}.targetRegionId"));
        }

        string slotId = ReadSourceSlotId(
            fragment,
            path,
            slotTemplateIds,
            issues);
        string? sourcePath = null;
        if (slotId.Length == 0 ||
            !slotsById.TryGetValue(slotId, out sourcePath))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.MappingRowSourceReference,
                $"Saved Rule v2 mapping '{fragmentId}' requires --slot {slotId}=<path>.",
                $"{path}.sourceSlot"));
        }

        if (!TryReadRange(
                fragment,
                "sourceRange",
                $"{path}.sourceRange",
                issues,
                out ByteRange sourceRange) ||
            !TryReadNonNegativeLong(
                fragment,
                "targetOffset",
                $"{path}.targetOffset",
                issues,
                out long targetOffset))
        {
            return;
        }

        ByteRange targetRange;
        try
        {
            targetRange = new ByteRange(targetOffset, sourceRange.Length);
        }
        catch (ArgumentOutOfRangeException)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.RangeOverflow,
                $"Saved Rule v2 mapping '{fragmentId}' target range overflows.",
                $"{path}.targetOffset"));
            return;
        }

        string reason = ReadRequiredString(
            fragment,
            "reason",
            $"{path}.reason",
            issues);
        if (fragmentId.Length == 0 ||
            sourcePath is null ||
            operationKind != SavedRuleSchemaTokens.OperationKindCopyRange ||
            overlapPolicy != SavedRuleSchemaTokens.MappingOverlapReject ||
            targetRegionId != WorkbenchGeneralMergeIds.OutputRegionId ||
            reason.Length == 0)
        {
            return;
        }

        try
        {
            rows.Add(new GeneralMappingDraftRow(
                fragmentId,
                ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(sourcePath),
                sourceRange,
                CompositionAddressSpaceIds.OutputImage,
                targetRange,
                OverlapPolicy.Reject,
                alignment: 1,
                reason,
                WorkbenchGeneralMergeIds.OutputRegionId,
                OperationProvenance.SavedRule(ruleId, ruleVersion)));
        }
        catch (ArgumentException exception)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.MappingRowInvalid,
                $"Saved Rule v2 mapping '{fragmentId}' is invalid: {exception.Message}",
                path));
        }
    }

    private static string ReadSourceSlotId(
        JsonElement fragment,
        string path,
        IReadOnlySet<string> slotTemplateIds,
        List<SavedRuleValidationIssue> issues)
    {
        if (!fragment.TryGetProperty(
                "sourceSlot",
                out JsonElement sourceSlot) ||
            sourceSlot.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.MappingRowSourceReference,
                "Saved Rule v2 mapping requires sourceSlot.",
                $"{path}.sourceSlot"));
            return string.Empty;
        }

        string kind = ReadRequiredString(
            sourceSlot,
            "kind",
            $"{path}.sourceSlot.kind",
            issues);
        string propertyName = kind switch
        {
            "rule-slot" => "slotTemplateId",
            "parent-slot" => "slotId",
            _ => string.Empty,
        };
        if (propertyName.Length == 0)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.MappingRowSourceReference,
                "Saved Rule v2 sourceSlot.kind must be rule-slot or parent-slot.",
                $"{path}.sourceSlot.kind"));
            return string.Empty;
        }

        string slotId = ReadRequiredString(
            sourceSlot,
            propertyName,
            $"{path}.sourceSlot.{propertyName}",
            issues);
        if (kind == "rule-slot" &&
            slotId.Length > 0 &&
            !slotTemplateIds.Contains(slotId))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.MappingRowSourceSlotTemplateUnknown,
                $"Saved Rule v2 mapping references undeclared slot template '{slotId}'.",
                $"{path}.sourceSlot.{propertyName}"));
        }

        return slotId;
    }

}
