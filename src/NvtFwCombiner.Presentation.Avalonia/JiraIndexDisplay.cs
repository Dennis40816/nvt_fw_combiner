namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Formats the decoded Jira index for firmware fact cards.</summary>
internal static class JiraIndexDisplay
{
    internal static string Format(ushort? number)
    {
        return number is > 0
            ? FormattableString.Invariant($"AUTO_PRJ-{number.Value}")
            : "Invalid (AUTO_PRJ-0)";
    }
}
