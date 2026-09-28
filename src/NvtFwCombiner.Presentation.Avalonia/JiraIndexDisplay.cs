namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Formats the decoded Jira index for firmware fact cards.</summary>
internal static class JiraIndexDisplay
{
    /// <summary>
    /// Formats a known Jira index. <see langword="null"/> is treated as zero because the input inspection
    /// decoder folds a decoded zero register value to <see langword="null"/>; callers pass
    /// <see langword="null"/> only for a successfully decoded value, never for missing metadata.
    /// </summary>
    internal static string Format(ushort? number)
    {
        return number is > 0
            ? FormattableString.Invariant($"AUTO_PRJ-{number.Value}")
            : "Invalid (AUTO_PRJ-0)";
    }
}
