using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Cli;

internal static class SavedRuleCliSupport
{
    internal static bool TryCreateSlotBindings(
        IReadOnlyList<string> values,
        TextWriter error,
        [NotNullWhen(true)] out Dictionary<string, string>? slotsById)
    {
        slotsById = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string value in values)
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

    internal static void PrintIssues(
        IReadOnlyList<SavedRuleValidationIssue> issues,
        TextWriter error)
    {
        error.WriteLine("error: saved rule validation failed");
        foreach (SavedRuleValidationIssue issue in issues)
        {
            error.WriteLine(
                $"  {issue.Code} at {issue.Path}: {issue.Message}");
        }
    }
}
