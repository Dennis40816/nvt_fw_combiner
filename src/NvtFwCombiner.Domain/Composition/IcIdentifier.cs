namespace NvtFwCombiner.Domain.Composition;

/// <summary>Canonicalizes one IC identifier shared by all client and compiler paths.</summary>
public static class IcIdentifier
{
    /// <summary>Returns the trimmed identifier with one uppercase NT prefix.</summary>
    public static string Normalize(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        string trimmed = icId.Trim();
        return trimmed.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? $"NT{trimmed[2..]}"
            : $"NT{trimmed}";
    }
}
