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
        IReadOnlyDictionary<string, string> slotsById)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(slotsById);

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
            return Parse(document.RootElement, slotsById);
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
        IReadOnlyDictionary<string, string> slotsById)
    {
        SavedRuleV2GeneralMergeInitializerLoadResult initialization =
            SavedRuleV2GeneralMergeInitializerLoader.Parse(root);
        List<SavedRuleValidationIssue> issues = [.. initialization.Issues];
        string ruleId = ReadRequiredString(root, "ruleId", "$.ruleId", issues);
        string ruleVersion = ReadRequiredString(
            root,
            "ruleVersion",
            "$.ruleVersion",
            issues);
        SavedRuleV2ParentBinding? parentBinding =
            ReadParentBinding(root, issues);
        ValidateProcessorStages(root, issues);
        HashSet<string> slotTemplateIds = ReadSlotTemplateIds(root, issues);
        GeneralMappingDraftRow[] rows = ReadMappings(
            root,
            slotsById,
            slotTemplateIds,
            ruleId,
            ruleVersion,
            issues);
        ValidateAccessEnvelope(root, rows, issues);

        if (initialization.Initializer is null || issues.Count != 0)
        {
            return new SavedRuleV2GeneralMergeDraftLoadResult(
                null,
                parentBinding,
                issues);
        }

        try
        {
            return new SavedRuleV2GeneralMergeDraftLoadResult(
                new GeneralMergeDraftState(
                    initialization.Initializer,
                    new GeneralMappingDraftState(rows)),
                parentBinding,
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
                parentBinding,
                issues);
        }
    }

    private static SavedRuleV2ParentBinding? ReadParentBinding(
        JsonElement root,
        List<SavedRuleValidationIssue> issues)
    {
        if (!root.TryGetProperty("parentBinding", out JsonElement parent) ||
            parent.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.CompatibilityRequired,
                "Saved Rule v2 requires an exact parentBinding object.",
                "$.parentBinding"));
            return null;
        }

        int issueCount = issues.Count;
        string bundleId = ReadRequiredString(
            parent,
            "bundleId",
            "$.parentBinding.bundleId",
            issues);
        string bundleVersion = ReadRequiredString(
            parent,
            "bundleVersion",
            "$.parentBinding.bundleVersion",
            issues);
        string bundleContentHash = ReadSha256(
            parent,
            "bundleContentHash",
            "$.parentBinding.bundleContentHash",
            issues);
        string profileId = ReadRequiredString(
            parent,
            "profileId",
            "$.parentBinding.profileId",
            issues);
        string profileVersion = ReadRequiredString(
            parent,
            "profileVersion",
            "$.parentBinding.profileVersion",
            issues);
        string profileContentHash = ReadSha256(
            parent,
            "profileContentHash",
            "$.parentBinding.profileContentHash",
            issues);
        string familyId = ReadRequiredString(
            parent,
            "familyId",
            "$.parentBinding.familyId",
            issues);
        string familyVersion = ReadRequiredString(
            parent,
            "familyVersion",
            "$.parentBinding.familyVersion",
            issues);
        string familyContentHash = ReadSha256(
            parent,
            "familyContentHash",
            "$.parentBinding.familyContentHash",
            issues);
        string mapId = ReadRequiredString(
            parent,
            "mapId",
            "$.parentBinding.mapId",
            issues);
        return issues.Count == issueCount
            ? new SavedRuleV2ParentBinding(
                bundleId,
                bundleVersion,
                bundleContentHash,
                profileId,
                profileVersion,
                profileContentHash,
                familyId,
                familyVersion,
                familyContentHash,
                mapId)
            : null;
    }

    private static void ValidateProcessorStages(
        JsonElement root,
        List<SavedRuleValidationIssue> issues)
    {
        if (!root.TryGetProperty(
                "processorStageIds",
                out JsonElement stages) ||
            stages.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.ArrayRequired,
                "Saved Rule v2 requires processorStageIds.",
                "$.processorStageIds"));
            return;
        }

        if (stages.GetArrayLength() > 0)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.ProcessorDependencyUnsupported,
                "Current General Merge Saved Rule v2 consumption does not support processor stages.",
                "$.processorStageIds"));
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

    private static void ValidateAccessEnvelope(
        JsonElement root,
        GeneralMappingDraftRow[] rows,
        List<SavedRuleValidationIssue> issues)
    {
        if (!root.TryGetProperty(
                "accessEnvelope",
                out JsonElement envelope) ||
            envelope.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.CompatibilityRequired,
                "Saved Rule v2 requires accessEnvelope.",
                "$.accessEnvelope"));
            return;
        }

        HashSet<string> allowedRegionIds = ReadRequiredStringArray(
            envelope,
            "allowedRegionIds",
            "$.accessEnvelope.allowedRegionIds",
            issues);
        if (!allowedRegionIds.SetEquals([WorkbenchGeneralMergeIds.OutputRegionId]))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.MappingRowTargetRegionUnsupported,
                $"General Merge Saved Rule v2 accessEnvelope must close over only '{WorkbenchGeneralMergeIds.OutputRegionId}'.",
                "$.accessEnvelope.allowedRegionIds"));
        }

        string protectedRangePolicy = ReadRequiredString(
            envelope,
            "protectedRangePolicy",
            "$.accessEnvelope.protectedRangePolicy",
            issues);
        if (protectedRangePolicy is not ("deny-touch" or "parent-profile"))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.EnumInvalid,
                "Saved Rule v2 protectedRangePolicy must be deny-touch or parent-profile.",
                "$.accessEnvelope.protectedRangePolicy"));
        }

        if (!TryReadPositiveLong(
                envelope,
                "maximumMappingCount",
                "$.accessEnvelope.maximumMappingCount",
                issues,
                out long maximumMappingCount) ||
            !TryReadPositiveLong(
                envelope,
                "maximumTotalWriteBytes",
                "$.accessEnvelope.maximumTotalWriteBytes",
                issues,
                out long maximumTotalWriteBytes))
        {
            return;
        }

        long totalWriteBytes;
        try
        {
            totalWriteBytes = rows.Aggregate(
                0L,
                static (total, row) => checked(total + row.TargetRange.Length));
        }
        catch (OverflowException)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.RangeOverflow,
                "Saved Rule v2 total mapping length overflows.",
                "$.mappingFragments"));
            return;
        }

        if (rows.Length > maximumMappingCount ||
            totalWriteBytes > maximumTotalWriteBytes)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.MappingRowsEmpty,
                "Saved Rule v2 mappings exceed their closed accessEnvelope.",
                "$.accessEnvelope"));
        }
    }

}
