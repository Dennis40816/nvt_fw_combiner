using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Bootstrap;

internal static partial class MergeCliCommandHandler
{
    private static bool TryCreateDraftFromSavedRule(
        string rulePath,
        IReadOnlyList<string> slotValues,
        string icId,
        TextWriter error,
        [NotNullWhen(true)] out GeneralMergeDraftState? draft,
        [NotNullWhen(true)] out GeneralSavedRuleResourcePolicy? savedRulePolicy)
    {
        draft = null;
        savedRulePolicy = null;
        if (!TryCreateSlotBindings(slotValues, error, out Dictionary<string, string>? slotsById))
        {
            return false;
        }

        if (!BuiltInV2RegistrationRegistry.GeneralMergeByIc.TryGetValue(
                icId,
                out GeneralMergeV2CandidateRegistration? registration))
        {
            error.WriteLine(
                $"error: no exact trusted {icId} / General Merge parent is registered");
            return false;
        }

        SavedRuleV2GeneralMergeDraftLoadResult load =
            SavedRuleV2GeneralMergeDraftLoader.Load(
                rulePath,
                slotsById,
                registration.Bundle.GetGeneralMergeSavedRuleAdmissionContext(
                    registration.ProfileId));
        if (!load.IsValid)
        {
            PrintSavedRuleIssues(load.Issues, error);
            return false;
        }

        draft = load.Draft!;
        savedRulePolicy = load.ResourcePolicy!;
        return true;
    }

    private static bool TryCreateSlotBindings(
        IReadOnlyList<string> slotValues,
        TextWriter error,
        [NotNullWhen(true)] out Dictionary<string, string>? slotsById)
    {
        slotsById = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string value in slotValues)
        {
            int separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
            {
                error.WriteLine("error: --slot must use <slot-id=path>");
                return false;
            }

            string slotId = value[..separatorIndex].Trim();
            string path = value[(separatorIndex + 1)..].Trim();
            if (slotId.Length == 0 || path.Length == 0)
            {
                error.WriteLine("error: --slot must use non-empty slot id and path");
                return false;
            }

            if (!slotsById.TryAdd(slotId, Path.GetFullPath(path)))
            {
                error.WriteLine($"error: duplicate --slot binding for '{slotId}'");
                return false;
            }
        }

        return true;
    }

    private static void PrintSavedRuleIssues(IReadOnlyList<SavedRuleValidationIssue> issues, TextWriter error)
    {
        error.WriteLine("error: saved rule validation failed");
        foreach (SavedRuleValidationIssue issue in issues)
        {
            error.WriteLine($"  {issue.Code} at {issue.Path}: {issue.Message}");
        }
    }
}
