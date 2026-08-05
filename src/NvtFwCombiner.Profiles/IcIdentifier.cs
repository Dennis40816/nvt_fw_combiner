namespace NvtFwCombiner.Profiles;

/// <summary>Canonical syntax for required Novatek IC identifiers.</summary>
public static class IcIdentifier
{
    /// <summary>Normalizes a required IC identifier to its canonical NT-prefixed form.</summary>
    public static string Normalize(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        string trimmed = icId.Trim();
        return trimmed.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? $"NT{trimmed[2..]}"
            : $"NT{trimmed}";
    }
}
