namespace NvtFwCombiner.Domain.Composition;

/// <summary>Stable IC number selection tokens shared by catalogs, CLI, and UI adapters.</summary>
public static class IcNumberSelectionTokens
{
    /// <summary>Single-chip selector token.</summary>
    public const string SingleChip = "single";

    /// <summary>Cascade selector token for multi-chip workflows without an explicit numeric count.</summary>
    public const string Cascade = "cascade";

    /// <summary>Returns true when the value is the stable single-chip selector token.</summary>
    public static bool IsSingle(string? value)
    {
        return string.Equals(value, SingleChip, StringComparison.OrdinalIgnoreCase);
    }
}
