using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>One executable typed draft closed over by a Saved Rule v2 document.</summary>
internal sealed record SavedRuleV2DraftLoadResult<TDraft>(
    TDraft? Draft,
    SavedRuleV2ParentBinding? ParentBinding,
    SavedRuleExecutionIdentity? ExecutionIdentity,
    GeneralSavedRuleResourcePolicy? ResourcePolicy,
    IReadOnlyList<SavedRuleValidationIssue> Issues)
    where TDraft : class
{
    internal bool IsValid =>
        Draft is not null &&
        ParentBinding is not null &&
        ExecutionIdentity is not null &&
        ResourcePolicy is not null &&
        Issues.Count == 0;
}

/// <summary>
/// Projects the already versioned Saved Rule v2 initializer and mapping fragments into the one
/// canonical General Merge draft used by manual CLI, Preview, and Build.
/// </summary>
internal static partial class SavedRuleV2GeneralMergeDraftLoader
{
    internal static SavedRuleV2DraftLoadResult<GeneralMergeDraftState> Load(
        string path,
        IReadOnlyDictionary<string, string> slotsById,
        SavedRuleV2GeneralMergeAdmissionContext admissionContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(slotsById);
        ArgumentNullException.ThrowIfNull(admissionContext);
        return LoadFile(
            path,
            root => Parse(root, slotsById, admissionContext),
            Failed);
    }

    private static SavedRuleV2DraftLoadResult<GeneralMergeDraftState> Parse(
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
            return new SavedRuleV2DraftLoadResult<GeneralMergeDraftState>(
                null,
                admission.ParentBinding,
                null,
                null,
                admission.Issues);
        }

        SavedRuleV2MappingProjection? projection = ProjectMappings(
            root,
            slotsById,
            targetRegions: null,
            admission.ParentBinding!,
            out IReadOnlyList<SavedRuleValidationIssue> issues);
        return projection is null
            ? new SavedRuleV2DraftLoadResult<GeneralMergeDraftState>(
                null,
                admission.ParentBinding,
                null,
                null,
                issues)
            : new SavedRuleV2DraftLoadResult<GeneralMergeDraftState>(
                new GeneralMergeDraftState(
                    admission.Initializer!,
                    projection.Draft),
                admission.ParentBinding,
                projection.ExecutionIdentity,
                projection.ResourcePolicy,
                []);
    }

    private static SavedRuleV2MappingProjection? ProjectMappings(
        JsonElement root,
        IReadOnlyDictionary<string, string> slotsById,
        IReadOnlyDictionary<string, ByteRange>? targetRegions,
        SavedRuleV2ParentBinding parentBinding,
        out IReadOnlyList<SavedRuleValidationIssue> issues)
    {
        List<SavedRuleValidationIssue> issueList = [];
        string ruleId = ReadRequiredString(
            root,
            "ruleId",
            "$.ruleId",
            issueList);
        string ruleVersion = ReadRequiredString(
            root,
            "ruleVersion",
            "$.ruleVersion",
            issueList);
        GeneralMappingDraftRow[] rows = ReadMappings(
            root,
            slotsById,
            ReadSlotTemplateIds(root, issueList),
            targetRegions,
            ruleId,
            ruleVersion,
            issueList);
        if (issueList.Count != 0)
        {
            issues = issueList;
            return null;
        }

        try
        {
            SavedRuleExecutionIdentity executionIdentity =
                CreateExecutionIdentity(
                    root,
                    ruleId,
                    ruleVersion,
                    parentBinding);
            issues = [];
            return new SavedRuleV2MappingProjection(
                new GeneralMappingDraftState(rows),
                executionIdentity,
                CreateSavedRuleResourcePolicy(root, executionIdentity, rows));
        }
        catch (ArgumentException exception)
        {
            issueList.Add(Issue(
                SavedRuleIssueCodes.MappingRowInvalid,
                $"Saved Rule v2 cannot form one typed General {(targetRegions is null ? "Merge" : "Replace")} draft: {exception.Message}",
                "$.mappingFragments"));
            issues = issueList;
            return null;
        }
    }

    private sealed record SavedRuleV2MappingProjection(
        GeneralMappingDraftState Draft,
        SavedRuleExecutionIdentity ExecutionIdentity,
        GeneralSavedRuleResourcePolicy ResourcePolicy);

    private static SavedRuleExecutionIdentity CreateExecutionIdentity(
        JsonElement root,
        string ruleId,
        string ruleVersion,
        SavedRuleV2ParentBinding parent)
    {
        return new SavedRuleExecutionIdentity(
            ruleId,
            ruleVersion,
            SavedCompositionRuleV2ContentHasher.Calculate(root),
            parent);
    }

    private static GeneralSavedRuleResourcePolicy CreateSavedRuleResourcePolicy(
        JsonElement root,
        SavedRuleExecutionIdentity executionIdentity,
        IReadOnlyList<GeneralMappingDraftRow> rows)
    {
        JsonElement accessEnvelope = root.GetProperty("accessEnvelope");
        GeneralResourceLimits technical =
            GeneralAuthoringTechnicalLimits.Default;
        return new GeneralSavedRuleResourcePolicy(
            executionIdentity,
            new GeneralResourceLimits(
                accessEnvelope.GetProperty("maximumMappingCount").GetInt32(),
                accessEnvelope.GetProperty("maximumTotalWriteBytes").GetInt64(),
                technical.MaximumFileBytes,
                technical.MaximumSafeMaterializationBytes,
                rows
                    .Where(static row =>
                        row.Source.Kind ==
                        GeneralMappingSourceKind.FileArtifact)
                    .Select(static row =>
                        new GeneralSlotLengthLimits(
                            row.MappingId,
                            minimumBytes: 1,
                            maximumBytes: int.MaxValue))));
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
        IReadOnlyDictionary<string, ByteRange>? targetRegions,
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
                targetRegions,
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
        IReadOnlyDictionary<string, ByteRange>? targetRegions,
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
        bool isReplace = targetRegions is not null;
        string expectedOperationKind = isReplace
            ? SavedRuleSchemaTokens.OperationKindReplaceRange
            : SavedRuleSchemaTokens.OperationKindCopyRange;
        if (!StringComparer.Ordinal.Equals(
                operationKind,
                expectedOperationKind))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.OperationFragmentKindUnsupported,
                $"General {(isReplace ? "Replace" : "Merge")} Saved Rule v2 mappings must use {expectedOperationKind}.",
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
        ByteRange targetRegion = default;
        if (isReplace
                ? !targetRegions!.TryGetValue(targetRegionId, out targetRegion)
                : !StringComparer.Ordinal.Equals(
                    targetRegionId,
                    WorkbenchGeneralMergeIds.OutputRegionId))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.MappingRowTargetRegionUnsupported,
                isReplace
                    ? $"General Replace Saved Rule v2 target region '{targetRegionId}' is not writable by its exact Parent."
                    : $"Current General Merge supports only target region '{WorkbenchGeneralMergeIds.OutputRegionId}'.",
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
            targetRange = new ByteRange(
                checked(
                    (isReplace ? targetRegion.Start : 0) +
                    targetOffset),
                sourceRange.Length);
        }
        catch (Exception exception) when (
            exception is ArgumentOutOfRangeException or OverflowException)
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
            operationKind != expectedOperationKind ||
            overlapPolicy != SavedRuleSchemaTokens.MappingOverlapReject ||
            (isReplace
                ? !targetRegion.Contains(targetRange)
                : targetRegionId !=
                  WorkbenchGeneralMergeIds.OutputRegionId) ||
            reason.Length == 0)
        {
            return;
        }

        try
        {
            rows.Add(new GeneralMappingDraftRow(
                fragmentId,
                isReplace
                    ? ExplicitMappingOperationKind.ReplaceRange
                    : ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(sourcePath),
                sourceRange,
                CompositionAddressSpaceIds.OutputImage,
                targetRange,
                OverlapPolicy.Reject,
                alignment: 1,
                reason,
                isReplace
                    ? null
                    : WorkbenchGeneralMergeIds.OutputRegionId,
                OperationProvenance.SavedRule(ruleId, ruleVersion),
                GeneralMappingFileRangePreset.SourceSlice));
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
