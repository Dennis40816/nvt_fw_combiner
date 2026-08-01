using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedRuleCliCommandHandler
{
    private static bool HasV2SchemaVersion(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.GetFullPath(path)));
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(
                    "schemaVersion",
                    out JsonElement schemaVersion) &&
                schemaVersion.ValueKind == JsonValueKind.String &&
                StringComparer.Ordinal.Equals(
                    schemaVersion.GetString(),
                    "2.0");
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            ArgumentException)
        {
            return false;
        }
    }

    private static async Task<int> RunV2Async(
        string action,
        string path,
        TextWriter output,
        TextWriter error)
    {
        SavedRuleV2Inspection? inspection;
        try
        {
            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.GetFullPath(path)));
            JsonElement root = document.RootElement;
            if (MatchesV2Workflow(
                    root,
                    SavedRuleSchemaTokens.CompositionKindMerge,
                    IcWorkflowIds.GeneralMerge))
            {
                if (!TryResolveV2GeneralMergeParent(
                        root,
                        out SavedRuleV2GeneralMergeAdmissionContext? context,
                        out SavedRuleValidationIssue? issue))
                {
                    await PrintIssuesAsync([issue!], error).ConfigureAwait(false);
                    return CompositionFailed;
                }

                SavedRuleV2DraftLoadResult<GeneralMergeDraftState> load =
                    SavedRuleV2GeneralMergeDraftLoader.Load(
                        path,
                        CreatePlaceholderBindings(root, context!.InputPolicies),
                        context);
                if (!load.IsValid)
                {
                    await PrintIssuesAsync(load.Issues, error).ConfigureAwait(false);
                    return CompositionFailed;
                }

                inspection = new SavedRuleV2Inspection(
                    load.ExecutionIdentity!,
                    load.Draft!.Mappings);
            }
            else if (MatchesV2Workflow(
                         root,
                         SavedRuleSchemaTokens.CompositionKindReplace,
                         IcWorkflowIds.GeneralReplace))
            {
                if (!TryResolveV2GeneralReplaceParent(
                        root,
                        out SavedRuleV2GeneralReplaceAdmissionContext? context,
                        out SavedRuleValidationIssue? issue))
                {
                    await PrintIssuesAsync([issue!], error).ConfigureAwait(false);
                    return CompositionFailed;
                }

                SavedRuleV2DraftLoadResult<GeneralMappingDraftState> load =
                    SavedRuleV2GeneralMergeDraftLoader.LoadGeneralReplace(
                        path,
                        CreatePlaceholderBindings(root, context!.InputPolicies),
                        context);
                if (!load.IsValid)
                {
                    await PrintIssuesAsync(load.Issues, error).ConfigureAwait(false);
                    return CompositionFailed;
                }

                inspection = new SavedRuleV2Inspection(
                    load.ExecutionIdentity!,
                    load.Draft!);
            }
            else
            {
                await PrintIssuesAsync(
                    [
                        new SavedRuleValidationIssue(
                            SavedRuleIssueCodes.V2ContractInvalid,
                            "Saved Rule v2 requires one supported compositionKind/sourceExperienceId pair.",
                            "$"),
                    ],
                    error).ConfigureAwait(false);
                return CompositionFailed;
            }
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            ArgumentException)
        {
            await PrintIssuesAsync(
                [
                    new SavedRuleValidationIssue(
                        SavedRuleIssueCodes.JsonInvalid,
                        exception.Message,
                        "$"),
                ],
                error).ConfigureAwait(false);
            return CompositionFailed;
        }

        SavedRuleExecutionIdentity identity = inspection.Identity;
        SavedRuleLifecycleSnapshot lifecycle = SavedRuleLifecycle.Import(identity);
        await output.WriteLineAsync(
            $"Rule: {identity.RuleId} {identity.RuleVersion}").ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Content SHA256: {identity.ContentHash}").ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Lifecycle: {lifecycle.State}").ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Trusted: {lifecycle.IsTrusted}").ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Parent: {identity.Parent.BundleId} / {identity.Parent.ProfileId} / {identity.Parent.FamilyId} / {identity.Parent.MapId}")
            .ConfigureAwait(false);
        await output.WriteLineAsync(
            $"Mappings: {inspection.Mappings.Rows.Count}").ConfigureAwait(false);
        if (action == "mappings")
        {
            await PrintMappingsAsync(inspection.Mappings, output).ConfigureAwait(false);
        }

        return Success;
    }

    private static bool TryResolveV2GeneralMergeParent(
        JsonElement root,
        out SavedRuleV2GeneralMergeAdmissionContext? context,
        out SavedRuleValidationIssue? issue)
    {
        context = null;
        if (!TryReadV2ParentProfileId(root, out string? profileId, out issue))
        {
            return false;
        }

        SavedRuleV2GeneralMergeAdmissionContext[] matches =
        [
            .. BuiltInV2RegistrationRegistry.GeneralMergeByIc.Values
                .Where(registration =>
                    StringComparer.Ordinal.Equals(
                        registration.ProfileId,
                        profileId))
                .Select(registration =>
                    registration.Bundle.GetGeneralMergeSavedRuleAdmissionContext(
                        registration.ProfileId))
                .DistinctBy(static candidate => candidate.ParentBinding),
        ];
        if (matches.Length != 1)
        {
            issue = new SavedRuleValidationIssue(
                SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                $"Exact Saved Rule v2 Parent profile '{profileId}' is not uniquely installed in the Trusted Catalog.",
                "$.parentBinding");
            return false;
        }

        context = matches[0];
        issue = null;
        return true;
    }

    private static bool TryResolveV2GeneralReplaceParent(
        JsonElement root,
        out SavedRuleV2GeneralReplaceAdmissionContext? context,
        out SavedRuleValidationIssue? issue)
    {
        context = null;
        if (!TryReadV2ParentProfileId(root, out string? profileId, out issue))
        {
            return false;
        }

        SavedRuleV2GeneralReplaceAdmissionContext installed =
            WorkbenchCompositionService
                .GetNt51926GeneralReplaceSavedRuleAdmissionContext();
        if (!StringComparer.Ordinal.Equals(
                installed.ParentBinding.ProfileId,
                profileId))
        {
            issue = new SavedRuleValidationIssue(
                SavedRuleIssueCodes.V2ParentNarrowingInvalid,
                $"Exact Saved Rule v2 Parent profile '{profileId}' is not uniquely installed in the Trusted Catalog.",
                "$.parentBinding");
            return false;
        }

        context = installed;
        issue = null;
        return true;
    }

    private static bool TryReadV2ParentProfileId(
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

    private static bool MatchesV2Workflow(
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
                    template.TryGetProperty(
                        "slotTemplateId",
                        out JsonElement idElement) &&
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

    private sealed record SavedRuleV2Inspection(
        SavedRuleExecutionIdentity Identity,
        GeneralMappingDraftState Mappings);
}
