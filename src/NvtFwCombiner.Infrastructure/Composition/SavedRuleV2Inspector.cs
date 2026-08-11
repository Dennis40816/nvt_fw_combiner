using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>Admits one Saved Rule document through the exact trusted Parent catalog.</summary>
internal static class SavedRuleV2Inspector
{
    internal static SavedRuleV2InspectionResult Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return Failed(
                    SavedRuleIssueCodes.FileNotFound,
                    $"Saved Rule v2 JSON was not found: {fullPath}");
            }

            using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
            JsonElement root = document.RootElement;
            SavedRuleValidationIssue? schemaIssue =
                SavedRuleSchemaVersionGate.Validate(root);
            return schemaIssue is not null
                ? new SavedRuleV2InspectionResult(null, null, [schemaIssue])
                : MatchesWorkflow(
                    root,
                    SavedRuleSchemaTokens.CompositionKindMerge,
                    ExperienceIds.GeneralMerge)
                    ? InspectGeneralMerge(path, root)
                    : MatchesWorkflow(
                        root,
                        SavedRuleSchemaTokens.CompositionKindReplace,
                        ExperienceIds.GeneralReplace)
                        ? InspectGeneralReplace(path, root)
                        : Failed(
                            SavedRuleIssueCodes.V2ContractInvalid,
                            "Saved Rule v2 requires one supported compositionKind/sourceExperienceId pair.");
        }
        catch (JsonException exception)
        {
            return Failed(
                SavedRuleIssueCodes.JsonInvalid,
                $"Saved Rule v2 JSON is invalid: {exception.Message}");
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Failed(
                SavedRuleIssueCodes.FileNotFound,
                $"Saved Rule v2 JSON was not found: {exception.Message}");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Failed(
                SavedRuleIssueCodes.FileReadFailed,
                $"Saved Rule v2 JSON could not be read: {exception.Message}");
        }
    }

    private static SavedRuleV2InspectionResult InspectGeneralMerge(
        string path,
        JsonElement root)
    {
        if (!TryReadParentProfileId(root, out string? profileId, out SavedRuleValidationIssue? issue))
        {
            return new SavedRuleV2InspectionResult(null, null, [issue!]);
        }

        SavedRuleV2GeneralMergeAdmissionContext[] matches =
        [
            .. BuiltInV2RegistrationRegistry.GeneralMergeByIc.Values
                .Where(registration => StringComparer.Ordinal.Equals(
                    registration.ProfileId,
                    profileId))
                .Select(registration =>
                    registration.Bundle.GetGeneralMergeSavedRuleAdmissionContext(
                        registration.ProfileId))
                .DistinctBy(static candidate => candidate.ParentBinding),
        ];
        if (matches.Length != 1)
        {
            return ParentUnavailable(profileId!);
        }

        SavedRuleV2GeneralMergeAdmissionContext context = matches[0];
        SavedRuleV2DraftLoadResult<GeneralMergeDraftState> load =
            SavedRuleV2GeneralMergeDraftLoader.Load(
                path,
                CreatePlaceholderBindings(root, context.InputPolicies),
                context);
        return load.IsValid
            ? new SavedRuleV2InspectionResult(
                load.ExecutionIdentity,
                load.Draft!.Mappings,
                [])
            : new SavedRuleV2InspectionResult(null, null, load.Issues);
    }

    private static SavedRuleV2InspectionResult InspectGeneralReplace(
        string path,
        JsonElement root)
    {
        if (!TryReadParentProfileId(root, out string? profileId, out SavedRuleValidationIssue? issue))
        {
            return new SavedRuleV2InspectionResult(null, null, [issue!]);
        }

        SavedRuleV2GeneralReplaceAdmissionContext[] matches =
        [
            .. BuiltInV2RegistrationRegistry.GeneralReplaceByIc.Values
                .Where(registration => StringComparer.Ordinal.Equals(
                    registration.ProfileId,
                    profileId))
                .Select(static registration => registration.SavedRuleAdmissionContext)
                .DistinctBy(static candidate => candidate.ParentBinding),
        ];
        if (matches.Length != 1)
        {
            return ParentUnavailable(profileId!);
        }

        SavedRuleV2GeneralReplaceAdmissionContext context = matches[0];
        SavedRuleV2DraftLoadResult<GeneralMappingDraftState> load =
            SavedRuleV2GeneralMergeDraftLoader.LoadGeneralReplace(
                path,
                CreatePlaceholderBindings(root, context.InputPolicies),
                context);
        return load.IsValid
            ? new SavedRuleV2InspectionResult(
                load.ExecutionIdentity,
                load.Draft,
                [])
            : new SavedRuleV2InspectionResult(null, null, load.Issues);
    }

    private static bool TryReadParentProfileId(
        JsonElement root,
        out string? profileId,
        out SavedRuleValidationIssue? issue)
    {
        profileId = null;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("parentBinding", out JsonElement parent) ||
            parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty("profileId", out JsonElement profileIdElement) ||
            profileIdElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(profileIdElement.GetString()))
        {
            issue = new SavedRuleValidationIssue(
                SavedRuleIssueCodes.V2ContractInvalid,
                "Saved Rule v2 requires one exact parentBinding.profileId.",
                "$.parentBinding.profileId");
            return false;
        }

        profileId = profileIdElement.GetString();
        issue = null;
        return true;
    }

    private static bool MatchesWorkflow(
        JsonElement root,
        string compositionKind,
        string experienceId)
    {
        return root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("compositionKind", out JsonElement kind) &&
            kind.ValueKind == JsonValueKind.String &&
            StringComparer.Ordinal.Equals(kind.GetString(), compositionKind) &&
            root.TryGetProperty("sourceExperienceId", out JsonElement experience) &&
            experience.ValueKind == JsonValueKind.String &&
            StringComparer.Ordinal.Equals(experience.GetString(), experienceId);
    }

    private static Dictionary<string, string> CreatePlaceholderBindings(
        JsonElement root,
        IReadOnlyList<SavedRuleV2ParentInputPolicy> inputPolicies)
    {
        var bindings = inputPolicies.ToDictionary(
            static policy => policy.SlotId,
            static policy => policy.SlotId,
            StringComparer.Ordinal);
        if (root.TryGetProperty("slotTemplates", out JsonElement templates) &&
            templates.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement template in templates.EnumerateArray())
            {
                if (template.ValueKind == JsonValueKind.Object &&
                    template.TryGetProperty("slotTemplateId", out JsonElement idElement) &&
                    idElement.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(idElement.GetString()))
                {
                    string id = idElement.GetString()!;
                    _ = bindings.TryAdd(id, id);
                }
            }
        }

        return bindings;
    }

    private static SavedRuleV2InspectionResult ParentUnavailable(string profileId)
    {
        return new SavedRuleV2InspectionResult(
            null,
            null,
            [new SavedRuleValidationIssue(
                SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                $"Exact Saved Rule v2 Parent profile '{profileId}' is not uniquely installed in the Trusted Catalog.",
                "$.parentBinding")]);
    }

    private static SavedRuleV2InspectionResult Failed(string code, string message)
    {
        return new SavedRuleV2InspectionResult(
            null,
            null,
            [new SavedRuleValidationIssue(code, message, "$")]);
    }
}
